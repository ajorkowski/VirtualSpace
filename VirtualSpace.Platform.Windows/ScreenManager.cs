using VirtualSpace.Core.Screen;

namespace VirtualSpace.Platform.Windows
{
    public sealed class ScreenManager : IScreenManager
    {
        public IScreen CreateDesktopScreen()
        {
            return new ScreenCaptureGdi();
        }
    }
}
