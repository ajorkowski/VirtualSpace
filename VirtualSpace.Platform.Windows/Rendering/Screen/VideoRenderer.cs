using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.MediaFoundation;
using System;
using System.IO;
using VirtualSpace.Core;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Platform.Windows.Rendering.Providers;

namespace VirtualSpace.Platform.Windows.Rendering.Screen
{
    internal sealed class VideoRenderer : ScreenRenderer, IVideoScreen
    {
        private readonly string _file;
        
        private SharpDX.Direct3D11.Texture2D _renderTexture;
        private SharpDX.Direct3D11.Texture2D _sharedTexture;
        private Surface _surface;
        private KeyedMutex _mutex;
        private KeyedMutex _renderMutex;
        
        private int _width = 1280;
        private int _height = 688;

        private MediaEngine _mediaEngine;
        private MediaEngineEx _mediaEngineEx;
        private bool _isPlaying;
        private bool _canPlay;

        public VideoRenderer(SharpDX.Toolkit.Game game, ICameraProvider camera, string file)
            : base(game, camera)
        {
            _file = file;
            UpdateOrder = -1;
        }

        public override int Width { get { return _width; } }
        public override int Height { get { return _height; } }

        protected override SharpDX.Direct3D11.Texture2D ScreenTexture { get { return _renderTexture; } }

        public void Play()
        {
            _isPlaying = true;
            if (_canPlay)
            {
                _mediaEngineEx.Play();
            }
        }

        public void Stop()
        {
            _isPlaying = false;
            if (_canPlay)
            {
                _mediaEngineEx.Pause();
            }
        }

        protected override void LoadContent()
        {
            _renderTexture = ToDisposeContent(new SharpDX.Direct3D11.Texture2D(GraphicsDevice, new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.None,
                BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Height = _height,
                Width = _width,
                OptionFlags = ResourceOptionFlags.SharedKeyedmutex,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default
            }));

            // Initialize MediaFoundation
            MediaManager.Startup();
            ToDisposeContent(new Disposable(() => MediaManager.Shutdown()));

            //Device need bgra and video support
            var device = ToDisposeContent(new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport));

            //Add multi thread protection on device
            using (var mt = device.QueryInterface<DeviceMultithread>())
            {
                mt.SetMultithreadProtected(true);
            }

            //Reset device
            var dxgiManager = ToDisposeContent(new DXGIDeviceManager());
            dxgiManager.ResetDevice(device);

            using (var sharedResource = _renderTexture.QueryInterface<SharpDX.DXGI.Resource1>())
            {
                _sharedTexture = ToDisposeContent(device.OpenSharedResource<Texture2D>(sharedResource.SharedHandle));
            }

            // Create mutexes
            _mutex = ToDisposeContent(_sharedTexture.QueryInterface<KeyedMutex>());
            _renderMutex = ToDisposeContent(_renderTexture.QueryInterface<KeyedMutex>());

            // Creates the MediaEngineClassFactory
            using (var mediaEngineFactory = new MediaEngineClassFactory())
            {
                //Assign our dxgi manager, and set format to bgra
                MediaEngineAttributes attr = new MediaEngineAttributes();
                attr.VideoOutputFormat = (int)SharpDX.DXGI.Format.B8G8R8A8_UNorm;
                attr.DxgiManager = dxgiManager;

                // Creates MediaEngine for AudioOnly 
                _mediaEngine = new MediaEngine(mediaEngineFactory, attr, MediaEngineCreateFlags.None);
                ToDisposeContent(new Disposable(() => { _mediaEngine.Shutdown(); _mediaEngine.Dispose(); }));
            }

            // Register our PlayBackEvent
            _mediaEngine.PlaybackEvent += OnPlaybackCallback;

            // Query for MediaEngineEx interface
            _mediaEngineEx = ToDisposeContent(_mediaEngine.QueryInterface<MediaEngineEx>());

            // Creates an URL to the file
            var url = new Uri(_file, UriKind.RelativeOrAbsolute);

            // Opens the file
            var fileStream = ToDisposeContent(new FileStream(_file, FileMode.Open));

            // Create a ByteStream object from it
            var stream = ToDisposeContent(new ByteStream(fileStream));

            // Set the source stream
            _isPlaying = true;
            _mediaEngineEx.SetSourceFromByteStream(stream, url.AbsoluteUri);

            //Get DXGI surface to be used by our media engine
            _surface = ToDisposeContent(_sharedTexture.QueryInterface<SharpDX.DXGI.Surface>());

            base.LoadContent();
        }

        protected override void UnloadContent()
        {
            base.UnloadContent();

            _canPlay = false;
            _isPlaying = false;
        }

        public override void Update(SharpDX.Toolkit.GameTime gameTime)
        {
            if (_canPlay)
            {
                var result = _mutex.Acquire(0, 100);
                if (result != Result.WaitTimeout && result != Result.Ok)
                {
                    throw new SharpDXException(result);
                }

                if (result == Result.Ok)
                {
                    //Transfer frame if a new one is available
                    long ts;
                    if (_mediaEngine.OnVideoStreamTick(out ts))
                    {
                        _mediaEngine.TransferVideoFrame(_surface, null, new SharpDX.Rectangle(0, 0, _width, _height), null);
                    }
                    _mutex.Release(0);
                }
            }

            base.Update(gameTime);
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

        private void OnPlaybackCallback(MediaEngineEvent playEvent, long param1, int param2)
        {
            switch (playEvent)
            {
                case MediaEngineEvent.CanPlay:
                    //Get our video size
                    _mediaEngine.GetNativeVideoSize(out _width, out _height);

                    // Play the video
                    _canPlay = true;
                    if (_isPlaying)
                    {
                        _mediaEngineEx.Play();
                    }
                    else
                    {
                        _mediaEngineEx.Pause();
                    }
                    break;
                case MediaEngineEvent.TimeUpdate:
                    break;
                case MediaEngineEvent.Error:
                case MediaEngineEvent.Abort:
                case MediaEngineEvent.Ended:
                    _isPlaying = false;
                    break;
            }
        }
    }
}
