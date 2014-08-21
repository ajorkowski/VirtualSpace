using MediaFoundation;
using System;
using System.IO;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public class MkvVideoTrack : MkvBaseTrack
    {
        private static readonly byte[] AnnexxB = new byte[] { 0, 0, 0, 1 };

        private readonly byte[] _sharedBuffer;
        private byte[] _codecPrivateData;

        public MkvVideoTrack(TrackEntry entry, MkvMediaSource mediaSource)
            : base(entry, mediaSource)
        {
            if (entry.TrackType != TrackType.Video)
            {
                throw new InvalidOperationException("Only video tracks should be here");
            }

            _sharedBuffer = new byte[2048];
        }

        protected override IMFMediaType CreateMediaType(TrackEntry entry)
        {
            IMFMediaType type;
            TestSuccess("Could not create media type", MFExtern.MFCreateMediaType(out type));

            TestSuccess("Could not set video type", type.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, MFMediaType.Video));

            switch (entry.CodecID)
            {
                case "V_MPEG4/ISO/AVC":
                    if (entry.CodecPrivate == null) { throw new InvalidOperationException("Expecting private information in mkv to decode H264 streams"); }
                    ParseAVCCFormatHeader(entry.CodecPrivate);

                    TestSuccess("Could not set video subtype", type.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, MFMediaType.H264));

                    TestSuccess("Could not set video size", type.SetUINT64(MFAttributesClsid.MF_MT_FRAME_SIZE, MakeLong((int)entry.Video.DisplayWidth, (int)entry.Video.DisplayHeight)));
                    TestSuccess("Could not set pixel aspect ratio", type.SetUINT64(MFAttributesClsid.MF_MT_PIXEL_ASPECT_RATIO, MakeLong(1, 1)));
                    TestSuccess("Could not set interlace mode", type.SetUINT32(MFAttributesClsid.MF_MT_INTERLACE_MODE, (int)MFVideoInterlaceMode.MixedInterlaceOrProgressive));
                    break;
                default:
                    return null;
            }

            return type;
        }

        public override IMFMediaBuffer CreateBufferFromBlock(int blockDataSize, Func<byte[], int, int, int> readBlockDataFunc, ref MkvBlockHeader header)
        {
            int bufferRealLength = header.KeyFrame ? blockDataSize + _codecPrivateData.Length : blockDataSize;
            IMFMediaBuffer buffer;
            TestSuccess("Could not create media buffer", MFExtern.MFCreateMemoryBuffer(bufferRealLength, out buffer));

            int currentLength;
            IntPtr bufferPtr;
            TestSuccess("Could not lock media buffer", buffer.Lock(out bufferPtr, out bufferRealLength, out currentLength));

            unsafe
            {
                using (var buffStream = new UnmanagedMemoryStream((byte*)bufferPtr.ToPointer(), bufferRealLength, bufferRealLength, FileAccess.Write))
                {
                    if(header.KeyFrame)
                    {
                        // We have to dump the PPS/SPS information in every frame...
                        buffStream.Write(_codecPrivateData, 0, _codecPrivateData.Length);
                        currentLength += _codecPrivateData.Length;
                    }

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
    }
}
