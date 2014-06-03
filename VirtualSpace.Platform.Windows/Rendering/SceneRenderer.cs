using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;

namespace VirtualSpace.Platform.Windows.Rendering
{
    internal sealed class SceneRenderer : GameSystem
    {
        private ICameraService _cameraService;

        private GeometricPrimitive _cube;
        //private Texture2D _cubeTexture;
        private Vector3 _cubeLighting;
        private Matrix _cubeTransform;

        private BasicEffect _basicEffect;

        public SceneRenderer(Game game)
            : base(game)
        {
            Visible = true;
            Enabled = true;

            game.GameSystems.Add(this);
            game.GameSystems.Add(new Screen.ScreenRendererDX11(game, null));
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

            LoadCube();
        }

        private void LoadCube()
        {
            _cube = ToDisposeContent(GeometricPrimitive.Cube.New(GraphicsDevice));
            _cubeLighting = new Vector3(0, 1, 0);
            _cubeTransform = Matrix.Identity;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            var time = (float)gameTime.TotalGameTime.TotalSeconds;
            _cubeTransform = Matrix.RotationX(time) * Matrix.RotationY(time * 2f) * Matrix.RotationZ(time * .7f);

            _basicEffect.View = _cameraService.View;
            _basicEffect.Projection = _cameraService.Projection;
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            _basicEffect.AmbientLightColor = _cubeLighting;
            _basicEffect.World = _cubeTransform;
            _basicEffect.TextureEnabled = false;
            _basicEffect.LightingEnabled = true;
            _cube.Draw(_basicEffect);
        }
    }
}
