using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System.Linq;
using VirtualSpace.Core;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Renderer;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Platform.Windows.Rendering.Providers;
using VirtualSpace.Platform.Windows.Rendering.Screen;

namespace VirtualSpace.Platform.Windows.Rendering
{
    public sealed class WindowOutputRenderer : Game, IRenderer
    {
        private readonly GraphicsDeviceManager _device;
        private KeyboardProvider _keyboardProvider;

        private CameraProvider _cameraProvider;
        private ScreenManager _screenManager;
        private FpsRenderer _fpsRenderer;
        private SceneRenderer _sceneRenderer;
        private IEnvironment _environment;

        private bool _currentVSync;

        public WindowOutputRenderer()
        {
#if DEBUG
            SharpDX.Configuration.EnableObjectTracking = true;
#endif

            _device = new GraphicsDeviceManager(this);
//#if DEBUG
//            _device.DeviceCreationFlags = SharpDX.Direct3D11.DeviceCreationFlags.Debug;
//#endif

            Content.RootDirectory = "Content";

            IsMouseVisible = true;
        }

        public IInput Input { get { return _keyboardProvider; } }
        public ICamera Camera { get { return _cameraProvider; } }
        public IScreenManager ScreenManager { get { return _screenManager; } }

        public void Run(IEnvironment environment)
        {
            _environment = environment;
            base.Run();
        }

        protected override void Initialize()
        {
            _keyboardProvider = ToDispose(new KeyboardProvider(this));
            _cameraProvider = ToDispose(new CameraProvider(this));
            _screenManager = ToDispose(new ScreenManager(this, _cameraProvider));
            _fpsRenderer = ToDispose(new FpsRenderer(this));
            _sceneRenderer = ToDispose(new SceneRenderer(this, _cameraProvider));

            base.Initialize();

            _environment.Initialise(this, _keyboardProvider);

            _currentVSync = GraphicsDevice.Presenter.PresentInterval != PresentInterval.Immediate;
        }

        protected override void Update(GameTime gameTime)
        {
            _environment.Update(this, gameTime.TotalGameTime, gameTime.ElapsedGameTime, gameTime.IsRunningSlowly);

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

        protected override void Dispose(bool disposeManagedResources)
        {
            _environment.Uninitialise(this);

            if (disposeManagedResources)
            {
                foreach (var gs in GameSystems.ToList())
                {
                    GameSystems.Remove(gs);
                    (gs as IContentable).UnloadContent();
                }
            }

            base.Dispose(disposeManagedResources);
        }
    }
}
