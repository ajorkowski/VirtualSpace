using SharpDX;

namespace VirtualSpace.Platform.Windows.Rendering
{
    internal interface ICameraService
    {
        Matrix View { get; }
        Matrix Projection { get; }
    }
}
