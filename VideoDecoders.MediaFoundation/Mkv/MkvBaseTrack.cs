using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public abstract class MkvBaseTrack : COMBase, IMkvTrack
    {
        private readonly IMFStreamDescriptor _descriptor;
        private readonly TrackEntry _entry;
        private readonly MkvMediaSource _mediaSource;
        private readonly IMFMediaEventQueue _eventQueue;
        private readonly ConcurrentQueue<object> _tokenQueue;

        private bool _hasShutdown;
        private bool _endOfStream;

        public IMFStreamDescriptor Descriptor { get { return _descriptor; } }
        public TrackEntry Metadata { get { return _entry; } }
        public bool IsEOS { get { return _endOfStream; } }
        public bool IsSelected { get; set; }

        public MkvBaseTrack(TrackEntry entry, MkvMediaSource mediaSource)
        {
            _entry = entry;
            _mediaSource = mediaSource;
            _tokenQueue = new ConcurrentQueue<object>();

            TestSuccess("Could not create event queue for video track", MFExtern.MFCreateEventQueue(out _eventQueue));

            var mediaType = CreateMediaType(entry);
            TestSuccess("Could not create stream descriptor", MFExtern.MFCreateStreamDescriptor((int)entry.TrackNumber, 1, new IMFMediaType[] { mediaType }, out _descriptor));

            // Add Stream descriptor attributes from the track information
            if (!string.IsNullOrWhiteSpace(entry.Name))
            {
                _descriptor.SetString(MFAttributesClsid.MF_SD_STREAM_NAME, entry.Name);
            }

            if (!string.IsNullOrWhiteSpace(entry.Language))
            {
                _descriptor.SetString(MFAttributesClsid.MF_SD_LANGUAGE, entry.Language);
            }

            IMFMediaTypeHandler typeHandler;
            _descriptor.GetMediaTypeHandler(out typeHandler);

            typeHandler.SetCurrentMediaType(mediaType);
        }

        protected abstract IMFMediaType CreateMediaType(TrackEntry entry);
        public abstract IMFMediaBuffer CreateBufferFromBlock(int blockDataSize, Func<byte[], int, int, int> readBlockDataFunc);

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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hasShutdown = true;
                _eventQueue.Shutdown();

                // Release any left over tokens...
                EndAndPurgeStream();
            }
        }

        protected void TestSuccess(string message, int hResult)
        {
            if (hResult < 0)
            {
                throw new COMException(message, hResult);
            }
        }

        protected long MakeLong(int left, int right)
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

        protected void EndAndPurgeStream()
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
    }
}
