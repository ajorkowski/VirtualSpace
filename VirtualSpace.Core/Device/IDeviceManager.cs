using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VirtualSpace.Core.Device
{
    public interface IDeviceManager
    {
        IEnumerable<IDevice> GetDevices();
    }
}
