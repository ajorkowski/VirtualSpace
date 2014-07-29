using System;
using System.Threading;
using System.Threading.Tasks;
using VirtualSpace.Core.Device;
using VirtualSpace.Platform.Windows.Rendering;

namespace VirtualSpace.Platform.Windows.Device
{
    public class OcculusOutputDevice : IOutputDevice
    {
        private readonly DeviceManager _manager;

        private CancellationTokenSource _source;
        private Task _rendererTask;

        public OcculusOutputDevice(DeviceManager manager)
        {
            _manager = manager;
        }

        public string Name
        {
            get { return "Occulus Window"; }
        }

        public void Run()
        {
            if (_source != null)
            {
                throw new InvalidOperationException("That device is already running!");
            }

            _source = new CancellationTokenSource();
            _rendererTask = Task.Run(() =>
            {
                using (var renderer = new OcculusOutputRenderer())
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
