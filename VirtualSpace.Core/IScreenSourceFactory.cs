using VirtualSpace.Core.Desktop;
using VirtualSpace.Core.Video;

namespace VirtualSpace.Core
{
    public interface IScreenSourceFactory
    {
        IVideo OpenVideo(string file);
        IDesktop OpenPrimaryDesktop();
    }
}
