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
        private readonly GraphicsDeviceManager _device;
        private KeyboardProvider _keyboardProvider;

        private CameraProvider _cameraProvider;
        private ScreenManager _screenManager;
        private FpsRenderer _fpsRenderer;
        private IEnvironment _environment;

        private bool _currentVSync;

        private HMD _hmd;
        private Rect[] _eyeRenderViewport;
        private D3D11TextureData[] _eyeTexture;

        private RenderTarget2D _renderTarget;
        private RenderTargetView _renderTargetView;
        private ShaderResourceView _renderTargetSRView;
        private DepthStencilBuffer _depthStencilBuffer;
        private EyeRenderDesc[] _eyeRenderDesc;
        private PoseF[] _renderPose = new PoseF[2];

        public OcculusOutputRenderer()
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

            // Initialize OVR Library
            OVR.Initialize();
            ToDispose(new Disposable(() => OVR.Shutdown()));

            // Create our HMD
            _hmd = ToDispose(OVR.HmdCreate(0) ?? OVR.HmdCreateDebug(HMDType.DK2));

            // Match back buffer size with HMD resolution
            _device.PreferredBackBufferWidth = _hmd.Resolution.Width;
            _device.PreferredBackBufferHeight = _hmd.Resolution.Height;
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

            // Attach HMD to window
            var control = (System.Windows.Forms.Control)Window.NativeWindow;
            _hmd.AttachToWindow(control.Handle);

            // Create our render target
            var renderTargetSize = _hmd.GetDefaultRenderTargetSize(1.5f);
            _renderTarget = ToDispose(RenderTarget2D.New(GraphicsDevice, renderTargetSize.Width, renderTargetSize.Height, new MipMapCount(1), PixelFormat.R8G8B8A8.UNorm, TextureFlags.RenderTarget | TextureFlags.ShaderResource));
            _renderTargetView = ToDispose((RenderTargetView)_renderTarget);
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

            // Dismiss the Heatlh and Safety Window
            //_hmd.DismissHSWDisplay();

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
            // Set Render Target and Viewport
            GraphicsDevice.SetRenderTargets(_depthStencilBuffer, _renderTarget);
            GraphicsDevice.SetViewport(0f, 0f, _renderTarget.Width, _renderTarget.Height);

            // Begin frame
            _hmd.BeginFrame(0);

            GraphicsDevice.Clear(Color.CornflowerBlue);

            for (int eyeIndex = 0; eyeIndex < 2; eyeIndex++)
            {
                var eye = _hmd.EyeRenderOrder[eyeIndex];
                var renderDesc = _eyeRenderDesc[(int)eye];
                var renderViewport = _eyeRenderViewport[(int)eye];
                _renderPose[(int)eye] = _hmd.GetEyePose(eye);

                // Calculate view matrix
                //var rollPitchYaw = Matrix.RotationY(eyeYaw);
                //var finalRollPitchYaw = rollPitchYaw * Matrix.RotationQuaternion(renderPose[(int)eye].Orientation);
                //var finalUp = Vector3.TransformNormal(new Vector3(0, 1, 0), finalRollPitchYaw);
                //var finalForward = Vector3.TransformNormal(new Vector3(0, 0, 1), finalRollPitchYaw);
                //var shiftedEyePos = eyePos + Vector3.TransformNormal(renderPose[(int)eye].Position * -Vector3.UnitZ, rollPitchYaw);
                //view = Matrix.Translation(renderDesc.ViewAdjust) * Matrix.LookAtRH(shiftedEyePos, shiftedEyePos + finalForward, finalUp);

                //// Calculate projection matrix
                //projection = OVR.MatrixProjection(renderDesc.Fov, 0.001f, 1000.0f, true);
                //projection.Transpose();

                // Set Viewport for our eye
                GraphicsDevice.SetViewport(renderViewport.ToViewportF());

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

    ///// <summary>
    ///// Simple RiftGame game using SharpDX.Toolkit.
    ///// </summary>
    //public class RiftGame : Game
    //{
    //    private GraphicsDeviceManager graphicsDeviceManager;

    //    private Matrix view;
    //    private Matrix projection;

    //    private Model model;

    //    private HMD hmd;
    //    private Rect[] eyeRenderViewport;
    //    private D3D11TextureData[] eyeTexture;

    //    private RenderTarget2D renderTarget;
    //    private RenderTargetView renderTargetView;
    //    private ShaderResourceView renderTargetSRView;
    //    private DepthStencilBuffer depthStencilBuffer;
    //    private EyeRenderDesc[] eyeRenderDesc;
    //    private PoseF[] renderPose = new PoseF[2];

    //    private Vector3 eyePos = new Vector3(0, 0, 7);
    //    private float eyeYaw = 3.141592f;

    //    /// <summary>
    //    /// Initializes a new instance of the <see cref="RiftGame" /> class.
    //    /// </summary>
    //    public RiftGame()
    //    {
    //        // Creates a graphics manager. This is mandatory.
    //        graphicsDeviceManager = new GraphicsDeviceManager(this);

    //        // Setup the relative directory to the executable directory 
    //        // for loading contents with the ContentManager
    //        Content.RootDirectory = "Content";

    //        // Initialize OVR Library
    //        OVR.Initialize();

    //        // Create our HMD
    //        hmd = OVR.HmdCreate(0) ?? OVR.HmdCreateDebug(HMDType.DK1);

    //        // Match back buffer size with HMD resolution
    //        graphicsDeviceManager.PreferredBackBufferWidth = hmd.Resolution.Width;
    //        graphicsDeviceManager.PreferredBackBufferHeight = hmd.Resolution.Height;
    //    }

    //    protected override void Initialize()
    //    {
    //        // Modify the title of the window
    //        Window.Title = "RiftGame";

    //        // Attach HMD to window
    //        var control = (System.Windows.Forms.Control)Window.NativeWindow;
    //        hmd.AttachToWindow(control.Handle);

    //        // Create our render target
    //        var renderTargetSize = hmd.GetDefaultRenderTargetSize(1.5f);
    //        renderTarget = RenderTarget2D.New(GraphicsDevice, renderTargetSize.Width, renderTargetSize.Height, new MipMapCount(1), PixelFormat.R8G8B8A8.UNorm, TextureFlags.RenderTarget | TextureFlags.ShaderResource);
    //        renderTargetView = (RenderTargetView)renderTarget;
    //        renderTargetSRView = (ShaderResourceView)renderTarget;

    //        // Create a depth stencil buffer for our render target
    //        depthStencilBuffer = DepthStencilBuffer.New(GraphicsDevice, renderTargetSize.Width, renderTargetSize.Height, DepthFormat.Depth32, true);

    //        // Adjust render target size if there were any hardware limitations
    //        renderTargetSize.Width = renderTarget.Width;
    //        renderTargetSize.Height = renderTarget.Height;

    //        // The viewport sizes are re-computed in case renderTargetSize changed
    //        eyeRenderViewport = new Rect[2];
    //        eyeRenderViewport[0] = new Rect(0, 0, renderTargetSize.Width / 2, renderTargetSize.Height);
    //        eyeRenderViewport[1] = new Rect((renderTargetSize.Width + 1) / 2, 0, eyeRenderViewport[0].Width, eyeRenderViewport[0].Height);

    //        // Create our eye texture data
    //        eyeTexture = new D3D11TextureData[2];
    //        eyeTexture[0].Header.API = RenderAPIType.D3D11;
    //        eyeTexture[0].Header.TextureSize = renderTargetSize;
    //        eyeTexture[0].Header.RenderViewport = eyeRenderViewport[0];
    //        eyeTexture[0].pTexture = ((SharpDX.Direct3D11.Texture2D)renderTarget).NativePointer;
    //        eyeTexture[0].pSRView = renderTargetSRView.NativePointer;

    //        // Right eye uses the same texture, but different rendering viewport
    //        eyeTexture[1] = eyeTexture[0];
    //        eyeTexture[1].Header.RenderViewport = eyeRenderViewport[1];

    //        // Configure d3d11
    //        var device = (SharpDX.Direct3D11.Device)GraphicsDevice;
    //        D3D11ConfigData d3d11cfg = new D3D11ConfigData();
    //        d3d11cfg.Header.API = RenderAPIType.D3D11;
    //        d3d11cfg.Header.RTSize = hmd.Resolution;
    //        d3d11cfg.Header.Multisample = 1;
    //        d3d11cfg.pDevice = device.NativePointer;
    //        d3d11cfg.pDeviceContext = device.ImmediateContext.NativePointer;
    //        d3d11cfg.pBackBufferRT = ((RenderTargetView)GraphicsDevice.BackBuffer).NativePointer;
    //        d3d11cfg.pSwapChain = ((SharpDX.DXGI.SwapChain)GraphicsDevice.Presenter.NativePresenter).NativePointer;

    //        // Configure rendering
    //        eyeRenderDesc = new EyeRenderDesc[2];
    //        if (!hmd.ConfigureRendering(d3d11cfg, DistortionCapabilities.Chromatic | DistortionCapabilities.TimeWarp, hmd.DefaultEyeFov, eyeRenderDesc))
    //        {
    //            throw new Exception("Failed to configure rendering");
    //        }

    //        // Set enabled capabilities
    //        hmd.EnabledCaps = HMDCapabilities.LowPersistence | HMDCapabilities.DynamicPrediction;

    //        // Configure tracking
    //        hmd.ConfigureTracking(TrackingCapabilities.Orientation | TrackingCapabilities.Position | TrackingCapabilities.MagYawCorrection, TrackingCapabilities.None);

    //        // Dismiss the Heatlh and Safety Window
    //        hmd.DismissHSWDisplay();

    //        // Get HMD output
    //        var adapter = (Adapter)GraphicsDevice.Adapter;
    //        var hmdOutput = adapter.Outputs.FirstOrDefault(o => hmd.DeviceName.StartsWith(o.Description.DeviceName, StringComparison.OrdinalIgnoreCase));
    //        if (hmdOutput != null)
    //        {
    //            // Set game to fullscreen on rift
    //            var swapChain = (SwapChain)GraphicsDevice.Presenter.NativePresenter;
    //            var description = swapChain.Description.ModeDescription;
    //            swapChain.ResizeTarget(ref description);
    //            swapChain.SetFullscreenState(true, hmdOutput);
    //        }

    //        base.Initialize();
    //    }

    //    protected override void LoadContent()
    //    {
    //        // Load a 3D model
    //        // The [Ship.fbx] file is defined with the build action [ToolkitModel] in the project
    //        model = Content.Load<Model>("Ship");

    //        // Enable default lighting on model.
    //        BasicEffect.EnableDefaultLighting(model, true);

    //        base.LoadContent();
    //    }

    //    protected override void Update(GameTime gameTime)
    //    {
    //        base.Update(gameTime);

    //        // Calculates the world and the view based on the model size
    //        view = Matrix.LookAtRH(new Vector3(0.0f, 0.0f, 7.0f), new Vector3(0, 0.0f, 0), Vector3.UnitY);
    //        projection = Matrix.PerspectiveFovRH(0.9f, (float)GraphicsDevice.BackBuffer.Width / GraphicsDevice.BackBuffer.Height, 0.1f, 100.0f);
    //    }

    //    protected override void Draw(GameTime gameTime)
    //    {
    //        // Set Render Target and Viewport
    //        GraphicsDevice.SetRenderTargets(depthStencilBuffer, renderTarget);
    //        GraphicsDevice.SetViewport(0f, 0f, (float)renderTarget.Width, (float)renderTarget.Height);

    //        // Begin frame
    //        hmd.BeginFrame(0);

    //        // Clear the screen
    //        GraphicsDevice.Clear(Color.CornflowerBlue);

    //        for (int eyeIndex = 0; eyeIndex < 2; eyeIndex++)
    //        {
    //            var eye = hmd.EyeRenderOrder[eyeIndex];
    //            var renderDesc = eyeRenderDesc[(int)eye];
    //            var renderViewport = eyeRenderViewport[(int)eye];
    //            renderPose[(int)eye] = hmd.GetEyePose(eye);

    //            // Calculate view matrix
    //            var rollPitchYaw = Matrix.RotationY(eyeYaw);
    //            var finalRollPitchYaw = rollPitchYaw * Matrix.RotationQuaternion(renderPose[(int)eye].Orientation);
    //            var finalUp = Vector3.TransformNormal(new Vector3(0, 1, 0), finalRollPitchYaw);
    //            var finalForward = Vector3.TransformNormal(new Vector3(0, 0, 1), finalRollPitchYaw);
    //            var shiftedEyePos = eyePos + Vector3.TransformNormal(renderPose[(int)eye].Position * -Vector3.UnitZ, rollPitchYaw);
    //            view = Matrix.Translation(renderDesc.ViewAdjust) * Matrix.LookAtRH(shiftedEyePos, shiftedEyePos + finalForward, finalUp);

    //            // Calculate projection matrix
    //            projection = OVR.MatrixProjection(renderDesc.Fov, 0.001f, 1000.0f, true);
    //            projection.Transpose();

    //            // Set Viewport for our eye
    //            GraphicsDevice.SetViewport(renderViewport.ToViewportF());

    //            // Perform the actual drawing
    //            InternalDraw(gameTime);
    //        }

    //        // End frame
    //        hmd.EndFrame(renderPose, eyeTexture);
    //    }

    //    protected virtual void InternalDraw(GameTime gameTime)
    //    {
    //        // Use time in seconds directly
    //        var time = (float)gameTime.TotalGameTime.TotalSeconds;

    //        // Constant used to translate 3d models
    //        float translateX = 0.0f;

    //        // ------------------------------------------------------------------------
    //        // Draw the 3d model
    //        // ------------------------------------------------------------------------
    //        var world = Matrix.Scaling(0.003f) *
    //                    Matrix.RotationY(time) *
    //                    Matrix.Translation(0, -1.5f, 2.0f);
    //        model.Draw(GraphicsDevice, world, view, projection);
    //        translateX += 3.5f;

    //        base.Draw(gameTime);
    //    }

    //    protected override void EndDraw()
    //    {
    //        // Cancel EndDraw() as the Present call is made through hmd.EndFrame()
    //    }

    //    protected override void Dispose(bool disposeManagedResources)
    //    {
    //        base.Dispose(disposeManagedResources);
    //        if (disposeManagedResources)
    //        {
    //            // Release the HMD
    //            hmd.Dispose();

    //            // Shutdown the OVR Library
    //            OVR.Shutdown();
    //        }
    //    }
    //}
}
