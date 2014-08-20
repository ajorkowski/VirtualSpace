using DirectShow;
using DirectShow.BaseClasses;
using System;
using System.Runtime.InteropServices;
using MF = MediaFoundation;

namespace VideoDecoders.MediaFoundation.DirectShowAudio
{
    public class AudioSourceFilter : BaseSourceFilter
    {
        private readonly AMMediaType _mediaType;

        private long _lastSampleTime = 0;

        public AudioSourceFilter(MF.IMFMediaType mediaType)
            : base("Audio Source Filter")
        {
            Guid subType;
            mediaType.GetGUID(MF.MFAttributesClsid.MF_MT_SUBTYPE, out subType);
            if (subType != MediaSubTypes.MEDIASUBTYPE_DTS)
            {
                throw new NotImplementedException();
            }

            MF.Misc.WaveFormatEx format;
            int formatSize;
            var hr = MF.MFExtern.MFCreateWaveFormatExFromMFMediaType(mediaType, out format, out formatSize, MF.MFWaveFormatExConvertFlags.Normal);
            if (hr != S_OK) { throw new NotSupportedException(); }

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

            _mediaType = new AMMediaType();
            _mediaType.majorType = MediaType.Audio;
            _mediaType.subType = MediaFoundation.MediaSubTypes.MEDIASUBTYPE_DTS;
            _mediaType.formatType = FormatType.WaveEx;
            AMMediaType.SetFormat(ref _mediaType, ref pwfx);
        }

        //~AudioSourceFilter()
        //{
        //    if (m_pBitmap != null)
        //    {
        //        m_pBitmap.Dispose();
        //        m_pBitmap = null;
        //    }
        //}

        protected override int OnInitializePins()
        {
            AddPin(new AudioSourceStream("Output", this));
            return NOERROR;
        }

        public override int Pause()
        {
            if (m_State == FilterState.Stopped)
            {
                _lastSampleTime = 0;
            }
            return base.Pause();
        }

        public int GetMediaType(ref AMMediaType mediaType)
        {
            mediaType.Set(_mediaType);
            return NOERROR;
        }

        public int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            AllocatorProperties actual = new AllocatorProperties();

            var wfx = (WaveFormatEx)_mediaType;
            if (wfx == null) return VFW_E_INVALIDMEDIATYPE;

            prop.cbBuffer = wfx.nAvgBytesPerSec;
            if (prop.cbBuffer < wfx.nBlockAlign * wfx.nSamplesPerSec)
            {
                prop.cbBuffer = wfx.nBlockAlign * wfx.nSamplesPerSec;
            }
            if(prop.cbBuffer == 0)
            {
                prop.cbBuffer = 24 * wfx.nSamplesPerSec;
            }
            prop.cbAlign = wfx.nBlockAlign;
            prop.cBuffers = 3;
            int hr = pAlloc.SetProperties(prop, actual);
            return hr;
        }

        public int FillBuffer(ref IMediaSampleImpl _sample)
        {
            throw new NotImplementedException();
            //BitmapInfoHeader _bmi = (BitmapInfoHeader)Pins[0].CurrentMediaType;
            
            //IntPtr _ptr;
            //_sample.GetPointer(out _ptr);
            //Bitmap _bmp = new Bitmap(_bmi.Width, _bmi.Height, _bmi.Width * 4, PixelFormat.Format32bppRgb, _ptr);
            //Graphics _graphics = Graphics.FromImage(_bmp);

            //_graphics.DrawImage(m_pBitmap, new Rectangle(0, 0, _bmp.Width, _bmp.Height), 0, 0, m_pBitmap.Width, m_pBitmap.Height,GraphicsUnit.Pixel);
            //_graphics.Dispose();
            //_bmp.Dispose();
            //_sample.SetActualDataLength(_bmi.ImageSize);
            //_sample.SetSyncPoint(true);
            //long _stop = m_lLastSampleTime + m_nAvgTimePerFrame;
            //_sample.SetTime((DsLong)m_lLastSampleTime, (DsLong)_stop);
            //m_lLastSampleTime = _stop;
            //return NOERROR;
        }
    }
}
