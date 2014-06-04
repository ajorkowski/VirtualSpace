using SharpDX;

namespace VirtualSpace.Platform.Windows.Rendering.Providers
{
    internal interface ICameraProvider
    {
        Matrix View { get; }
        Matrix Projection { get; }
    }
}
