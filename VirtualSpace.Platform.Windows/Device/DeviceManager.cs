using SharpDX.Toolkit;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using VirtualSpace.Core;
using VirtualSpace.Platform.Windows.Rendering.Providers;
using D = VirtualSpace.Core.Device;

namespace VirtualSpace.Platform.Windows.Device
{
    public class DeviceManager : Game, D.IDeviceManager
    {
        private readonly GraphicsDeviceManager _device;
        private readonly KeyboardProvider _keyboardProvider;

        private IEnumerable<D.MenuItem> _menuItems;
        private IEnvironment _environment;

        private NotifyIcon _trayIcon;
        private ContextMenu _trayMenu;

        private D.IOutputDevice _window;

        public DeviceManager()
        {
#if DEBUG
            SharpDX.Configuration.EnableObjectTracking = true;
#endif

            _device = ToDispose(new GraphicsDeviceManager(this));

#if DEBUG
            _device.DeviceCreationFlags = SharpDX.Direct3D11.DeviceCreationFlags.Debug;
#endif

            IsMouseVisible = true;

            _keyboardProvider = ToDispose(new KeyboardProvider(this));
            

            Content.RootDirectory = "Content";

            _window = ToDispose(new WindowOutputDevice(this));
        }

        public IEnumerable<D.IOutputDevice> GetDevices()
        {
            return new List<D.IOutputDevice> { _window };
        }

        public D.IInput Input { get { return _keyboardProvider; } }

        public void Run(IEnumerable<D.MenuItem> menuItems, IEnvironment environment)
        {
            _environment = environment;
            Services.AddService(environment);
            _menuItems = menuItems;

            base.Run();
        }

        protected override void LoadContent()
        {
            _trayMenu = ToDisposeContent(new ContextMenu());
            var items = CreateMenuItems(_menuItems);
            foreach (var i in items)
            {
                _trayMenu.MenuItems.Add(i);
            }

            _trayIcon = ToDisposeContent(new NotifyIcon());
            _trayIcon.Text = "Virtual Space";
            _trayIcon.Icon = new Icon(SystemIcons.Application, 40, 40);

            // Add menu to tray icon and show it.
            _trayIcon.ContextMenu = _trayMenu;
            _trayIcon.Visible = true;

            //var form = (Form)Window.NativeWindow;
            //form.Visible = true;
            //form.ShowInTaskbar = false;

            base.LoadContent();
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
    }
}
