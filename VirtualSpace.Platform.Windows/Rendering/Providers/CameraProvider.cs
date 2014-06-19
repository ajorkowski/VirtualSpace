﻿using SharpDX;
using SharpDX.Toolkit;
using VirtualSpace.Core.Renderer;

namespace VirtualSpace.Platform.Windows.Rendering.Providers
{
    internal sealed class CameraProvider : GameSystem, ICameraProvider, ICamera
    {
        private Matrix _view;
        private Matrix _projection;

        public CameraProvider(GameSystem parent)
            : base(parent.Game)
        {
            Enabled = true;
        }

        public Matrix View { get { return _view; } }
        public Matrix Projection { get { return _projection; } }

        public override void Initialize()
        {
 	         base.Initialize();

            _projection = Matrix.PerspectiveFovRH(MathUtil.PiOverFour, (float)GraphicsDevice.BackBuffer.Width / GraphicsDevice.BackBuffer.Height, 0.1f, 200.0f);
            _view = Matrix.Identity;
        }

        public void MoveRelative(float x, float y, float z)
        {
            _view = _view * Matrix.Translation(x, y, -z);
        }

        public void RotateRelative(float x, float y, float z)
        {
            if (x != 0)
            {
                _view = _view * Matrix.RotationX(x);
            }

            if (y != 0)
            {
                _view = _view * Matrix.RotationY(y);
            }

            if (z != 0)
            {
                _view = _view * Matrix.RotationZ(z);
            }
        }

        public void MoveAbsolute(float x, float y, float z)
        {
            _view = Matrix.Translation(x, y, -z) * _view;
        }

        public void MoveTo(float x, float y, float z)
        {
            _view.TranslationVector = new Vector3(x, y, -z);
        }

        public void LookAt(float x, float y, float z)
        {
            _view = Matrix.LookAtRH(_view.TranslationVector, new Vector3(x, y, z), _view.Up);
        }
    }
}
