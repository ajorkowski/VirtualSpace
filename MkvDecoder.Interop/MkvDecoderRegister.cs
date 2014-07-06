using MediaFoundation;
using System;
using System.Runtime.InteropServices;
using System.Security;

namespace MkvDecoder.Interop
{
    public static class MkvDecoderRegister
    {
        public static void Register()
        {
            MFRegisterLocalByteStreamHandler(".mkv", "video/x-matroska", new MkvDecoderActivator());
        }

        [DllImport("mfplat.dll", ExactSpelling = true), SuppressUnmanagedCodeSecurity]
        private extern static int MFRegisterLocalByteStreamHandler([In]string fileExtension, [In]string mimeType, [In]IMFActivate activate);
    }
}
