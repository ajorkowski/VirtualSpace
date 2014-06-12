using SharpDX.Toolkit;
using VirtualSpace.Core.Renderer.Screen;

namespace VirtualSpace.Platform.Windows.Rendering.Screen
{
    public class ScreenManager : GameSystem, IScreenManager
    {
        private readonly ScreenRenderer _desktop;

        public ScreenManager(Game game)
            : base(game)
        {
            if (Screen.ScreenRendererDX11.IsSupported)
            {
                _desktop = new Screen.ScreenRendererDX11(Game);
            }
            else
            {
                _desktop = new Screen.ScreenRendererGdi(Game);
            }

            game.GameSystems.Add(_desktop);
        }

        public IScreen Desktop { get { return _desktop; } }
    }
}
