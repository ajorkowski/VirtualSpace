using MediaFoundation;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public interface IMkvTrack
    {
        TrackEntry Metadata { get; }
        IMFStreamDescriptor Descriptor { get; }

        bool HasStarted { get; set; }
    }
}
