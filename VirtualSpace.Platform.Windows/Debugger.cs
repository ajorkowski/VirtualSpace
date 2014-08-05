using System;
using VirtualSpace.Core;

namespace VirtualSpace.Platform.Windows
{
    public class Debugger : IDebugger
    {
        private string _lastWrite;

        public void WriteLine(string format, params object[] items)
        {
            _lastWrite = string.Format(format, items);
            Console.WriteLine(_lastWrite);
        }

        public string LastWrite
        {
            get { return _lastWrite; }
        }
    }
}
