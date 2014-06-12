using System;
using VirtualSpace.Core.Renderer;

namespace VirtualSpace.Core
{
    public interface IEnvironment
    {
        void Initialise(IRenderer renderer);
        void Update(TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly);

        bool VSync { get; }
        bool ShowFPS { get; }
    }
}
