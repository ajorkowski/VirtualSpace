using System;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Screen;

namespace VirtualSpace.Core
{
    public interface IEnvironment
    {
        ICamera Camera { get; }
        IScreen Desktop { get; }

        void Run();
        void Update(TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly);

        int WindowWidth { get; }
        int WindowHeight { get; }

        bool VSync { get; }
        bool ShowFPS { get; }
    }
}
