using DirectShow;
using DirectShow.BaseClasses;

namespace VideoDecoders.MediaFoundation.DirectShowAudio
{
    public class AudioSourceStream : SourceStream
    {
        public AudioSourceStream(string _name, BaseSourceFilter _filter)
            : base(_name, _filter)
        {
        }

        public override int GetMediaType(ref AMMediaType pMediaType)
        {
            return (m_Filter as AudioSourceFilter).GetMediaType(ref pMediaType);
        }

        public override int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            if (!IsConnected) return VFW_E_NOT_CONNECTED;
            return (m_Filter as AudioSourceFilter).DecideBufferSize(ref pAlloc, ref prop);
        }

        public override int FillBuffer(ref IMediaSampleImpl pSample)
        {
            return (m_Filter as AudioSourceFilter).FillBuffer(ref pSample);
        }
    }
}
