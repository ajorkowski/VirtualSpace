using SharpOVR;
using System;
using System.Threading;
using System.Threading.Tasks;
using VirtualSpace.Core;
using VirtualSpace.Core.Device;
using VirtualSpace.Platform.Windows.Rendering;

namespace VirtualSpace.Platform.Windows.Device
{
    public sealed class OcculusOutputDevice : IOutputDevice
    {
        private readonly DeviceManager _manager;
        private readonly IDebugger _debugger;

        private CancellationTokenSource _source;
        private Task _rendererTask;

        public OcculusOutputDevice(DeviceManager manager, IDebugger debugger)
        {
            _manager = manager;
            _debugger = debugger;

            // Initialize OVR Library
            OVR.Initialize();
        }

        public string Name
        {
            get { return "Occulus Rift"; }
        }

        public bool IsAvailable
        {
            get 
            { 
#if DEBUG
                return true;
#else
                return OVR.HmdDetect() > 0;
#endif
            }
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
                using (var renderer = new OcculusOutputRenderer(_debugger, 90))
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
            OVR.Shutdown();
        }
    }
}
