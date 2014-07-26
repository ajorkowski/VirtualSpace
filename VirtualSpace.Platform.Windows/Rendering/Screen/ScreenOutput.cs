using SharpDX.Direct3D11;
using SharpDX.XAudio2;

namespace VirtualSpace.Platform.Windows.Rendering.Screen
{
    internal sealed class ScreenOutput
    {
        public Texture2D Texture { get; set; }
        public SourceVoice Audio { get; set; } 
    }
}
