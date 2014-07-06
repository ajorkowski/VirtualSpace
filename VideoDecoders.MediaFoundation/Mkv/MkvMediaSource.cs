using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Runtime.InteropServices;

namespace VideoDecoders.MediaFoundation.Mkv
{
    [ComVisible(true)]
    [Guid("F7C75FFE-06FE-4C7A-98DF-D7FBD1BD9642")]
    public class MkvMediaSource : COMBase, IMFMediaSource
    {
        private readonly MkvDecoder _decoder;

        public MkvMediaSource(MkvDecoder decoder)
        {
            _decoder = decoder;
        }

        public int BeginGetEvent(IMFAsyncCallback pCallback, object o)
        {
            throw new NotImplementedException();
        }

        public int CreatePresentationDescriptor(out IMFPresentationDescriptor ppPresentationDescriptor)
        {
            throw new NotImplementedException();
        }

        public int EndGetEvent(IMFAsyncResult pResult, out IMFMediaEvent ppEvent)
        {
            throw new NotImplementedException();
        }

        public int GetCharacteristics(out MFMediaSourceCharacteristics pdwCharacteristics)
        {
            pdwCharacteristics = MFMediaSourceCharacteristics.None;
            if(_decoder.StreamMetadata.CanPause)
            {
                pdwCharacteristics &= MFMediaSourceCharacteristics.CanPause;
            }

            if(_decoder.StreamMetadata.CanSeek)
            {
                pdwCharacteristics &= MFMediaSourceCharacteristics.CanSeek;
            }

            if(_decoder.StreamMetadata.HasSlowSeek)
            {
                pdwCharacteristics &= MFMediaSourceCharacteristics.HasSlowSeek;
            }

            return S_Ok;
        }

        public int GetEvent(MFEventFlag dwFlags, out IMFMediaEvent ppEvent)
        {
            throw new NotImplementedException();
        }

        public int Pause()
        {
            throw new NotImplementedException();
        }

        public int QueueEvent(MediaEventType met, Guid guidExtendedType, int hrStatus, global::MediaFoundation.Misc.ConstPropVariant pvValue)
        {
            throw new NotImplementedException();
        }

        public int Shutdown()
        {
            throw new NotImplementedException();
        }

        public int Start(IMFPresentationDescriptor pPresentationDescriptor, Guid pguidTimeFormat, global::MediaFoundation.Misc.ConstPropVariant pvarStartPosition)
        {
            throw new NotImplementedException();
        }

        public int Stop()
        {
            throw new NotImplementedException();
        }
    }
}
