namespace VirtualSpace.Core.Renderer.Screen
{
    public interface IScreen
    {
        int Width { get; }
        int Height { get; }

        /// <summary>
        /// Diagonal size of screen
        /// </summary>
        float ScreenSize { get; set; }

        /// <summary>
        /// Radius to center of curvature, 0 to keep it flat
        /// </summary>
        float CurveRadius { get; set; }
    }
}
