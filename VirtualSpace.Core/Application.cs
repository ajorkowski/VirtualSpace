using System;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Core.Device;

namespace VirtualSpace.Core
{
    public sealed class Application : IApplication
    {
        private readonly IDeviceManager _deviceManager;
        private readonly IEnvironment _environment;

        private IOutputDevice _currentOutput;

        public Application(IDeviceManager deviceManager, IEnvironment environment)
        {
            _deviceManager = deviceManager;
            _environment = environment;
        }

        public void Run(string[] args)
        {
            var devices = _deviceManager.GetDevices();
            if (devices != null)
            {
                RefreshMenu();

                if(args.Any())
                {
                    _environment.WatchMovie(args[0]);
                }

                RunDevice(devices.First(d => d.IsAvailable));
                _deviceManager.Run(_environment);
            }
        }

        private void RunDevice(IOutputDevice device)
        {
            if (_currentOutput != null)
            {
                _currentOutput.Stop();
            }

            _currentOutput = device;
            _currentOutput.Run();

            RefreshMenu();
        }

        private void RefreshMenu()
        {
            var deviceItems = _deviceManager.GetDevices().Select(d => new MenuItem
            {
                Name = d.Name,
                IsDisabled = !d.IsAvailable,
                IsSelected = _currentOutput != null && d.Name == _currentOutput.Name,
                Click = d.IsAvailable && (_currentOutput == null || d.Name != _currentOutput.Name) ? () => RunDevice(d) : (Action)null
            });

            var menu = new List<MenuItem>
            {
                new MenuItem { Name = "Output Device", Children = deviceItems },
                new MenuItem { Name = "Exit", Click = () => _deviceManager.Exit() }
            };

            _deviceManager.UpdateMenu(menu);
        }
    }
}
