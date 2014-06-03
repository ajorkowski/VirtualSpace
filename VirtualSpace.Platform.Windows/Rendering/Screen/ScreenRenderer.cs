using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using VirtualSpace.Core.Environment;

namespace VirtualSpace.Platform.Windows.Rendering.Screen
{
    internal abstract class ScreenRenderer : GameSystem
    {
        protected readonly IScreen Screen;

        private ICameraService _cameraService;
        private BasicEffect _basicEffect;

        private GeometricPrimitive _plane;
        private Vector3 _planeLighting;
        private SharpDX.Direct3D11.ShaderResourceView _planeShaderView;
        private Matrix _planeTransform;

        public ScreenRenderer(Game game, IScreen screen)
            : base(game)
        {
            Visible = true;
            Enabled = true;

            Screen = screen;
        }

        public override void Initialize()
        {
            base.Initialize();

            _cameraService = Services.GetService<ICameraService>();
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            if (ScreenTexture == null)
            {
                throw new InvalidOperationException("Must create shared texture for screen before LoadContent is called (override LoadContent and call base.LoadContent after creating surface)");
            }

            _basicEffect = ToDisposeContent(new BasicEffect(GraphicsDevice));
            _basicEffect.TextureEnabled = true;
            _basicEffect.LightingEnabled = false;

            var desc = ScreenTexture.Description;
            _planeShaderView = ToDisposeContent(new SharpDX.Direct3D11.ShaderResourceView(GraphicsDevice, ScreenTexture, new SharpDX.Direct3D11.ShaderResourceViewDescription
            {
                Format = desc.Format,
                Dimension = SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D,
                Texture2D = new SharpDX.Direct3D11.ShaderResourceViewDescription.Texture2DResource { MipLevels = desc.MipLevels, MostDetailedMip = desc.MipLevels - 1 }
            }));

            _plane = ToDisposeContent(GeometricPrimitive.Plane.New(GraphicsDevice, Width, Height));

            _planeLighting = new Vector3(1, 0, 0);
            _planeTransform = Matrix.Scaling(0.005f) * Matrix.RotationX(-MathUtil.PiOverTwo) * Matrix.Translation(0f, -1f, 0f);
            _basicEffect.TextureView = _planeShaderView;
            _basicEffect.World = _planeTransform;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            _basicEffect.View = _cameraService.View;
            _basicEffect.Projection = _cameraService.Projection;
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            _plane.Draw(_basicEffect);
        }

        protected abstract int Width { get; }
        protected abstract int Height { get; }
        protected abstract SharpDX.Direct3D11.Texture2D ScreenTexture { get; } 
    }
}
