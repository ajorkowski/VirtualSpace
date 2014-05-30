using SharpDX;
using SharpDX.Toolkit;
using VirtualSpace.Core;

namespace VirtualSpace.Platform.Windows.Rendering
{
    public sealed class WindowOutputRenderer : Game, IOutputRenderer
    {
        private readonly GraphicsDeviceManager _device;

        private readonly SceneRenderer _sceneRenderer;
        private readonly CameraProvider _cameraProvider;

        public WindowOutputRenderer()
        {
            _device = new GraphicsDeviceManager(this)
            {
                DeviceCreationFlags = SharpDX.Direct3D11.DeviceCreationFlags.BgraSupport
            };

            _sceneRenderer = new SceneRenderer(this);
            _cameraProvider = new CameraProvider(this);

            Content.RootDirectory = "Content";
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            base.Draw(gameTime);
        }

        public void Run()
        {
            base.Run();
        }
    }
}
