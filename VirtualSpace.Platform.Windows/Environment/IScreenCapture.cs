using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualSpace.Platform.Windows.Environment
{
    public interface IScreenCapture : IDisposable
    {
        int Width { get; }
        int Height { get; }
        Texture2D ScreenTexture { get; }
    }
}
