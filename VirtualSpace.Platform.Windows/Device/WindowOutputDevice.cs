using System;
using System.Threading;
using System.Threading.Tasks;
using VirtualSpace.Core;
using VirtualSpace.Core.Device;
using VirtualSpace.Platform.Windows.Rendering;

namespace VirtualSpace.Platform.Windows.Device
{
    public class WindowOutputDevice : IOutputDevice
    {
        private readonly DeviceManager _manager;

        private WindowOutputRenderer _renderer;

        public WindowOutputDevice(DeviceManager manager)
        {
            _manager = manager;
        }

        public string Name
        {
            get { return "Window"; }
        }

        public void Run()
        {
            if(_renderer != null)
            {
                throw new InvalidOperationException("That device is already running!");
            }

            _renderer = new WindowOutputRenderer(_manager, _manager.Input);
        }

        public void Stop()
        {
            if (_renderer != null)
            {
                _renderer.Dispose();
                _renderer = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
