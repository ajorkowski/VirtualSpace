using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VirtualSpace.Core.AppContext;
using VirtualSpace.Core.Device;

namespace VirtualSpace.Core
{
    public sealed class Application : IApplication
    {
        private readonly IDeviceManager _deviceManager;
        private readonly IEnvironment _environment;
        private readonly IApplicationContext _context;

        public Application(IDeviceManager deviceManager, IEnvironment environment, IApplicationContext context)
        {
            _deviceManager = deviceManager;
            _environment = environment;
            _context = context;
        }

        public void Run()
        {
            var menuItems = new List<MenuItem>
            {
                new MenuItem { Name = "10 sec run", Click = RunDevice },
                new MenuItem { Name = "Exit", Click = () => _context.Exit() }
            };

            _context.Run(menuItems);
        }

        private async void RunDevice()
        {
            var device = _deviceManager.GetDevices().First();
            device.Run(_environment, _context);
            await Task.Delay(10000);
            await device.Stop();
        }
    }
}
