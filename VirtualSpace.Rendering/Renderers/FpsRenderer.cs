using OpenTK.Graphics.OpenGL4;
using System.Drawing;
using VirtualSpace.Core;
using VirtualSpace.Core.Services;
using VirtualSpace.Rendering.OpenGL;

namespace VirtualSpace.Rendering.Renderers
{
    internal sealed class FpsRenderer : Renderer
    {
        private readonly IFpsService _fpsService;

        private Bitmap _textBitmap;
        private Font _textFont;
        private int _textureHandle;
        private string _currentFpsString;

        private Shader _spriteShader;
        private VertexFloatBuffer _buffer;

        public FpsRenderer(IFpsService fpsService)
        {
            _fpsService = fpsService;
        }

        protected override void OnLoad(IEnvironment environment)
        {
            _textFont = ToContentDispose(new Font(FontFamily.GenericSansSerif, 16));
            _textBitmap = ToContentDispose(new Bitmap(400, 30));

            _textureHandle = GL.GenTexture();
            ToContentDispose(Disposable.TextureHandle(_textureHandle));
            GL.BindTexture(TextureTarget.Texture2D, _textureHandle);

            var data = _textBitmap.LockBits(new Rectangle(0, 0, _textBitmap.Width, _textBitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, data.Width, data.Height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
            GL.Finish();
            _textBitmap.UnlockBits(data);

            _spriteShader = Shader.Default.SpriteShader_XY_UV;
            _buffer = ToContentDispose(VertexFloatBuffer.Sprite.SimpleSprite(0, _textBitmap.Width / (float)environment.WindowWidth, 0, _textBitmap.Height / (float)environment.WindowHeight));
            _buffer.Load();

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        protected override void OnUpdate(IEnvironment environment, GameTime gameTime)
        {
            _fpsService.Update(gameTime.TotalTime);

            var str = string.Format("fps: {0}, cpu: {1:0.00}%, mem: {2:0.00}mb", _fpsService.Fps, _fpsService.Cpu, _fpsService.Ram);
            if (_currentFpsString != str)
            {
                using (Graphics gfx = Graphics.FromImage(_textBitmap))
                {
                    gfx.Clear(Color.Transparent);
                    gfx.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    gfx.DrawString(str, _textFont, new SolidBrush(Color.White), new PointF(0, 0));
                }

                var data = _textBitmap.LockBits(new Rectangle(0, 0, _textBitmap.Width, _textBitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                GL.BindTexture(TextureTarget.Texture2D, _textureHandle);
                GL.TexSubImage2D(TextureTarget.Texture2D, 0, 0, 0, _textBitmap.Width, _textBitmap.Height, PixelFormat.Bgra, PixelType.UnsignedByte, data.Scan0);
                GL.BindTexture(TextureTarget.Texture2D, 0);
                _textBitmap.UnlockBits(data);

                _currentFpsString = str;
            }
        }

        protected override void OnDraw(IEnvironment environment, GameTime gameTime)
        {
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.Enable(EnableCap.Blend);
            GL.UseProgram(_spriteShader.Program);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _textureHandle);
            _buffer.Bind(_spriteShader);
            GL.UseProgram(0);
            GL.Disable(EnableCap.Blend);
        }
    }
}
