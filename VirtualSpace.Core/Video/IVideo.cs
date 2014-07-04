using System;
using VirtualSpace.Core.Renderer.Screen;

namespace VirtualSpace.Core.Video
{
    public interface IVideo : IScreenSource
    {
        bool CanSeek { get; }
        TimeSpan Duration { get; }
        VideoState State { get; }

        void Play();
        void Stop();
    }
}
