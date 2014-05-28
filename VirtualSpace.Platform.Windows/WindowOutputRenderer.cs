using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using VirtualSpace.Core;

namespace VirtualSpace.Platform.Windows
{
    public sealed class WindowOutputRenderer : IOutputRenderer
    {
        // Window rendering D3 privates
        private RenderForm _window;
        private SharpDX.Direct3D11.Device _device;
        private DeviceContext _deviceContext;
        private Texture2D _backBuffer;
        private RenderTargetView _renderView;
        private SwapChain _swapChain;

        // Shared surface privates
        private Texture2D _sharedSurface;
        private KeyedMutex _sharedSurfaceLock;

        public void Run()
        {
            InitializeWindow();

            uint numAdapter = 0;   // # of graphics card adapter
            uint numOutput = 0;    // # of output device (i.e. monitor)

            var factory = new Factory1();
            var textureDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.None,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                Format = Format.B8G8R8A8_UNorm,
                Height = factory.Adapters1[numAdapter].Outputs[numOutput].Description.DesktopBounds.Height,
                Width = factory.Adapters1[numAdapter].Outputs[numOutput].Description.DesktopBounds.Width,
                OptionFlags = ResourceOptionFlags.SharedKeyedmutex,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default
            };
            _sharedSurface = new Texture2D(_device, textureDesc);
            _sharedSurfaceLock = _sharedSurface.QueryInterface<KeyedMutex>();

            factory.Dispose();

            RenderLoop.Run(_window, MainLoop);
        }

        private void MainLoop()
        {
            _deviceContext.Rasterizer.SetViewports(new ViewportF[] { new Viewport(0, 0, _window.ClientSize.Width, _window.ClientSize.Height, 0.0f, 1.0f) }, 1);
            _deviceContext.OutputMerger.SetTargets(_renderView);

            _deviceContext.ClearRenderTargetView(_renderView, Color.CornflowerBlue);

            _swapChain.Present(0, PresentFlags.None);
        }

        private void DrawSharedSurface()
        {
            var result = _sharedSurfaceLock.Acquire(1, 100);
            switch (result)
            {
                case Result.Ok:
                    break;

            }
            if (result == Result.WaitTimeout)
            {
                // Try again later...
                return;
            }
            if (result != Result.Ok)
            {
                throw new SharpDXException(result);
            }
        }

        private void InitializeWindow()
        {
            bool isWindowed = true;
            _window = new RenderForm("VirtualSpace Debug Output")
            {
                AllowUserResizing = false,
                IsFullscreen = !isWindowed,
                Width = 800,
                Height = 600
            };

            // SwapChain description
            var desc = new SwapChainDescription()
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription(_window.ClientSize.Width, _window.ClientSize.Height, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                IsWindowed = isWindowed,
                OutputHandle = _window.Handle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };

            // Create Device and SwapChain
            SharpDX.Direct3D11.Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.None, desc, out _device, out _swapChain);
            _deviceContext = _device.ImmediateContext;

            // New RenderTargetView from the backbuffer
            var _backBuffer = Texture2D.FromSwapChain<Texture2D>(_swapChain, 0);
            _renderView = new RenderTargetView(_device, _backBuffer);
        }

        public void Dispose()
        {
            if (_sharedSurface != null)
            {
                _sharedSurface.Dispose();
                _sharedSurface = null;
            }

            if (_sharedSurfaceLock != null)
            {
                _sharedSurfaceLock.Dispose();
                _sharedSurfaceLock = null;
            }

            // Release all resources
            if (_renderView != null)
            {
                _renderView.Dispose();
                _renderView = null;
            }

            if (_backBuffer != null)
            {
                _backBuffer.Dispose();
                _backBuffer = null;
            }

            if (_deviceContext != null)
            {
                _deviceContext.Dispose();
                _deviceContext = null;
            }

            if (_device != null)
            {
                _device.Dispose();
                _device = null;
            }

            if (_swapChain != null)
            {
                _swapChain.Dispose();
                _swapChain = null;
            }
        }
    }
}
