using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Input;
using System;
using System.Collections.Generic;
using VirtualSpace.Core;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Services;
using VirtualSpace.Rendering.Renderers;

namespace VirtualSpace.Rendering
{
    public sealed class Device : GameWindow, IDevice
    {
        private readonly GameTime _gameTime;
        private readonly List<Renderer> _renderers;
        private readonly FpsRenderer _fpsRenderer;

        private IEnvironment _environment;
        private bool _updateWaiting;

        private int _lastHeight;
        private int _lastWidth;

        public Device(IFpsService fpsService)
            : base(800, 600, GraphicsMode.Default, "Virtual Space", GameWindowFlags.Default, DisplayDevice.Default, 4, 0, GraphicsContextFlags.ForwardCompatible)
        {
            CursorVisible = true;

            _renderers = new List<Renderer>();
            _gameTime = new GameTime
            {
                TotalTime = new TimeSpan(),
                ElapsedTime = new TimeSpan(),
                IsRunningSlowly = false
            };

            _renderers.Add(new ScreenRenderer());
            _fpsRenderer = new FpsRenderer(fpsService);
            _renderers.Add(_fpsRenderer);
        }

        public void Run(IEnvironment environment)
        {
            _environment = environment;

            VSync = environment.VSync ? VSyncMode.Adaptive : VSyncMode.Off;
            _fpsRenderer.IsEnabled = _environment.ShowFPS;
            _fpsRenderer.IsVisible = _environment.ShowFPS;

            _environment.Camera.M.Projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, Width / (float)Height, 0.5f, 10000.0f);

            base.Run(60);
        }

        public bool IsKeyDown(Key key)
        {
            return Keyboard[key];
        }

        protected override void OnLoad(EventArgs e)
        {
            GL.ClearColor(Color4.CornflowerBlue);

            _lastHeight = Height;
            _lastWidth = Width;

            foreach (var r in _renderers)
            {
                r.Load(_environment);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            GL.Viewport(ClientRectangle);

            if (_lastHeight != Height || _lastWidth != Width)
            {
                _lastHeight = Height;
                _lastWidth = Width;

                _environment.Camera.M.Projection = Matrix4.CreatePerspectiveFieldOfView(MathHelper.PiOver4, Width / (float)Height, 0.5f, 10000.0f);

                foreach (var r in _renderers)
                {
                    r.Resize(_environment);
                }
            }
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            if (_updateWaiting) { return; }

            _gameTime.ElapsedTime = TimeSpan.FromSeconds(e.Time);
            _gameTime.TotalTime = _gameTime.TotalTime.Add(_gameTime.ElapsedTime);
            _environment.Update(_gameTime.TotalTime, _gameTime.ElapsedTime, e.Time < TargetUpdatePeriod);

            if (_environment.VSync != (VSync == VSyncMode.Adaptive))
            {
                VSync = _environment.VSync ? VSyncMode.Adaptive : VSyncMode.Off;
            }

            _fpsRenderer.IsEnabled = _environment.ShowFPS;
            _fpsRenderer.IsVisible = _environment.ShowFPS;

            foreach(var r in _renderers)
            {
                r.Update(_environment, _gameTime);
            }

            _environment.Camera.HasUpdate = false;
            _updateWaiting = true;
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            GL.Clear(ClearBufferMask.ColorBufferBit);

            foreach (var r in _renderers)
            {
                r.Draw(_environment, _gameTime);
            }

            SwapBuffers();
            _updateWaiting = false;
        }

        protected override void OnUnload(EventArgs e)
        {
            foreach (var r in _renderers)
            {
                r.Unload();
            }
        }
    }
}
