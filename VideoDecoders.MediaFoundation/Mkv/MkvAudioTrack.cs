using MediaFoundation;
using System;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public class MkvAudioTrack : MkvBaseTrack
    {
        public MkvAudioTrack(TrackEntry entry, MkvMediaSource mediaSource)
            : base(entry, mediaSource)
        {
            if (entry.TrackType != TrackType.Audio)
            {
                throw new InvalidOperationException("Only audio tracks should be here");
            }
        }

        protected override IMFMediaType CreateMediaType(TrackEntry entry)
        {
            IMFMediaType type;
            TestSuccess("Could not create media type", MFExtern.MFCreateMediaType(out type));

            TestSuccess("Could not set audio type", type.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, MFMediaType.Audio));

            Guid subtype;
            switch (entry.CodecID)
            {
                case "A_MPEG/L2":
                    subtype = MFMediaType.MPEG;
                    break;
                default:
                    throw new InvalidOperationException("Unknown codec type");
            }

            TestSuccess("Could not set audio subtype", type.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, subtype));

            TestSuccess("Could not set number of channels", type.SetUINT32(MFAttributesClsid.MF_MT_AUDIO_NUM_CHANNELS, (int)entry.Audio.Channels));
            TestSuccess("Could not set the sample rate", type.SetUINT32(MFAttributesClsid.MF_MT_AUDIO_SAMPLES_PER_SECOND, (int)entry.Audio.SamplingFrequency));

            // TODO: Channel Mask?
            //TestSuccess("Could not set channel mask", type.SetUINT32(MFAttributesClsid.MF_MT_AUDIO_CHANNEL_MASK, (int)entry.Audio.SamplingFrequency));

            return type;
        }

        public override IMFMediaBuffer CreateBufferFromBlock(int blockDataSize, Func<byte[], int, int, int> readBlockDataFunc)
        {
            throw new NotImplementedException();
        }
    }
}
