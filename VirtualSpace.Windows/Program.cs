using PCLStorage;
using System;
using System.Collections.Generic;
using System.IO;
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
                typeof(WindowOutputDevice).Assembly
            }, true);

            TinyIoCContainer.Current.Register<IFolder>(new FileSystemFolder(Path.Combine(Directory.GetCurrentDirectory(), "Content"), false));

            var app = TinyIoCContainer.Current.Resolve<IApplication>();
            app.Run(args);
            TinyIoCContainer.Current.Dispose();
        }
    }
}
