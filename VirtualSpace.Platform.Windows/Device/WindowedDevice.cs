using System;
using System.Threading;
using System.Threading.Tasks;
using VirtualSpace.Core;
using VirtualSpace.Core.AppContext;
using VirtualSpace.Core.Device;
using VirtualSpace.Platform.Windows.Rendering;

namespace VirtualSpace.Platform.Windows.Device
{
    public class WindowedDevice : IDevice
    {
        private bool _isRunning;
        private WindowOutputRenderer _renderer;
        private CancellationTokenSource _cancelToken;
        private Task _renderTask;

        public string Name
        {
            get { return "Window"; }
        }

        public void Run(IEnvironment environment, IApplicationContext context)
        {
            if(_isRunning)
            {
                throw new InvalidOperationException("That device is already running!");
            }

            _isRunning = true;
            _cancelToken = new CancellationTokenSource();
            _renderer = new WindowOutputRenderer(context);
            _renderTask = Task.Run(() =>
            {
                _renderer.Run(environment, _cancelToken.Token);
            });
        }

        public async Task Stop()
        {
            if (_isRunning)
            {
                _cancelToken.Cancel();
                await _renderTask.ConfigureAwait(false);
                _renderTask.Dispose();
                _cancelToken.Dispose();

                _renderer.Dispose();
                _renderer = null;

                _renderTask = null;
                _cancelToken = null;
                _isRunning = false;
            }
        }

        public void Dispose()
        {
            Stop().Wait();
        }
    }
}
