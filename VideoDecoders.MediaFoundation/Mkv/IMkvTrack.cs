using MediaFoundation;
using System;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public interface IMkvTrack : IMFMediaStream, IDisposable
    {
        bool IsSelected { get; set; }
        bool IsEOS { get; set; }

        TrackEntry Metadata { get; }
        IMFStreamDescriptor Descriptor { get; }

        void ProcessSample();
    }
}
