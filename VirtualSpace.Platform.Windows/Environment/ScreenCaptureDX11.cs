using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Runtime.InteropServices;
using VirtualSpace.Core.Environment;

namespace VirtualSpace.Platform.Windows.Environment
{
    public class ScreenCaptureDX11 : IScreenCapture
    {
        private static readonly int SizeOfMoveRectangle = Marshal.SizeOf(typeof(OutputDuplicateMoveRectangle));
        private static readonly int SizeOfDirtyRectangle = Marshal.SizeOf(typeof(Rectangle));
        private readonly IScreen _screen;
        private readonly SharpDX.Direct3D11.Texture2D _sharedTexture;

        private int _nScreenWidth;
        private int _nScreenHeight;

        private SharpDX.Direct3D11.Device _captureDevice;
        private OutputDuplication _outputDuplication;
        private SharpDX.Direct3D11.Texture2D _capturedTexture;
        private KeyedMutex _mutex;

        private int _moveBufferLength;
        private OutputDuplicateMoveRectangle[] _moveBuffer;

        private int _dirtyBufferLength;
        private Rectangle[] _dirtyBuffer;

        public ScreenCaptureDX11(IScreen screen, SharpDX.Toolkit.Graphics.GraphicsDevice device)
        {
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
                _captureDevice.OpenSharedResource<Texture2D>(sharedResource.CreateSharedHandle(null, SharedResourceFlags.Write));
            }

            _mutex = _sharedTexture.QueryInterface<KeyedMutex>();
            using(var output = new Output1(desktop.NativePointer))
            {
                _outputDuplication = output.DuplicateOutput(_captureDevice);
            }
        }

        public int Width { get { return _nScreenWidth; } }
        public int Height { get { return _nScreenHeight; } }
        public SharpDX.Direct3D11.Texture2D ScreenTexture { get { return _sharedTexture; } }

        public void CaptureScreen()
        {
            SharpDX.DXGI.Resource resource; 
            OutputDuplicateFrameInformation frameInfo;
            _outputDuplication.AcquireNextFrame(1000, out frameInfo, out resource);

            if(_capturedTexture == null)
            {
                _capturedTexture.Dispose();
                _capturedTexture = null;
            }

            _capturedTexture = resource.QueryInterface<Texture2D>();
            resource.Dispose();

            if(frameInfo.TotalMetadataBufferSize > 0)
            {
                if (frameInfo.TotalMetadataBufferSize > _moveBufferLength)
                {
                    _moveBufferLength = frameInfo.TotalMetadataBufferSize;
                    _moveBuffer = new OutputDuplicateMoveRectangle[frameInfo.TotalMetadataBufferSize / SizeOfMoveRectangle];
                }

                int bufferSize;
                _outputDuplication.GetFrameMoveRects(_moveBufferLength, _moveBuffer, out bufferSize);
                var moveCount = bufferSize / SizeOfMoveRectangle;

                if(frameInfo.TotalMetadataBufferSize - bufferSize > _dirtyBufferLength)
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
                if(moveCount > 0)
                {

                }

                if(dirtyCount > 0)
                {

                }

                _mutex.Release(0);
            }
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
        }
    }
}
