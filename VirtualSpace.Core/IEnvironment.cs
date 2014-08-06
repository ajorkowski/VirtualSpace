using System;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Renderer;

namespace VirtualSpace.Core
{
    public interface IEnvironment
    {
        void WatchMovie(string file);

        void Initialise(IRenderer renderer, IInput input);
        void Update(IRenderer renderer, TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly);
        void Uninitialise(IRenderer renderer);

        bool VSync { get; }
        bool ShowFPS { get; }
    }
}
