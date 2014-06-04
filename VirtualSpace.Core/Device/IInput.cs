using System;

namespace VirtualSpace.Core.Device
{
    public interface IInput
    {
        ButtonState GetState(Keys key);
        bool IsPressed(Keys key);
        bool IsDown(Keys key);
        bool IsReleased(Keys key);
    }
}
