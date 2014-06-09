using System;

namespace VirtualSpace.Core.Screen
{
    public interface IScreen : IDisposable
    {
        int Width { get; }
        int Height { get; }

        /// <summary>
        /// Diagonal size of screen in world space
        /// </summary>
        float ScreenSize { get; set; }

        /// <summary>
        /// Radius to center of curvature, 0 to keep it flat
        /// </summary>
        float CurveRadius { get; set; }

        void StartCapture();
        void StopCapture();
        void CaptureFrame(int textureHandle);
    }
}
