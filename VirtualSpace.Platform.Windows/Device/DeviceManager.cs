using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VirtualSpace.Core;
using D = VirtualSpace.Core.Device;

namespace VirtualSpace.Platform.Windows.Device
{
    public sealed class DeviceManager : D.IDeviceManager, IDisposable
    {
        private NotifyIcon _trayIcon;
        private ContextMenu _trayMenu;
        private IEnvironment _environment;

        private List<D.IOutputDevice> _outputDevices;

        public DeviceManager(IDebugger debugger)
        {
            _outputDevices = new List<D.IOutputDevice>
            {
                new OcculusOutputDevice(this, debugger),
                new WindowOutputDevice(this, debugger)
            };

            _trayMenu = new ContextMenu();

            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "Virtual Space";
            _trayIcon.Icon = new Icon(SystemIcons.Application, 40, 40);

            // Add menu to tray icon and show it.
            _trayIcon.ContextMenu = _trayMenu;
            _trayIcon.Visible = true;
        }

        public IEnumerable<D.IOutputDevice> GetDevices()
        {
            return _outputDevices;
        }

        public IEnvironment Environment { get { return _environment; } }

        public void UpdateMenu(IEnumerable<D.MenuItem> menuItems)
        {
            _trayMenu.MenuItems.Clear();

            var items = CreateMenuItems(menuItems);
            _trayMenu.MenuItems.AddRange(items);
        }

        public void Run(IEnvironment environment)
        {
            _environment = environment;
            System.Windows.Forms.Application.Run();
        }

        public void Exit()
        {
            System.Windows.Forms.Application.Exit();
        }

        private MenuItem[] CreateMenuItems(IEnumerable<D.MenuItem> menuItems)
        {
            return menuItems.Select(m =>
            {
                var menu = new MenuItem(m.Name)
                {
                    Enabled = !m.IsDisabled,
                    Checked = m.IsSelected
                };

                if (m.Click != null)
                {
                    menu.Click += (o, s) => m.Click();
                }

                if (m.Children != null && m.Children.Any())
                {
                    menu.MenuItems.AddRange(CreateMenuItems(m.Children));
                }

                return menu;
            }).ToArray();
        }

        public void Dispose()
        {
            if (_trayIcon != null)
            {
                _trayIcon.Dispose();
            }

            if (_trayMenu != null)
            {
                _trayMenu.Dispose();
            }

            foreach (var o in _outputDevices)
            {
                o.Dispose();
            }
        }
    }
}
