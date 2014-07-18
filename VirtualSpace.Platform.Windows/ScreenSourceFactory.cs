using VirtualSpace.Core;
using VirtualSpace.Core.Desktop;
using VirtualSpace.Core.Video;
using VirtualSpace.Platform.Windows.Video;

namespace VirtualSpace.Platform.Windows
{
    public class ScreenSourceFactory : IScreenSourceFactory
    {
        public IVideo OpenVideo(string file)
        {
            return new VideoSource(file);
        }

        public IDesktop OpenPrimaryDesktop()
        {
            if (Screen.DesktopDX11.IsSupported)
            {
                return new Screen.DesktopDX11();
            }
            else
            {
                return new Screen.DesktopGdi();
            }
        }
    }
}
