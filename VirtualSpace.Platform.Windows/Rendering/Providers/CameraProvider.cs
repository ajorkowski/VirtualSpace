using SharpDX;
using SharpDX.Toolkit;
using VirtualSpace.Core.Math;
using VirtualSpace.Core.Renderer;

namespace VirtualSpace.Platform.Windows.Rendering.Providers
{
    internal class CameraProvider : GameSystem, ICameraProvider, ICamera
    {
        private Matrix _view;
        private Matrix _projection;

        public CameraProvider(Game game)
            : base(game)
        {
            Enabled = true;
            Visible = false;

            DrawOrder = UpdateOrder = RenderingOrder.Provider;

            game.GameSystems.Add(this);
        }

        public virtual Matrix View { get { return _view; } }
        public virtual Matrix Projection { get { return _projection; } }

        public override void Initialize()
        {
 	         base.Initialize();

            _projection = Matrix.PerspectiveFovRH(MathUtil.PiOverFour, (float)GraphicsDevice.BackBuffer.Width / GraphicsDevice.BackBuffer.Height, 0.1f, 200.0f);
            _view = Matrix.Identity;
        }

        public void MoveRelative(Vec3 move)
        {
            _view = _view * Matrix.Translation(move.X, move.Y, move.Z);
        }

        public void RotateRelative(Vec3 rot)
        {
            if (rot.X != 0)
            {
                _view = _view * Matrix.RotationX(rot.X);
            }

            if (rot.Y != 0)
            {
                _view = _view * Matrix.RotationY(rot.Y);
            }

            if (rot.Z != 0)
            {
                _view = _view * Matrix.RotationZ(rot.Z);
            }
        }

        public void MoveAbsolute(Vec3 move)
        {
            _view = Matrix.Translation(move.X, move.Y, move.Z) * _view;
        }

        public void MoveTo(Vec3 pos)
        {
            _view.TranslationVector = Vector3.Zero;
            _view = Matrix.Translation(pos.X, pos.Y, pos.Z) * _view;
        }

        public void LookAt(Vec3 pos)
        {
            _view = Matrix.LookAtRH(_view.TranslationVector, new Vector3(pos.X, pos.Y, pos.Z), _view.Up);
        }

        public Vec3 FindPointInWorldSpace(Vec3 pos)
        {
            var p = new Vector3(pos.X, pos.Y, pos.Z);
            var inv = _view;
            inv.Invert();
            Vector3 res;
            Vector3.Transform(ref p, ref inv, out res);
            return new Vec3(res.X, res.Y, res.Z);
        }
    }
}
