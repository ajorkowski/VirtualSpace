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
        
        private readonly ManualResetEvent _intputSync;
        private readonly ManualResetEvent _outputSync;

        private MF.IMFSample _sample;
        private bool _isDone;
        private long _lastSampleTime;

        public AudioSourceFilter(MF.IMFMediaType mediaType)
            : base("Audio Source Filter")
        {
            _intputSync = new ManualResetEvent(true);
            _outputSync = new ManualResetEvent(false);

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

        public void PushSample(MF.IMFSample sample)
        {
            while(_sample != null)
            {
                _intputSync.Reset();
                _intputSync.WaitOne(500);
            }

            if (sample == null)
            {
                _isDone = true;
            }
            else
            {
                _isDone = false;
                _sample = sample;
            }

            _outputSync.Set();
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
            try
            {
                while (true)
                {
                    if(_sample != null)
                    {
                        MF.IMFMediaBuffer buffer;
                        var hr = _sample.ConvertToContiguousBuffer(out buffer);
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

                        int hasSyncPoint;
                        _sample.GetUINT32(MF.MFAttributesClsid.MFSampleExtension_CleanPoint, out hasSyncPoint);
                        hr = outSample.SetSyncPoint(hasSyncPoint == 1);
                        if (hr != S_OK) { return hr; }

                        long duration;
                        _sample.GetSampleDuration(out duration);

                        long stop = _lastSampleTime + duration;
                        hr = outSample.SetTime((DsLong)_lastSampleTime, (DsLong)stop);
                        if (hr != S_OK) { return hr; }
                        _lastSampleTime = stop;

                        Marshal.ReleaseComObject(buffer);
                        Marshal.ReleaseComObject(_sample);
                        _sample = null;

                        _intputSync.Set();
                        return NOERROR;
                    }
                    else
                    {
                        if (_isDone) { return S_FALSE; }
                        _outputSync.Reset();
                        _outputSync.WaitOne(500);
                    }
                }
            }
            catch(Exception)
            {
                return VFW_E_RUNTIME_ERROR;
            }
        }

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, int count);
    }
}
