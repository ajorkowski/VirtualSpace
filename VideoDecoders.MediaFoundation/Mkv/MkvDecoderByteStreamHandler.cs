using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace VideoDecoders.MediaFoundation.Mkv
{
    [ComVisible(true)]
    [Guid("44AB221B-5444-4D99-A084-8B9233D88A0F")]
    public class MkvDecoderByteStreamHandler : COMBase, IMFByteStreamHandler
    {
        public int BeginCreateObject(IMFByteStream pByteStream, string pwszURL, MFResolution dwFlags, IPropertyStore pProps, out object ppIUnknownCancelCookie, IMFAsyncCallback pCallback, object pUnkState)
        {
            ppIUnknownCancelCookie = null;
            if(dwFlags != (MFResolution.MediaSource | MFResolution.Read | MFResolution.ContentDoesNotHaveToMatchExtensionOrMimeType))
            {
                throw new InvalidOperationException("Only can use this byte stream handler to get a media source");
            }

            Task.Run(() =>
            {
                MkvMediaSource mediaSource = null;

                // Silently ignore errors, we will manage this later
                try
                {
                    var wrapperStream = new IMFByteStreamWrapper(pByteStream);
                    var decoder = new MkvDecoder(wrapperStream, wrapperStream.Metadata);
                    mediaSource = new MkvMediaSource(decoder);
                }
                catch(Exception) { }

                IMFAsyncResult result;
                TestSuccess("Could not create async result", MFExtern.MFCreateAsyncResult(mediaSource, pCallback, pUnkState, out result));
                TestSuccess("Could not invoke callback", MFExtern.MFInvokeCallback(result));
            });

            return S_Ok;
        }

        public int EndCreateObject(IMFAsyncResult pResult, out MFObjectType pObjectType, out object ppObject)
        {
            var hr = pResult.GetObject(out ppObject);
            pObjectType = MFObjectType.MediaSource;
            if (ppObject == null || hr < 0)
            {
                return MFError.MF_E_CANNOT_PARSE_BYTESTREAM;
            }

            return S_Ok;
        }

        public int CancelObjectCreation(object pIUnknownCancelCookie)
        {
            return S_Ok;
        }

        public int GetMaxNumberOfBytesRequiredForResolution(out long pqwBytes)
        {
            pqwBytes = long.MaxValue;
            return S_Ok;
        }

        private void TestSuccess(string message, int hResult)
        {
            if (hResult < 0)
            {
                throw new COMException(message, hResult);
            }
        }
    }
}
