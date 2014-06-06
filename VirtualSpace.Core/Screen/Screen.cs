namespace VirtualSpace.Core.Screen
{
    public sealed class Screen : IScreen
    {
        public Screen()
        {
            ScreenSize = 1.0668f; // 42in screen
            CurveRadius = 0;
        }

        public float ScreenSize { get; set; }
        public float CurveRadius { get; set; }
    }
}
