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
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using VideoDecoders.MediaFoundation;
using VirtualSpace.Core;
using VirtualSpace.Core.Video;
using VirtualSpace.Platform.Windows.Rendering.Screen;

namespace VirtualSpace.Platform.Windows.Video
{
    internal sealed class VideoSource : IVideo, IScreenSource
    {
        private const int MF_SOURCE_READER_FIRST_VIDEO_STREAM = unchecked((int)(0xFFFFFFFC));
        private const int MaxNumberOfFramesToQueue = 25;

        private readonly List<IDisposable> _toDispose;
        private readonly ManualResetEvent _event;

        private SharpDX.Direct3D11.Device _device;
        private SharpDX.Direct3D11.DeviceContext _deviceContext;
        private SharpDX.Direct3D11.Texture2D _surface;
        private SharpDX.Direct3D9.DeviceEx _d9Device;
        private SharpDX.Direct3D9.Texture _d9Surface;
        private IntPtr _d9SurfaceSharedHandle;
        private ConcurrentBag<VideoFrame> _unusedFrames;
        private ConcurrentQueue<VideoFrame> _bufferedFrames;
        private KeyedMutex _mutex;

        private VideoMode _mode;
        private SourceReader _reader;
        private VideoState _state;
        private Task _decodeLoop;
        private TimeSpan _currentTime;
        private ManualResetEvent _waitForUnusedFrame;

        private int _width;
        private int _height;
        private Rectangle _pictureRegion;
        private int _stride;
        private bool _canSeek;
        private TimeSpan _duration;
        private bool _isBuffering;

        public bool CanSeek { get { return _canSeek; } }
        public TimeSpan Duration { get { return _duration; } }

        public VideoSource(string file)
        {
            _toDispose = new List<IDisposable>();
            _event = new ManualResetEvent(false);
            _unusedFrames = new ConcurrentBag<VideoFrame>();
            _bufferedFrames = new ConcurrentQueue<VideoFrame>();

            // Initialize MediaFoundation
            MediaManager.Startup();
            DecoderRegister.Register();
            AddDisposable(new Disposable(() => MediaManager.Shutdown()));

            // Creates an URL to the file
            var url = new Uri(file, UriKind.RelativeOrAbsolute);

            // Create Source Reader
            var manager = SetupModeAndCreateManager(IntPtr.Zero);
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
                    // (ie pump Yv12 format into texture and do Yv12 -> rgb conversion in shader)
                    attr.Set(SourceReaderAttributeKeys.EnableVideoProcessing, 1); 
                }
                _reader = AddDisposable(new SourceReader(url.AbsoluteUri, attr));
            }

            // Set correct video output format (for DX surface)
            var supportedTypes = new List<Guid>();
            while (true)
            {
                try
                {
                    using (var nativeFormat = _reader.GetNativeMediaType(MF_SOURCE_READER_FIRST_VIDEO_STREAM, supportedTypes.Count))
                    {
                        supportedTypes.Add(nativeFormat.Get(MediaTypeAttributeKeys.Subtype));
                    }
                }
                catch (SharpDXException)
                {
                    break;
                }
            }

            if (supportedTypes.Count == 0)
            {
                throw new InvalidOperationException("No output types supported...");
            }

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

            // Grab out some metadata...
            _canSeek = GetCanSeek(_reader);
            _duration = GetDuration(_reader);

            _state = VideoState.Paused;
        }

        public SharpDX.Direct3D11.Texture2D GetOutputRenderTexture(SharpDX.Direct3D11.Device device)
        {
            var renderTexture = AddDisposable(new SharpDX.Direct3D11.Texture2D(device, new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.None,
                BindFlags = BindFlags.ShaderResource,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Height = _height,
                Width = _width,
                OptionFlags = ResourceOptionFlags.SharedKeyedmutex,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default
            }));

            if (_mode == VideoMode.Dx9)
            {
                _d9Surface = AddDisposable(new SharpDX.Direct3D9.Texture(_d9Device, _width, _height, 1, SharpDX.Direct3D9.Usage.RenderTarget, SharpDX.Direct3D9.Format.A8R8G8B8, SharpDX.Direct3D9.Pool.Default, ref _d9SurfaceSharedHandle));
            }

            using (var sharedResource = renderTexture.QueryInterface<SharpDX.DXGI.Resource1>())
            {
                //Get texture to be used by our media engine
                _surface = AddDisposable(_device.OpenSharedResource<Texture2D>(sharedResource.SharedHandle));

                // Create mutexes
                _mutex = AddDisposable(_surface.QueryInterface<KeyedMutex>());
            }

            var desc = renderTexture.Description;
            switch(_mode)
            {
                case VideoMode.Software:
                    desc.CpuAccessFlags = CpuAccessFlags.Write;
                    desc.OptionFlags = ResourceOptionFlags.None;
                    desc.Usage = ResourceUsage.Dynamic;
                    break;
                case VideoMode.Dx9:
                    desc.OptionFlags = ResourceOptionFlags.None;
                    break;
                case VideoMode.Dx11:
                    desc.OptionFlags = ResourceOptionFlags.None;
                    break;
            }

            for (int i = 0; i < MaxNumberOfFramesToQueue; i++)
            {
                _unusedFrames.Add(new VideoFrame { Texture = new Texture2D(_device, desc) });
            }

            // Make sure we let the decoder know we have frames to use!
            if (_waitForUnusedFrame != null)
            {
                _waitForUnusedFrame.Set();
            }

            return renderTexture;
        }

        public void Update(GameTime time)
        {
            if (_state != VideoState.Playing && _state != VideoState.Buffering) { return; }

            _currentTime = _currentTime.Add(time.ElapsedGameTime);
            if (_mutex != null && _surface != null && _bufferedFrames.Count > 0)
            {
                VideoFrame peeked = null;
                VideoFrame dequeued = null;
                TimeSpan? lastTimestamp = null;
                while (_bufferedFrames.TryPeek(out peeked) && peeked.Timestamp <= _currentTime)
                {
                    // We might have to skip frames...
                    if (dequeued != null)
                    {
                        _unusedFrames.Add(dequeued);
                        _waitForUnusedFrame.Set();
                        dequeued = null;
                    }

                    lastTimestamp = peeked.Timestamp;
                    _bufferedFrames.TryDequeue(out dequeued);
                }

                if (peeked == null)
                {
                    if (lastTimestamp.HasValue)
                    {
                        // We have hit the last frame, most likely due to a pause... reset the counter...
                        _currentTime = lastTimestamp.Value;
                    }
                    else
                    {
                        // We do not have any buffered, make sure that we are decoding more!
                        _waitForUnusedFrame.Set();
                    }
                }

                // We have a frame to update
                if (dequeued != null)
                {
                    _state = VideoState.Playing;

                    try
                    {
                        var result = _mutex.Acquire(0, 100);

                        if (result == Result.Ok)
                        {
                            _deviceContext.CopyResource(dequeued.Texture, _surface);
                            _mutex.Release(0);
                        }
                    }
                    catch(SharpDXException)
                    {
                        // TODO: Might need to handle certain types of errors better?
                    }

                    _unusedFrames.Add(dequeued);
                    _waitForUnusedFrame.Set();
                }
            }
            else
            {
                _state = VideoState.Buffering;
            }

            if (_bufferedFrames.Count == 0 && !_isBuffering)
            {
                _state = VideoState.Finished;

                // Let the decode loop finish
                _waitForUnusedFrame.Set();
            }
        }

        public VideoState State { get { return _state; } }

        public void Play()
        {
            if (_decodeLoop == null)
            {
                _state = VideoState.Buffering;
                _waitForUnusedFrame = new ManualResetEvent(true);
                _decodeLoop = Task.Run(() => DecodeLoop());
            }
            else if(_state == VideoState.Paused)
            {
                _state = _bufferedFrames.Count > 0 ? VideoState.Playing : VideoState.Buffering;
            }
            else if (_state == VideoState.Finished)
            {
                throw new NotImplementedException();
            }
        }

        public void Stop()
        {
            if (_state == VideoState.Buffering || _state == VideoState.Playing)
            {
                _state = VideoState.Paused;
            }
        }

        private void DecodeLoop()
        {
            _currentTime = TimeSpan.FromMilliseconds(0);
            _isBuffering = true;
            while (_state != VideoState.Finished)
            {
                VideoFrame unused;
                if (_unusedFrames.TryTake(out unused))
                {
                    int streamIndex;
                    SourceReaderFlags flags;
                    long timeStamp;
                    using (var sample = _reader.ReadSample(MF_SOURCE_READER_FIRST_VIDEO_STREAM, SourceReaderControlFlags.None, out streamIndex, out flags, out timeStamp))
                    {
                        if (flags.HasFlag(SourceReaderFlags.StreamTick))
                        {
                            _unusedFrames.Add(unused);
                            continue;
                        }

                        if (flags.HasFlag(SourceReaderFlags.Endofstream))
                        {
                            _unusedFrames.Add(unused);
                            _isBuffering = false;
                            break;
                        }

                        if (sample != null)
                        {
                            unused.Timestamp = TimeSpan.FromMilliseconds(timeStamp / 10000.0); // timestamps are in 100 nanoseconds
                            switch (_mode)
                            {
                                case VideoMode.Dx11:
                                    QueueSampleDx11(sample, unused.Texture);
                                    break;
                                case VideoMode.Dx9:
                                    QueueSampleDx9(sample, unused.Texture);
                                    break;
                                case VideoMode.Software:
                                    QueueSampleSoftware(sample, unused.Texture);
                                    break;
                                default:
                                    _unusedFrames.Add(unused);
                                    throw new NotImplementedException("Video mode is not yet supported...");
                            }

                            _bufferedFrames.Enqueue(unused);
                        }
                        else
                        {
                            _unusedFrames.Add(unused);
                        }
                    }
                }
                else
                {
                    _waitForUnusedFrame.Reset();
                    _waitForUnusedFrame.WaitOne();
                }
            }
        }

        private void QueueSampleDx11(Sample sample, Texture2D bufferText)
        {
            using (var buffer = sample.GetBufferByIndex(0))
            using (var dxgi = buffer.QueryInterface<DXGIBuffer>())
            {
                IntPtr texture2d;
                dxgi.GetResource(typeof(SharpDX.Direct3D11.Texture2D).GUID, out texture2d);
                using (var tex = new SharpDX.Direct3D11.Texture2D(texture2d))
                {
                    _deviceContext.CopyResource(tex, bufferText);
                }
            }
        }

        private void QueueSampleDx9(Sample sample, Texture2D bufferText)
        {
            using (var buffer = sample.GetBufferByIndex(0))
            {
                IntPtr surfacePtr;
                MediaFactory.GetService(buffer, MediaServiceKeys.Buffer, typeof(SharpDX.Direct3D9.Surface).GUID, out surfacePtr);

                // Copy to sharable texture...
                using (var surface = new SharpDX.Direct3D9.Surface(surfacePtr))
                using (var tempSurface = _d9Surface.GetSurfaceLevel(0))
                {
                    _d9Device.StretchRectangle(surface, null, tempSurface, null, SharpDX.Direct3D9.TextureFilter.None);
                }

                // We have to wait for the copy... yawn :(
                using (var query = new SharpDX.Direct3D9.Query(_d9Device, SharpDX.Direct3D9.QueryType.Event))
                {
                    query.Issue(SharpDX.Direct3D9.Issue.End);
                    bool temp;
                    while (!query.GetData(out temp, true) || !temp)
                    {
                    }
                }

                // Copy to Dx11 texture...
                using (var sharedResource = _device.OpenSharedResource<SharpDX.Direct3D11.Resource>(_d9SurfaceSharedHandle))
                using (var sharedTexture = sharedResource.QueryInterface<Texture2D>())
                {
                    _deviceContext.CopyResource(sharedTexture, bufferText);
                }
            }
        }

        private void QueueSampleSoftware(Sample sample, Texture2D bufferText)
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

        private ComObject SetupModeAndCreateManager(IntPtr hWnd)
        {
            var dx11Manager = TryCreateDx11Manager();
            if (dx11Manager != null)
            {
                _mode = VideoMode.Dx11;
                return dx11Manager;
            }

            var dx9Manager = TryCreateDx9Manager(hWnd);
            if (dx9Manager != null)
            {
                _mode = VideoMode.Dx9;
                return dx9Manager;
            }

            // Fallback software device
            _device = AddDisposable(new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport));
            _deviceContext = AddDisposable(_device.ImmediateContext);
            _mode = VideoMode.Software;
            return null;
        }

        private ComObject TryCreateDx9Manager(IntPtr hWnd)
        {
            ComObject manager = null;
            try
            {
                using (var d3d9 = new SharpDX.Direct3D9.Direct3DEx())
                {
                    if (!d3d9.CheckDeviceFormatConversion(0, SharpDX.Direct3D9.DeviceType.Hardware, SharpDX.Direct3D9.D3DX.MakeFourCC((byte)'N', (byte)'V', (byte)'1', (byte)'2'), SharpDX.Direct3D9.Format.X8R8G8B8))
                    {
                        return null;
                    }

                    _d9Device = AddDisposable(new SharpDX.Direct3D9.DeviceEx(d3d9, 0, SharpDX.Direct3D9.DeviceType.Hardware, hWnd, SharpDX.Direct3D9.CreateFlags.FpuPreserve | SharpDX.Direct3D9.CreateFlags.Multithreaded | SharpDX.Direct3D9.CreateFlags.MixedVertexProcessing, new SharpDX.Direct3D9.PresentParameters
                    {
                        BackBufferWidth = 1,
                        BackBufferHeight = 1,
                        BackBufferFormat = SharpDX.Direct3D9.Format.Unknown,
                        BackBufferCount = 1,
                        SwapEffect = SharpDX.Direct3D9.SwapEffect.Discard,
                        DeviceWindowHandle = hWnd,
                        Windowed = true,
                        PresentFlags = SharpDX.Direct3D9.PresentFlags.Video
                    }));

                    int resetToken;
                    IDirect3DDeviceManager9 dxManager;
                    DXVA2CreateDirect3DDeviceManager9(out resetToken, out dxManager);

                    dxManager.ResetDevice(_d9Device.NativePointer, resetToken);

                    manager = AddDisposable(new ComObject(dxManager));
                }

                // Use default dx11 devices that will be able to chat to dx9?
                _device = AddDisposable(new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport));
                _deviceContext = AddDisposable(_device.ImmediateContext);
            }
            catch (Exception)
            {
                if (manager != null)
                {
                    _toDispose.Remove(manager);
                    manager.Dispose();
                    manager = null;
                }

                if(_d9Device != null)
                {
                    _toDispose.Remove(_d9Device);
                    _d9Device.Dispose();
                    _d9Device = null;
                }

                if (_device != null)
                {
                    _toDispose.Remove(_device);
                    _device.Dispose();
                    _device = null;
                }
            }

            return manager;
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
                case VideoMode.Dx9:
                    return VideoFormatGuids.NV12;
                case VideoMode.Software:
                    return VideoFormatGuids.Rgb32;
                default:
                    throw new NotImplementedException("Unknown video mode");
            }
        }

        private T AddDisposable<T>(T toDisopse)
            where T: IDisposable
        {
            _toDispose.Add(toDisopse);
            return toDisopse;
        }

        public void Dispose()
        {
            _state = VideoState.Finished;
            _reader.Dispose();
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

        private enum VideoMode
        {
            Software,
            Dx9,
            Dx11
        }

        private class VideoFrame
        {
            public TimeSpan Timestamp { get; set; }
            public Texture2D Texture { get; set; }
        }

        private static TimeSpan GetDuration(SourceReader reader)
        {
            try
            {
                var duration = reader.GetPresentationAttribute(SourceReaderIndex.MediaSource, PresentationDescriptionAttributeKeys.Duration);
                return TimeSpan.FromMilliseconds(duration / 10000.0);
            }
            catch (SharpDXException)
            {
                return TimeSpan.Zero;
            }
        }

        private static bool GetCanSeek(SourceReader reader)
        {
            try
            {
                var ch = (MediaSourceCharacteristics)reader.GetPresentationAttribute(SourceReaderIndex.MediaSource, SourceReaderAttributeKeys.MediaSourceCharacteristics);
                return (ch & MediaSourceCharacteristics.CanSeek) == MediaSourceCharacteristics.CanSeek;
            }
            catch (SharpDXException)
            {
                return false;
            }
        }

        private static Rectangle GetPictureRegion(MediaType type)
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
            int width, height;
            UnpackLong(type.Get<long>(MediaTypeAttributeKeys.FrameSize.Guid), out height, out width);
            return new Rectangle(0, 0, width, height);
        }

        private static int GetDefaultStride(MediaType type)
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

        private static T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T stuff = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return stuff;
        }

        private static int OffsetToInt(Offset offset)
        {
            return (int)(offset.Value + (offset.Fract / 65536.0f));
        }

        private static void UnpackLong(long a, out int a1, out int a2)
        {
            a1 = (int)(a & uint.MaxValue);
            a2 = (int)(a >> 32);
        }

        /**********************************************************
         * COM Imports only used here...
         * ********************************************************/
        [ComImport, System.Security.SuppressUnmanagedCodeSecurity,
	    Guid("a0cade0f-06d5-4cf4-a1c7-f3cdd725aa75"),
	    InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IDirect3DDeviceManager9
	    {
	        void ResetDevice(
	            [In]  IntPtr pDevice,
	            [In]  int resetToken);
	
	        void Junk2();
	        void Junk3();
	        void Junk4();
	        void Junk5();
	        void Junk6();
	    }

        [DllImport("dxva2.DLL", ExactSpelling = true, PreserveSig = false), SuppressUnmanagedCodeSecurity]
	        public extern static void DXVA2CreateDirect3DDeviceManager9(
	            out int pResetToken,
	            out IDirect3DDeviceManager9 ppDXVAManager
	            );
    }
}
