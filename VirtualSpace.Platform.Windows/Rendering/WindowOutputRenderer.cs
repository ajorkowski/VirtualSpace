using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
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
        private bool _currentVSync;

        public WindowOutputRenderer()
        {
#if DEBUG
            SharpDX.Configuration.EnableObjectTracking = true;
#endif

            _device = ToDispose(new GraphicsDeviceManager(this));
            _currentVSync = _device.SynchronizeWithVerticalRetrace;

#if DEBUG
            _device.DeviceCreationFlags = SharpDX.Direct3D11.DeviceCreationFlags.Debug;
#endif

            IsMouseVisible = true;

            _keyboardProvider = ToDispose(new KeyboardProvider(this));
            _cameraProvider = ToDispose(new CameraProvider(this));
            _sceneRenderer = ToDispose(new SceneRenderer(this));
            _fpsRenderer = ToDispose(new FpsRenderer(this));

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

        protected override void Initialize()
        {
            base.Initialize();

            _environment.Initialise();
        }

        protected override void Update(GameTime gameTime)
        {
            _environment.Update(gameTime.TotalGameTime, gameTime.ElapsedGameTime, gameTime.IsRunningSlowly);

            if (_environment.VSync != _currentVSync)
            {
                GraphicsDevice.Presenter.PresentInterval = _environment.VSync ? PresentInterval.One : PresentInterval.Immediate;
                _currentVSync = _environment.VSync;
            }
            _fpsRenderer.Enabled = _environment.ShowFPS;
            _fpsRenderer.Visible = _environment.ShowFPS;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            base.Draw(gameTime);
        }
    }
}
