using MediaFoundation;
using System;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public interface IMkvTrack : IMFMediaStream, IDisposable
    {
        bool IsSelected { get; set; }
        bool IsEOS { get; }

        TrackEntry Metadata { get; }
        IMFStreamDescriptor Descriptor { get; }

        bool ProcessSample();

        IMFMediaBuffer CreateBufferFromBlock(int blockDataSize, Func<byte[], int, int, int> readBlockDataFunc, ref MkvBlockHeader header);
    }
}
