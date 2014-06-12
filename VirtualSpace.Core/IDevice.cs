using System;
using VirtualSpace.Core.Device;

namespace VirtualSpace.Core
{
    public interface IDevice : IDisposable
    {
        void Run(IEnvironment environment);

        IInput Input { get; }
    }
}
