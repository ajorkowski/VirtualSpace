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

        private D.IOutputDevice _window;

        public DeviceManager()
        {
            _window = new WindowOutputDevice(this);
        }

        public IEnumerable<D.IOutputDevice> GetDevices()
        {
            return new List<D.IOutputDevice> { _window };
        }

        public IEnvironment Environment { get { return _environment; } }

        public void Run(IEnumerable<D.MenuItem> menuItems, IEnvironment environment)
        {
            _environment = environment;

            _trayMenu = new ContextMenu();
            var items = CreateMenuItems(menuItems);
            foreach (var i in items)
            {
                _trayMenu.MenuItems.Add(i);
            }

            _trayIcon = new NotifyIcon();
            _trayIcon.Text = "Virtual Space";
            _trayIcon.Icon = new Icon(SystemIcons.Application, 40, 40);

            // Add menu to tray icon and show it.
            _trayIcon.ContextMenu = _trayMenu;
            _trayIcon.Visible = true;

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
                if (m.Click != null)
                {
                    var local = m;
                    return new MenuItem(m.Name, (o, s) => local.Click());
                }
                else
                {
                    return new MenuItem(m.Name, CreateMenuItems(m.Children));
                }
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

            _window.Dispose();
        }
    }
}
