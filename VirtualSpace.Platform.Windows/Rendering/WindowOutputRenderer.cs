using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using SharpDX.Windows;
using System.Threading;
using System.Windows.Forms;
using VirtualSpace.Core;
using VirtualSpace.Core.AppContext;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Renderer;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Platform.Windows.Rendering.Providers;
using VirtualSpace.Platform.Windows.Rendering.Screen;

namespace VirtualSpace.Platform.Windows.Rendering
{
    internal sealed class WindowOutputRenderer : Game, IRenderer
    {
        private readonly GraphicsDeviceManager _device;
        private readonly GameContext _context;

        private readonly SceneRenderer _sceneRenderer;
        private readonly ScreenManager _screenManager;
        private readonly CameraProvider _cameraProvider;
        private readonly FpsRenderer _fpsRenderer;
        private readonly KeyboardProvider _keyboardProvider;

        private CancellationToken _runToken;

        private IEnvironment _environment;
        private bool _currentVSync;

        public WindowOutputRenderer(IApplicationContext context)
        {
            var window = new RenderForm("Virtual Space");
            window.Owner = context.NativeHandle as Form;
            _context = new GameContext(window, 800, 600);

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
            _screenManager = ToDispose(new ScreenManager(this));
            _fpsRenderer = ToDispose(new FpsRenderer(this));

            Content.RootDirectory = "Content";
        }

        public void Run(IEnvironment environment, CancellationToken token)
        {
            _environment = environment;
            Services.AddService(environment);

            _runToken = token;

            base.Run(_context);
        }

        public IInput Input { get { return _keyboardProvider; } }
        public ICamera Camera { get { return _cameraProvider; } }
        public IScreenManager ScreenManager { get { return _screenManager; } }

        protected override void Initialize()
        {
            base.Initialize();

            _environment.Initialise(this, Input);
        }

        protected override void Update(GameTime gameTime)
        {
            if (_runToken.IsCancellationRequested)
            {
                Exit();
                return;
            }

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
