using System;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Renderer;

namespace VirtualSpace.Core
{
    public interface IEnvironment
    {
        void Initialise(IRenderer renderer, IInput input);
        void Update(IRenderer renderer, TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly);

        bool VSync { get; }
        bool ShowFPS { get; }
    }
}
