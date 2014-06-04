using SharpDX;
using SharpDX.Toolkit;
using VirtualSpace.Core.Device;

namespace VirtualSpace.Platform.Windows.Rendering.Providers
{
    internal sealed class CameraProvider : GameSystem, ICameraProvider, ICamera
    {
        private Matrix _view;
        private Matrix _projection;

        public CameraProvider(Game game)
            : base(game)
        {
            Enabled = true;
            game.GameSystems.Add(this);
            game.Services.AddService(typeof(ICameraProvider), this);
        }

        public Matrix View { get { return _view; } }
        public Matrix Projection { get { return _projection; } }

        protected override void LoadContent()
        {
            base.LoadContent();

            _projection = Matrix.PerspectiveFovRH(MathUtil.PiOverFour, (float)GraphicsDevice.BackBuffer.Width / GraphicsDevice.BackBuffer.Height, 0.1f, 200.0f);
            _view = Matrix.LookAtRH(new Vector3(0, 2, 5), new Vector3(0, -2, -5), Vector3.UnitY);
        }

        public void MoveRelative(float x, float y, float z)
        {
            _view = _view * Matrix.Translation(x, y, z);
        }

        public void MoveAbsolute(float x, float y, float z)
        {
            _view = Matrix.Translation(x, y, z) * _view;
        }

        public void MoveTo(float x, float y, float z)
        {
            _view.TranslationVector = new Vector3(x, y, z);
        }

        public void LookAt(float x, float y, float z)
        {
            _view = Matrix.LookAtLH(_view.TranslationVector, new Vector3(x, y, z), _view.Up);
        }
    }
}
