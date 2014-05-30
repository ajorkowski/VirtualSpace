using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using VirtualSpace.Core.Environment;
using VirtualSpace.Platform.Windows.Environment;

namespace VirtualSpace.Platform.Windows.Rendering
{
    internal sealed class SceneRenderer : GameSystem
    {
        private ICameraService _cameraService;

        private GeometricPrimitive _cube;
        //private Texture2D _cubeTexture;
        private Vector3 _cubeLighting;
        private Matrix _cubeTransform;

        private GeometricPrimitive _plane;
        private ScreenCapture _planeTexture;
        private Vector3 _planeLighting;
        private SharpDX.Direct3D11.ShaderResourceView _planeShaderView;
        private Matrix _planeTransform;

        private BasicEffect _basicEffect;

        public SceneRenderer(Game game)
            : base(game)
        {
            Visible = true;
            Enabled = true;

            game.GameSystems.Add(this);
        }

        public override void Initialize()
        {
            base.Initialize();

            _cameraService = Services.GetService<ICameraService>();
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            _basicEffect = ToDisposeContent(new BasicEffect(GraphicsDevice));
            
            //_basicEffect.EnableDefaultLighting();
            //

            LoadCube();
            LoadPlane();
        }

        private void LoadCube()
        {
            _cube = ToDisposeContent(GeometricPrimitive.Cube.New(GraphicsDevice));
            //_cubeTexture = Content.Load<Texture2D>("logo_large");
            _cubeLighting = new Vector3(0, 1, 0);
            _cubeTransform = Matrix.Identity;
        }
        private void LoadPlane()
        {
            _plane = ToDisposeContent(GeometricPrimitive.Plane.New(GraphicsDevice, 5f, 5f));
            //_planeTexture = Content.Load<Texture2D>("GeneticaMortarlessBlocks");
            _planeTexture = new ScreenCapture((IScreen)null, GraphicsDevice);

            var desc = _planeTexture.ScreenTexture.Description;
            _planeShaderView = new SharpDX.Direct3D11.ShaderResourceView(GraphicsDevice, _planeTexture.ScreenTexture, new SharpDX.Direct3D11.ShaderResourceViewDescription
            {
                Format = desc.Format,
                Dimension = SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D,
                Texture2D = new SharpDX.Direct3D11.ShaderResourceViewDescription.Texture2DResource { MipLevels = desc.MipLevels, MostDetailedMip = desc.MipLevels - 1 }
            });

            _planeLighting = new Vector3(1, 0, 0);
            _planeTransform = Matrix.RotationX(-MathUtil.PiOverTwo) * Matrix.Translation(0f, -1f, 0f);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            //_basicEffect.Texture = _cubeTexture;
            _basicEffect.AmbientLightColor = _cubeLighting;
            _basicEffect.World = _cubeTransform;
            _basicEffect.TextureEnabled = false;
            _basicEffect.LightingEnabled = true;
            _cube.Draw(_basicEffect);

            //_basicEffect.Texture = _planeTexture;
            //_basicEffect.AmbientLightColor = _planeLighting;
            _planeTexture.CaptureScreen();
            _basicEffect.TextureView = _planeShaderView;
            _basicEffect.World = _planeTransform;
            _basicEffect.TextureEnabled = true;
            _basicEffect.LightingEnabled = false;
            _plane.Draw(_basicEffect);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var time = (float)gameTime.TotalGameTime.TotalSeconds;
            _cubeTransform = Matrix.RotationX(time) * Matrix.RotationY(time * 2f) * Matrix.RotationZ(time * .7f);

            _basicEffect.View = _cameraService.View;
            _basicEffect.Projection = _cameraService.Projection;
        }
    }
}
