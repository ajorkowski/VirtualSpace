using MediaFoundation;
using System.Runtime.InteropServices;
using System.Security;
using VideoDecoders.MediaFoundation.Mkv;

namespace VideoDecoders.MediaFoundation
{
    public static class DecoderRegister
    {
        public static void Register()
        {
            MFRegisterLocalByteStreamHandler(".mkv", "video/x-matroska", new MkvDecoderActivator());
        }

        [DllImport("mfplat.dll", ExactSpelling = true), SuppressUnmanagedCodeSecurity]
        private extern static int MFRegisterLocalByteStreamHandler([In]string fileExtension, [In]string mimeType, [In]IMFActivate activate);
    }
}
