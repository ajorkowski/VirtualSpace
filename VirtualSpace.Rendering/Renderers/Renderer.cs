using System;
using System.Collections.Generic;
using VirtualSpace.Core;

namespace VirtualSpace.Rendering.Renderers
{
    public abstract class Renderer : IDisposable
    {
        private readonly List<IDisposable> _toDispose;
        private readonly List<IDisposable> _toContentDispose;
        private bool _hasLoaded;

        public Renderer()
        {
            _toDispose = new List<IDisposable>();
            _toContentDispose = new List<IDisposable>();
            IsEnabled = true;
            IsVisible = true;
        }

        public bool IsEnabled { get; set; }
        public bool IsVisible { get; set; }

        public void Load(IEnvironment environment)
        {
            if (!_hasLoaded && (IsEnabled || IsVisible))
            {
                OnLoad(environment);
                _hasLoaded = true;
            }
        }

        public void Resize(IEnvironment environment)
        {
            if (_hasLoaded)
            {
                OnResize(environment);
            }
        }

        public void Update(IEnvironment environment, GameTime gameTime)
        {
            if(IsEnabled)
            {
                if(!_hasLoaded)
                {
                    Load(environment);
                }

                OnUpdate(environment, gameTime);
            }
        }

        public void Draw(IEnvironment environment, GameTime gameTime)
        {
            if(IsVisible)
            {
                if (!_hasLoaded)
                {
                    Load(environment);
                }

                OnDraw(environment, gameTime);
            }
        }

        public void Unload()
        {
            OnUnload();

            if(_toContentDispose.Count > 0)
            {
                foreach(var c in _toContentDispose)
                {
                    c.Dispose();
                }
                _toContentDispose.Clear();
            }

            _hasLoaded = false;
        }

        protected virtual void OnLoad(IEnvironment environment) { }
        protected virtual void OnResize(IEnvironment environment) { }
        protected virtual void OnUnload() { }

        protected virtual void OnUpdate(IEnvironment environment, GameTime gameTime) { }
        protected virtual void OnDraw(IEnvironment environment, GameTime gameTime) { }

        protected T ToDispose<T>(T toDispose)
            where T : IDisposable
        {
            _toDispose.Add(toDispose);
            return toDispose;
        }

        protected T ToContentDispose<T>(T toContentDispose)
            where T : IDisposable
        {
            _toContentDispose.Add(toContentDispose);
            return toContentDispose;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if(disposing)
            {
                if (_toContentDispose.Count > 0)
                {
                    foreach (var c in _toContentDispose)
                    {
                        c.Dispose();
                    }
                    _toContentDispose.Clear();
                }

                if (_toDispose.Count > 0)
                {
                    foreach (var c in _toDispose)
                    {
                        c.Dispose();
                    }
                    _toDispose.Clear();
                }
            }
        }
    }
}
