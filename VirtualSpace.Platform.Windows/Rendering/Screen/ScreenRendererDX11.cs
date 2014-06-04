using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VirtualSpace.Core.Screen;

namespace VirtualSpace.Platform.Windows.Rendering.Screen
{
    internal sealed class ScreenRendererDX11 : ScreenRenderer
    {
        private static readonly int SizeOfMoveRectangle = Marshal.SizeOf(typeof(OutputDuplicateMoveRectangle));
        private static readonly int SizeOfDirtyRectangle = Marshal.SizeOf(typeof(Rectangle));
        private static readonly int SizeOfVertex = Marshal.SizeOf(typeof(SharpDX.Toolkit.Graphics.VertexPositionNormalTexture));

        private SharpDX.Direct3D11.Texture2D _sharedTexture;
        private SharpDX.Direct3D11.Texture2D _renderTexture;

        private int _nScreenWidth;
        private int _nScreenHeight;

        private SharpDX.Direct3D11.Device _captureDevice;
        private OutputDuplication _outputDuplication;
        private KeyedMutex _mutex;
        private KeyedMutex _renderMutex;

        private int _moveBufferLength;
        private OutputDuplicateMoveRectangle[] _moveBuffer;

        private int _dirtyBufferLength;
        private Rectangle[] _dirtyBuffer;

        private Texture2D _moveTexture;

        private bool _isRunning;
        private Task _captureLoop;

        private static bool? _isSupported;
        public static bool IsSupported
        {
            get
            {
                if(!_isSupported.HasValue)
                {
                    try
                    {
                        using (var factory = new Factory1())
                        using (var output = new Output1(factory.Adapters1[0].Outputs[0].NativePointer))
                        using (var device = new SharpDX.Direct3D11.Device(DriverType.Hardware))
                        using (var dupl = output.DuplicateOutput(device))
                        {
                            _isSupported = true;
                        }
                    }
                    catch(SharpDXException)
                    {
                        _isSupported = false;
                    }
                }
                return _isSupported.Value;
            }
        }

        public ScreenRendererDX11(SharpDX.Toolkit.Game game, IScreen screen)
            : base(game, screen)
        {
        }

        protected override int Width { get { return _nScreenWidth; } }
        protected override int Height { get { return _nScreenHeight; } }
        protected override SharpDX.Direct3D11.Texture2D ScreenTexture { get { return _renderTexture; } }

        protected override void LoadContent()
        {
            //Output desktop;
            var factory = new Factory1();
            var desktop = factory.Adapters1[0].Outputs[0];

            _nScreenWidth = desktop.Description.DesktopBounds.Width;
            _nScreenHeight = desktop.Description.DesktopBounds.Height;

            _renderTexture = ToDisposeContent(new SharpDX.Direct3D11.Texture2D(GraphicsDevice, new Texture2DDescription
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
            }));

            _renderMutex = ToDisposeContent(_renderTexture.QueryInterface<KeyedMutex>());

            _captureDevice = ToDisposeContent(new SharpDX.Direct3D11.Device(DriverType.Hardware));
            using (var sharedResource = _renderTexture.QueryInterface<SharpDX.DXGI.Resource1>())
            {
                _sharedTexture = ToDisposeContent(_captureDevice.OpenSharedResource<Texture2D>(sharedResource.SharedHandle));
            }

            _mutex = ToDisposeContent(_sharedTexture.QueryInterface<KeyedMutex>());
            using (var output = new Output1(desktop.NativePointer))
            {
                _outputDuplication = ToDisposeContent(output.DuplicateOutput(_captureDevice));
            }

            _isRunning = true;
            _captureLoop = Task.Run(() => CaptureLoop());

            base.LoadContent();
        }

        protected override void UnloadContent()
        {
            _isRunning = false;
            if (_captureLoop != null)
            {
                _captureLoop.Wait();
                _captureLoop.Dispose();
                _captureLoop = null;
            }

            base.UnloadContent();
        }

        public override void Draw(SharpDX.Toolkit.GameTime gameTime)
        {
            // We need to make sure the surface is free!
            var result = _renderMutex.Acquire(0, 100);
            if (result != Result.WaitTimeout && result != Result.Ok)
            {
                throw new SharpDXException(result);
            }

            if (result == Result.Ok)
            {
                base.Draw(gameTime);

                _renderMutex.Release(0);
            }
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            _isRunning = false;
            if (_captureLoop != null)
            {
                _captureLoop.Wait();
                _captureLoop.Dispose();
                _captureLoop = null;
            }

            base.Dispose(disposeManagedResources);
        }

        private void CaptureLoop()
        {
            var context = _captureDevice.ImmediateContext;

            while (_isRunning)
            {
                SharpDX.DXGI.Resource resource;
                OutputDuplicateFrameInformation frameInfo;
                try
                {
                    _outputDuplication.AcquireNextFrame(1000, out frameInfo, out resource);
                }
                catch(SharpDXException e)
                {
                    if(e.ResultCode == Result.WaitTimeout)
                    {
                        continue;
                    }
                    throw;
                }

                var capturedTexture = resource.QueryInterface<Texture2D>();
                resource.Dispose();

                int bufferSize = 0;
                var moveCount = 0;
                if (frameInfo.TotalMetadataBufferSize > SizeOfMoveRectangle)
                {
                    if (frameInfo.TotalMetadataBufferSize > _moveBufferLength)
                    {
                        _moveBufferLength = frameInfo.TotalMetadataBufferSize;
                        _moveBuffer = new OutputDuplicateMoveRectangle[frameInfo.TotalMetadataBufferSize / SizeOfMoveRectangle];
                    }

                        
                    _outputDuplication.GetFrameMoveRects(_moveBufferLength, _moveBuffer, out bufferSize);
                    moveCount = bufferSize / SizeOfMoveRectangle;
                }

                int dirtyCount = 0;
                if (frameInfo.TotalMetadataBufferSize != bufferSize)
                {
                    if (frameInfo.TotalMetadataBufferSize - bufferSize > _dirtyBufferLength)
                    {
                        _dirtyBufferLength = frameInfo.TotalMetadataBufferSize - bufferSize;
                        _dirtyBuffer = new Rectangle[(_dirtyBufferLength / SizeOfDirtyRectangle)];
                    }
                    _outputDuplication.GetFrameDirtyRects(_dirtyBufferLength, _dirtyBuffer, out bufferSize);
                    dirtyCount = bufferSize / SizeOfDirtyRectangle;
                }

                if(dirtyCount > 0 || moveCount > 0)
                {
                    var result = _mutex.Acquire(0, 1000);
                    if (result != Result.WaitTimeout && result != Result.Ok)
                    {
                        capturedTexture.Dispose();
                        _outputDuplication.ReleaseFrame();
                        throw new SharpDXException(result);
                    }

                    if(result == Result.Ok)
                    {
                        context.CopyResource(capturedTexture, _sharedTexture);
                        var desc = _sharedTexture.Description;
                        if (moveCount > 0)
                        {
                            DoMoves(context, moveCount, _moveBuffer);
                        }

                        if (dirtyCount > 0)
                        {
                            DoDirty(context, capturedTexture, dirtyCount, _dirtyBuffer);
                        }

                        _mutex.Release(0);
                    }
                }

                capturedTexture.Dispose();
                _outputDuplication.ReleaseFrame();
            }
        }

        private void DoMoves(SharpDX.Direct3D11.DeviceContext context, int moveCount, OutputDuplicateMoveRectangle[] rect)
        {
            if (_moveTexture == null)
            {
                var desc = _sharedTexture.Description;
                desc.BindFlags = BindFlags.RenderTarget;
                desc.OptionFlags = ResourceOptionFlags.None;
                _moveTexture = new Texture2D(_captureDevice, desc);
            }

            for (int i = 0; i < moveCount; i++)
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

        private void DoDirty(SharpDX.Direct3D11.DeviceContext context, Texture2D src, int dirtyCount, Rectangle[] rect)
        {
            var region = new ResourceRegion() { Front = 0, Back = 1 };
            for (int i = 0; i < dirtyCount; i++)
            {
                region.Left = rect[i].Left;
                region.Top = rect[i].Top;
                region.Right = rect[i].Right;
                region.Bottom = rect[i].Bottom;

                context.CopySubresourceRegion(src, 0, region, _sharedTexture, 0, rect[i].Left, rect[i].Top);
            }
        }
    }
}
