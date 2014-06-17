using System.Collections.Generic;

namespace VirtualSpace.Core.Device
{
    public interface IDeviceManager
    {
        /// <summary>
        /// Blocking call, starts the message loop
        /// </summary>
        /// <param name="menuItems"></param>
        void Run(IEnumerable<MenuItem> menuItems, IEnvironment environment);
        void Exit();

        IEnumerable<IOutputDevice> GetDevices();
    }
}
