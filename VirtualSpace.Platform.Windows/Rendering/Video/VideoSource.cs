using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.MediaFoundation;
using SharpDX.Toolkit;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VirtualSpace.Core;

namespace VirtualSpace.Platform.Windows.Rendering.Video
{
    internal sealed class VideoSource : IDisposable
    {
        private const int MF_SOURCE_READER_FIRST_VIDEO_STREAM = unchecked((int)(0xFFFFFFFC));
        private const int MaxNumberOfFramesToQueue = 5;

        private readonly List<IDisposable> _toDispose;
        private readonly ManualResetEvent _event;

        private SharpDX.Direct3D11.Device _device;
        private SharpDX.Direct3D11.DeviceContext _deviceContext;
        private SharpDX.Direct3D11.Texture2D _surface;
        private ConcurrentBag<VideoFrame> _unusedFrames;
        private ConcurrentQueue<VideoFrame> _bufferedFrames;
        private KeyedMutex _mutex;

        private VideoMode _mode;
        private SourceReader _reader;
        private bool _isPlaying;
        private Task _decodeLoop;
        private TimeSpan _currentTime;
        private ManualResetEvent _waitForUnusedFrame;

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
            _unusedFrames = new ConcurrentBag<VideoFrame>();
            _bufferedFrames = new ConcurrentQueue<VideoFrame>();

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
                if (_mode == VideoMode.Software)
                {
                    // This allows output format of rgb32 when software rendering
                    // NOTE: This is NOT RECOMMENDED, so I need to find a better way of doing this...
                    attr.Set(SourceReaderAttributeKeys.EnableVideoProcessing, 1); 
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
                UnpackLong(currentFormat.Get(MediaTypeAttributeKeys.FrameSize), out _height, out _width);
            }

            if (_mode != VideoMode.Software)
            {
                using(var decoder = new Transform(_reader.GetServiceForStream(MF_SOURCE_READER_FIRST_VIDEO_STREAM, Guid.Empty, typeof(Transform).GUID)))
                {
                    decoder.ProcessMessage(TMessageType.SetD3DManager, manager.NativePointer);
                }
            }

            _isPlaying = true;
            _waitForUnusedFrame = new ManualResetEvent(true);
            _decodeLoop = Task.Run(() => DecodeLoop());
        }

        public void AttachVideoOutput(SharpDX.Direct3D11.Texture2D output)
        {
            VideoFrame f;
            while (_bufferedFrames.TryDequeue(out f))
            {
                f.Texture.Dispose();
            }
            while (_unusedFrames.TryTake(out f))
            {
                f.Texture.Dispose();
            }

            if (_surface != null)
            {
                _toDispose.Remove(_surface);
                _surface.Dispose();
                _surface = null;
            }

            if (_mutex != null)
            {
                _toDispose.Remove(_mutex);
                _mutex.Dispose();
                _mutex = null;
            }

            using (var sharedResource = output.QueryInterface<SharpDX.DXGI.Resource1>())
            {
                //Get texture to be used by our media engine
                _surface = AddDisposable(_device.OpenSharedResource<Texture2D>(sharedResource.SharedHandle));

                // Create mutexes
                _mutex = AddDisposable(_surface.QueryInterface<KeyedMutex>());
            }

            var desc = output.Description;
            switch(_mode)
            {
                case VideoMode.Software:
                    desc.CpuAccessFlags = CpuAccessFlags.Write;
                    desc.OptionFlags = ResourceOptionFlags.None;
                    desc.Usage = ResourceUsage.Dynamic;
                    break;
                case VideoMode.Dx11:
                    desc.OptionFlags = ResourceOptionFlags.None;
                    desc.CpuAccessFlags = CpuAccessFlags.None;
                    desc.Usage = ResourceUsage.Default;
                    break;
            }

            for (int i = 0; i < MaxNumberOfFramesToQueue; i++)
            {
                _unusedFrames.Add(new VideoFrame { Texture = new Texture2D(_device, desc) });
            }

            // Make sure we let the decoder know we have frames to use!
            _waitForUnusedFrame.Set();
        }

        public void Update(GameTime time)
        {
            _currentTime = _currentTime.Add(time.ElapsedGameTime);
            if (_mutex != null && _surface != null && _bufferedFrames.Count > 0)
            {
                VideoFrame peeked;
                if (_bufferedFrames.TryPeek(out peeked))
                {
                    if (peeked.Timestamp <= _currentTime && _bufferedFrames.TryDequeue(out peeked))
                    {
                        var result = _mutex.Acquire(0, 100);
                        if (result != Result.WaitTimeout && result != Result.Ok)
                        {
                            throw new SharpDXException(result);
                        }

                        if (result == Result.Ok)
                        {
                            _deviceContext.CopyResource(peeked.Texture, _surface);
                            _mutex.Release(0);
                        }

                        _unusedFrames.Add(peeked);
                        _waitForUnusedFrame.Set();
                    }
                }
                else
                {
                    // We do not have any buffered, make sure that we are decoding more!
                    _waitForUnusedFrame.Set();
                }
            }
        }

        private void DecodeLoop()
        {
            _currentTime = TimeSpan.FromMilliseconds(0);
            while (_isPlaying)
            {
                VideoFrame unused;
                if (_unusedFrames.TryTake(out unused))
                {
                    int streamIndex;
                    SourceReaderFlags flags;
                    long timeStamp;
                    using (var sample = _reader.ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM, SourceReaderControlFlags.None, out streamIndex, out flags, out timeStamp))
                    {
                        if (sample == null)
                        {
                            _isPlaying = false;
                            _unusedFrames.Add(unused);
                            break;
                        }

                        unused.Timestamp = TimeSpan.FromMilliseconds(timeStamp / 10000.0); // timestamps are in 100 nanoseconds
                        switch (_mode)
                        {
                            case VideoMode.Dx11:
                                UpdateTextureDx11(sample, unused.Texture);
                                break;
                            case VideoMode.Software:
                                UpdateTextureSoftware(sample, unused.Texture);
                                break;
                            default:
                                _unusedFrames.Add(unused);
                                throw new NotImplementedException("Video mode is not yet supported...");
                        }

                        _bufferedFrames.Enqueue(unused);
                    }
                }
                else
                {
                    _waitForUnusedFrame.Reset();
                    _waitForUnusedFrame.WaitOne();
                }
            }
        }

        private void UpdateTextureDx11(Sample sample, Texture2D bufferText)
        {
            using (var buffer = sample.GetBufferByIndex(0))
            using (var dxgi = buffer.QueryInterface<DXGIBuffer>())
            {
                IntPtr texture2d;
                dxgi.GetResource(typeof(SharpDX.Direct3D11.Texture2D).GUID, out texture2d);
                using (var tex = new SharpDX.Direct3D11.Texture2D(texture2d))
                {
                    var result = _mutex.Acquire(0, 100);
                    if (result != Result.WaitTimeout && result != Result.Ok)
                    {
                        throw new SharpDXException(result);
                    }

                    if (result == Result.Ok)
                    {
                        _deviceContext.CopyResource(tex, bufferText);
                        _mutex.Release(0);
                    }
                }
            }
        }

        private void UpdateTextureSoftware(Sample sample, Texture2D bufferText)
        {
            using (var buffer = sample.ConvertToContiguousBuffer())
            {
                int maxLength;
                int currentLength;
                var dataPtr = buffer.Lock(out maxLength, out currentLength);
                var box = _deviceContext.MapSubresource(bufferText, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
                Utilities.CopyMemory(box.DataPointer, dataPtr, currentLength);
                _deviceContext.UnmapSubresource(bufferText, 0);
                buffer.Unlock();
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

            _device = AddDisposable(new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport));
            _deviceContext = AddDisposable(_device.ImmediateContext);
            _mode = VideoMode.Software;
            return null;
        }

        private DXGIDeviceManager TryCreateDx11Manager()
        {
            //Device need bgra and video support
            DXGIDeviceManager dxgiManager = null;
            try
            {
                _device = AddDisposable(new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport));

                //Add multi thread protection on device
                using (var mt = _device.QueryInterface<DeviceMultithread>())
                {
                    mt.SetMultithreadProtected(true);
                }

                //Reset device
                dxgiManager = AddDisposable(new DXGIDeviceManager());
                dxgiManager.ResetDevice(_device);

                _deviceContext = AddDisposable(_device.ImmediateContext);
            }
            catch (Exception)
            {
                if (dxgiManager != null)
                {
                    _toDispose.Remove(dxgiManager);
                    dxgiManager.Dispose();
                    dxgiManager = null;
                }

                if (_device != null)
                {
                    _toDispose.Remove(_device);
                    _device.Dispose();
                    _device = null;
                }
            }

            return dxgiManager;
        }

        private Guid GetVideoFormat()
        {
            switch (_mode)
            {
                case VideoMode.Dx11:
                    return VideoFormatGuids.Argb32;
                case VideoMode.Software:
                    return VideoFormatGuids.Rgb32;
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
            UnpackLong(type.Get(MediaTypeAttributeKeys.FrameSize), out height, out width);

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
            _isPlaying = false;
            if (_decodeLoop.Status == TaskStatus.Running)
            {
                _waitForUnusedFrame.Set();
                _decodeLoop.Wait();
            }
            _decodeLoop.Dispose();
            _waitForUnusedFrame.Dispose();

            VideoFrame f;
            while (_bufferedFrames.TryDequeue(out f))
            {
                f.Texture.Dispose();
            }
            while (_unusedFrames.TryTake(out f))
            {
                f.Texture.Dispose();
            }

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
            T stuff = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
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
            Dx11
        }

        private struct VideoFrame
        {
            public TimeSpan Timestamp { get; set; }
            public Texture2D Texture { get; set; }
        }
    }
}
