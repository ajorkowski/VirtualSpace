using System;
using VirtualSpace.Core.Screen;

namespace VirtualSpace.Core
{
    public interface IEnvironment
    {
        IScreen Desktop { get; }

        void Initialise();
        void Update(TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly);

        bool VSync { get; }
        bool ShowFPS { get; }
    }
}
