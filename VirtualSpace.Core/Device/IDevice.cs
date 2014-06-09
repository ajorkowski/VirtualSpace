using OpenTK;
using OpenTK.Input;
using System;

namespace VirtualSpace.Core.Device
{
    public interface IDevice : IDisposable
    {
        void Run(IEnvironment environment);

        int Width { get; }
        int Height { get; }

        bool IsKeyDown(Key key);
        event EventHandler<KeyboardKeyEventArgs> KeyDown;
        event EventHandler<KeyPressEventArgs> KeyPress;
        event EventHandler<KeyboardKeyEventArgs> KeyUp;
    }
}
