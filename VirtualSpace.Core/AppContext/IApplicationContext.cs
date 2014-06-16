using System.Collections.Generic;

namespace VirtualSpace.Core.AppContext
{
    public interface IApplicationContext
    {
        void Run(IEnumerable<MenuItem> menuItems);
        void Exit();

        object NativeHandle { get; }
    }
}
