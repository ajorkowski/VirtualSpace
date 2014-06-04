using System;

namespace VirtualSpace.Core
{
    public class Environment : IEnvironment
    {
        private readonly IDevice _device;

        public Environment(IDevice device)
        {
            _device = device;
        }

        public void Update(TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly)
        {
            float z = 0;
            float x = 0;

            if (_device.Input.IsDown(Device.Keys.W))
            {
                z += .002f * (float)elapsedGameTime.TotalMilliseconds;
            }

            if (_device.Input.IsDown(Device.Keys.S))
            {
                z -= .002f * (float)elapsedGameTime.TotalMilliseconds;
            }

            if (_device.Input.IsDown(Device.Keys.A))
            {
                x += .002f * (float)elapsedGameTime.TotalMilliseconds;
            }

            if (_device.Input.IsDown(Device.Keys.D))
            {
                x -= .002f * (float)elapsedGameTime.TotalMilliseconds;
            }

            if (z != 0 || x != 0)
            {
                _device.Camera.MoveRelative(x, 0, z);
            }
        }
    }
}
