using OpenTK.Input;
using System;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Screen;

namespace VirtualSpace.Core
{
    public class Environment : IEnvironment
    {
        private readonly IDevice _device;
        private readonly ICamera _camera;
        private readonly IScreen _desktop;

        public Environment(IDevice device, ICamera camera, IScreenManager screenManager)
        {
            _device = device;
            _camera = camera;
            _desktop = screenManager.CreateDesktopScreen();
            _desktop.ScreenSize = 17.2f;
            VSync = true;

            _device.KeyPress += KeyPress;
        }

        public IScreen Desktop { get { return _desktop; } }
        public ICamera Camera { get { return _camera; } }

        public void Run()
        {
            _camera.MoveTo(0, 0, 20);
            _camera.LookAt(0, 0, 0);

            _desktop.StartCapture();

            _device.Run(this);
        }

        public void Update(TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly)
        {
            MoveCamera(elapsedGameTime);
        }

        public int WindowWidth { get { return _device.Width; } }
        public int WindowHeight { get { return _device.Height; } }

        public bool VSync { get; private set; }
        public bool ShowFPS { get; private set; }

        private void KeyPress(object sender, OpenTK.KeyPressEventArgs e)
        {
#if DEBUG
            if (e.KeyChar == 'v')
            {
                VSync = !VSync;
            }

            if (e.KeyChar == 'b')
            {
                ShowFPS = !ShowFPS;
            }
#endif
        }

        private void MoveCamera(TimeSpan elapsedGameTime)
        {
            float z = 0;
            float x = 0;

            if (_device.IsKeyDown(Key.W))
            {
                z -= .002f * (float)elapsedGameTime.TotalMilliseconds;
            }

            if (_device.IsKeyDown(Key.S))
            {
                z += .002f * (float)elapsedGameTime.TotalMilliseconds;
            }

            if (_device.IsKeyDown(Key.A))
            {
                x += .002f * (float)elapsedGameTime.TotalMilliseconds;
            }

            if (_device.IsKeyDown(Key.D))
            {
                x -= .002f * (float)elapsedGameTime.TotalMilliseconds;
            }

            if (z != 0 || x != 0)
            {
                _camera.MoveRelative(x, 0, z);
            }
        }
    }
}
