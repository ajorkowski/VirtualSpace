using MediaFoundation;
using MediaFoundation.Misc;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public interface IMkvTrack : IMFMediaStream
    {
        bool IsSelected { get; set; }

        TrackEntry Metadata { get; }
        IMFStreamDescriptor Descriptor { get; }
    }
}
