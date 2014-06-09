using System;
using System.Diagnostics;
using VirtualSpace.Core.Services;

namespace VirtualSpace.Platform.Windows
{
    public sealed class FpsService : IFpsService
    {
        // FPS timers
        private int _count;
        private TimeSpan _endOfSecond;

        // CPU timers
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;

        private void Initialise()
        {
            // Setup CPU/RAM counters
            var processName = Process.GetCurrentProcess().ProcessName;
            _cpuCounter = new PerformanceCounter
            {
                CategoryName = "Process",
                CounterName = "% Processor Time",
                InstanceName = processName
            };

            _ramCounter = new PerformanceCounter
            {
                CategoryName = "Process",
                CounterName = "Working Set",
                InstanceName = processName
            };

            // Need to do an initial sample
            _cpuCounter.NextValue();
            _ramCounter.NextValue();
        }

        public int Fps { get; private set; }
        public float Cpu { get; private set; }
        public float Ram { get; private set; }

        public void Update(TimeSpan totalGameTime)
        {
            if(_cpuCounter == null)
            {
                Initialise();
            }

            _count++;
            // Sample once every second
            if (totalGameTime >= _endOfSecond)
            {
                // Fps calculation
                Fps = _count;
                _count = 0;

                // Cpu calculation
                Cpu = _cpuCounter.NextValue();

                // Memory calc
                Ram = _ramCounter.NextValue() / 1048576.0f; // bytes -> mb

                // Get the next second
                _endOfSecond = totalGameTime.Add(TimeSpan.FromSeconds(1));
            }
        }

        public void Dispose()
        {
            if (_cpuCounter != null)
            {
                _cpuCounter.Dispose();
            }

            if (_ramCounter != null)
            {
                _ramCounter.Dispose();
            }
        }
    }
}
