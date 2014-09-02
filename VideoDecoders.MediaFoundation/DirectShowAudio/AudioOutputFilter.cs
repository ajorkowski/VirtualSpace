using DirectShow;
using DirectShow.BaseClasses;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MF = MediaFoundation;

namespace VideoDecoders.MediaFoundation.DirectShowAudio
{
    public class AudioOutputFilter : BaseRendererFilter
    {
        private readonly MF.IMFMediaType _mediaType;
        private readonly ConcurrentQueue<MF.IMFSample> _outputQueue;

        public AudioOutputFilter(MF.IMFMediaType mediaType, ConcurrentQueue<MF.IMFSample> outputQueue) 
            : base("Audio Output Filter")
        {
            _mediaType = mediaType;
            _outputQueue = outputQueue;
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

        public override int GetState(int dwMilliSecsTimeout, out FilterState filtState)
        {
            var hr = base.GetState(dwMilliSecsTimeout, out filtState);
            return NOERROR;
        }

        public override int DoRenderSample(ref IMediaSampleImpl pMediaSample)
        {
            MF.IMFSample sample;
            var hr = MF.MFExtern.MFCreateSample(out sample);
            if (hr != 0) { return hr; }

            MF.IMFMediaBuffer buffer;
            hr = MF.MFExtern.MFCreateMemoryBuffer(pMediaSample.GetSize(), out buffer);
            if (hr != 0) { return hr; }

            IntPtr inPtr;
            hr = pMediaSample.GetPointer(out inPtr);
            if (hr != S_OK) { return hr; }

            int length;
            int maxLength;
            IntPtr bufferPtr;
            hr = buffer.Lock(out bufferPtr, out maxLength, out length);
            if (hr != S_OK) { return hr; }

            CopyMemory(bufferPtr, inPtr, pMediaSample.GetActualDataLength());

            hr = buffer.Unlock();
            if (hr != S_OK) { return hr; }

            hr = buffer.SetCurrentLength(pMediaSample.GetActualDataLength());
            if (hr != S_OK) { return hr; }

            if(pMediaSample.IsSyncPoint() == 1)
            {
                hr = sample.SetUINT32(MF.MFAttributesClsid.MFSampleExtension_CleanPoint, 1);
                if (hr != S_OK) { return hr; }
            }

            long timeStart;
            long timeEnd;
            hr = pMediaSample.GetTime(out timeStart, out timeEnd);
            if (hr != S_OK) { return hr; }

            hr = sample.SetSampleDuration(timeEnd - timeStart);
            if (hr != S_OK) { return hr; }

            hr = sample.SetSampleTime(timeStart);
            if (hr != S_OK) { return hr; }

            hr = sample.AddBuffer(buffer);
            if (hr != 0) { return hr; }

            _outputQueue.Enqueue(sample);

            return NOERROR;
        }

        [DllImport("kernel32.dll", EntryPoint = "CopyMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, int count);
    }
}
