using SharpDX.Toolkit;
using System;
using VirtualSpace.Core.Renderer.Screen;
using VirtualSpace.Platform.Windows.Rendering.Providers;

namespace VirtualSpace.Platform.Windows.Rendering.Screen
{
    internal class ScreenManager : GameSystem, IScreenManager
    {
        private readonly ICameraProvider _camera;

        public ScreenManager(Game game, ICameraProvider camera)
            : base(game)
        {
            _camera = camera;
        }

        public IScreen CreateScreen(Core.Renderer.Screen.IScreenSource screenSource, float screenSize, float curveRadius = 0)
        {
            var internalSource = screenSource as IScreenSource;
            if(internalSource == null)
            {
                throw new ArgumentException("Not a correctly implemented screen source", "screenSource");
            }

            return new ScreenRenderer(Game, _camera, internalSource, screenSize, curveRadius);
        }
    }
}
