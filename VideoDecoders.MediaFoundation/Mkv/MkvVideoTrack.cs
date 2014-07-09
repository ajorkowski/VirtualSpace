using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public class MkvVideoTrack : COMBase, IMkvTrack
    {
        private readonly IMFStreamDescriptor _descriptor;
        private readonly TrackEntry _entry;
        private readonly MkvMediaSource _mediaSource;
        private readonly IMFMediaEventQueue _eventQueue;
        private readonly ConcurrentQueue<object> _tokenQueue;

        private bool _hasShutdown;

        private bool _hasSelected;
        private bool _isSelected;
        private bool _endOfStream;

        public IMFStreamDescriptor Descriptor { get { return _descriptor; } }
        public TrackEntry Metadata { get { return _entry; } }
        public bool IsSelected 
        {
            get { return _isSelected; }
            set
            {
                _isSelected = value;
                if (!_isSelected) { _hasSelected = false; }
            }
        }

        public MkvVideoTrack(TrackEntry entry, MkvMediaSource mediaSource)
        {
            if (entry.TrackType != TrackType.Video)
            {
                throw new InvalidOperationException("Only video tracks should be here");
            }

            _entry = entry;
            _mediaSource = mediaSource;
            _tokenQueue = new ConcurrentQueue<object>();
            TestSuccess("Could not create event queue for video track", MFExtern.MFCreateEventQueue(out _eventQueue));

            IMFMediaType type;
            TestSuccess("Could not create media type", MFExtern.MFCreateMediaType(out type));

            TestSuccess("Could not set video type", type.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, MFMediaType.Video));

            Guid subtype;
            switch (entry.CodecID)
            {
                case "V_MPEG4/ISO/AVC":
                    subtype = MFMediaType.H264;
                    break;
                default:
                    throw new InvalidOperationException("Unknown codec type");
            }

            TestSuccess("Could not set video subtype", type.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, subtype));

            TestSuccess("Could not set video size", type.SetUINT64(MFAttributesClsid.MF_MT_FRAME_SIZE, MakeLong((int)entry.Video.DisplayWidth, (int)entry.Video.DisplayHeight)));
            TestSuccess("Could not set pixel aspect ratio", type.SetUINT64(MFAttributesClsid.MF_MT_PIXEL_ASPECT_RATIO, MakeLong(1, 1)));

            TestSuccess("Could not create stream descriptor", MFExtern.MFCreateStreamDescriptor((int)entry.TrackNumber, 1, new IMFMediaType[] { type }, out _descriptor));

            IMFMediaTypeHandler typeHandler;
            _descriptor.GetMediaTypeHandler(out typeHandler);

            typeHandler.SetCurrentMediaType(type);
        }

        public void ProcessSample()
        {
            // Only dequeue one token at a time...
            object token;
            if (_tokenQueue.TryDequeue(out token))
            {
                var sample = _mediaSource.LoadNextSample((int)Metadata.TrackNumber);

                if (token != null)
                {
                    sample.SetUnknown(MFAttributesClsid.MFSampleExtension_Token, token);
                }

                QueueEvent(MediaEventType.MEMediaSample, Guid.Empty, S_Ok, new PropVariant(sample));
            }
        }

        public int RequestSample(object pToken)
        {
            if (_hasShutdown) { return MFError.MF_E_SHUTDOWN; }
            if (_mediaSource.CurrentState == MkvState.Stop) { return MFError.MF_E_MEDIA_SOURCE_WRONGSTATE; }
            if (_endOfStream) { return MFError.MF_E_END_OF_STREAM; }

            _tokenQueue.Enqueue(pToken);

            //QueueEvent(MediaEventType.MEStreamTick, Guid.Empty, S_Ok, new PropVariant((long)0));
            //QueueEvent(MediaEventType.MEStreamTick, Guid.Empty, S_Ok, new PropVariant((long)100));
            //QueueEvent(MediaEventType.MEStreamTick, Guid.Empty, S_Ok, new PropVariant((long)200));

            //QueueEvent(MediaEventType.MEEndOfStream, Guid.Empty, S_Ok, new PropVariant());
            //_mediaSource.QueueEvent(MediaEventType.MEEndOfPresentation, Guid.Empty, S_Ok, new PropVariant());

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
            object token;
            while (_tokenQueue.TryDequeue(out token)) {
                if (token != null)
                {
                    Marshal.ReleaseComObject(token);
                }
            };
        }

        private void TestSuccess(string message, int hResult)
        {
            if (hResult < 0)
            {
                throw new COMException(message, hResult);
            }
        }

        private long MakeLong(int left, int right)
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
