using MediaFoundation;
using Microsoft.Win32;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using VideoDecoders.MediaFoundation.Mkv;

namespace VideoDecoders.MediaFoundation
{
    public static class DecoderRegister
    {
        private static bool _hasRegistered = false;

        public static void Register()
        {
            if (_hasRegistered)
            {
                return;
            }

            // Test the registry... we might already have an mkv handler
            var mkvByteStreamKey = "{" + typeof(MkvDecoderByteStreamHandler).GUID.ToString().ToUpperInvariant() + "}";
            var handlerKey = OpenKey(Registry.CurrentUser, "Software", "Microsoft", "Windows Media Foundation", "ByteStreamHandlers");
            var mkvKey = OpenKey(handlerKey, ".mkv");

            _hasRegistered = mkvKey != null && mkvKey.GetValue(mkvByteStreamKey) != null;
            if (_hasRegistered) { return; }

            try
            {
                // Try to register a local mkv handler (only Win8)
                MFRegisterLocalByteStreamHandler(".mkv", "video/x-matroska", new MkvDecoderActivator());
            }
            catch(EntryPointNotFoundException)
            {
                // We are probably running on Windows 7 here...
                // We have to literally change registry values :( :( :(
                var handlerWriteKey = CreateKey(Registry.CurrentUser, "Software", "Microsoft", "Windows Media Foundation", "ByteStreamHandlers", ".mkv");
                handlerWriteKey.SetValue(mkvByteStreamKey, "VideoDecoders.MediaFoundation.MkvDecoderByteStreamHandler", RegistryValueKind.String);
            }
        }

        private static RegistryKey OpenKey(RegistryKey key, params string[] keys)
        {
            if (key == null) { return null; }
            if (keys == null || keys.Length == 0) { return key; }

            return OpenKey(key.OpenSubKey(keys[0]), keys.Skip(1).ToArray());
        }

        private static RegistryKey CreateKey(RegistryKey key, params string[] keys)
        {
            if (key == null) { throw new ArgumentNullException("key"); }
            if (keys == null || keys.Length == 0) { return key; }

            var toOpen = key.OpenSubKey(keys[0], true);
            if (toOpen == null)
            {
                toOpen = key.CreateSubKey(keys[0]);
            }

            return CreateKey(toOpen, keys.Skip(1).ToArray());
        }

        [DllImport("mfplat.dll", ExactSpelling = true), SuppressUnmanagedCodeSecurity]
        private extern static int MFRegisterLocalByteStreamHandler([In]string fileExtension, [In]string mimeType, [In]IMFActivate activate);
    }
}
