using System;
using System.Collections.Generic;
using System.Reflection;
using TinyIoC;
using VirtualSpace.Core;
using VirtualSpace.Platform.Windows;

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
                typeof(Device).Assembly
            }, true);

            var windowsOutput = TinyIoCContainer.Current.Resolve<IDevice>();
            windowsOutput.Run(TinyIoCContainer.Current.Resolve<IEnvironment>());
            TinyIoCContainer.Current.Dispose();
        }
    }
}
