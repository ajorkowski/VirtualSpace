using SharpDX.Toolkit;

namespace VirtualSpace.Platform.Windows.Rendering.Screen
{
    internal interface IScreenSource
    {
        // The output is the item that creates the texture
        SharpDX.Direct3D11.Texture2D GetOutputRenderTexture(SharpDX.Direct3D11.Device device);

        void Update(GameTime gameTime);
    }
}
