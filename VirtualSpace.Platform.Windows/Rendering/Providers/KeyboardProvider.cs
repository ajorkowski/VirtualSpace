using SharpDX.Toolkit;
using System.Collections.Generic;
using VirtualSpace.Core.Device;

namespace VirtualSpace.Platform.Windows.Rendering.Providers
{
    internal sealed class KeyboardProvider : SharpDX.Toolkit.Input.KeyboardManager, IInput
    {
        private readonly Dictionary<Keys, SharpDX.Toolkit.Input.ButtonState> _cache;

        public KeyboardProvider(Game game)
            : base(game)
        {
            _cache = new Dictionary<Keys, SharpDX.Toolkit.Input.ButtonState>();

            DrawOrder = UpdateOrder = RenderingOrder.Provider;
        }

        public override void Update(GameTime gameTime)
        {
            _cache.Clear();

            base.Update(gameTime);
        }

        public ButtonState GetState(Keys key)
        {
            return (ButtonState)GetBtnState(key).Flags;
        }

        public bool IsPressed(Keys key)
        {
            return GetBtnState(key).Pressed;
        }

        public bool IsDown(Keys key)
        {
            return GetBtnState(key).Down;
        }

        public bool IsReleased(Keys key)
        {
            return GetBtnState(key).Released;
        }

        private SharpDX.Toolkit.Input.ButtonState GetBtnState(Keys key)
        {
            SharpDX.Toolkit.Input.ButtonState result;
            if(!_cache.TryGetValue(key, out result))
            {
                var state = GetState();
                var btnState = state[(SharpDX.Toolkit.Input.Keys)key];
                result = _cache[key] = btnState;
            }

            return result;
        }
    }
}
