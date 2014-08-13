using DirectShow;
using DirectShow.BaseClasses;
using DirectShow.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MF = MediaFoundation;

namespace VideoDecoders.MediaFoundation.DirectShowAudio
{
    public class DTSSourceParser : FileParser
    {
        private AMMediaType _mediaType;

        public DTSSourceParser(MF.IMFMediaType mediaType)
            : base(false)
        {
            MF.Misc.WaveFormatEx format;
            int formatSize;
            var hr = MF.MFExtern.MFCreateWaveFormatExFromMFMediaType(mediaType, out format, out formatSize, MF.MFWaveFormatExConvertFlags.Normal);
            if(hr != S_OK) { throw new NotSupportedException(); }

            //WaveFormatEx pwfx = (WaveFormatEx)m_Stream.ReadValue<WaveFormatEx>((int)_header.dwFormatLength);
            //if (pwfx == null) return E_UNEXPECTED;
            //if (pwfx.nBlockAlign == 0)
            //{
            //    pwfx.nBlockAlign = (ushort)(pwfx.nChannels * pwfx.wBitsPerSample / 8);
            //}
            //if (pwfx.nAvgBytesPerSec == 0)
            //{
            //    pwfx.nAvgBytesPerSec = pwfx.nSamplesPerSec * pwfx.nBlockAlign;
            //}

            _mediaType = new AMMediaType();
            _mediaType.majorType = MediaType.Audio;
            _mediaType.subType = MediaFoundation.MediaSubTypes.MEDIASUBTYPE_DTS;
            //_mediaType.SetFormat(pwfx);
        }

        public long DataOffset
        {
            get { return 0; }
        }

        protected override HRESULT CheckFile()
        {
            return NOERROR;
        }

        protected override HRESULT LoadTracks()
        {
            m_Stream.Seek(0);
            m_Tracks.Add(new WaveTrack(this, mt));
            if (pwfx.nAvgBytesPerSec != 0)
            {
                m_rtDuration = (UNITS * m_Stream.TotalSize) / pwfx.nAvgBytesPerSec;
            }
            return NOERROR;
        }
    }
}
