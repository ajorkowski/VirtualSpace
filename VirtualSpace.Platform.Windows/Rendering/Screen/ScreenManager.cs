using SharpDX.Toolkit;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Platform.Windows.Rendering.Providers;

namespace VirtualSpace.Platform.Windows.Rendering.Screen
{
    internal class ScreenManager : SubGameSystem, IScreenManager
    {
        private readonly ScreenRenderer _desktop;

        public ScreenManager(GameSystem game, ICameraProvider camera)
            : base(game)
        {
            Enabled = true;
            Visible = true;

            if (Screen.ScreenRendererDX11.IsSupported)
            {
                _desktop = new Screen.ScreenRendererDX11(this, camera);
            }
            else
            {
                _desktop = new Screen.ScreenRendererGdi(this, camera);
            }
        }

        public IScreen Desktop { get { return _desktop; } }
    }
}
