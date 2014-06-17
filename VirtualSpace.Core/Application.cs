using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VirtualSpace.Core.Device;

namespace VirtualSpace.Core
{
    public sealed class Application : IApplication
    {
        private readonly IDeviceManager _deviceManager;
        private readonly IEnvironment _environment;

        public Application(IDeviceManager deviceManager, IEnvironment environment)
        {
            _deviceManager = deviceManager;
            _environment = environment;
        }

        public void Run()
        {
            var menuItems = new List<MenuItem>
            {
                new MenuItem { Name = "5 sec run", Click = RunDevice },
                new MenuItem { Name = "Exit", Click = () => _deviceManager.Exit() }
            };

            _deviceManager.Run(menuItems, _environment);
        }

        private async void RunDevice()
        {
            var device = _deviceManager.GetDevices().First();
            device.Run();
            await Task.Delay(5000);
            device.Stop();
        }
    }
}
