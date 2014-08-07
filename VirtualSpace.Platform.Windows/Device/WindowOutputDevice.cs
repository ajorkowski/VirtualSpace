using System;
using System.Threading;
using System.Threading.Tasks;
using VirtualSpace.Core;
using VirtualSpace.Core.Device;
using VirtualSpace.Platform.Windows.Rendering;

namespace VirtualSpace.Platform.Windows.Device
{
    public class WindowOutputDevice : IOutputDevice
    {
        private readonly DeviceManager _manager;
        private readonly IDebugger _debugger;

        private CancellationTokenSource _source;
        private Task _rendererTask;

        public WindowOutputDevice(DeviceManager manager, IDebugger debugger)
        {
            _manager = manager;
            _debugger = debugger;
        }

        public string Name { get { return "Window"; } }
        public bool IsAvailable { get { return true; } }

        public void Run()
        {
            if (_source != null)
            {
                throw new InvalidOperationException("That device is already running!");
            }

            _source = new CancellationTokenSource();
            _rendererTask = Task.Run(() =>
            {
                using (var renderer = new WindowOutputRenderer(_debugger, 60))
                {
                    bool isRunning = true;
                    _source.Token.Register(() => { if (isRunning) renderer.Exit(); }, true);
                    renderer.Run(_manager.Environment);
                    isRunning = false;
                }

                _source.Dispose();
                _source = null;
            });
        }

        public void Stop()
        {
            if (_source != null)
            {
                _source.Cancel();
                _rendererTask.Wait();
                _rendererTask.Dispose();
                _rendererTask = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
