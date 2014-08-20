using DirectShow;
using DirectShow.BaseClasses;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using MF = MediaFoundation;

namespace VideoDecoders.MediaFoundation.DirectShowAudio
{
    public class AudioSourceFilter : BaseSourceFilter
    {
        private readonly AMMediaType _mediaType;
        private readonly ConcurrentQueue<MF.IMFSample> _samples;

        private bool _isDone;
        private long _lastSampleTime;

        public AudioSourceFilter(MF.IMFMediaType mediaType)
            : base("Audio Source Filter")
        {
            _samples = new ConcurrentQueue<MF.IMFSample>();

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

        public void PushData(MF.IMFSample sample)
        {
            if (sample == null)
            {
                _isDone = true;
            }
            else
            {
                _samples.Enqueue(sample);
                _isDone = false;
            }
        }

        public override int Pause()
        {
            _lastSampleTime = 0;
            return base.Pause();
        }

        protected override int OnInitializePins()
        {
            AddPin(new AudioSourceStream("Output", this));
            return NOERROR;
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
                prop.cbBuffer = 4 * 16 * wfx.nSamplesPerSec;
            }
            prop.cbAlign = wfx.nBlockAlign == 0 ? 4 : wfx.nBlockAlign;
            prop.cBuffers = 3;
            int hr = pAlloc.SetProperties(prop, actual);
            return hr;
        }

        public int FillBuffer(ref IMediaSampleImpl outSample)
        {
            MF.IMFSample sample;
            try
            {
                while (true)
                {
                    if (_samples.TryDequeue(out sample))
                    {
                        MF.IMFMediaBuffer buffer;
                        var hr = sample.ConvertToContiguousBuffer(out buffer);
                        if (hr != S_OK) { return hr; }

                        IntPtr outPtr;
                        hr = outSample.GetPointer(out outPtr);
                        if (hr != S_OK) { return hr; }

                        int length;
                        int maxLength;
                        IntPtr bufferPtr;
                        hr = buffer.Lock(out bufferPtr, out maxLength, out length);
                        if (hr != S_OK) { return hr; }

                        if (length > outSample.GetSize()) 
                        {
                            buffer.Unlock();
                            return VFW_E_RUNTIME_ERROR;
                        }

                        CopyMemory(outPtr, bufferPtr, length);

                        hr = buffer.Unlock();
                        if (hr != S_OK) { return hr; }

                        hr = outSample.SetActualDataLength(length);
                        if (hr != S_OK) { return hr; }

                        hr = outSample.SetSyncPoint(true);
                        if (hr != S_OK) { return hr; }

                        long stop = 

                        Marshal.ReleaseComObject(buffer);
                        Marshal.ReleaseComObject(sample);
                    }
                    else
                    {
                        if (_isDone) { return S_FALSE; }
                        Thread.Sleep(5); // Wait for more data!
                    }
                }
            }
            catch(Exception)
            {
                return VFW_E_RUNTIME_ERROR;
            }
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

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, int count);
    }
}
