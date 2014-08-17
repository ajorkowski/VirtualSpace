using DirectShow;
using DirectShow.BaseClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MF = MediaFoundation;

namespace VideoDecoders.MediaFoundation.DirectShowAudio
{
    public class AudioOutputFilter : BaseRendererFilter
    {
        private readonly MF.IMFMediaType _mediaType;

        public AudioOutputFilter(MF.IMFMediaType mediaType) 
            : base("Audio Output Filter")
        {
            _mediaType = mediaType;
        }

        protected override int OnInitializePins()
        {
            AddPin(new AudioRenderStream("Input", this));
            return NOERROR;
        }

        public int GetMediaType(ref AMMediaType mediaType)
        {
            MF.Misc.WaveFormatEx format;
            int formatSize;
            var hr = MF.MFExtern.MFCreateWaveFormatExFromMFMediaType(_mediaType, out format, out formatSize, MF.MFWaveFormatExConvertFlags.Normal);
            if (hr != S_OK) { return hr; }

            var pwfx = new WaveFormatEx
            {
                cbSize = (ushort)format.cbSize,
                nAvgBytesPerSec = format.nAvgBytesPerSec,
                nBlockAlign = (ushort)format.nBlockAlign,
                nChannels = (ushort)format.nChannels,
                nSamplesPerSec = format.nSamplesPerSec,
                wBitsPerSample = (ushort)format.wBitsPerSample,
                wFormatTag = (ushort)format.wFormatTag
            };

            Guid subType;
            hr = _mediaType.GetGUID(MF.MFAttributesClsid.MF_MT_SUBTYPE, out subType);
            if (hr != S_OK) { return hr; }

            mediaType.majorType = MediaType.Audio;
            mediaType.subType = subType;
            mediaType.formatType = FormatType.WaveEx;
            AMMediaType.SetFormat(ref mediaType, ref pwfx);

            return NOERROR;
        }

        public override int CheckMediaType(AMMediaType pmt)
        {
            if (pmt.IsValid() && pmt.formatPtr != IntPtr.Zero)
            {
                return NOERROR;
            }
            return VFW_E_TYPE_NOT_ACCEPTED;
        }

        public override int DoRenderSample(ref IMediaSampleImpl pMediaSample)
        {
            return NOERROR;
        }
    }
}
