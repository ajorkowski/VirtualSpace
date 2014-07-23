using SharpDX;
using System;

namespace VirtualSpace.Platform.Windows.Video
{
    internal sealed class VideoDevice : IDisposable
    {
        public VideoDevice(VideoMode mode, ComObject d3dManager, SharpDX.Direct3D11.Device device, SharpDX.Direct3D9.DeviceEx d9Device)
        {
            VideoMode = mode;
            D3DManager = d3dManager;
            Device = device;
            Context = device.ImmediateContext;
            D9Device = d9Device;
        }

        public VideoMode VideoMode { get; private set; }
        public ComObject D3DManager { get; private set; }
        public SharpDX.Direct3D11.Device Device { get; private set; }
        public SharpDX.Direct3D11.DeviceContext Context { get; private set; }
        public SharpDX.Direct3D9.DeviceEx D9Device { get; private set; }

        public void Dispose()
        {
            if (D3DManager != null)
            {
                D3DManager.Dispose();
            }

            if (Context != null)
            {
                Context.Dispose();
            }

            if (Device != null)
            {
                Device.Dispose();
            }

            if (D9Device != null)
            {
                D9Device.Dispose();
            }

            GC.SuppressFinalize(this);
        }
    }

    internal enum VideoMode
    {
        Software,
        Dx9,
        Dx11
    }
}
