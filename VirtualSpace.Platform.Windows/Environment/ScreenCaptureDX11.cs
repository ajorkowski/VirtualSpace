using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VirtualSpace.Core.Environment;

namespace VirtualSpace.Platform.Windows.Environment
{
    internal sealed class ScreenCaptureDX11 : IScreenCapture
    {
        private static readonly int SizeOfMoveRectangle = Marshal.SizeOf(typeof(OutputDuplicateMoveRectangle));
        private static readonly int SizeOfDirtyRectangle = Marshal.SizeOf(typeof(Rectangle));
        
        private readonly IScreen _screen;
        private readonly SharpDX.Direct3D11.Texture2D _sharedTexture;

        private int _nScreenWidth;
        private int _nScreenHeight;

        private SharpDX.Direct3D11.Device _captureDevice;
        private OutputDuplication _outputDuplication;
        private KeyedMutex _mutex;

        private int _moveBufferLength;
        private OutputDuplicateMoveRectangle[] _moveBuffer;

        private int _dirtyBufferLength;
        private Rectangle[] _dirtyBuffer;

        private Texture2D _moveTexture;
        private RenderTargetView _dirtyRenderView;

        private bool _isRunning;
        private Task _captureLoop;

        public ScreenCaptureDX11(IScreen screen, SharpDX.Toolkit.Graphics.GraphicsDevice device)
        {
            _screen = screen;

            Factory1 factory = new Factory1();
            var desktop = factory.Adapters1[0].Outputs[0];

            _nScreenWidth = desktop.Description.DesktopBounds.Width;
            _nScreenHeight = desktop.Description.DesktopBounds.Height;

            _sharedTexture = new SharpDX.Direct3D11.Texture2D(device, new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.None,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Height = _nScreenHeight,
                Width = _nScreenWidth,
                OptionFlags = ResourceOptionFlags.SharedKeyedmutex,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default
            });

            _captureDevice = new SharpDX.Direct3D11.Device(DriverType.Hardware);
            using (var sharedResource = _sharedTexture.QueryInterface<SharpDX.DXGI.Resource1>())
            {
                _captureDevice.OpenSharedResource<Texture2D>(sharedResource.SharedHandle);
            }

            _mutex = _sharedTexture.QueryInterface<KeyedMutex>();
            using(var output = new Output1(desktop.NativePointer))
            {
                _outputDuplication = output.DuplicateOutput(_captureDevice);
            }

            _isRunning = true;
            _captureLoop = Task.Run(() => CaptureLoop());
        }

        public int Width { get { return _nScreenWidth; } }
        public int Height { get { return _nScreenHeight; } }
        public SharpDX.Direct3D11.Texture2D ScreenTexture { get { return _sharedTexture; } }

        public void CaptureScreen(SharpDX.Direct3D11.DeviceContext context)
        {
            // Do nothing...
        }

        public void Dispose()
        {
            if(_captureDevice != null)
            {
                _captureDevice.Dispose();
                _captureDevice = null;
            }

            if(_outputDuplication != null)
            {
                _outputDuplication.Dispose();
                _outputDuplication = null;
            }

            if(_mutex != null)
            {
                _mutex.Dispose();
                _mutex = null;
            }

            _sharedTexture.Dispose();

            if(_moveTexture != null)
            {
                _moveTexture.Dispose();
            }

            if(_dirtyRenderView != null)
            {
                _dirtyRenderView.Dispose();
            }
        }

        private void CaptureLoop()
        {
            var context = _captureDevice.ImmediateContext;

            SharpDX.DXGI.Resource resource;
            OutputDuplicateFrameInformation frameInfo;
            _outputDuplication.AcquireNextFrame(1000, out frameInfo, out resource);

            var capturedTexture = resource.QueryInterface<Texture2D>();
            resource.Dispose();

            if (frameInfo.TotalMetadataBufferSize > 0)
            {
                if (frameInfo.TotalMetadataBufferSize > _moveBufferLength)
                {
                    _moveBufferLength = frameInfo.TotalMetadataBufferSize;
                    _moveBuffer = new OutputDuplicateMoveRectangle[frameInfo.TotalMetadataBufferSize / SizeOfMoveRectangle];
                }

                int bufferSize;
                _outputDuplication.GetFrameMoveRects(_moveBufferLength, _moveBuffer, out bufferSize);
                var moveCount = bufferSize / SizeOfMoveRectangle;

                if (frameInfo.TotalMetadataBufferSize - bufferSize > _dirtyBufferLength)
                {
                    _dirtyBufferLength = frameInfo.TotalMetadataBufferSize - bufferSize;
                    _dirtyBuffer = new Rectangle[(_dirtyBufferLength / SizeOfDirtyRectangle)];
                }
                _outputDuplication.GetFrameDirtyRects(_dirtyBufferLength, _dirtyBuffer, out bufferSize);
                var dirtyCount = bufferSize / SizeOfDirtyRectangle;

                var result = _mutex.Acquire(0, 1000);
                if (result == Result.WaitTimeout)
                {
                    // TODO:
                }

                var desc = _sharedTexture.Description;
                if (moveCount > 0)
                {
                    DoMoves(context, moveCount, _moveBuffer);
                }

                if (dirtyCount > 0)
                {
                    //DoDirty(context, dirtyCount, _dirtyBuffer);
                }

                _mutex.Release(0);
            }

            capturedTexture.Dispose();
        }

        private void DoMoves(SharpDX.Direct3D11.DeviceContext context, int moveCount, OutputDuplicateMoveRectangle[] rect)
        {
            if(_moveTexture == null)
            {
                var desc = _sharedTexture.Description;
                desc.BindFlags = BindFlags.RenderTarget;
                desc.OptionFlags = ResourceOptionFlags.None;
                _moveTexture = new Texture2D(_captureDevice, desc);
            }

            for(int i=0; i<moveCount; i++)
            {
                var currentMove = rect[i];

                // Copy subregion to move texture
                var region = new ResourceRegion
                {
                    Left = currentMove.SourcePoint.X,
                    Top = currentMove.SourcePoint.Y,
                    Front = 0,
                    Right = currentMove.SourcePoint.X + currentMove.DestinationRect.Width,
                    Bottom = currentMove.SourcePoint.Y + currentMove.DestinationRect.Height,
                    Back = 0
                };
                context.CopySubresourceRegion(_sharedTexture, 0, region, _moveTexture, 0, currentMove.SourcePoint.X, currentMove.SourcePoint.Y);

                // Copy it back into the shared texture in the right place...
                context.CopySubresourceRegion(_moveTexture, 0, region, _sharedTexture, 0, currentMove.DestinationRect.Left, currentMove.DestinationRect.Top);
            }
        }

        //private void DoDirty(SharpDX.Direct3D11.DeviceContext context, Texture2D src, int dirtyCount, Rectangle[] rect)
        //{
        //    if(_dirtyRenderView == null)
        //    {
        //        _dirtyRenderView = new RenderTargetView(_captureDevice, _sharedTexture);
        //    }

        //    var srcDesc = src.Description;
        //    using(var shader = new ShaderResourceView(_captureDevice, src, new ShaderResourceViewDescription
        //    {
        //        Format = srcDesc.Format,
        //        Dimension = ShaderResourceViewDimension.Texture2D,
        //        Texture2D = new ShaderResourceViewDescription.Texture2DResource { MipLevels = srcDesc.MipLevels, MostDetailedMip = srcDesc.MipLevels - 1 }
        //    }))
        //    {
        //        context.OutputMerger.BlendFactor = Color4.Black;
        //        context.OutputMerger.SetRenderTargets(_dirtyRenderView);
        //        context.PixelShader.SetShaderResources(0, shader);
        //        context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;

        //        for(int i=0; i<dirtyCount; i++)
        //        {

        //        }
        //    }
        //}
    }
}
