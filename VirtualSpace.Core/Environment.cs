﻿using PCLStorage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtualSpace.Core.Device;
using VirtualSpace.Core.Renderer;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Core.Video;

namespace VirtualSpace.Core
{
    public sealed class Environment : IEnvironment, IDisposable
    {
        private readonly IDebugger _debugger;

        private IInput _input;
        private IRenderer _renderer;
        private IScreenSourceFactory _screenFactory;

        private IVideo _videoSource;
        private IScreen _currentScreen;

        private IScreen _desktopScreen;

        private List<string> _videos;
        private int _currentVideo;
        private bool _enableStereoDelay;

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
            _renderer = renderer;
            _renderer.Camera.MoveTo(0, 0, 20);
            _renderer.Camera.LookAt(0, 0, 0);
        }

        public void Uninitialise(IRenderer renderer)
        {
            if (_currentScreen != null)
            {
                _currentScreen.Dispose();
                _currentScreen = null;
            }

            if (_videoSource != null)
            {
                _videoSource.Dispose();
                _videoSource = null;
            }
        }

        public void Update(IRenderer renderer, TimeSpan totalGameTime, TimeSpan elapsedGameTime, bool isRunningSlowly)
        {
            if (_videoSource != null && _videoSource.State == VideoState.Finished)
            {
                _currentScreen.Dispose();
                _videoSource.Dispose();

                _videoSource = null;
                _currentScreen = null;
            }

            if (_videoSource == null && _videos.Count > 0)
            {
                if (_currentVideo >= _videos.Count)
                {
                    _currentVideo = 0;
                }

                var file = _videos[_currentVideo];
                _debugger.WriteLine("Attempting to play file {0}", file);
                try
                {
                    _videoSource = _screenFactory.OpenVideo(file);
                    //_currentSource = _screenFactory.OpenPrimaryDesktop();
                    _currentScreen = _renderer.ScreenManager.CreateScreen(_videoSource, 17.2f, 17.2f);
                    _currentScreen.StereoDelayEnabled = _enableStereoDelay;
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

            MoveCamera(elapsedGameTime);

#if DEBUG
            if (_input.IsPressed(Keys.V))
            {
                VSync = !VSync;
            }

            if (_input.IsPressed(Keys.B))
            {
                ShowFPS = !ShowFPS;
            }

            if(_input.IsPressed(Keys.N) && _currentScreen != null)
            {
                _enableStereoDelay = !_enableStereoDelay;
                _currentScreen.StereoDelayEnabled = _enableStereoDelay;
                _desktopScreen.StereoDelayEnabled = _enableStereoDelay;
            }
#endif
        }

        public bool VSync { get; private set; }
        public bool ShowFPS { get; private set; }

        private void MoveCamera(TimeSpan elapsedGameTime)
        {
            float z = 0;
            float x = 0;
            float velocity = .002f * (float)elapsedGameTime.TotalMilliseconds;

            if (_input.IsDown(Keys.W))
            {
                z -= velocity;
            }

            if (_input.IsDown(Keys.S))
            {
                z += velocity;
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
                _renderer.Camera.MoveRelative(x, 0, z);
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
                _renderer.Camera.RotateRelative(xRot, yRot, 0);
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
            if (_currentScreen != null)
            {
                _currentScreen.Dispose();
                _currentScreen = null;
            }

            if (_videoSource != null)
            {
                _videoSource.Dispose();
                _videoSource = null;
            }
        }
    }
}
