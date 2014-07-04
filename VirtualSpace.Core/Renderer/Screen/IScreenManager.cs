namespace VirtualSpace.Core.Renderer.Screen
{
    public interface IScreenManager
    {
        IScreen CreateScreen(IScreenSource screenSource, float screenSize, float curveRadius = 0);
    }
}
