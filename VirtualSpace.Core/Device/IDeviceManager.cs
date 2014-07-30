using System.Collections.Generic;

namespace VirtualSpace.Core.Device
{
    public interface IDeviceManager
    {
        void UpdateMenu(IEnumerable<MenuItem> menuItems);

        /// <summary>
        /// Blocking call, starts the message loop
        /// </summary>
        /// <param name="menuItems"></param>
        void Run(IEnvironment environment);
        void Exit();

        IEnumerable<IOutputDevice> GetDevices();
    }
}
