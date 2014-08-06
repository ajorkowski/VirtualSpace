using System;
using System.Collections.Generic;
using System.Linq;
using VirtualSpace.Core;

namespace VirtualSpace.Platform.Windows
{
    public class Debugger : IDebugger
    {
        private readonly Queue<string> _lastWrites;

        public Debugger()
        {
            _lastWrites = new Queue<string>();
        }

        public void WriteLine(string format, params object[] items)
        {
#if DEBUG
            var last = string.Format(format, items);
            Console.WriteLine(last);

            _lastWrites.Enqueue(last);
            if(_lastWrites.Count > 10)
            {
                _lastWrites.Dequeue();
            }
#endif
        }

        public IEnumerable<string> LastWrites
        {
            get { return _lastWrites.Reverse(); }
        }
    }
}
