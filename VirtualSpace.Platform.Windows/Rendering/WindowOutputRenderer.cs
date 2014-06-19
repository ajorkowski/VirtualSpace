using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using SharpDX.Windows;
using System.Collections.Generic;
using VirtualSpace.Core;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Renderer;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Platform.Windows.Rendering.Providers;
using VirtualSpace.Platform.Windows.Rendering.Screen;

namespace VirtualSpace.Platform.Windows.Rendering
{
    internal sealed class WindowOutputRenderer : GameWindowRenderer, IRenderer
    {
        private readonly IInput _input;

        private CameraProvider _cameraProvider;
        private ScreenManager _screenManager;
        private FpsRenderer _fpsRenderer;
        private SceneRenderer _sceneRenderer;
        private IEnvironment _environment;

        private bool _currentVSync;

        public WindowOutputRenderer(Game game, IInput input)
            : base(game, NewWindow())
        {
            Enabled = true;
            Visible = true;

            _input = input;
            
            game.GameSystems.Add(this);
        }

        public IInput Input { get { return _input; } }
        public ICamera Camera { get { return _cameraProvider; } }
        public IScreenManager ScreenManager { get { return _screenManager; } }

        public override void Initialize()
        {
            base.Initialize();

            _cameraProvider = ToDispose(new CameraProvider(this));
            _screenManager = ToDispose(new ScreenManager(this, _cameraProvider));
            _fpsRenderer = ToDispose(new FpsRenderer(this));
            _sceneRenderer = ToDispose(new SceneRenderer(this, _cameraProvider));

            _environment = Services.GetService<IEnvironment>();
            _environment.Initialise(this, _input);

            _currentVSync = GraphicsDevice.Presenter.PresentInterval != PresentInterval.Immediate;
        }

        protected override void LoadContent()
        {
            Window.IsMouseVisible = true;
            Window.Visible = true;

            base.LoadContent();
        }

        protected override void UnloadContent()
        {
            Window.Visible = false;

            base.UnloadContent();
        }

        public override void Update(GameTime gameTime)
        {
            _environment.Update(this, gameTime.TotalGameTime, gameTime.ElapsedGameTime, gameTime.IsRunningSlowly);

            if (_environment.VSync != _currentVSync)
            {
                GraphicsDevice.Presenter.PresentInterval = _environment.VSync ? PresentInterval.One : PresentInterval.Immediate;
                _currentVSync = _environment.VSync;
            }

            _sceneRenderer.Update(gameTime);
            _screenManager.Update(gameTime);

            if (_environment.ShowFPS)
            {
                _fpsRenderer.Update(gameTime);
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            _sceneRenderer.Draw(gameTime);
            _screenManager.Draw(gameTime);

            if (_environment.ShowFPS)
            {
                _fpsRenderer.Draw(gameTime);
            }

            base.Draw(gameTime);
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            Game.GameSystems.Remove(this);

            base.Dispose(disposeManagedResources);
        }

        private static GameContext NewWindow()
        {
            var window = new RenderForm("Virtual Space");
            return new GameContext(window, 1024, 800);
        }
    }
}
