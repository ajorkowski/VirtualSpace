using System;
using System.Collections.Generic;
using VirtualSpace.Core.Renderer.Screen;

namespace VirtualSpace.Core.Video
{
    public interface IVideo : IScreenSource
    {
        bool CanSeek { get; }
        TimeSpan Duration { get; }
        VideoState State { get; }

        IEnumerable<StreamMetadata> Metadata { get; }

        void Play();
        void Stop();
    }
}
