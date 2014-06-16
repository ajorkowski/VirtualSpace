using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AC = VirtualSpace.Core.AppContext;

namespace VirtualSpace.Platform.Windows
{
    public class ApplicationContext : AC.IApplicationContext
    {
        private SystemTrayApp _form;

        public void Run(IEnumerable<AC.MenuItem> menuItems)
        {
            _form = new SystemTrayApp(menuItems);
            Application.Run(_form);
        }

        public void Exit()
        {
            Application.Exit();
        }

        public object NativeHandle { get { return _form; } }

        private class SystemTrayApp : Form
        {
            private readonly NotifyIcon _trayIcon;
            private readonly ContextMenu _trayMenu;

            public SystemTrayApp(IEnumerable<AC.MenuItem> menuItems)
            {
                _trayMenu = new ContextMenu();
                var items = CreateMenuItems(menuItems);
                foreach(var i in items)
                {
                    _trayMenu.MenuItems.Add(i);
                }

                _trayIcon = new NotifyIcon();
                _trayIcon.Text = "MyTrayApp";
                _trayIcon.Icon = new Icon(SystemIcons.Application, 40, 40);

                // Add menu to tray icon and show it.
                _trayIcon.ContextMenu = _trayMenu;
                _trayIcon.Visible = true;
            }

            protected override void OnLoad(EventArgs e)
            {
                Visible = false;
                ShowInTaskbar = false;

                base.OnLoad(e);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _trayIcon.Dispose();
                    _trayMenu.Dispose();
                }

                base.Dispose(disposing);
            }

            private MenuItem[] CreateMenuItems(IEnumerable<AC.MenuItem> menuItems)
            {
                return menuItems.Select(m =>
                {
                    if(m.Click != null)
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
}
