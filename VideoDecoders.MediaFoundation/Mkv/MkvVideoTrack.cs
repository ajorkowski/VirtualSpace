using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public class MkvVideoTrack : COMBase, IMkvTrack
    {
        private static readonly byte[] AnnexxB = new byte[] { 0, 0, 0, 1 };

        private readonly IMFStreamDescriptor _descriptor;
        private readonly TrackEntry _entry;
        private readonly MkvMediaSource _mediaSource;
        private readonly IMFMediaEventQueue _eventQueue;
        private readonly ConcurrentQueue<object> _tokenQueue;

        private readonly byte[] _sharedBuffer;

        private bool _hasShutdown;
        private bool _endOfStream;
        private byte[] _codecPrivateData;

        public IMFStreamDescriptor Descriptor { get { return _descriptor; } }
        public TrackEntry Metadata { get { return _entry; } }
        public bool IsEOS { get { return _endOfStream; } }
        public bool IsSelected { get; set; }

        public MkvVideoTrack(TrackEntry entry, MkvMediaSource mediaSource)
        {
            if (entry.TrackType != TrackType.Video)
            {
                throw new InvalidOperationException("Only video tracks should be here");
            }

            _entry = entry;
            _mediaSource = mediaSource;
            _tokenQueue = new ConcurrentQueue<object>();
            _sharedBuffer = new byte[2048];
            TestSuccess("Could not create event queue for video track", MFExtern.MFCreateEventQueue(out _eventQueue));

            IMFMediaType type;
            TestSuccess("Could not create media type", MFExtern.MFCreateMediaType(out type));

            TestSuccess("Could not set video type", type.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, MFMediaType.Video));

            Guid subtype;
            switch (entry.CodecID)
            {
                case "V_MPEG4/ISO/AVC":
                    if (entry.CodecPrivate == null) { throw new InvalidOperationException("Must have private information in mkv to decode H264 streams"); }
                    ParseAVCCFormatHeader(entry.CodecPrivate);

                    subtype = MFMediaType.H264;
                    break;
                default:
                    throw new InvalidOperationException("Unknown codec type");
            }

            TestSuccess("Could not set video subtype", type.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, subtype));

            TestSuccess("Could not set video size", type.SetUINT64(MFAttributesClsid.MF_MT_FRAME_SIZE, MakeLong((int)entry.Video.DisplayWidth, (int)entry.Video.DisplayHeight)));
            TestSuccess("Could not set pixel aspect ratio", type.SetUINT64(MFAttributesClsid.MF_MT_PIXEL_ASPECT_RATIO, MakeLong(1, 1)));
            TestSuccess("Could not set interlace mode", type.SetUINT32(MFAttributesClsid.MF_MT_INTERLACE_MODE, (int)MFVideoInterlaceMode.MixedInterlaceOrProgressive));

            TestSuccess("Could not create stream descriptor", MFExtern.MFCreateStreamDescriptor((int)entry.TrackNumber, 1, new IMFMediaType[] { type }, out _descriptor));

            IMFMediaTypeHandler typeHandler;
            _descriptor.GetMediaTypeHandler(out typeHandler);

            typeHandler.SetCurrentMediaType(type);
        }

        public IMFMediaBuffer CreateBufferFromBlock(int blockDataSize, Func<byte[], int, int, int> readBlockDataFunc)
        {
            int bufferRealLength = blockDataSize + _codecPrivateData.Length;
            IMFMediaBuffer buffer;
            TestSuccess("Could not create media buffer", MFExtern.MFCreateMemoryBuffer(bufferRealLength, out buffer));

            int currentLength;
            IntPtr bufferPtr;
            TestSuccess("Could not lock media buffer", buffer.Lock(out bufferPtr, out bufferRealLength, out currentLength));

            unsafe
            {
                using (var buffStream = new UnmanagedMemoryStream((byte*)bufferPtr.ToPointer(), bufferRealLength, bufferRealLength, FileAccess.Write))
                {
                    // We have to dump the PPS/SPS information in every frame...
                    buffStream.Write(_codecPrivateData, 0, _codecPrivateData.Length);
                    currentLength += _codecPrivateData.Length;

                    int replaceToken = 0;
                    while (blockDataSize > 0)
                    {
                        int r = readBlockDataFunc(_sharedBuffer, 0, Math.Min(blockDataSize, 2048));
                        if (r < 0)
                        {
                            throw new EndOfStreamException();
                        }
                        
                        // In AVC? format the NALs are seperated by 4 bytes that give the length of the next frame.
                        // We need to replace this with 0x00 0x00 0x00 0x01 to make it AnnexB. There can also be many
                        // frames inside an mkv block...
                        while (replaceToken < r)
                        {
                            var tokenLength = _sharedBuffer[replaceToken] << 24 | _sharedBuffer[replaceToken + 1] << 16 | _sharedBuffer[replaceToken + 2] << 8 | _sharedBuffer[replaceToken + 3];
                            if (tokenLength <= 0)
                            {
                                throw new InvalidOperationException("Token length cannot be less than or equal to 0");
                            }

                            Buffer.BlockCopy(AnnexxB, 0, _sharedBuffer, replaceToken, 4);

                            replaceToken += tokenLength + 4;
                        }
                        
                        buffStream.Write(_sharedBuffer, 0, r);
                        blockDataSize -= r;
                        currentLength += r;
                        replaceToken -= r;
                    }
                }
            }

            TestSuccess("Could not set media buffer length", buffer.SetCurrentLength(currentLength));
            TestSuccess("Could not unlock media buffer", buffer.Unlock());

            return buffer;
        }

        public bool ProcessSample()
        {
            if (_tokenQueue.IsEmpty) { return false; }

            // Only dequeue one token at a time...
            object token;
            if (_tokenQueue.TryDequeue(out token))
            {
                var sample = _mediaSource.LoadNextSample((int)Metadata.TrackNumber);
                if (sample == null)
                {
                    QueueEvent(MediaEventType.MEEndOfStream, Guid.Empty, S_Ok, new PropVariant());
                    EndAndPurgeStream();
                    return false;
                }

                if (token != null)
                {
                    sample.SetUnknown(MFAttributesClsid.MFSampleExtension_Token, token);
                }

                QueueEvent(MediaEventType.MEMediaSample, Guid.Empty, S_Ok, new PropVariant(sample));
                return true;
            }

            return false;
        }

        public int RequestSample(object pToken)
        {
            if (_hasShutdown) { return MFError.MF_E_SHUTDOWN; }
            if (_mediaSource.CurrentState == MkvState.Stop) { return MFError.MF_E_MEDIA_SOURCE_WRONGSTATE; }
            if (_endOfStream) { return MFError.MF_E_END_OF_STREAM; }

            _tokenQueue.Enqueue(pToken);
            _mediaSource.HasFrameToProcess();

            return S_Ok;
        }

        public int BeginGetEvent(IMFAsyncCallback pCallback, object o)
        {
            if (_hasShutdown) { return MFError.MF_E_SHUTDOWN; }

            return _eventQueue.BeginGetEvent(pCallback, o);
        }

        public int EndGetEvent(IMFAsyncResult pResult, out IMFMediaEvent ppEvent)
        {
            if (_hasShutdown) { ppEvent = null; return MFError.MF_E_SHUTDOWN; }

            return _eventQueue.EndGetEvent(pResult, out ppEvent);
        }

        public int GetEvent(MFEventFlag dwFlags, out IMFMediaEvent ppEvent)
        {
            if (_hasShutdown) { ppEvent = null; return MFError.MF_E_SHUTDOWN; }

            return _eventQueue.GetEvent(dwFlags, out ppEvent);
        }

        public int QueueEvent(MediaEventType met, Guid guidExtendedType, int hrStatus, ConstPropVariant pvValue)
        {
            if (_hasShutdown) { return MFError.MF_E_SHUTDOWN; }

            return _eventQueue.QueueEventParamVar(met, guidExtendedType, hrStatus, pvValue);
        }

        public int GetMediaSource(out IMFMediaSource ppMediaSource)
        {
            if (_hasShutdown) { ppMediaSource = null; return MFError.MF_E_SHUTDOWN; }

            ppMediaSource = _mediaSource;
            return S_Ok;
        }

        public int GetStreamDescriptor(out IMFStreamDescriptor ppStreamDescriptor)
        {
            if (_hasShutdown) { ppStreamDescriptor = null; return MFError.MF_E_SHUTDOWN; }

            ppStreamDescriptor = Descriptor;
            return S_Ok;
        }

        public void Dispose()
        {
            _hasShutdown = true;
            _eventQueue.Shutdown();

            // Release any left over tokens...
            EndAndPurgeStream();
        }

        private void ParseAVCCFormatHeader(byte[] headerBytes)
        {
            if (headerBytes[0] != 1)
            {
                throw new InvalidOperationException("Header is not in AVCC format");
            }

            var spsSize = headerBytes[6] << 8 | headerBytes[7];
            if (headerBytes[8 + spsSize] != 1)
            {
                throw new InvalidOperationException("Do not know how to handle multiple pps values...");
            }
            var ppsSize = headerBytes[9 + spsSize] << 8 | headerBytes[10 + spsSize];

            // The output format is annexB + spsBytes + annexB + ppsBytes
            _codecPrivateData = new byte[spsSize + ppsSize + 8];
            Buffer.BlockCopy(AnnexxB, 0, _codecPrivateData, 0, 4);
            Buffer.BlockCopy(headerBytes, 8, _codecPrivateData, 4, spsSize);
            Buffer.BlockCopy(AnnexxB, 0, _codecPrivateData, 4 + spsSize, 4);
            Buffer.BlockCopy(headerBytes, 11 + spsSize, _codecPrivateData, 8 + spsSize, ppsSize);
        }

        private void EndAndPurgeStream()
        {
            _endOfStream = true;
            object token;
            while (_tokenQueue.TryDequeue(out token))
            {
                if (token != null)
                {
                    Marshal.ReleaseComObject(token);
                }
            };
        }

        private static void TestSuccess(string message, int hResult)
        {
            if (hResult < 0)
            {
                throw new COMException(message, hResult);
            }
        }

        private static long MakeLong(int left, int right)
        {
            //implicit conversion of left to a long
            long res = left;

            //shift the bits creating an empty space on the right
            // ex: 0x0000CFFF becomes 0xCFFF0000
            res = (res << 32);

            //combine the bits on the right with the previous value
            // ex: 0xCFFF0000 | 0x0000ABCD becomes 0xCFFFABCD
            res = res | (long)(uint)right; //uint first to prevent loss of signed bit

            //return the combined result
            return res;
        }
    }
}
