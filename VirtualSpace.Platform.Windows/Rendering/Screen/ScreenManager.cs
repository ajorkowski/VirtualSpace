using SharpDX.Toolkit;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Platform.Windows.Rendering.Providers;

namespace VirtualSpace.Platform.Windows.Rendering.Screen
{
    internal class ScreenManager : GameSystem, IScreenManager
    {
        private readonly ScreenRenderer _desktop;

        public ScreenManager(Game game, ICameraProvider camera)
            : base(game)
        {
            Enabled = true;
            Visible = true;

            if (Screen.ScreenRendererDX11.IsSupported)
            {
                _desktop = ToDispose(new Screen.ScreenRendererDX11(game, camera));
            }
            else
            {
                _desktop = ToDispose(new Screen.ScreenRendererGdi(game, camera));
            }

            game.GameSystems.Add(_desktop);
        }

        public IScreen Desktop { get { return _desktop; } }
    }
}
