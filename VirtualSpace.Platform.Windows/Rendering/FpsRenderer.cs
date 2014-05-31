using SharpDX.Toolkit;
using System;
using System.Runtime.InteropServices;
using VirtualSpace.Platform.Windows.Native;

namespace VirtualSpace.Platform.Windows.Rendering
{
    internal sealed class FpsRenderer : GameSystem
    {
        private const string CpuCounterName = @"\Processor(0)\% Processor Time";

        private int _fps;
        private int _count;
        private TimeSpan _endOfSecond;

        private bool _canReadCpu;
        private PdhQueryHandle _queryHandle;
        private PdhCounterHandle _counterHandle;
        private double _cpuUsage;

        public FpsRenderer(Game game)
            : base(game)
        {
            Visible = true;
            Enabled = true;

            game.GameSystems.Add(this);
        }

        public override void Initialize()
        {
            base.Initialize();

            // Setup CPU counters
            _canReadCpu = true;
            var result = PdhApi.PdhOpenQuery(null, IntPtr.Zero, out _queryHandle);
            if(result != 0)
            {
                _canReadCpu = false;
            }

            result = PdhApi.PdhAddCounter(_queryHandle, CpuCounterName, IntPtr.Zero, out _counterHandle);
            if(result != 0)
            {
                _canReadCpu = false;
            }

            // Need to do an initial sample
            if(_canReadCpu)
            {
                PdhApi.PdhCollectQueryData(_queryHandle);
            }
        }

        public override void Update(GameTime gameTime)
        {
            _count++;
            // Sample once every second
            if (gameTime.TotalGameTime >= _endOfSecond)
            {
                // Fps calculation
                _fps = _count;
                _count = 0;

                // Cpu calculation
                if(_canReadCpu)
                {
                    var result = PdhApi.PdhCollectQueryData(_queryHandle);

                    if (result == 0)
                    {
                        PDH_FMT_COUNTERVALUE value;
                        result = PdhApi.PdhGetFormattedCounterValue(_counterHandle, PdhFormat.PDH_FMT_DOUBLE, IntPtr.Zero, out value);
                        if(result == 0 && value.CStatus == 0)
                        {
                            _cpuUsage = value.doubleValue;
                        }
                    }
                }

                // Get the next second
                _endOfSecond = gameTime.TotalGameTime.Add(TimeSpan.FromSeconds(1));
                
                Console.WriteLine(string.Format("fps: {0}, cpu: {1}", _fps, _cpuUsage));
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            if (_queryHandle != null)
            {
                _queryHandle.Dispose();
                _queryHandle = null;
            }

            if(_counterHandle != null)
            {
                _counterHandle.Dispose();
                _counterHandle = null;
            }

            base.Dispose(disposeManagedResources);
        }
    }
}
