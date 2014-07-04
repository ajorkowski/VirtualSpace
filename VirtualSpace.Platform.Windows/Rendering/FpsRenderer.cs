using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using System;
using System.Diagnostics;

namespace VirtualSpace.Platform.Windows.Rendering
{
    internal sealed class FpsRenderer : GameSystem
    {
        private const string CpuCounterName = @"\Processor(0)\% Processor Time";

        // FPS timers
        private int _fps;
        private int _count;
        private TimeSpan _endOfSecond;

        // CPU timers
        private float _cpu;
        private float _memory;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _ramCounter;

        // Rendering
        private SpriteFont _spriteFont;
        private SpriteBatch _spriteBatch;

        public FpsRenderer(Game game)
            : base(game)
        {
            Visible = true;
            Enabled = true;

            DrawOrder = UpdateOrder = RenderingOrder.Overlay;

            game.GameSystems.Add(this);
        }

        public override void Initialize()
        {
            base.Initialize();

            // Setup CPU/RAM counters
            var processName = Process.GetCurrentProcess().ProcessName;
            _cpuCounter = ToDispose(new PerformanceCounter
            {
                CategoryName = "Process",
                CounterName = "% Processor Time",
                InstanceName = processName
            });

            _ramCounter = ToDispose(new PerformanceCounter
            {
                CategoryName = "Process",
                CounterName = "Working Set",
                InstanceName = processName
            });

            // Need to do an initial sample
            _cpuCounter.NextValue();
            _ramCounter.NextValue();
        }

        protected override void LoadContent()
        {
            base.LoadContent();

            _spriteFont = ToDisposeContent(Content.Load<SpriteFont>("Arial16"));
            _spriteBatch = ToDisposeContent(new SpriteBatch(GraphicsDevice));
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
                _cpu = _cpuCounter.NextValue();

                // Memory calc
                _memory = _ramCounter.NextValue() / 1048576.0f; // bytes -> mb

                // Get the next second
                _endOfSecond = gameTime.TotalGameTime.Add(TimeSpan.FromSeconds(1));
            }

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            _spriteBatch.Begin();

            _spriteBatch.DrawString(_spriteFont, string.Format("fps: {0}, cpu: {1:0.00}%, mem: {2:0.00}mb", _fps, _cpu, _memory), new Vector2(10, 10), Color.White);

            _spriteBatch.End();
        }
    }
}
