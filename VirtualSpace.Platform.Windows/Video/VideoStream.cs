using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.MediaFoundation;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using VirtualSpace.Core.Video;

namespace VirtualSpace.Platform.Windows.Video
{
    internal sealed class VideoStream : IDisposable
    {
        private const int MaxNumberOfFramesToQueue = 25;

        private readonly SourceReader _reader;
        private readonly VideoDevice _videoDevice;
        private readonly int _sourceIndex;
        private readonly List<IDisposable> _toDispose;

        private Texture2D _surface;
        private SharpDX.Direct3D9.Texture _d9Surface;
        private IntPtr _d9SurfaceSharedHandle;
        private ConcurrentBag<VideoFrame> _unusedFrames;
        private ConcurrentQueue<VideoFrame> _bufferedFrames;
        private KeyedMutex _mutex;

        private int _width;
        private int _height;
        private Rectangle _pictureRegion;
        private int _stride;

        public VideoStream(SourceReader reader, VideoDevice videoDevice, int sourceIndex)
        {
            _reader = reader;
            _videoDevice = videoDevice;
            _sourceIndex = sourceIndex;

            _toDispose = new List<IDisposable>();
            _unusedFrames = new ConcurrentBag<VideoFrame>();
            _bufferedFrames = new ConcurrentQueue<VideoFrame>();

            // Set correct video output format (for DX surface)
            var supportedTypes = new List<Guid>();
            var deviceCount = 0;
            while (true)
            {
                try
                {
                    using (var nativeFormat = _reader.GetNativeMediaType(sourceIndex, deviceCount))
                    {
                        if (nativeFormat.Get(MediaTypeAttributeKeys.MajorType) != MediaTypeGuids.Video)
                        {
                            throw new InvalidOperationException("The stream is not a video type stream");
                        }

                        var nativeSubType = nativeFormat.Get(MediaTypeAttributeKeys.Subtype);
                        if (nativeSubType == GetVideoFormat() || MediaAndDeviceManager.Current.HasDecoder(false, nativeSubType, GetVideoFormat()))
                        {
                            supportedTypes.Add(nativeSubType);
                        }
                    }
                }
                catch (SharpDXException)
                {
                    break;
                }

                deviceCount++;
            }

            if (supportedTypes.Count == 0)
            {
                throw new NotSupportedException("No output types for video supported...");
            }

            using (var videoFormat = new MediaType())
            {
                videoFormat.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                videoFormat.Set(MediaTypeAttributeKeys.Subtype, GetVideoFormat());
                _reader.SetCurrentMediaType(sourceIndex, videoFormat);
            }

            using (var currentFormat = _reader.GetCurrentMediaType(sourceIndex))
            {
                _pictureRegion = GetPictureRegion(currentFormat);
                _stride = GetDefaultStride(currentFormat);
                UnpackLong(currentFormat.Get(MediaTypeAttributeKeys.FrameSize), out _height, out _width);
            }
        }

        public bool HasFrames { get { return _bufferedFrames.Count > 0; } }

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

            if (_videoDevice.VideoMode == VideoMode.Dx9)
            {
                _d9Surface = AddDisposable(new SharpDX.Direct3D9.Texture(_videoDevice.D9Device, _width, _height, 1, SharpDX.Direct3D9.Usage.RenderTarget, SharpDX.Direct3D9.Format.A8R8G8B8, SharpDX.Direct3D9.Pool.Default, ref _d9SurfaceSharedHandle));
            }

            using (var sharedResource = renderTexture.QueryInterface<SharpDX.DXGI.Resource>())
            {
                //Get texture to be used by our media engine
                _surface = AddDisposable(_videoDevice.Device.OpenSharedResource<Texture2D>(sharedResource.SharedHandle));

                // Create mutexes
                _mutex = AddDisposable(_surface.QueryInterface<KeyedMutex>());
            }

            var desc = renderTexture.Description;
            switch (_videoDevice.VideoMode)
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
                _unusedFrames.Add(new VideoFrame { Texture = new Texture2D(_videoDevice.Device, desc) });
            }

            return renderTexture;
        }

        public void UpdateFrame(ManualResetEvent frameResetEvent, ref TimeSpan currentTime, ref VideoState state)
        {
            if (_mutex != null && _surface != null && _bufferedFrames.Count > 0)
            {
                VideoFrame peeked = null;
                VideoFrame dequeued = null;
                TimeSpan? lastTimestamp = null;
                while (_bufferedFrames.TryPeek(out peeked) && peeked.Timestamp <= currentTime)
                {
                    // We might have to skip frames...
                    if (dequeued != null)
                    {
                        _unusedFrames.Add(dequeued);
                        frameResetEvent.Set();
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
                        currentTime = lastTimestamp.Value;
                    }
                    else
                    {
                        // We do not have any buffered, make sure that we are decoding more!
                        frameResetEvent.Set();
                    }
                }

                // We have a frame to update
                if (dequeued != null)
                {
                    state = VideoState.Playing;

                    try
                    {
                        var result = _mutex.Acquire(0, 100);

                        if (result == Result.Ok)
                        {
                            _videoDevice.Context.CopyResource(dequeued.Texture, _surface);
                            _mutex.Release(0);
                        }
                    }
                    catch (SharpDXException)
                    {
                        // TODO: Might need to handle certain types of errors better?
                    }

                    _unusedFrames.Add(dequeued);
                    frameResetEvent.Set();
                }
            }
            else
            {
                state = VideoState.Buffering;
            }
        }

        public bool TryEnqueue(ref bool isBuffering)
        {
            VideoFrame unused;
            if (_unusedFrames.TryTake(out unused))
            {
                int streamIndex;
                SourceReaderFlags flags;
                long timeStamp;
                using (var sample = _reader.ReadSample(_sourceIndex, SourceReaderControlFlags.None, out streamIndex, out flags, out timeStamp))
                {
                    if (flags.HasFlag(SourceReaderFlags.StreamTick))
                    {
                        _unusedFrames.Add(unused);
                        return true;
                    }

                    if (flags.HasFlag(SourceReaderFlags.Endofstream))
                    {
                        _unusedFrames.Add(unused);
                        isBuffering = false;
                        return true;
                    }

                    if (sample != null)
                    {
                        unused.Timestamp = TimeSpan.FromMilliseconds(timeStamp / 10000.0); // timestamps are in 100 nanoseconds
                        switch (_videoDevice.VideoMode)
                        {
#if Win8
                            case VideoMode.Dx11:
                                QueueSampleDx11(sample, unused.Texture);
                                break;
#endif
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

                return true;
            }
            else
            {
                return false;
            }
        }

        public void Dispose()
        {
            VideoFrame f;
            while (_bufferedFrames.TryDequeue(out f) || _unusedFrames.TryTake(out f))
            {
                f.Texture.Dispose();
            }

            foreach (var d in _toDispose)
            {
                d.Dispose();
            }
            _toDispose.Clear();

            GC.SuppressFinalize(this);
        }

#if Win8
        private void QueueSampleDx11(Sample sample, Texture2D bufferText)
        {
            using (var buffer = sample.GetBufferByIndex(0))
            using (var dxgi = buffer.QueryInterface<DXGIBuffer>())
            {
                IntPtr texture2d;
                dxgi.GetResource(typeof(SharpDX.Direct3D11.Texture2D).GUID, out texture2d);
                using (var tex = new SharpDX.Direct3D11.Texture2D(texture2d))
                {
                    _videoDevice.Context.CopyResource(tex, bufferText);
                }
            }
        }
#endif

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
                    _videoDevice.D9Device.StretchRectangle(surface, null, tempSurface, null, SharpDX.Direct3D9.TextureFilter.None);
                }

                // We have to wait for the copy... yawn :(
                //using (var query = new SharpDX.Direct3D9.Query(_videoDevice.D9Device, SharpDX.Direct3D9.QueryType.Event))
                //{
                //    query.Issue(SharpDX.Direct3D9.Issue.End);
                //    bool temp;
                //    while (!query.GetData(out temp, true) || !temp)
                //    {
                //    }
                //}

                // Copy to Dx11 texture...
                using (var sharedResource = _videoDevice.Device.OpenSharedResource<SharpDX.Direct3D11.Resource>(_d9SurfaceSharedHandle))
                using (var sharedTexture = sharedResource.QueryInterface<Texture2D>())
                {
                    _videoDevice.Context.CopyResource(sharedTexture, bufferText);
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
                var box = _videoDevice.Context.MapSubresource(bufferText, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
                Utilities.CopyMemory(box.DataPointer, dataPtr, currentLength);
                _videoDevice.Context.UnmapSubresource(bufferText, 0);
                buffer.Unlock();
            }
        }

        private Guid GetVideoFormat()
        {
            switch (_videoDevice.VideoMode)
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
            UnpackLong(type.Get(MediaTypeAttributeKeys.FrameSize), out height, out width);
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

        private T AddDisposable<T>(T toDisopse)
            where T : IDisposable
        {
            _toDispose.Add(toDisopse);
            return toDisopse;
        }

        private class VideoFrame
        {
            public TimeSpan Timestamp { get; set; }
            public Texture2D Texture { get; set; }
        }
    }
}
