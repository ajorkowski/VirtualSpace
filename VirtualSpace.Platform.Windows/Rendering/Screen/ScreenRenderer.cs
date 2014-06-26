using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Platform.Windows.Rendering.Providers;

namespace VirtualSpace.Platform.Windows.Rendering.Screen
{
    internal abstract class ScreenRenderer : GameSystem, IScreen
    {
        private ICameraProvider _cameraService;
        private BasicEffect _basicEffect;

        private GeometricPrimitive _plane;
        private SharpDX.Direct3D11.ShaderResourceView _planeShaderView;
        private Matrix _planeTransform;

        public ScreenRenderer(Game game, ICameraProvider camera)
            : base(game)
        {
            _cameraService = camera;
            Visible = true;
            Enabled = true;
            ScreenSize = 1;
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

            if (CurveRadius <= 0.01 || CurveRadius > 100000)
            {
                _plane = ToDisposeContent(GeometricPrimitive.Plane.New(GraphicsDevice, Width, Height));
            }
            else
            {
                _plane = ToDisposeContent(CreateCurvedSurface(GraphicsDevice, CurveRadius * Width / ScreenSize, Width, Height, 100));
            }

            _planeTransform = Matrix.Scaling(ScreenSize / Width); // Make it 6.7m wide...
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

        public abstract int Width { get; }
        public abstract int Height { get; }
        public float ScreenSize { get; set; }
        public float CurveRadius { get; set; }
        protected abstract SharpDX.Direct3D11.Texture2D ScreenTexture { get; }

        private static GeometricPrimitive CreateCurvedSurface(GraphicsDevice device, float distance, float width, float height, int tessellation)
        {
            if (tessellation < 1)
            {
                throw new ArgumentOutOfRangeException("tessellation", "tessellation must be > 0");
            }

            // Setup memory
            var vertices = new VertexPositionNormalTexture[tessellation * 2 + 2];
            var indices = new int[tessellation * 6];

            UpdateCurvedVectors(vertices, distance, width, height);

            var currentIndex = 0;
            for (var i = 0; i < tessellation; i++)
            {
                var iBase = i * 2;
                indices[currentIndex++] = iBase;
                indices[currentIndex++] = iBase + 1;
                indices[currentIndex++] = iBase + 3;

                indices[currentIndex++] = iBase;
                indices[currentIndex++] = iBase + 3;
                indices[currentIndex++] = iBase + 2;
            }

            return new GeometricPrimitive(device, vertices, indices) { Name = "Half cylinder" }; 
        }

        private static void UpdateCurvedVectors(VertexPositionNormalTexture[] vertices, float distance, float width, float height)
        {
            var tessellation = vertices.Length / 2 - 1;
            var invTes = 1.0f / tessellation;

            var totalAngle = width / distance;
            var deltaAngle = totalAngle * invTes;
            var sizeY = height / 2;
            var startAngle = totalAngle / 2;

            var currentVertex = 0;

            for (var i = 0; i <= tessellation; i++)
            {
                var currentAngle = startAngle - deltaAngle * i;

                // Top coord
                var x = distance * (float)Math.Sin(currentAngle);
                var z = distance * (1 - (float)Math.Cos(currentAngle)); // Will be positive, means towards the user in RHS
                var position = new Vector3(x, sizeY, z);
                var normal = new Vector3(-x, 0, -z); // shared normal for both points
                normal.Normalize();
                var textCoord = new Vector2(1 - i * invTes, 0);
                vertices[currentVertex++] = new VertexPositionNormalTexture(position, normal, textCoord);

                // Bottom coord
                position = new Vector3(x, -sizeY, z);
                textCoord = new Vector2(1 - i * invTes, 1);
                vertices[currentVertex++] = new VertexPositionNormalTexture(position, normal, textCoord);
            }
        }
    }
}
