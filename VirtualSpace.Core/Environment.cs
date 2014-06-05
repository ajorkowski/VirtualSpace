using System;
using VirtualSpace.Core.Device;

namespace VirtualSpace.Core
{
    public class Environment : IEnvironment
    {
        private readonly IDevice _device;
        private readonly IInput _input;

        public Environment(IDevice device)
        {
            _device = device;
            _input = device.Input;
            VSync = true;
        }

        public void Update(TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly)
        {
            MoveCamera(elapsedGameTime);

#if DEBUG
            if (_input.IsPressed(Keys.V))
            {
                VSync = !VSync;
            }

            if (_input.IsPressed(Keys.B))
            {
                ShowFPS = !ShowFPS;
            }
#endif
        }

        public bool VSync { get; private set; }
        public bool ShowFPS { get; private set; }

        private void MoveCamera(TimeSpan elapsedGameTime)
        {
            float z = 0;
            float x = 0;

            if (_input.IsDown(Keys.W))
            {
                z += .002f * (float)elapsedGameTime.TotalMilliseconds;
            }

            if (_input.IsDown(Keys.S))
            {
                z -= .002f * (float)elapsedGameTime.TotalMilliseconds;
            }

            if (_input.IsDown(Keys.A))
            {
                x += .002f * (float)elapsedGameTime.TotalMilliseconds;
            }

            if (_input.IsDown(Keys.D))
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
