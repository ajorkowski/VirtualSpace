using System;
using VirtualSpace.Core;

namespace VirtualSpace.Platform.Windows
{
    public class Debugger : IDebugger
    {
        public void WriteLine(string format, params object[] items)
        {
            Console.WriteLine(format, items);
        }
    }
}
