using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.MediaFoundation;
using SharpDX.Multimedia;
using SharpDX.X3DAudio;
using SharpDX.XAudio2;
using System;
using VideoDecoders.MediaFoundation;
using VirtualSpace.Platform.Windows.Rendering;

#if Win7
using SharpDX.MediaFoundation.DirectX;
#endif

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
#if Win8
                    var speakers = (Speakers)_masteringVoice.ChannelMask;
#endif
#if Win7
                    var speakers = _audioEngine.GetDeviceDetails(0).OutputFormat.ChannelMask;
#endif

                    _x3DAudio = new X3DAudio(speakers);
                }

                return _x3DAudio;
            }
        }

        public VideoDevice CreateVideoDevice()
        {
#if Win8
            var dx11Manager = TryCreateDx11Manager();
            if (dx11Manager != null)
            {
                return dx11Manager;
            }
#endif

#if Win7
            var dx9Manager = TryCreateDx9Manager();
            if (dx9Manager != null)
            {
                return dx9Manager;
            }
#endif

            // Fallback software device
            return new VideoDevice(VideoMode.Software, null, D3D11Device.CreateDevice(DeviceCreationFlags.BgraSupport), null);
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

#if Win7
        private VideoDevice TryCreateDx9Manager()
        {
            Direct3DDeviceManager manager = null;
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

                    manager = new Direct3DDeviceManager();
                    manager.ResetDevice(d9Device, manager.CreationToken);
                }

                // Use default dx11 devices that will be able to chat to dx9?
                device = D3D11Device.CreateDevice(DeviceCreationFlags.BgraSupport);
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
#endif

#if Win8
        private VideoDevice TryCreateDx11Manager()
        {
            //Device need bgra and video support
            DXGIDeviceManager dxgiManager = null;
            SharpDX.Direct3D11.Device device = null;
            try
            {
                device = D3D11Device.CreateDevice(DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport);

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
#endif
    }
}
