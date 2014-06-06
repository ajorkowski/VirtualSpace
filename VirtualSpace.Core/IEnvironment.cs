using System;
using VirtualSpace.Core.Screen;

namespace VirtualSpace.Core
{
    public interface IEnvironment
    {
        public IScreen Desktop { get; set; }

        void Initialise();
        void Update(TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly);

        bool VSync { get; }
        bool ShowFPS { get; }
    }
}
