using System.Collections.Generic;
using VirtualSpace.Core.Device;

namespace VirtualSpace.Platform.Windows.Device
{
    public class DeviceManager : IDeviceManager
    {
        private IDevice _window;

        public DeviceManager()
        {
            _window = new WindowedDevice();
        }

        public IEnumerable<IDevice> GetDevices()
        {
            return new List<IDevice> { _window };
        }
    }
}
