using MediaFoundation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public class MkvVideoTrack : IMkvTrack
    {
        private readonly IMFStreamDescriptor _descriptor;
        private readonly TrackEntry _entry;

        public IMFStreamDescriptor Descriptor { get { return _descriptor; } }
        public TrackEntry Metadata { get { return _entry; } }

        public MkvVideoTrack(TrackEntry entry)
        {
            if (entry.TrackType != TrackType.Video)
            {
                throw new InvalidOperationException("Only video tracks should be here");
            }

            _entry = entry;
            IMFMediaType type;
            TestSuccess("Could not create media type", MFExtern.MFCreateMediaType(out type));

            TestSuccess("Could not set video type", type.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, MFMediaType.Video));

            Guid subtype;
            switch (entry.CodecID)
            {
                case "V_MPEG4/ISO/AVC":
                    subtype = MFMediaType.H264;
                    break;
                default:
                    throw new InvalidOperationException("Unknown codec type");
            }

            TestSuccess("Could not set video subtype", type.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, subtype));

            TestSuccess("Could not set video size", type.SetUINT64(MFAttributesClsid.MF_MT_FRAME_SIZE, MakeLong((int)entry.Video.DisplayWidth, (int)entry.Video.DisplayHeight)));
            TestSuccess("Could not set pixel aspect ratio", type.SetUINT64(MFAttributesClsid.MF_MT_PIXEL_ASPECT_RATIO, MakeLong(1, 1)));

            TestSuccess("Could not create stream descriptor", MFExtern.MFCreateStreamDescriptor((int)entry.TrackNumber, 1, new IMFMediaType[] { type }, out _descriptor));

            IMFMediaTypeHandler typeHandler;
            _descriptor.GetMediaTypeHandler(out typeHandler);

            typeHandler.SetCurrentMediaType(type);
        }

        private void TestSuccess(string message, int hResult)
        {
            if (hResult < 0)
            {
                throw new COMException(message, hResult);
            }
        }

        public long MakeLong(int left, int right)
        {
            //implicit conversion of left to a long
            long res = left;

            //shift the bits creating an empty space on the right
            // ex: 0x0000CFFF becomes 0xCFFF0000
            res = (res << 32);

            //combine the bits on the right with the previous value
            // ex: 0xCFFF0000 | 0x0000ABCD becomes 0xCFFFABCD
            res = res | (long)(uint)right; //uint first to prevent loss of signed bit

            //return the combined result
            return res;
        }
    }
}
