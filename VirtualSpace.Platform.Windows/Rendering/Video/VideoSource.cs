using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.MediaFoundation;
using SharpDX.Toolkit;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using VirtualSpace.Core;

namespace VirtualSpace.Platform.Windows.Rendering.Video
{
    internal sealed class VideoSource : IDisposable
    {
        private const int MF_SOURCE_READER_FIRST_VIDEO_STREAM = unchecked((int)(0xFFFFFFFC));

        private readonly List<IDisposable> _toDispose;
        private readonly ManualResetEvent _event;

        private SharpDX.Direct3D11.Texture2D _surface;
        private KeyedMutex _mutex;

        private VideoMode _mode;
        private SourceReader _reader;
        private bool _isPlaying;
        private DateTime _startTime;

        private int _width;
        private int _height;
        private Rectangle _pictureRegion;
        private int _stride;

        public int Width { get { return _width; } }
        public int Height { get { return _height; } }

        public VideoSource(string file)
        {
            _toDispose = new List<IDisposable>();
            _event = new ManualResetEvent(false);

            // Initialize MediaFoundation
            MediaManager.Startup();
            AddDisposable(new Disposable(() => MediaManager.Shutdown()));

            // Creates an URL to the file
            var url = new Uri(file, UriKind.RelativeOrAbsolute);

            // Create Source Reader
            var manager = SetupModeAndCreateManager();
            using(var attr = new MediaAttributes())
            {
                if (manager != null)
                {
                    attr.Set(SourceReaderAttributeKeys.D3DManager, manager);
                }
                if (_mode == VideoMode.Dx11)
                {
                    attr.Set(SourceReaderAttributeKeys.EnableAdvancedVideoProcessing, true);
                }
                _reader = AddDisposable(new SourceReader(url.AbsoluteUri, attr));
            }

            // Set correct video output format (for DX surface)
            using (var nativeFormat = _reader.GetNativeMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, 0))
            using (var videoFormat = new MediaType())
            {
                videoFormat.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                videoFormat.Set(MediaTypeAttributeKeys.Subtype, GetVideoFormat());
                _reader.SetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, videoFormat);
            }

            using (var currentFormat = _reader.GetCurrentMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM))
            {
                _pictureRegion = GetPictureRegion(currentFormat);
                _stride = GetDefaultStride(currentFormat);
                UnpackLong(currentFormat.Get(MediaTypeAttributeKeys.FrameSize), out _width, out _height);
            }

            if (_mode != VideoMode.Software)
            {
                using(var decoder = new Transform(_reader.GetServiceForStream(MF_SOURCE_READER_FIRST_VIDEO_STREAM, Guid.Empty, typeof(Transform).GUID)))
                {
                    decoder.ProcessMessage(TMessageType.SetD3DManager, manager.NativePointer);
                }
            }

            _isPlaying = true;
        }

        public void AttachVideoOutput(SharpDX.Direct3D11.Texture2D output)
        {
            if(_surface != null)
            {
                _toDispose.Remove(_surface);
                _surface.Dispose();
                _surface = null;
            }

            if(_mutex != null)
            {
                _toDispose.Remove(_mutex);
                _mutex.Dispose();
                _mutex = null;
            }

            using (var sharedResource = output.QueryInterface<SharpDX.DXGI.Resource1>())
            {
                //Get texture to be used by our media engine
                //_surface = AddDisposable(_device.OpenSharedResource<Texture2D>(sharedResource.SharedHandle));

                // Create mutexes
                _mutex = AddDisposable(_surface.QueryInterface<KeyedMutex>());
            }
        }

        public void Update(GameTime time)
        {
            if (_isPlaying)
            {
                int streamIndex;
                SourceReaderFlags flags;
                long timeStamp;
                using (var sample = _reader.ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM, SourceReaderControlFlags.None, out streamIndex, out flags, out timeStamp))
                {
                    switch (_mode)
                    {
                        case VideoMode.Dx11:
                            UpdateTextureDx11(sample);
                            break;
                        default:
                            throw new NotImplementedException("Video mode is not yet supported...");
                    }
                }
            }
        }

        private void UpdateTextureDx11(Sample sample)
        {
            using (var buffer = sample.GetBufferByIndex(0))
            using (var dxgi = buffer.QueryInterface<DXGIBuffer>())
            {
                IntPtr texture2d;
                dxgi.GetResource(typeof(SharpDX.Direct3D11.Texture2D).GUID, out texture2d);
                using (var tex = new SharpDX.Direct3D11.Texture2D(texture2d))
                {
                    if (_mutex != null && _surface != null)
                    {
                        var result = _mutex.Acquire(0, 100);
                        if (result != Result.WaitTimeout && result != Result.Ok)
                        {
                            throw new SharpDXException(result);
                        }

                        if (result == Result.Ok)
                        {
                            //_device.ImmediateContext.CopyResource(tex, _surface);
                            //Transfer frame if a new one is available
                            //long ts;
                            //if (_mediaEngineEx.OnVideoStreamTick(out ts))
                            //{
                            //    _mediaEngineEx.TransferVideoFrame(_surface, null, new SharpDX.Rectangle(0, 0, _width, _height), null);
                            //}
                            _mutex.Release(0);
                        }
                    }
                }
            }
        }

        private ComObject SetupModeAndCreateManager()
        {
            var dx11Manager = TryCreateDx11Manager();
            if (dx11Manager != null)
            {
                _mode = VideoMode.Dx11;
                return dx11Manager;
            }

            _mode = VideoMode.Software;
            return null;
        }

        private DXGIDeviceManager TryCreateDx11Manager()
        {
            //Device need bgra and video support
            SharpDX.Direct3D11.Device device = null;
            DXGIDeviceManager dxgiManager = null;
            try
            {
                device = AddDisposable(new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport));

                //Add multi thread protection on device
                using (var mt = device.QueryInterface<DeviceMultithread>())
                {
                    mt.SetMultithreadProtected(true);
                }

                //Reset device
                dxgiManager = AddDisposable(new DXGIDeviceManager());
                dxgiManager.ResetDevice(device);
            }
            catch (Exception)
            {
                if (dxgiManager != null)
                {
                    _toDispose.Remove(dxgiManager);
                    dxgiManager.Dispose();
                    dxgiManager = null;
                }

                if (device != null)
                {
                    _toDispose.Remove(device);
                    device.Dispose();
                }
            }

            return dxgiManager;
        }

        private void TryCreateDx9Manager(IntPtr hWnd)
        {
            var d3d9 = AddDisposable(new SharpDX.Direct3D9.Direct3DEx());

            var fromFormat = SharpDX.Direct3D9.D3DX.MakeFourCC(Convert.ToByte('N'),Convert.ToByte('V'),Convert.ToByte('1'),Convert.ToByte('2'));
            var canConvert = d3d9.CheckDeviceFormatConversion(0, SharpDX.Direct3D9.DeviceType.Hardware, fromFormat, SharpDX.Direct3D9.Format.X8R8G8B8);
            if(!canConvert)
            {
                // TODO: return null
            }

            var param = new SharpDX.Direct3D9.PresentParameters
            {
                BackBufferWidth = 1,
                BackBufferHeight = 1,
                BackBufferFormat = SharpDX.Direct3D9.Format.Unknown,
                BackBufferCount = 1,
                SwapEffect = SharpDX.Direct3D9.SwapEffect.Discard,
                DeviceWindowHandle = hWnd,
                Windowed = true,
                PresentFlags = SharpDX.Direct3D9.PresentFlags.Video
            };

            var device = AddDisposable(new SharpDX.Direct3D9.DeviceEx(d3d9, 0, SharpDX.Direct3D9.DeviceType.Hardware, hWnd, SharpDX.Direct3D9.CreateFlags.FpuPreserve | SharpDX.Direct3D9.CreateFlags.Multithreaded | SharpDX.Direct3D9.CreateFlags.MixedVertexProcessing, param));

            // Ensure we can create queries to synchronize operations between devices.
            using (var query = new SharpDX.Direct3D9.Query(device, SharpDX.Direct3D9.QueryType.Event)) { }

            var manager = Direct3DDeviceManager
        }

        private Guid GetVideoFormat()
        {
            switch (_mode)
            {
                case VideoMode.Dx11:
                    return VideoFormatGuids.Argb32;
                case VideoMode.Dx9:
                    return VideoFormatGuids.NV12;
                case VideoMode.Software:
                    return VideoFormatGuids.Yv12;
                default:
                    throw new NotImplementedException("Unknown video mode");
            }
        }

        private Rectangle GetPictureRegion(MediaType type)
        {
            if (type.MajorType != MediaTypeGuids.Video)
            {
                throw new InvalidOperationException("Was expecting video stream");
            }

            // Determine if "pan and scan" is enabled for this media. If it is, we
            // only display a region of the video frame, not the entire frame.
            var panScan = false;

            try
            {
                panScan = type.Get(MediaTypeAttributeKeys.PanScanEnabled);
            }
            catch (SharpDXException) { }

            // If pan and scan mode is enabled. Try to get the display region.
            VideoArea? area = null;
            if (panScan)
            {
                try
                {
                    area = ByteArrayToStructure<VideoArea>(type.Get(MediaTypeAttributeKeys.PanScanAperture));
                }
                catch (SharpDXException) { }
            }

            // If we're not in pan-and-scan mode, or the pan-and-scan region is not set,
            // check for a minimimum display aperture.
            if (!area.HasValue)
            {
                try
                {
                    area = ByteArrayToStructure<VideoArea>(type.Get(MediaTypeAttributeKeys.MinimumDisplayAperture));
                }
                catch (SharpDXException) { }
            }

            // Minimum display aperture is not set, for "backward compatibility with
            // some components", check for a geometric aperture.
            if (!area.HasValue)
            {
                try
                {
                    area = ByteArrayToStructure<VideoArea>(type.Get(MediaTypeAttributeKeys.GeometricAperture));
                }
                catch (SharpDXException) { }
            }

            // The media specified a picture region, return it.
            if (area.HasValue)
            {
                return new Rectangle(OffsetToInt(area.Value.OffsetX), OffsetToInt(area.Value.OffsetY), area.Value.Area.Width, area.Value.Area.Height);
            }

            // No picture region defined, fall back to using the entire video area.
            var data = type.Get(MediaTypeAttributeKeys.FrameSize.Guid);
            return new Rectangle();
        }

        private int GetDefaultStride(MediaType type)
        {
            // Try to get the default stride from the media type.
            try
            {
                return type.Get(MediaTypeAttributeKeys.DefaultStride);
            }
            catch (SharpDXException) { }

            // Stride attribute not set, calculate it.
            var subType = type.Get(MediaTypeAttributeKeys.Subtype);
            int width;
            int height;
            UnpackLong(type.Get(MediaTypeAttributeKeys.FrameSize), out width, out height);

            int stride;
            MediaFactory.GetStrideForBitmapInfoHeader(BitConverter.ToInt32(subType.ToByteArray(), 0), width, out stride);
            return stride;
        }

        private T AddDisposable<T>(T toDisopse)
            where T: IDisposable
        {
            _toDispose.Add(toDisopse);
            return toDisopse;
        }

        public void Dispose()
        {
            foreach(var d in _toDispose)
            {
                d.Dispose();
            }
            _toDispose.Clear();

            GC.SuppressFinalize(this);
        }

        private T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T stuff = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(),
                typeof(T));
            handle.Free();
            return stuff;
        }

        private int OffsetToInt(Offset offset)
        {
            return (int)(offset.Value + (offset.Fract / 65536.0f));
        }

        private void UnpackLong(long a, out int a1, out int a2)
        {
            a1 = (int)(a & uint.MaxValue);
            a2 = (int)(a >> 32);
        }

        private enum VideoMode
        {
            Software,
            Dx9,
            Dx11
        }
    }
}
