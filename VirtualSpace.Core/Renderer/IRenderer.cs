using VirtualSpace.Core.Renderer.Screen;

namespace VirtualSpace.Core.Renderer
{
    public interface IRenderer
    {
        ICamera Camera { get; }
        IScreenManager ScreenManager { get; }
    }
}
