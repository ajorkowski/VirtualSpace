using OpenTK;
using OpenTK.Graphics.OpenGL4;
using System;
using VirtualSpace.Core;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Screen;
using VirtualSpace.Rendering.OpenGL;

namespace VirtualSpace.Rendering.Renderers
{
    internal sealed class ScreenRenderer : Renderer
    {
        private int _textureHandle;
        private Shader _shader;
        private VertexFloatBuffer _buffer;
        private Matrix4 _modelView;

        protected override void OnLoad(IEnvironment environment)
        {
            var screen = environment.Desktop;

            // Setup the texture
            _textureHandle = GL.GenTexture();
            ToContentDispose(Disposable.TextureHandle(_textureHandle));
            GL.BindTexture(TextureTarget.Texture2D, _textureHandle);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, screen.Width, screen.Height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero);
            GL.Finish();

            _shader = Shader.Default.DefaultShader_XYZ_UV;
            _buffer = CreateCurvedSurface(screen.CurveRadius, screen.Width, screen.Height, 100);
            _buffer.Load();

            _modelView = Matrix4.CreateScale(screen.ScreenSize / (float)screen.Width);
        }

        protected override void OnUpdate(IEnvironment environment, GameTime gameTime)
        {
            if (environment.Camera.HasUpdate)
            {
                var mvpMatrix = _modelView * environment.Camera.M.View * environment.Camera.M.Projection;
                GL.UseProgram(_shader.Program);
                int mvpLoc = GL.GetUniformLocation(_shader.Program, "mvp_matrix");
                GL.UniformMatrix4(mvpLoc, false, ref mvpMatrix);
                GL.UseProgram(0);
            }

            environment.Desktop.CaptureFrame(_textureHandle);
        }

        protected override void OnDraw(IEnvironment environment, GameTime gameTime)
        {
            GL.UseProgram(_shader.Program);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _textureHandle);
            _buffer.Bind(_shader);
            GL.UseProgram(0);
        }

        private static VertexFloatBuffer CreateCurvedSurface(float distance, float width, float height, int tessellation)
        {
            if (tessellation < 1)
            {
                throw new ArgumentOutOfRangeException("tessellation", "tessellation must be > 0");
            }

            // Setup memory
            var vertices = new float[tessellation * 10 + 10];
            var indices = new uint[tessellation * 6];

            UpdateCurvedVectors(vertices, distance, width, height);

            var currentIndex = 0;
            for (var i = 0; i < tessellation; i++)
            {
                var iBase = (uint)(i * 2);
                indices[currentIndex++] = iBase;
                indices[currentIndex++] = iBase + 1;
                indices[currentIndex++] = iBase + 3;

                indices[currentIndex++] = iBase;
                indices[currentIndex++] = iBase + 3;
                indices[currentIndex++] = iBase + 2;
            }

            var buffer = new VertexFloatBuffer(VertexFormat.XYZ_UV, vertices.Length);
            buffer.Set(vertices, indices);
            return buffer;
        }

        private static void UpdateCurvedVectors(float[] vertices, float distance, float width, float height)
        {
            var tessellation = vertices.Length / 10 - 1;
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
                var x = distance == 0 ? (-width / 2) + i * width * invTes : distance * (float)Math.Sin(currentAngle);
                var z = distance == 0 ? 0 : distance * (1 - (float)Math.Cos(currentAngle)); // Will be positive, means towards the user in RHS
                vertices[currentVertex++] = x;        // position
                vertices[currentVertex++] = sizeY;
                vertices[currentVertex++] = z;
                vertices[currentVertex++] = 1 - i * invTes;     // uv
                vertices[currentVertex++] = 0;

                // Bottom coord
                vertices[currentVertex++] = x;        // position
                vertices[currentVertex++] = -sizeY;
                vertices[currentVertex++] = z;
                vertices[currentVertex++] = 1 - i * invTes;     // uv
                vertices[currentVertex++] = 1;
            }
        }
    }
}
