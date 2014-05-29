using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VirtualSpace.Platform.Windows.Rendering
{
    internal sealed class SceneRenderer : GameSystem
    {
        private ICameraService _cameraService;

        private GeometricPrimitive _cube;
        //private Texture2D _cubeTexture;
        private Matrix _cubeTransform;

        private GeometricPrimitive _plane;
        //private Texture2D _planeTexture;
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
            _basicEffect.EnableDefaultLighting();
            _basicEffect.TextureEnabled = true;

            LoadCube();
            LoadPlane();
        }

        private void LoadCube()
        {
            _cube = ToDisposeContent(GeometricPrimitive.Cube.New(GraphicsDevice));
            //_cubeTexture = Content.Load<Texture2D>("logo_large");
            _cubeTransform = Matrix.Identity;
        }
        private void LoadPlane()
        {
            _plane = ToDisposeContent(GeometricPrimitive.Plane.New(GraphicsDevice, 50f, 50f));
            //_planeTexture = Content.Load<Texture2D>("GeneticaMortarlessBlocks");
            _planeTransform = Matrix.RotationX(-MathUtil.PiOverTwo) * Matrix.Translation(0f, -5f, 0f);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            //_basicEffect.Texture = _cubeTexture;
            _basicEffect.World = _cubeTransform;
            _cube.Draw(_basicEffect);

            //_basicEffect.Texture = _planeTexture;
            _basicEffect.World = _planeTransform;
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
