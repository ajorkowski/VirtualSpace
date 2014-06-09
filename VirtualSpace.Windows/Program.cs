using System;
using System.Collections.Generic;
using System.Reflection;
using TinyIoC;
using VirtualSpace.Core;
using VirtualSpace.Platform.Windows;
using VirtualSpace.Rendering;

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
                typeof(Device).Assembly,
                typeof(FpsService).Assembly
            }, true);

            var environment = TinyIoCContainer.Current.Resolve<IEnvironment>();
            environment.Run();
            TinyIoCContainer.Current.Dispose();
        }
    }
}
