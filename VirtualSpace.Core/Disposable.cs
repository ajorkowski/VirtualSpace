using System;

namespace VirtualSpace.Core
{
    public sealed class Disposable : IDisposable
    {
        private readonly Action _toDispose;
        private bool _hasDisposed;

        public Disposable(Action toDispose)
        {
            _toDispose = toDispose;
        }

        public void Dispose()
        {
            if (!_hasDisposed)
            {
                _toDispose();
                _hasDisposed = true;
            }
        }
    }
}
