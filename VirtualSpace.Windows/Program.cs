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
                typeof(WindowOutputRenderer).Assembly
            }, true);

            TinyIoCContainer.Current.Register<IOutputRenderer, WindowOutputRenderer>();

            var windowsOutput = TinyIoCContainer.Current.Resolve<IOutputRenderer>();
            windowsOutput.Run();
        }
    }
}
