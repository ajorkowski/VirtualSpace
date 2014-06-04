using SharpDX;
using SharpDX.Toolkit;
using VirtualSpace.Core;
using VirtualSpace.Core.Device;
using VirtualSpace.Platform.Windows.Rendering.Providers;

namespace VirtualSpace.Platform.Windows.Rendering
{
    public sealed class WindowOutputRenderer : Game
    {
        private readonly GraphicsDeviceManager _device;

        private readonly SceneRenderer _sceneRenderer;
        private readonly CameraProvider _cameraProvider;
        private readonly FpsRenderer _fpsRenderer;
        private readonly KeyboardProvider _keyboardProvider;

        private IEnvironment _environment;

        public WindowOutputRenderer()
        {
            _device = new GraphicsDeviceManager(this);

            IsMouseVisible = true;
            
            _sceneRenderer = new SceneRenderer(this);
            _cameraProvider = new CameraProvider(this);
            _fpsRenderer = new FpsRenderer(this);
            _keyboardProvider = new KeyboardProvider(this);

            Content.RootDirectory = "Content";
        }

        public void Run(IEnvironment environment)
        {
            _environment = environment;
            Services.AddService(environment);

            base.Run();
        }

        public IInput Input { get { return _keyboardProvider; } }
        public ICamera Camera { get { return _cameraProvider; } }

        protected override void Update(GameTime gameTime)
        {
            _environment.Update(gameTime.TotalGameTime, gameTime.ElapsedGameTime, gameTime.IsRunningSlowly);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            base.Draw(gameTime);
        }
    }
}
