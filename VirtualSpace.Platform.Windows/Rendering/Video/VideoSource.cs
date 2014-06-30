using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.MediaFoundation;
using SharpDX.Toolkit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using VirtualSpace.Core;

namespace VirtualSpace.Platform.Windows.Rendering.Video
{
    internal sealed class VideoSource : IDisposable
    {
        private readonly SharpDX.Direct3D11.Device _device;
        private readonly List<IDisposable> _toDispose;
        private readonly ManualResetEvent _event;

        private SharpDX.Direct3D11.Texture2D _surface;
        private KeyedMutex _mutex;

        //private MediaEngineEx _mediaEngineEx;
        private SourceReader _reader;
        private bool _isPlaying;
        private DateTime _startTime;

        private int _width = 1280;
        private int _height = 688;

        public int Width { get { return _width; } }
        public int Height { get { return _height; } }

        public VideoSource(string file)
        {
            _toDispose = new List<IDisposable>();
            _event = new ManualResetEvent(false);

            // Initialize MediaFoundation
            MediaManager.Startup();
            AddDisposable(new Disposable(() => MediaManager.Shutdown()));

            //Device need bgra and video support
            _device = AddDisposable(new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport));

            //Add multi thread protection on device
            using (var mt = _device.QueryInterface<DeviceMultithread>())
            {
                mt.SetMultithreadProtected(true);
            }

            //Reset device
            var dxgiManager = AddDisposable(new DXGIDeviceManager());
            dxgiManager.ResetDevice(_device);

            // Creates an URL to the file
            var url = new Uri(file, UriKind.RelativeOrAbsolute);

            // Create Source Reader
            using(var attr = new MediaAttributes())
            {
                attr.Set(SourceReaderAttributeKeys.D3DManager, dxgiManager);
                _reader = AddDisposable(new SourceReader(url.AbsoluteUri, attr));
            }

            // Set correct video output format (for DX surface)
            using (var videoFormat = _reader.GetNativeMediaType(SourceReaderIndex.FirstVideoStream, 0))
            {
                videoFormat.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Video);
                videoFormat.Set(MediaTypeAttributeKeys.Subtype, VideoFormatGuids.NV12);
                _reader.SetCurrentMediaType(SourceReaderIndex.FirstVideoStream, videoFormat);
            }

            _isPlaying = true;

            // Creates the MediaEngineClassFactory
            //using (var mediaEngineFactory = new MediaEngineClassFactory())
            //{
            //    //Assign our dxgi manager, and set format to bgra
            //    MediaEngineAttributes attr = new MediaEngineAttributes();
            //    attr.VideoOutputFormat = (int)SharpDX.DXGI.Format.B8G8R8A8_UNorm;
            //    attr.DxgiManager = dxgiManager;

            //    // Creates MediaEngine 
            //    var mediaEngine = AddDisposable(new MediaEngine(mediaEngineFactory, attr, MediaEngineCreateFlags.None));
                
            //    // Query for MediaEngineEx interface
            //    _mediaEngineEx = AddDisposable(mediaEngine.QueryInterface<MediaEngineEx>());
            //}

            //// Register our PlayBackEvent
            //_mediaEngineEx.PlaybackEvent += OnPlaybackCallback;

            //// Creates an URL to the file
            //var url = new Uri(file, UriKind.RelativeOrAbsolute);

            //// Opens the file
            //var fileStream = AddDisposable(new FileStream(file, FileMode.Open));

            //// Create a ByteStream object from it
            //var stream = AddDisposable(new ByteStream(fileStream));

            //// Set the source stream
            //_isPlaying = true;
            //_mediaEngineEx.SetSourceFromByteStream(stream, url.AbsoluteUri);

            //// Do not return until the media is ready...
            //_event.WaitOne();
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
                _surface = AddDisposable(_device.OpenSharedResource<Texture2D>(sharedResource.SharedHandle));

                // Create mutexes
                _mutex = AddDisposable(_surface.QueryInterface<KeyedMutex>());
            }
        }

        //private void OnPlaybackCallback(MediaEngineEvent playEvent, long param1, int param2)
        //{
        //    switch (playEvent)
        //    {
        //        case MediaEngineEvent.CanPlay:
        //            //Get our video size
        //            _mediaEngineEx.GetNativeVideoSize(out _width, out _height);

        //            _event.Set();

        //            // Play the video
        //            if (_isPlaying)
        //            {
        //                _mediaEngineEx.Play();
        //            }
        //            else
        //            {
        //                _mediaEngineEx.Pause();
        //            }
        //            break;
        //        case MediaEngineEvent.TimeUpdate:
        //            break;
        //        case MediaEngineEvent.Error:
        //        case MediaEngineEvent.Abort:
        //        case MediaEngineEvent.Ended:
        //            _isPlaying = false;
        //            _event.Set();
        //            break;
        //    }
        //}

        public void Update(GameTime time)
        {
            if (_isPlaying)
            {
                int streamIndex;
                SourceReaderFlags flags;
                long timeStamp;
                using(var sample = _reader.ReadSample(SourceReaderIndex.FirstVideoStream, SourceReaderControlFlags.None, out streamIndex, out flags, out timeStamp))
                using(var buffer = sample.GetBufferByIndex(0))
                using(var dxgi = buffer.QueryInterface<DXGIBuffer>())
                {
                    IntPtr texture2d;
                    dxgi.GetResource(typeof(SharpDX.Direct3D11.Texture2D).GUID, out texture2d);
                    using(var tex = new SharpDX.Direct3D11.Texture2D(texture2d))
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
                                _device.ImmediateContext.CopyResource(tex, _surface);
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
    }
}
