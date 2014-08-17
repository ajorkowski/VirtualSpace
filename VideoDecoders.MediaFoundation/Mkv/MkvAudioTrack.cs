using MediaFoundation;
using System;
using System.IO;
using System.Text;
using System.Linq;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public class MkvAudioTrack : MkvBaseTrack
    {
        private readonly byte[] _sharedBuffer;

        public MkvAudioTrack(TrackEntry entry, MkvMediaSource mediaSource)
            : base(entry, mediaSource)
        {
            _sharedBuffer = new byte[2048];

            if (entry.TrackType != TrackType.Audio)
            {
                throw new InvalidOperationException("Only audio tracks should be here");
            }
        }

        protected override IMFMediaType CreateMediaType(TrackEntry entry)
        {
            Guid subtype;
            switch (entry.CodecID)
            {
                case "A_MPEG/L2":
                    // http://msdn.microsoft.com/en-us/library/windows/desktop/dd390676(v=vs.85).aspx
                    subtype = MFMediaType.MPEG;
                    break;
                case "A_DTS":
                    subtype = MediaSubTypes.MEDIASUBTYPE_DTS;
                    break;
                case "A_AC3":
                    subtype = MediaSubTypes.MEDIASUBTYPE_DOLBY_AC3;
                    break;
                default:
                    return null;
            }

            IMFMediaType type;
            TestSuccess("Could not create media type", MFExtern.MFCreateMediaType(out type));

            TestSuccess("Could not set audio type", type.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, MFMediaType.Audio));

            TestSuccess("Could not set audio subtype", type.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, subtype));

            TestSuccess("Could not set number of channels", type.SetUINT32(MFAttributesClsid.MF_MT_AUDIO_NUM_CHANNELS, (int)entry.Audio.Channels));
            TestSuccess("Could not set the sample rate", type.SetUINT32(MFAttributesClsid.MF_MT_AUDIO_SAMPLES_PER_SECOND, (int)entry.Audio.SamplingFrequency));

            // TODO: Channel Mask?
            //TestSuccess("Could not set channel mask", type.SetUINT32(MFAttributesClsid.MF_MT_AUDIO_CHANNEL_MASK, (int)entry.Audio.SamplingFrequency));

            return type;
        }

        public override IMFMediaBuffer CreateBufferFromBlock(int blockDataSize, Func<byte[], int, int, int> readBlockDataFunc)
        {
            IMFMediaBuffer buffer;
            TestSuccess("Could not create media buffer", MFExtern.MFCreateMemoryBuffer(blockDataSize, out buffer));

            int currentLength;
            IntPtr bufferPtr;
            TestSuccess("Could not lock media buffer", buffer.Lock(out bufferPtr, out blockDataSize, out currentLength));

            unsafe
            {
                using (var buffStream = new UnmanagedMemoryStream((byte*)bufferPtr.ToPointer(), blockDataSize, blockDataSize, FileAccess.Write))
                {
                    while (blockDataSize > 0)
                    {
                        int r = readBlockDataFunc(_sharedBuffer, 0, Math.Min(blockDataSize, 2048));
                        if (r < 0)
                        {
                            throw new EndOfStreamException();
                        }

                        //Console.WriteLine(string.Join(" ", _sharedBuffer.Take(r).Select(b => byteToBitsString(b))));

                        buffStream.Write(_sharedBuffer, 0, r);
                        blockDataSize -= r;
                        currentLength += r;
                    }
                }
            }

            TestSuccess("Could not set media buffer length", buffer.SetCurrentLength(currentLength));
            TestSuccess("Could not unlock media buffer", buffer.Unlock());

            return buffer;
        }
    }
}
