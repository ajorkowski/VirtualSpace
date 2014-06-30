using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Platform.Windows.Rendering.Providers;
using VirtualSpace.Platform.Windows.Rendering.Video;

namespace VirtualSpace.Platform.Windows.Rendering.Screen
{
    internal sealed class VideoRenderer : ScreenRenderer, IScreen
    {
        private readonly string _file;

        private SharpDX.Direct3D11.Texture2D _renderTexture;
        private KeyedMutex _renderMutex;

        private VideoSource _source;

        public VideoRenderer(SharpDX.Toolkit.Game game, ICameraProvider camera, string file)
            : base(game, camera)
        {
            _file = file;
            UpdateOrder = -1;
        }

        public override int Width { get { return _source.Width; } }
        public override int Height { get { return _source.Height; } }

        protected override SharpDX.Direct3D11.Texture2D ScreenTexture { get { return _renderTexture; } }

        protected override void LoadContent()
        {
            _source = ToDisposeContent(new VideoSource(_file));

            _renderTexture = ToDisposeContent(new SharpDX.Direct3D11.Texture2D(GraphicsDevice, new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.None,
                BindFlags = BindFlags.ShaderResource,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Height = _source.Height,
                Width = _source.Width,
                OptionFlags = ResourceOptionFlags.SharedKeyedmutex,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default
            }));

            // Create mutexes
            _renderMutex = ToDisposeContent(_renderTexture.QueryInterface<KeyedMutex>());

            _source.AttachVideoOutput(_renderTexture);

            base.LoadContent();
        }

        public override void Update(SharpDX.Toolkit.GameTime gameTime)
        {
            _source.Update(gameTime);

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
    }
}
