using System;
using System.Collections.Generic;
using System.Reflection;
using TinyIoC;
using VirtualSpace.Core;
using VirtualSpace.Platform.Windows.Device;

namespace VirtualSpace.Window
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Setup container
            TinyIoCContainer.Current.AutoRegister(new List<Assembly>()
            {
                typeof(IEnvironment).Assembly,
                typeof(DeviceManager).Assembly
            }, true);

            var app = TinyIoCContainer.Current.Resolve<IApplication>();
            app.Run();
            TinyIoCContainer.Current.Dispose();
        }
    }
}
