﻿namespace VirtualSpace.Core.Screen
{
    public interface IScreen
    {
        /// <summary>
        /// Diagonal size of screen
        /// </summary>
        public float ScreenSize { get; set; }

        /// <summary>
        /// Radius to center of curvature, 0 to keep it flat
        /// </summary>
        public float CurveRadius { get; set; }
    }
}
