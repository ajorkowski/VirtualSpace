using SharpDX;
using SharpDX.Toolkit;

namespace VirtualSpace.Platform.Windows.Rendering
{
    internal sealed class CameraProvider : GameSystem, ICameraService
    {
        private Matrix _view;
        private Matrix _projection;

        public CameraProvider(Game game)
            : base(game)
        {
            Enabled = true;
            game.GameSystems.Add(this);
            game.Services.AddService(typeof(ICameraService), this);
        }

        public Matrix View { get { return _view; } }
        public Matrix Projection { get { return _projection; } }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var viewRotationAngle = (float)(gameTime.TotalGameTime.TotalSeconds * 0.2f);
            var eyePosition = Vector3.Transform(new Vector3(0, 2, 5), Quaternion.RotationAxis(Vector3.UnitY, viewRotationAngle));

            _view = Matrix.LookAtRH(eyePosition, new Vector3(0, 0, 0), Vector3.UnitY);
            _projection = Matrix.PerspectiveFovRH(MathUtil.PiOverFour, (float)GraphicsDevice.BackBuffer.Width / GraphicsDevice.BackBuffer.Height, 0.1f, 200.0f);
        }
    }
}
