using SharpDX.Direct3D11;
using System;

namespace VirtualSpace.Platform.Windows.Environment
{
    public interface IScreenCapture : IDisposable
    {
        int Width { get; }
        int Height { get; }
        Texture2D ScreenTexture { get; }

        void CaptureScreen(SharpDX.Direct3D11.DeviceContext context);
    }
}
