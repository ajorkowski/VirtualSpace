using System;

namespace VirtualSpace.Core
{
    public interface IEnvironment
    {
        void Update(TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly);

        bool VSync { get; }
        bool ShowFPS { get; }
    }
}
