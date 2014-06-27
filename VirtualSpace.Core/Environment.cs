using System;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Renderer;

namespace VirtualSpace.Core
{
    public class Environment : IEnvironment
    {
        private IInput _input;
        private IRenderer _renderer;

        public Environment()
        {
            VSync = true;
        }

        public void Initialise(IRenderer renderer, IInput input)
        {
            _input = input;
            _renderer = renderer;
            _renderer.Camera.MoveTo(0, 0, 20);
            _renderer.Camera.LookAt(0, 0, 0);
            _renderer.ScreenManager.Desktop.ScreenSize = 17.2f;
            _renderer.ScreenManager.Desktop.CurveRadius = 17.2f;
        }

        public void Update(IRenderer renderer, TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly)
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
            float velocity = .002f * (float)elapsedGameTime.TotalMilliseconds;

            if (_input.IsDown(Keys.W))
            {
                z -= velocity;
            }

            if (_input.IsDown(Keys.S))
            {
                z += velocity;
            }

            if (_input.IsDown(Keys.A))
            {
                x += velocity;
            }

            if (_input.IsDown(Keys.D))
            {
                x -= velocity;
            }

            if (z != 0 || x != 0)
            {
                _renderer.Camera.MoveRelative(x, 0, z);
            }

            float xRot = 0;
            float yRot = 0;
            if (_input.IsDown(Keys.Up))
            {
                xRot -= velocity;
            }

            if (_input.IsDown(Keys.Down))
            {
                xRot += velocity;
            }

            if (_input.IsDown(Keys.Left))
            {
                yRot -= velocity;
            }

            if (_input.IsDown(Keys.Right))
            {
                yRot += velocity;
            }

            if (xRot != 0 || yRot != 0)
            {
                _renderer.Camera.RotateRelative(xRot, yRot, 0);
            }
        }
    }
}
