using DirectShow;
using DirectShow.BaseClasses;
using System;
using MF = MediaFoundation;

namespace VideoDecoders.MediaFoundation.DirectShowAudio
{
    public class AudioSourceFilter : BaseSourceFilter
    {
        private readonly MF.IMFMediaType _mediaType;

        private long _lastSampleTime = 0;

        public AudioSourceFilter(MF.IMFMediaType mediaType)
            : base("Audio Source Filter")
        {
            _mediaType = mediaType;
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
            Guid subType;
            _mediaType.GetGUID(MF.MFAttributesClsid.MF_MT_SUBTYPE, out subType);
            if (subType != MediaSubTypes.MEDIASUBTYPE_DTS)
            {
                throw new NotImplementedException();
            }

            MF.Misc.WaveFormatEx format;
            int formatSize;
            var hr = MF.MFExtern.MFCreateWaveFormatExFromMFMediaType(_mediaType, out format, out formatSize, MF.MFWaveFormatExConvertFlags.Normal);
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

            mediaType.majorType = MediaType.Audio;
            mediaType.subType = MediaFoundation.MediaSubTypes.MEDIASUBTYPE_DTS;
            mediaType.formatType = FormatType.WaveEx;
            AMMediaType.SetFormat(ref mediaType, ref pwfx);

            return NOERROR;
        }

        public int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            throw new NotImplementedException();
            //AllocatorProperties _actual = new AllocatorProperties();

            //BitmapInfoHeader _bmi = (BitmapInfoHeader)Pins[0].CurrentMediaType;
            //prop.cbBuffer = _bmi.GetBitmapSize();
            //if (prop.cbBuffer < _bmi.ImageSize)
            //{
            //    prop.cbBuffer = _bmi.ImageSize;
            //}
            //prop.cBuffers = 1;

            //int hr = pAlloc.SetProperties(prop, _actual);
            //return hr;
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
