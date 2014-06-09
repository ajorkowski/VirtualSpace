using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VirtualSpace.Core.Services
{
    public interface IFpsService : IDisposable
    {
        int Fps { get; }
        float Cpu { get; }
        float Ram { get; }

        void Update(TimeSpan totalGameTime);
    }
}
