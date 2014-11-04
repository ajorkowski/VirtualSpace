using SharpDX.Direct3D;
using SharpDX.Direct3D11;

namespace VirtualSpace.Platform.Windows.Rendering
{
    public static class D3D11Device
    {
        public static SharpDX.Direct3D11.Device CreateDevice()
        {
#if DEBUG
            return new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.Debug);
#else
            return new SharpDX.Direct3D11.Device(DriverType.Hardware);
#endif
        }

        public static SharpDX.Direct3D11.Device CreateDevice(DeviceCreationFlags flags)
        {
#if DEBUG
            return new SharpDX.Direct3D11.Device(DriverType.Hardware, flags | DeviceCreationFlags.Debug);
#else
            return new SharpDX.Direct3D11.Device(DriverType.Hardware, flags);
#endif
        }
    }
}
