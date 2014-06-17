using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VirtualSpace.Core.Device
{
    public interface IOutputDevice : IDisposable
    {
        string Name { get; }

        void Run();
        void Stop();
    }
}
