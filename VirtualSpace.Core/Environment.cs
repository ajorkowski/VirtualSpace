using PCLStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualSpace.Core.Desktop;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Math;
using VirtualSpace.Core.Renderer;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Core.Video;

namespace VirtualSpace.Core
{
    public sealed class Environment : IEnvironment, IDisposable
    {
        private readonly IDebugger _debugger;

        private IInput _input;
        private IScreenSourceFactory _screenFactory;

        private IDesktop _desktop;
        private IScreen _desktopScreen;

        private IVideo _videoSource;
        private IScreen _movieScreen;

        private List<string> _videos;
        private int _currentVideo;
        private bool _enableStereoDelay;

        private string _movieToWatch;

        public Environment(IScreenSourceFactory screenFactory, IFolder folder, IDebugger debugger)
        {
            _debugger = debugger;
            VSync = true;
            _screenFactory = screenFactory;
            _enableStereoDelay = true;

            _videos = new List<string>();
            _currentVideo = 0;

            Task.Run(() => FindVideos(folder));
        }

        public void Initialise(IRenderer renderer, IInput input)
        {
            _input = input;
            renderer.Camera.MoveTo(new Vec3(0, 0, 20));
            renderer.Camera.LookAt(new Vec3());
        }

        public void Uninitialise(IRenderer renderer)
        {
            if (_movieScreen != null)
            {
                _movieScreen.Dispose();
                _movieScreen = null;
            }

            if (_videoSource != null)
            {
                _videoSource.Dispose();
                _videoSource = null;
            }

            if(_desktopScreen != null)
            {
                _desktopScreen.Dispose();
                _desktopScreen = null;
            }

            if(_desktop != null)
            {
                _desktop.Dispose();
                _desktop = null;
            }
        }

        public void WatchMovie(string file)
        {
            _movieToWatch = file;
        }

        public void Update(IRenderer renderer, TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly)
        {
            if (_videoSource != null && (_movieToWatch != null || _videoSource.State == VideoState.Finished))
            {
                _movieScreen.Dispose();
                _videoSource.Dispose();

                _videoSource = null;
                _movieScreen = null;
            }

            if (_videoSource == null && _videos.Count > 0)
            {
                if (_currentVideo >= _videos.Count)
                {
                    _currentVideo = 0;
                }

                var file = _movieToWatch ?? _videos[_currentVideo];
                _movieToWatch = null;
                _debugger.WriteLine("Attempting to play file {0}", file);
                try
                {
                    _videoSource = _screenFactory.OpenVideo(file);
                    _movieScreen = renderer.ScreenManager.CreateScreen(_videoSource, 17.2f, 17.2f);
                    _movieScreen.SetFacing(new Vec3(0, 0, 1));
                    _movieScreen.StereoDelayEnabled = _enableStereoDelay;
                }
                catch(NotSupportedException)
                {
                    _debugger.WriteLine("File {0} format is not supported", file);
                }

                _currentVideo++;
            }

            if (_videoSource != null && _videoSource.State == VideoState.Paused)
            {
                _videoSource.Play();
            }

            MoveCamera(elapsedGameTime, renderer);

            if(_input.IsPressed(Keys.Z))
            {
                if(_desktop == null)
                {
                    _desktop = _screenFactory.OpenPrimaryDesktop();
                    _desktopScreen = renderer.ScreenManager.CreateScreen(_desktop, 0.762f, 0.762f);
                    _desktopScreen.SetPosition(renderer.Camera.FindPointInWorldSpace(new Vec3(0, 0, -1)));
                    _desktopScreen.SetFacing(renderer.Camera.FindPointInWorldSpace(new Vec3()));
                    _desktopScreen.StereoDelayEnabled = _enableStereoDelay;
                }
                else
                {
                    _desktopScreen.Dispose();
                    _desktop.Dispose();
                    _desktopScreen = null;
                    _desktop = null;
                }
            }

#if DEBUG
            if (_input.IsPressed(Keys.V))
            {
                VSync = !VSync;
            }

            if (_input.IsPressed(Keys.B))
            {
                ShowFPS = !ShowFPS;
            }

            if(_input.IsPressed(Keys.N) && _movieScreen != null)
            {
                _enableStereoDelay = !_enableStereoDelay;
                _movieScreen.StereoDelayEnabled = _enableStereoDelay;
            }
#endif
        }

        public bool VSync { get; private set; }
        public bool ShowFPS { get; private set; }

        private void MoveCamera(TimeSpan elapsedGameTime, IRenderer renderer)
        {
            float z = 0;
            float x = 0;
            float velocity = .002f * (float)elapsedGameTime.TotalMilliseconds;

            if (_input.IsDown(Keys.W))
            {
                z += velocity;
            }

            if (_input.IsDown(Keys.S))
            {
                z -= velocity;
            }

            if (_input.IsDown(Keys.A))
            {
                x += velocity;
            }

            if (_input.IsDown(Keys.D))
            {
                x -= velocity;
            }

            if (z != 0 || x != 0)
            {
                renderer.Camera.MoveRelative(new Vec3(x, 0, z));
            }

            float xRot = 0;
            float yRot = 0;
            if (_input.IsDown(Keys.Up))
            {
                xRot -= velocity;
            }

            if (_input.IsDown(Keys.Down))
            {
                xRot += velocity;
            }

            if (_input.IsDown(Keys.Left))
            {
                yRot -= velocity;
            }

            if (_input.IsDown(Keys.Right))
            {
                yRot += velocity;
            }

            if (xRot != 0 || yRot != 0)
            {
                renderer.Camera.RotateRelative(new Vec3(xRot, yRot, 0));
            }
        }

        private async Task FindVideos(IFolder folder)
        {
            var fold = await folder.GetFolderAsync("Videos");
            var files = await fold.GetFilesAsync();
            _videos.AddRange(files.Select(f => f.Path));
        }

        public void Dispose()
        {
            Uninitialise(null);
        }
    }
}
