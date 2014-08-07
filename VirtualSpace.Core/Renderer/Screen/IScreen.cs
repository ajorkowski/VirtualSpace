using System;
using VirtualSpace.Core.Math;

namespace VirtualSpace.Core.Renderer.Screen
{
    public interface IScreen : IDisposable
    {
        /// <summary>
        /// Diagonal size of screen
        /// </summary>
        float ScreenSize { get; set; }

        /// <summary>
        /// Radius to center of curvature, 0 to keep it flat
        /// </summary>
        float CurveRadius { get; set; }

        bool HasStereoDelay { get; }
        bool StereoDelayEnabled { get; set; }

        void SetPosition(Vec3 pos);
        void SetFacing(Vec3 pos);
    }
}
