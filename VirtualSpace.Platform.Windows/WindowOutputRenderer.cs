using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using System.Runtime.InteropServices;
using VirtualSpace.Core;

namespace VirtualSpace.Platform.Windows
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public Vector3 position;
        public Vector2 texture;

        public Vertex(Vector3 pos, Vector2 tex)
        {
            position = pos;
            texture = tex;
        }
    }

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

            DrawSharedSurface();

            _swapChain.Present(0, PresentFlags.None);
        }

        private void DrawSharedSurface()
        {
            var result = _sharedSurfaceLock.Acquire(1, 100);
            if (result == Result.WaitTimeout)
            {
                // Try again later...
                return;
            }
            if (result != Result.Ok)
            {
                throw new SharpDXException(result);
            }


            var vertices = new Vertex[]
            {
                new Vertex(new Vector3(-1, -1, 0), new Vector2(0, 1)),
                new Vertex(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                new Vertex(new Vector3(1, -1, 0), new Vector2(1, 1)),
                new Vertex(new Vector3(1, -1, 0), new Vector2(1, 1)),
                new Vertex(new Vector3(-1, 1, 0), new Vector2(0, 0)),
                new Vertex(new Vector3(1, 1, 0), new Vector2(1, 0)),
            };

            // Setup shader for shared surface
            var sharedSurfaceDesc = _sharedSurface.Description;
            var shaderResource = new ShaderResourceView(_device, _sharedSurface, new ShaderResourceViewDescription
            {
                Format = sharedSurfaceDesc.Format,
                Dimension = ShaderResourceViewDimension.Texture2D,
                Texture2D = new ShaderResourceViewDescription.Texture2DResource
                {
                    MostDetailedMip = sharedSurfaceDesc.MipLevels - 1,
                    MipLevels = sharedSurfaceDesc.MipLevels
                }
            });

            _deviceContext.PixelShader.SetShaderResources(0, shaderResource);
            _deviceContext.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

            var vertexBuffer = Buffer.Create(_device, BindFlags.VertexBuffer, vertices);
            _deviceContext.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertexBuffer, Utilities.SizeOf<Vertex>(), 0));
            _deviceContext.Draw(6, 0);

            _sharedSurfaceLock.Release(1);
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
