using VirtualSpace.Core;
using VirtualSpace.Core.Device;
using VirtualSpace.Platform.Windows.Rendering;

namespace VirtualSpace.Platform.Windows
{
    public sealed class Device : IDevice
    {
        private readonly WindowOutputRenderer _renderer;

        public Device()
        {
            _renderer = new WindowOutputRenderer();
        }

        public void Run(IEnvironment environment)
        {
            _renderer.Run(environment);
        }

        public void Dispose()
        {
            _renderer.Dispose();
        }

        public IInput Input { get { return _renderer.Input; } }
    }
}
