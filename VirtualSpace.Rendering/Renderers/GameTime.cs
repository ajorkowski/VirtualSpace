using System;

namespace VirtualSpace.Rendering.Renderers
{
    public class GameTime
    {
        public TimeSpan TotalTime { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public bool IsRunningSlowly { get; set; }
    }
}
