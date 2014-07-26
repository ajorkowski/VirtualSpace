using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.MediaFoundation;
using SharpDX.Multimedia;
using SharpDX.X3DAudio;
using SharpDX.XAudio2;
using System;
using System.Runtime.InteropServices;
using System.Security;
using VideoDecoders.MediaFoundation;

namespace VirtualSpace.Platform.Windows.Video
{
    internal sealed class MediaAndDeviceManager : IDisposable
    {
        private static MediaAndDeviceManager _current;
        public static MediaAndDeviceManager Current 
        {
            get
            {
                if (_current == null)
                {
                    _current = new MediaAndDeviceManager();
                }
                return _current;
            }
        }

        private XAudio2 _audioEngine;
        private MasteringVoice _masteringVoice;
        private X3DAudio _x3DAudio;

        private MediaAndDeviceManager()
        {
            MediaManager.Startup();
            DecoderRegister.Register();
        }

        public XAudio2 AudioEngine 
        { 
            get 
            {
                InitialiseAudio();
                return _audioEngine; 
            } 
        }

        public Voice MasterVoice
        {
            get
            {
                InitialiseAudio();
                return _masteringVoice;
            }
        }

        public X3DAudio X3DAudioEngine
        {
            get
            {
                if(_x3DAudio == null)
                {
                    InitialiseAudio();
                    _x3DAudio = new X3DAudio((Speakers)_masteringVoice.ChannelMask);
                }

                return _x3DAudio;
            }
        }

        public VideoDevice CreateVideoDevice()
        {
            var dx11Manager = TryCreateDx11Manager();
            if (dx11Manager != null)
            {
                return dx11Manager;
            }

            var dx9Manager = TryCreateDx9Manager();
            if (dx9Manager != null)
            {
                return dx9Manager;
            }

            // Fallback software device
            return new VideoDevice(VideoMode.Software, null, new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport), null);
        }

        public void Dispose()
        {
            Utilities.Dispose(ref _masteringVoice);
            Utilities.Dispose(ref _audioEngine);
            MediaManager.Shutdown();
            GC.SuppressFinalize(this);
        }

        private void InitialiseAudio()
        {
            if (_audioEngine == null)
            {
                _audioEngine = new XAudio2();
                _audioEngine.StartEngine();
                _masteringVoice = new MasteringVoice(_audioEngine, 0, 0);
            }
        }

        private VideoDevice TryCreateDx9Manager()
        {
            ComObject manager = null;
            SharpDX.Direct3D9.DeviceEx d9Device = null;
            SharpDX.Direct3D11.Device device = null;
            try
            {
                using (var d3d9 = new SharpDX.Direct3D9.Direct3DEx())
                {
                    if (!d3d9.CheckDeviceFormatConversion(0, SharpDX.Direct3D9.DeviceType.Hardware, SharpDX.Direct3D9.D3DX.MakeFourCC((byte)'N', (byte)'V', (byte)'1', (byte)'2'), SharpDX.Direct3D9.Format.X8R8G8B8))
                    {
                        return null;
                    }

                    d9Device = new SharpDX.Direct3D9.DeviceEx(d3d9, 0, SharpDX.Direct3D9.DeviceType.Hardware, IntPtr.Zero, SharpDX.Direct3D9.CreateFlags.FpuPreserve | SharpDX.Direct3D9.CreateFlags.Multithreaded | SharpDX.Direct3D9.CreateFlags.MixedVertexProcessing, new SharpDX.Direct3D9.PresentParameters
                    {
                        BackBufferWidth = 1,
                        BackBufferHeight = 1,
                        BackBufferFormat = SharpDX.Direct3D9.Format.Unknown,
                        BackBufferCount = 1,
                        SwapEffect = SharpDX.Direct3D9.SwapEffect.Discard,
                        DeviceWindowHandle = IntPtr.Zero,
                        Windowed = true,
                        PresentFlags = SharpDX.Direct3D9.PresentFlags.Video
                    });

                    int resetToken;
                    IDirect3DDeviceManager9 dxManager;
                    DXVA2CreateDirect3DDeviceManager9(out resetToken, out dxManager);

                    dxManager.ResetDevice(d9Device.NativePointer, resetToken);

                    manager = new ComObject(dxManager);

                    // Try to create a query and execute it... seems to be a good test for a bad device...
                    using (var query = new SharpDX.Direct3D9.Query(d9Device, SharpDX.Direct3D9.QueryType.Event))
                    {
                        query.Issue(SharpDX.Direct3D9.Issue.End);
                        int iter = 10;
                        bool temp;
                        while (!query.GetData(out temp, true) || !temp)
                        {
                            if (iter < 0)
                            {
                                throw new InvalidOperationException("Could not query d9device");
                            }
                            iter--;
                        }
                    }
                }

                // Use default dx11 devices that will be able to chat to dx9?
                device = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            }
            catch (Exception)
            {
                if (manager != null)
                {
                    manager.Dispose();
                    manager = null;
                }

                if (d9Device != null)
                {
                    d9Device.Dispose();
                    d9Device = null;
                }

                if (device != null)
                {
                    device.Dispose();
                    device = null;
                }
            }

            return manager == null ? null : new VideoDevice(VideoMode.Dx9, manager, device, d9Device);
        }

        private VideoDevice TryCreateDx11Manager()
        {
            //Device need bgra and video support
            DXGIDeviceManager dxgiManager = null;
            SharpDX.Direct3D11.Device device = null;
            try
            {
                device = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport);

                //Add multi thread protection on device
                using (var mt = device.QueryInterface<DeviceMultithread>())
                {
                    mt.SetMultithreadProtected(true);
                }

                //Reset device
                dxgiManager = new DXGIDeviceManager();
                dxgiManager.ResetDevice(device);
            }
            catch (Exception)
            {
                if (dxgiManager != null)
                {
                    dxgiManager.Dispose();
                    dxgiManager = null;
                }

                if (device != null)
                {
                    device.Dispose();
                    device = null;
                }
            }

            return dxgiManager == null ? null : new VideoDevice(VideoMode.Dx11, dxgiManager, device, null);
        }

        /**********************************************************
         * COM Imports only used here...
         * ********************************************************/
        [ComImport, System.Security.SuppressUnmanagedCodeSecurity,
        Guid("a0cade0f-06d5-4cf4-a1c7-f3cdd725aa75"),
        InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDirect3DDeviceManager9
        {
            void ResetDevice([In] IntPtr pDevice, [In] int resetToken);

            void Junk2();
            void Junk3();
            void Junk4();
            void Junk5();
            void Junk6();
        }

        [DllImport("dxva2.DLL", ExactSpelling = true, PreserveSig = false), SuppressUnmanagedCodeSecurity]
        private extern static void DXVA2CreateDirect3DDeviceManager9(out int pResetToken, out IDirect3DDeviceManager9 ppDXVAManager);
    }
}
