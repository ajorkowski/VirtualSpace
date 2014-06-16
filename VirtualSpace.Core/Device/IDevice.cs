using System;
using System.Threading.Tasks;
using VirtualSpace.Core.AppContext;

namespace VirtualSpace.Core.Device
{
    public interface IDevice : IDisposable
    {
        string Name { get; }

        void Run(IEnvironment environment, IApplicationContext context);
        Task Stop();
    }
}
