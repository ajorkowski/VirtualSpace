using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using SharpOVR;
using System;
using System.Linq;
using VirtualSpace.Core;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Renderer;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Platform.Windows.Rendering.Providers;
using VirtualSpace.Platform.Windows.Rendering.Screen;

namespace VirtualSpace.Platform.Windows.Rendering
{
    public sealed class OcculusOutputRenderer : Game, IRenderer
    {
        private readonly IDebugger _debugger;
        private readonly GraphicsDeviceManager _device;
        private KeyboardProvider _keyboardProvider;

        private OcculusCameraProvider _cameraProvider;
        private ScreenManager _screenManager;
        private FpsRenderer _fpsRenderer;
        private IEnvironment _environment;

        private HMD _hmd;
        private Rect[] _eyeRenderViewport;
        private D3D11TextureData[] _eyeTexture;

        private RenderTarget2D _renderTarget;
        private ShaderResourceView _renderTargetSRView;
        private DepthStencilBuffer _depthStencilBuffer;
        private EyeRenderDesc[] _eyeRenderDesc;
        private PoseF[] _renderPose = new PoseF[2];

        public OcculusOutputRenderer(IDebugger debugger, int targetFPS)
        {
            _debugger = debugger;

#if DEBUG
            SharpDX.Configuration.EnableObjectTracking = true;
#endif

            _device = new GraphicsDeviceManager(this);
#if DEBUG
            _device.DeviceCreationFlags = _device.DeviceCreationFlags | SharpDX.Direct3D11.DeviceCreationFlags.Debug;
#endif

            Content.RootDirectory = "Content";

            IsMouseVisible = true;

            // Create our HMD
            var hmd = OVR.HmdCreate(0);

#if DEBUG
            hmd = hmd ?? OVR.HmdCreateDebug(HMDType.DK1);
#endif

            if (hmd == null)
            {
                throw new InvalidOperationException("Occulus Rift could not be detected...");
            }

            _hmd = ToDispose(hmd);

            // Match back buffer size with HMD resolution
            _device.PreferredBackBufferWidth = _hmd.Resolution.Width;
            _device.PreferredBackBufferHeight = _hmd.Resolution.Height;
            _device.SynchronizeWithVerticalRetrace = false;
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromMilliseconds(1000 / (double)targetFPS);
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
            _cameraProvider = ToDispose(new OcculusCameraProvider(this));
            _screenManager = ToDispose(new ScreenManager(this, _cameraProvider));
            _fpsRenderer = ToDispose(new FpsRenderer(this, _debugger));

            _hmd.RecenterPose();

            // Attach HMD to window
            var control = (System.Windows.Forms.Control)Window.NativeWindow;
            _hmd.AttachToWindow(control.Handle);

            // Create our render target
            var renderTargetSize = _hmd.GetDefaultRenderTargetSize(1.5f);
            _renderTarget = ToDispose(RenderTarget2D.New(GraphicsDevice, renderTargetSize.Width, renderTargetSize.Height, new MipMapCount(1), PixelFormat.R8G8B8A8.UNorm, TextureFlags.RenderTarget | TextureFlags.ShaderResource));
            _renderTargetSRView = ToDispose((ShaderResourceView)_renderTarget);

            // Create a depth stencil buffer for our render target
            _depthStencilBuffer = ToDispose(DepthStencilBuffer.New(GraphicsDevice, renderTargetSize.Width, renderTargetSize.Height, DepthFormat.Depth32, true));

            // Adjust render target size if there were any hardware limitations
            renderTargetSize.Width = _renderTarget.Width;
            renderTargetSize.Height = _renderTarget.Height;

            // The viewport sizes are re-computed in case renderTargetSize changed
            _eyeRenderViewport = new Rect[2];
            _eyeRenderViewport[0] = new Rect(0, 0, renderTargetSize.Width / 2, renderTargetSize.Height);
            _eyeRenderViewport[1] = new Rect((renderTargetSize.Width + 1) / 2, 0, _eyeRenderViewport[0].Width, _eyeRenderViewport[0].Height);

            // Create our eye texture data
            var renderTargetTexture = (SharpDX.Direct3D11.Texture2D)_renderTarget;
            _eyeTexture = new D3D11TextureData[2];
            _eyeTexture[0].Header.API = RenderAPIType.D3D11;
            _eyeTexture[0].Header.TextureSize = renderTargetSize;
            _eyeTexture[0].Header.RenderViewport = _eyeRenderViewport[0];
            _eyeTexture[0].pTexture = renderTargetTexture.NativePointer;
            _eyeTexture[0].pSRView = _renderTargetSRView.NativePointer;

            // Right eye uses the same texture, but different rendering viewport
            _eyeTexture[1] = _eyeTexture[0];
            _eyeTexture[1].Header.RenderViewport = _eyeRenderViewport[1];

            // Configure d3d11
            D3D11ConfigData d3d11cfg = new D3D11ConfigData();

            var device = (SharpDX.Direct3D11.Device)GraphicsDevice;
            var renderTargetView = (RenderTargetView)GraphicsDevice.BackBuffer;
            d3d11cfg.Header.API = RenderAPIType.D3D11;
            d3d11cfg.Header.RTSize = _hmd.Resolution;
            d3d11cfg.Header.Multisample = 1;
            d3d11cfg.pDevice = device.NativePointer;
            d3d11cfg.pDeviceContext = device.ImmediateContext.NativePointer;
            d3d11cfg.pBackBufferRT = renderTargetView.NativePointer;
            d3d11cfg.pSwapChain = (GraphicsDevice.Presenter.NativePresenter as CppObject).NativePointer;

            // Configure rendering
            _eyeRenderDesc = new EyeRenderDesc[2];
            if (!_hmd.ConfigureRendering(d3d11cfg, DistortionCapabilities.Chromatic | DistortionCapabilities.TimeWarp, _hmd.DefaultEyeFov, _eyeRenderDesc))
            {
                throw new Exception("Failed to configure rendering");
            }

            // Set enabled capabilities
            _hmd.EnabledCaps = HMDCapabilities.LowPersistence | HMDCapabilities.DynamicPrediction;

            // Configure tracking
            _hmd.ConfigureTracking(TrackingCapabilities.Orientation | TrackingCapabilities.Position | TrackingCapabilities.MagYawCorrection, TrackingCapabilities.None);

            // Dismiss the Heatlh and Safety Window (Note: this will take a few seconds)
            _hmd.DismissHSWDisplay();

            // Get HMD output
            var adapter = (Adapter)GraphicsDevice.Adapter;
            var hmdOutput = adapter.Outputs.FirstOrDefault(o => _hmd.DeviceName.StartsWith(o.Description.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (hmdOutput != null)
            {
                // Set game to fullscreen on rift
                var swapChain = (SwapChain)GraphicsDevice.Presenter.NativePresenter;
                var description = swapChain.Description.ModeDescription;
                swapChain.ResizeTarget(ref description);
                swapChain.SetFullscreenState(true, hmdOutput);
            }

            base.Initialize();

            _environment.Initialise(this, _keyboardProvider);
        }

        protected override void Update(GameTime gameTime)
        {
            // Use the base matrix during the update phase, for audio/game mechanics etc
            _cameraProvider.UseBaseMatrix();

            _environment.Update(this, gameTime.TotalGameTime, gameTime.ElapsedGameTime, gameTime.IsRunningSlowly);

            _fpsRenderer.Enabled = _environment.ShowFPS;
            _fpsRenderer.Visible = _environment.ShowFPS;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.SetRenderTargets(_depthStencilBuffer, _renderTarget);

            // Clear
            GraphicsDevice.Clear(Color.CornflowerBlue);

            // Begin frame
            _hmd.BeginFrame(0);

            for (int eyeIndex = 0; eyeIndex < 2; eyeIndex++)
            {
                var eye = _hmd.EyeRenderOrder[eyeIndex];
                _renderPose[(int)eye] = _hmd.GetEyePose(eye);

                _cameraProvider.UseOcculusEye(ref _eyeRenderDesc[(int)eye], ref _renderPose[(int)eye]);

                // Set Render Target and Viewport
                GraphicsDevice.SetViewport(_eyeRenderViewport[(int)eye].ToViewportF());

                // Perform the actual drawing
                base.Draw(gameTime);
            }

            // End frame
            _hmd.EndFrame(_renderPose, _eyeTexture);
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
