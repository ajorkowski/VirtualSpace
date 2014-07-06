using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace MkvDecoder.Interop
{
    [ComVisible(true)]
    [Guid("44AB221B-5444-4D99-A084-8B9233D88A0F")]
    public class MkvDecoderByteStreamHandler : COMBase, IMFByteStreamHandler
    {
        public int BeginCreateObject(IMFByteStream pByteStream, string pwszURL, MFResolution dwFlags, IPropertyStore pProps, out object ppIUnknownCancelCookie, IMFAsyncCallback pCallback, object pUnkState)
        {
            throw new NotImplementedException();
        }

        public int CancelObjectCreation(object pIUnknownCancelCookie)
        {
            throw new NotImplementedException();
        }

        public int EndCreateObject(IMFAsyncResult pResult, out MFObjectType pObjectType, out object ppObject)
        {
            throw new NotImplementedException();
        }

        public int GetMaxNumberOfBytesRequiredForResolution(out long pqwBytes)
        {
            throw new NotImplementedException();
        }
    }
}
