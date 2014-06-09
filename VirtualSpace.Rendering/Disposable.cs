using OpenTK.Graphics.OpenGL;
using System;

namespace VirtualSpace.Rendering
{
    public sealed class Disposable : IDisposable
    {
        private readonly Action _toDispose;
        private bool _hasDisposed;

        public static Disposable TextureHandle(int textureHandle)
        {
            return new Disposable(() => GL.DeleteTexture(textureHandle));
        }

        public Disposable(Action toDispose)
        {
            _toDispose = toDispose;
        }

        public void Dispose()
        {
            if(!_hasDisposed)
            {
                _toDispose();
                _hasDisposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
