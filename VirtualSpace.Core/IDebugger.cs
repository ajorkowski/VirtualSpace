using System.Collections.Generic;

namespace VirtualSpace.Core
{
    public interface IDebugger
    {
        void WriteLine(string format, params object[] items);

        IEnumerable<string> LastWrites { get; }
    }
}
