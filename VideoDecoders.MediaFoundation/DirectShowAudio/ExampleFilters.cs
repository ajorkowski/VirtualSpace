/*using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using DirectShow;
using DirectShow.BaseClasses;
using Sonic;
using System.IO;
using System.Threading;
using System.Net.Sockets;
using System.Net;

namespace ExampleFilters
{
    #region Null In Place Filter

    [ComVisible(true)]
    [Guid("eeb3eef7-0592-491b-b7d4-8c65763c79c6")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AboutForm))]
    public class NullInPlaceFilter : TransInPlaceFilter
    {
        #region Constructor

        public NullInPlaceFilter()
            : base("CSharp Null InPlace Filter")
        {

        }

        #endregion

        #region Overridden Methods

        public override int CheckInputType(AMMediaType pmt)
        {
            return NOERROR;
        }

        public override int Transform(ref IMediaSampleImpl pSample)
        {
            return S_OK;
        }

        #endregion
    }

    #endregion

    #region Null Transform Filter

    [ComVisible(true)]
    [Guid("20573990-D7C8-476f-BE02-0959F63C4240")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AboutForm))]
    public class NullTransformFilter : TransformFilter
    {
        #region Constructor

        public NullTransformFilter()
            : base("CSharp Null Transform Filter")
        {

        }

        #endregion

        #region Overridden Methods

        public override int CheckInputType(AMMediaType pmt)
        {
            if (pmt.IsValid() && pmt.formatPtr != IntPtr.Zero)
            {
                return NOERROR;
            }
            return VFW_E_TYPE_NOT_ACCEPTED;
        }

        public override int Transform(ref IMediaSampleImpl pIn, ref IMediaSampleImpl pOut)
        {
            if (pIn.IsPreroll() == S_OK) return NOERROR;
            int lDataLength = pIn.GetActualDataLength();
            pOut.SetActualDataLength(lDataLength);
            // Copy the sample data
            {
                IntPtr pSourceBuffer, pDestBuffer;
                int lSourceSize = pIn.GetSize();
                int lDestSize = pOut.GetSize();

                ASSERT(lDestSize >= lSourceSize && lDestSize >= lDataLength);

                pIn.GetPointer(out pSourceBuffer);
                pOut.GetPointer(out pDestBuffer);
                ASSERT(lDestSize == 0 || pSourceBuffer != IntPtr.Zero && pDestBuffer != IntPtr.Zero);

                API.CopyMemory(pDestBuffer, pSourceBuffer, lDataLength);
            }
            pOut.SetSyncPoint(true);
            long _start,_stop;
            if (S_OK == pIn.GetTime(out _start, out _stop))
            {
                pOut.SetTime((DsLong)_start, (DsLong)_stop);
            }
            return NOERROR;
        }

        public override int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            if (!Output.IsConnected) return VFW_E_NOT_CONNECTED;
            AllocatorProperties _actual = new AllocatorProperties();
            if (Output.CurrentMediaType.majorType == MediaType.Video)
            {
                BitmapInfoHeader _bmi = (BitmapInfoHeader)Output.CurrentMediaType;
                if (_bmi == null) return VFW_E_INVALIDMEDIATYPE;
                prop.cbBuffer = _bmi.GetBitmapSize();
                if (prop.cbBuffer < _bmi.ImageSize)
                {
                    prop.cbBuffer = _bmi.ImageSize;
                }
            }
            if (Output.CurrentMediaType.majorType == MediaType.Audio)
            {
                WaveFormatEx _wfx = (WaveFormatEx)Output.CurrentMediaType;
                if (_wfx == null) return VFW_E_INVALIDMEDIATYPE;
                prop.cbBuffer = _wfx.nAvgBytesPerSec;
                if (prop.cbBuffer < _wfx.nBlockAlign * _wfx.nSamplesPerSec)
                {
                    prop.cbBuffer = _wfx.nBlockAlign * _wfx.nSamplesPerSec;
                }
            }
            prop.cBuffers = 1;
            int hr = pAlloc.SetProperties(prop, _actual);
            return hr;
        }

        public override int GetMediaType(int iPosition, ref AMMediaType pMediaType)
        {
            if (iPosition > 0) return VFW_S_NO_MORE_ITEMS;
            if (pMediaType == null) return E_INVALIDARG;
            if (!Input.IsConnected) return VFW_E_NOT_CONNECTED;

            AMMediaType.Copy(Input.CurrentMediaType, ref pMediaType);

            return NOERROR;
        }

        public override int CheckTransform(AMMediaType mtIn, AMMediaType mtOut)
        {
            return AMMediaType.AreEquals(mtIn, mtOut) ? NOERROR : VFW_E_INVALIDMEDIATYPE;
        }

        #endregion
    };

    #endregion

    #region Dump Filter

    [ComVisible(true)]
    public class DumpInputPin : RenderedInputPin
    {
        #region Constructor

        public DumpInputPin(string _name, BaseFilter _filter)
            :base(_name,_filter)
        {
        }

        #endregion

        #region Overridden Methods

        public override int CheckMediaType(AMMediaType pmt)
        {
            return NOERROR;
        }

        public override int OnReceive(ref IMediaSampleImpl _sample)
        {
            HRESULT hr = (HRESULT)CheckStreaming();
            if (hr != S_OK) return hr;
            return (m_Filter as DumpFilter).OnReceive(ref _sample);
        }

        public override int EndOfStream()
        {
            (m_Filter as DumpFilter).EndOfStream();
            return base.EndOfStream();
        }

        #endregion
    }

    [ComVisible(true)]
    [Guid("12CCB25E-CB76-4512-BEE2-D9895DFA0B40")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AboutForm))]
    public class DumpFilter : BaseFilter, IFileSinkFilter, IAMFilterMiscFlags
    {
        #region Variables

        private FileStream m_Stream = null;
        private string m_sFileName = "";

        #endregion

        #region Constructor

        public DumpFilter()
            : base("CSharp Dump Filter")
        {

        }

        #endregion

        #region Overridden Methods

        protected override int OnInitializePins()
        {
            AddPin(new DumpInputPin("In",this));
            return NOERROR;
        }

        public override int Pause()
        {
            if (m_State == FilterState.Stopped && m_Stream == null)
            {
                if (m_sFileName != "")
                {
                    m_Stream = new FileStream(m_sFileName, FileMode.Create, FileAccess.Write, FileShare.Read);
                }
            }
            return base.Pause();
        }

        public override int Stop()
        {
            int hr = base.Stop();
            if (m_Stream != null)
            {
                m_Stream.Dispose();
                m_Stream = null;
            }
            return hr;
        }

        #endregion

        #region Methods

        public int EndOfStream()
        {
            lock (m_Lock)
            {
                if (m_Stream != null)
                {
                    m_Stream.Dispose();
                    m_Stream = null;
                }
            }
            NotifyEvent(EventCode.Complete, (IntPtr)((int)S_OK), Marshal.GetIUnknownForObject(this));
            return S_OK;
        }

        public int OnReceive(ref IMediaSampleImpl _sample)
        {
            lock (m_Lock)
            {
                if (m_Stream == null && m_sFileName != "")
                {
                    m_Stream = new FileStream(m_sFileName, FileMode.Create, FileAccess.Write, FileShare.Read);
                }

                int _length = _sample.GetActualDataLength();
                if (m_Stream != null && _length > 0)
                {
                    byte[] _data = new byte[_length];
                    IntPtr _ptr;
                    _sample.GetPointer(out _ptr);
                    Marshal.Copy(_ptr, _data, 0, _length);
                    m_Stream.Write(_data, 0, _length);
                }
            }
            return S_OK;
        }

        #endregion

        #region IFileSinkFilter Members

        public int SetFileName(string pszFileName, AMMediaType pmt)
        {
            if (string.IsNullOrEmpty(pszFileName)) return E_POINTER;
            if (IsActive) return VFW_E_WRONG_STATE;
            m_sFileName = pszFileName;
            return NOERROR;
        }

        public int GetCurFile(out string pszFileName, AMMediaType pmt)
        {
            pszFileName = m_sFileName;
            if (pmt != null)
            {
                pmt.Set(Pins[0].CurrentMediaType);
            }
            return NOERROR;
        }

        #endregion

        #region IAMFilterMiscFlags Members

        public int GetMiscFlags()
        {
            return 1;
        }

        #endregion
    }

    #endregion

    #region Text Over Filter

    [ComVisible(true)]
    [Guid("8AF6F710-1AF5-4952-AAFF-ACCD0DB2C9BB")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AboutForm))]
    public class TextOverFilter : TransformFilter
    {
        #region Variables

        private string m_sText = "Sample Overlay Text";
        private Font m_Font = new Font("Arial", 20.0f, FontStyle.Regular, GraphicsUnit.Point);
        private Color m_Color = Color.Red;

        #endregion

        #region Constructor

        public TextOverFilter()
            : base("CSharp Text Overlay Filter")
        {
        }

        #endregion

        #region Overridden Methods

        public override int CheckInputType(AMMediaType pmt)
        {
            if (pmt.majorType != MediaType.Video)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (pmt.subType != MediaSubType.RGB32)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (pmt.formatType != FormatType.VideoInfo)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (pmt.formatPtr == IntPtr.Zero)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            return NOERROR;
        }

        public override int Transform(ref IMediaSampleImpl _input, ref IMediaSampleImpl _sample)
        {
            int lDataLength = _input.GetActualDataLength();
            _sample.SetActualDataLength(lDataLength);
            IntPtr _ptrIn;
            IntPtr _ptrOut;

            _input.GetPointer(out _ptrIn);
            _sample.GetPointer(out _ptrOut);

            BitmapInfoHeader _bmiIn = (BitmapInfoHeader)Input.CurrentMediaType;
            BitmapInfoHeader _bmiOut = (BitmapInfoHeader)Output.CurrentMediaType;
            Bitmap _bmpIn = new Bitmap(_bmiIn.Width, _bmiIn.Height, _bmiIn.Width * 4, PixelFormat.Format32bppRgb, _ptrIn);
            Bitmap _bmpOut = new Bitmap(_bmiOut.Width, _bmiOut.Height, _bmiOut.Width * 4, PixelFormat.Format32bppRgb, _ptrOut);

            {
                _bmpIn.RotateFlip(RotateFlipType.RotateNoneFlipY);
                Graphics _graphics = Graphics.FromImage(_bmpIn);

                StringFormat _format = new StringFormat();
                _format.Alignment = StringAlignment.Center;
                _format.LineAlignment = StringAlignment.Center;

                Brush _brush = new SolidBrush(m_Color);
                RectangleF _rect = new RectangleF(0, 0, _bmpIn.Width, _bmpIn.Height);
                _graphics.DrawString(m_sText, m_Font, _brush, _rect, _format);

                _graphics.Dispose();
                _bmpIn.RotateFlip(RotateFlipType.RotateNoneFlipY);
            }
            {
                Graphics _graphics = Graphics.FromImage(_bmpOut);
                _graphics.DrawImage(_bmpIn, 0, 0);
                _graphics.Dispose();
            }
            _bmpOut.Dispose();
            _bmpIn.Dispose();
            return S_OK;
        }

        public override int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            if (!Output.IsConnected) return VFW_E_NOT_CONNECTED;
            if (Output.CurrentMediaType.majorType != MediaType.Video) return VFW_E_INVALIDMEDIATYPE;
            AllocatorProperties _actual = new AllocatorProperties();
            BitmapInfoHeader _bmi = (BitmapInfoHeader)Output.CurrentMediaType;
            if (_bmi == null) return VFW_E_INVALIDMEDIATYPE;
            prop.cbBuffer = _bmi.GetBitmapSize();
            if (prop.cbBuffer < _bmi.ImageSize)
            {
                prop.cbBuffer = _bmi.ImageSize;
            }
            prop.cBuffers = 1;
            int hr = pAlloc.SetProperties(prop, _actual);
            return hr;
        }

        public override int GetMediaType(int iPosition, ref AMMediaType pMediaType)
        {
            if (iPosition > 0) return VFW_S_NO_MORE_ITEMS;
            if (pMediaType == null) return E_INVALIDARG;
            if (!Input.IsConnected) return VFW_E_NOT_CONNECTED;

            AMMediaType.Copy(Input.CurrentMediaType, ref pMediaType);

            return NOERROR;
        }

        public override int CheckTransform(AMMediaType mtIn, AMMediaType mtOut)
        {
            return AMMediaType.AreEquals(mtIn, mtOut) ? NOERROR : VFW_E_INVALIDMEDIATYPE;
        }

        #endregion
    }

    #endregion

    #region Image Source

    [ComVisible(true)]
    public class ImageSourceStream : SourceStream
    {
        #region Constructor

        public ImageSourceStream(string _name, BaseSourceFilter _filter)
            :base(_name,_filter)
        {
        }

        #endregion

        #region Overridden Methods

        public override int GetMediaType(ref AMMediaType pMediaType)
        {
            return (m_Filter as ImageSourceFilter).GetMediaType(ref pMediaType);
        }

        public override int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            if (!IsConnected) return VFW_E_NOT_CONNECTED;
            return (m_Filter as ImageSourceFilter).DecideBufferSize(ref pAlloc, ref prop);
        }

        public override int FillBuffer(ref IMediaSampleImpl pSample)
        {
            return (m_Filter as ImageSourceFilter).FillBuffer(ref pSample);
        }

        #endregion
    }

    [ComVisible(true)]
    [Guid("170BB172-4FD1-4eb5-B6F6-A834B344268F")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AboutForm))]
    public class ImageSourceFilter : BaseSourceFilter, IFileSourceFilter
    {
        #region Variables

        protected string m_sFileName = "";
        protected Bitmap m_pBitmap = null;
        protected long m_nAvgTimePerFrame = UNITS / 20;
        protected long m_lLastSampleTime = 0;

        #endregion

        #region Constructor

        public ImageSourceFilter()
            :base("CSharp Image Source Filter")
        {

        }

        ~ImageSourceFilter()
        {
            if (m_pBitmap != null)
            {
                m_pBitmap.Dispose();
                m_pBitmap = null;
            }
        }

        #endregion

        #region Overridden Methods

        protected override int OnInitializePins()
        {
            AddPin(new ImageSourceStream("Output", this));
            return NOERROR;
        }

        public override int Pause()
        {
            if (m_State == FilterState.Stopped)
            {
                m_lLastSampleTime = 0;
            }
            return base.Pause();
        }

        #endregion

        #region Methods

        public int GetMediaType(ref AMMediaType pMediaType)
        {
            if (m_pBitmap == null) return E_UNEXPECTED;
            pMediaType.majorType = DirectShow.MediaType.Video;
            pMediaType.subType = DirectShow.MediaSubType.RGB32;
            pMediaType.formatType = DirectShow.FormatType.VideoInfo;

            VideoInfoHeader vih = new VideoInfoHeader();
            vih.AvgTimePerFrame = m_nAvgTimePerFrame;
            vih.BmiHeader = new BitmapInfoHeader();
            vih.BmiHeader.Size = Marshal.SizeOf(typeof(BitmapInfoHeader));
            vih.BmiHeader.Compression = 0;
            vih.BmiHeader.BitCount = 32;
            vih.BmiHeader.Width = m_pBitmap.Width;
            vih.BmiHeader.Height = m_pBitmap.Height;
            vih.BmiHeader.Planes = 1;
            vih.BmiHeader.ImageSize = vih.BmiHeader.Width * vih.BmiHeader.Height * vih.BmiHeader.BitCount / 8;
            vih.SrcRect = new DsRect();
            vih.TargetRect = new DsRect();

            AMMediaType.SetFormat(ref pMediaType, ref vih);
            pMediaType.fixedSizeSamples = true;
            pMediaType.sampleSize = vih.BmiHeader.ImageSize;

            return NOERROR;
        }

        public int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            AllocatorProperties _actual = new AllocatorProperties();

            BitmapInfoHeader _bmi = (BitmapInfoHeader)Pins[0].CurrentMediaType;
            prop.cbBuffer = _bmi.GetBitmapSize();
            if (prop.cbBuffer < _bmi.ImageSize)
            {
                prop.cbBuffer = _bmi.ImageSize;
            }
            prop.cBuffers = 1;

            int hr = pAlloc.SetProperties(prop, _actual);
            return hr;
        }

        public int FillBuffer(ref IMediaSampleImpl _sample)
        {
            BitmapInfoHeader _bmi = (BitmapInfoHeader)Pins[0].CurrentMediaType;
            
            IntPtr _ptr;
            _sample.GetPointer(out _ptr);
            Bitmap _bmp = new Bitmap(_bmi.Width, _bmi.Height, _bmi.Width * 4, PixelFormat.Format32bppRgb, _ptr);
            Graphics _graphics = Graphics.FromImage(_bmp);

            _graphics.DrawImage(m_pBitmap, new Rectangle(0, 0, _bmp.Width, _bmp.Height), 0, 0, m_pBitmap.Width, m_pBitmap.Height,GraphicsUnit.Pixel);
            _graphics.Dispose();
            _bmp.Dispose();
            _sample.SetActualDataLength(_bmi.ImageSize);
            _sample.SetSyncPoint(true);
            long _stop = m_lLastSampleTime + m_nAvgTimePerFrame;
            _sample.SetTime((DsLong)m_lLastSampleTime, (DsLong)_stop);
            m_lLastSampleTime = _stop;
            return NOERROR;
        }

        #endregion

        #region IFileSourceFilter Members

        public int Load(string pszFileName, AMMediaType pmt)
        {
            if (string.IsNullOrEmpty(pszFileName)) return E_POINTER;
            if (IsActive) return VFW_E_WRONG_STATE;
            m_sFileName = pszFileName;
            if (m_pBitmap != null)
            {
                m_pBitmap.Dispose();
            }
            m_pBitmap = new Bitmap(m_sFileName);
            m_pBitmap.RotateFlip(RotateFlipType.RotateNoneFlipY);
            if (Pins[0].IsConnected)
            {
                ((BaseOutputPin)Pins[0]).ReconnectPin();
            }
            return NOERROR;
        }

        public int GetCurFile(out string pszFileName, AMMediaType pmt)
        {
            pszFileName = m_sFileName;
            if (pmt != null)
            {
                pmt.Set(Pins[0].CurrentMediaType);
            }
            return NOERROR;
        }

        #endregion
    }

    #endregion

    #region Screen Capture

    [ComVisible(true)]
    public class ScreenCaptureStream : SourceStream
    {
        #region Constructor

        public ScreenCaptureStream(string _name, BaseSourceFilter _filter)
            : base(_name, _filter)
        {
        }

        #endregion

        #region Overridden Methods

        public override int GetMediaType(ref AMMediaType pMediaType)
        {
            return (m_Filter as ScreenCaptureFilter).GetMediaType(ref pMediaType);
        }

        public override int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            if (!IsConnected) return VFW_E_NOT_CONNECTED;
            return (m_Filter as ScreenCaptureFilter).DecideBufferSize(ref pAlloc, ref prop);
        }

        public override int FillBuffer(ref IMediaSampleImpl pSample)
        {
            return (m_Filter as ScreenCaptureFilter).FillBuffer(ref pSample);
        }

        #endregion
    }

    [ComVisible(true)]
    [Guid("63E2D3DC-9266-4277-A796-6DCD5400772C")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AboutForm))]
    public class ScreenCaptureFilter : BaseSourceFilter
    {
        #region Variables

        protected int m_nWidth = 640;
        protected int m_nHeight = 480;
        protected long m_nAvgTimePerFrame = UNITS / 20;
        protected long m_lLastSampleTime = 0;

        protected IntPtr m_hScreenDC = IntPtr.Zero;
        protected IntPtr m_hMemDC = IntPtr.Zero;
        protected IntPtr m_hBitmap = IntPtr.Zero;
        protected BitmapInfo m_bmi = new BitmapInfo();

        protected int m_nMaxWidth = 0;
        protected int m_nMaxHeight = 0;

        #endregion

        #region Constructor

        public ScreenCaptureFilter()
            : base("CSharp Screen Capture Filter")
        {

        }

        #endregion

        #region Overridden Methods

        protected override int OnInitializePins()
        {
            AddPin(new ScreenCaptureStream("Output", this));
            return NOERROR;
        }

        public override int Pause()
        {
            if (m_State == FilterState.Stopped)
            {
                m_lLastSampleTime = 0;
                m_hScreenDC = CreateDC("DISPLAY", null, null, IntPtr.Zero);
                m_nMaxWidth = GetDeviceCaps(m_hScreenDC,8); // HORZRES
                m_nMaxHeight = GetDeviceCaps(m_hScreenDC, 10); // VERTRES
                m_hMemDC    = CreateCompatibleDC(m_hScreenDC);
            }
            return base.Pause();
        }

        public override int Stop()
        {
            int hr = base.Stop();
            if (m_hBitmap != IntPtr.Zero)
            {
                DeleteObject(m_hBitmap);
                m_hBitmap = IntPtr.Zero;
            }
            if (m_hScreenDC != IntPtr.Zero)
            {
                DeleteDC(m_hScreenDC);
                m_hScreenDC = IntPtr.Zero;
            }
            if (m_hMemDC != IntPtr.Zero)
            {
                DeleteDC(m_hMemDC);
                m_hMemDC = IntPtr.Zero;
            }
            return hr;
        }

        #endregion

        #region Methods

        public int GetMediaType(ref AMMediaType pMediaType)
        {
            pMediaType.majorType = DirectShow.MediaType.Video;
            pMediaType.subType = DirectShow.MediaSubType.RGB32;
            pMediaType.formatType = DirectShow.FormatType.VideoInfo;

            VideoInfoHeader vih = new VideoInfoHeader();
            vih.AvgTimePerFrame = m_nAvgTimePerFrame;
            vih.BmiHeader = new BitmapInfoHeader();
            vih.BmiHeader.Size = Marshal.SizeOf(typeof(BitmapInfoHeader));
            vih.BmiHeader.Compression = 0;
            vih.BmiHeader.BitCount = 32;
            vih.BmiHeader.Width = m_nWidth;
            vih.BmiHeader.Height = m_nHeight;
            vih.BmiHeader.Planes = 1;
            vih.BmiHeader.ImageSize = vih.BmiHeader.Width * vih.BmiHeader.Height * vih.BmiHeader.BitCount / 8;
            vih.SrcRect = new DsRect();
            vih.TargetRect = new DsRect();

            AMMediaType.SetFormat(ref pMediaType, ref vih);
            pMediaType.fixedSizeSamples = true;
            pMediaType.sampleSize = vih.BmiHeader.ImageSize;

            return NOERROR;
        }

        public int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            AllocatorProperties _actual = new AllocatorProperties();

            BitmapInfoHeader _bmi = (BitmapInfoHeader)Pins[0].CurrentMediaType;
            prop.cbBuffer = _bmi.GetBitmapSize();
            if (prop.cbBuffer < _bmi.ImageSize)
            {
                prop.cbBuffer = _bmi.ImageSize;
            }
            prop.cBuffers = 1;

            int hr = pAlloc.SetProperties(prop, _actual);
            return hr;
        }

        public int FillBuffer(ref IMediaSampleImpl _sample)
        {
            BitmapInfoHeader _bmi = (BitmapInfoHeader)Pins[0].CurrentMediaType;
            if (m_hBitmap == IntPtr.Zero)
            {
                m_hBitmap = CreateCompatibleBitmap(m_hScreenDC, _bmi.Width, Math.Abs(_bmi.Height));
                m_bmi.bmiHeader = _bmi;
            }
            
            IntPtr _ptr;
            _sample.GetPointer(out _ptr);

            IntPtr hOldBitmap = SelectObject(m_hMemDC, m_hBitmap);

            StretchBlt(m_hMemDC, 0, 0, m_nWidth, m_nHeight, m_hScreenDC, 0, 0, m_nMaxWidth, m_nMaxHeight,TernaryRasterOperations.SRCCOPY);

            SelectObject(m_hMemDC, hOldBitmap);

            GetDIBits(m_hMemDC, m_hBitmap, 0, (uint)m_nHeight, _ptr, ref m_bmi, 0);

            _sample.SetActualDataLength(_bmi.ImageSize);
            _sample.SetSyncPoint(true);
            long _stop = m_lLastSampleTime + m_nAvgTimePerFrame;
            _sample.SetTime((DsLong)m_lLastSampleTime, (DsLong)_stop);
            m_lLastSampleTime = _stop;
            return NOERROR;
        }

        #endregion

        #region API

        [StructLayout(LayoutKind.Sequential)]
        protected struct BitmapInfo
        {
            public BitmapInfoHeader bmiHeader;
            public int[] bmiColors;
        }

        private enum TernaryRasterOperations : uint
        {
            SRCCOPY = 0x00CC0020,
            SRCPAINT = 0x00EE0086,
            SRCAND = 0x008800C6,
            SRCINVERT = 0x00660046,
            SRCERASE = 0x00440328,
            NOTSRCCOPY = 0x00330008,
            NOTSRCERASE = 0x001100A6,
            MERGECOPY = 0x00C000CA,
            MERGEPAINT = 0x00BB0226,
            PATCOPY = 0x00F00021,
            PATPAINT = 0x00FB0A09,
            PATINVERT = 0x005A0049,
            DSTINVERT = 0x00550009,
            BLACKNESS = 0x00000042,
            WHITENESS = 0x00FF0062,
            CAPTUREBLT = 0x40000000
        }

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool StretchBlt(IntPtr hdcDest, int nXOriginDest, int nYOriginDest,
            int nWidthDest, int nHeightDest,
            IntPtr hdcSrc, int nXOriginSrc, int nYOriginSrc, int nWidthSrc, int nHeightSrc,
            TernaryRasterOperations dwRop);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan,
           uint cScanLines, [Out] IntPtr lpvBits, ref BitmapInfo lpbmi, uint uUsage);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        #endregion
    }

    #endregion

    #region Video Rotation Filter

    [ComVisible(true)]
    [Guid("7FB1203E-5E75-4ead-AB36-92C41CF9DAA5")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AboutForm))]
    public class VideoRotationFilter : TransformFilter
    {
        #region Constructor

        public VideoRotationFilter()
            : base("CSharp Video Rotation Filter")
        {
        }

        #endregion

        #region Overridden Methods

        public override int CheckInputType(AMMediaType pmt)
        {
            if (pmt.majorType != MediaType.Video)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (pmt.formatType != FormatType.VideoInfo)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (pmt.subType != MediaSubType.RGB24)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (!pmt.fixedSizeSamples)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (pmt.formatPtr == IntPtr.Zero)
            {
                return VFW_E_INVALIDMEDIATYPE;
            }
            return NOERROR;
        }

        public override int Transform(ref IMediaSampleImpl pIn, ref IMediaSampleImpl pOut)
        {
            pOut.SetActualDataLength(pIn.GetActualDataLength());
            IntPtr pSourceBuffer, pDestBuffer;

            pIn.GetPointer(out pSourceBuffer);
            pOut.GetPointer(out pDestBuffer);

            BitmapInfoHeader _bmi = Output.CurrentMediaType;

            Bitmap _bmpOut = new Bitmap(_bmi.Width, _bmi.Height, _bmi.Width * 3, PixelFormat.Format24bppRgb, pDestBuffer);

            Bitmap _bmpInput = null;

            _bmpInput = new Bitmap(_bmi.Height, _bmi.Width, _bmi.Height * 3, PixelFormat.Format24bppRgb, pSourceBuffer);
            _bmpInput.RotateFlip(RotateFlipType.Rotate90FlipNone);

            Graphics _gr = Graphics.FromImage(_bmpOut);
            _gr.DrawImage(_bmpInput, 0, 0);
            _gr.Dispose();
            return NOERROR;
        }

        public override int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            if (!Output.IsConnected) return VFW_E_NOT_CONNECTED;
            if (Output.CurrentMediaType.majorType != MediaType.Video) return VFW_E_INVALIDMEDIATYPE;
            AllocatorProperties _actual = new AllocatorProperties();
            BitmapInfoHeader _bmi = (BitmapInfoHeader)Output.CurrentMediaType;
            if (_bmi == null) return VFW_E_INVALIDMEDIATYPE;
            prop.cbBuffer = _bmi.GetBitmapSize();
            if (prop.cbBuffer < _bmi.ImageSize)
            {
                prop.cbBuffer = _bmi.ImageSize;
            }
            prop.cBuffers = 1;
            int hr = pAlloc.SetProperties(prop, _actual);
            return hr;
        }

        public override int GetMediaType(int iPosition, ref AMMediaType pMediaType)
        {
            if (iPosition > 0) return VFW_S_NO_MORE_ITEMS;
            if (pMediaType == null) return E_INVALIDARG;
            if (!Input.IsConnected) return VFW_E_NOT_CONNECTED;

            AMMediaType.Copy(Input.CurrentMediaType, ref pMediaType);

            VideoInfoHeader _vih = (VideoInfoHeader)pMediaType;
            int Width = _vih.BmiHeader.Width;
            _vih.BmiHeader.Width = Math.Abs(_vih.BmiHeader.Height);
            _vih.BmiHeader.Height = Width;
            Marshal.StructureToPtr(_vih, pMediaType.formatPtr, true);
            return NOERROR;
        }

        public override int CheckTransform(AMMediaType mtIn, AMMediaType mtOut)
        {
            if (mtIn.majorType != mtOut.majorType)
            {
                return VFW_E_INVALIDMEDIATYPE;
            }
            if (mtIn.subType != mtOut.subType)
            {
                return VFW_E_INVALIDMEDIATYPE;
            }

            BitmapInfoHeader _bmiIn = mtIn;
            BitmapInfoHeader _bmiOut = mtOut;

            if (_bmiIn == null || _bmiOut == null)
            {
                return VFW_E_INVALIDMEDIATYPE;
            }
            if (_bmiIn.Width != _bmiOut.Height)
            {
                return VFW_E_INVALIDMEDIATYPE;
            }
            return NOERROR;
        }

        #endregion
    };

    #endregion

    #region Wav Dest Filter

    [ComVisible(true)]
    public class WavDestInputPin : RenderedInputPin
    {
        #region Constructor

        public WavDestInputPin(string _name, BaseFilter _filter)
            : base(_name, _filter)
        {
        }

        #endregion

        #region Overridden Methods

        public override int CheckMediaType(AMMediaType pmt)
        {
            return (m_Filter as WavDestFilter).CheckMediaType(pmt);
        }

        public override int OnReceive(ref IMediaSampleImpl _sample)
        {
            HRESULT hr = (HRESULT)CheckStreaming();
            if (hr != S_OK) return hr;
            return (m_Filter as WavDestFilter).OnReceive(ref _sample);
        }

        public override int EndOfStream()
        {
            (m_Filter as WavDestFilter).EndOfStream();
            return base.EndOfStream();
        }

        #endregion
    }

    [ComVisible(true)]
    [Guid("EC5D99F0-8D1E-49ec-8C30-9CBE4AF4FD0B")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AboutForm))]
    public class WavDestFilter : BaseFilter, IFileSinkFilter, IAMFilterMiscFlags
    {
        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        private class OUTPUT_DATA_HEADER
        {
            public uint dwData = 0;
            public uint dwDataLength = 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class OUTPUT_FILE_HEADER
        {
            public uint dwRiff = 0;
            public uint dwFileSize = 0;
            public uint dwWave = 0;
            public uint dwFormat = 0;
            public uint dwFormatLength = 0;
        }

        #endregion

        #region Constants

        private const uint RIFF_TAG = 0x46464952;
        private const uint WAVE_TAG = 0x45564157;
        private const uint FMT__TAG = 0x20746D66;
        private const uint DATA_TAG = 0x61746164;
        private const uint WAVE_FORMAT_PCM = 0x01;

        #endregion

        #region Variables

        private FileStream m_Stream = null;
        private string m_sFileName = "";

        #endregion

        #region Constructor

        public WavDestFilter()
            : base("CSharp Wav Dest Filter")
        {

        }

        #endregion

        #region Overridden Methods

        protected override int OnInitializePins()
        {
            AddPin(new WavDestInputPin("In", this));
            return NOERROR;
        }

        public override int Pause()
        {
            if (m_State == FilterState.Stopped && m_Stream == null)
            {
                OpenFile();
            }
            return base.Pause();
        }

        public override int Stop()
        {
            int hr = base.Stop();
            CloseFile();
            return hr;
        }

        #endregion

        #region Methods

        public int CheckMediaType(AMMediaType pmt)
        {
            if (pmt.majorType != MediaType.Audio)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (pmt.subType != MediaSubType.PCM)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            WaveFormatEx _wfx = pmt;
            if (_wfx == null || _wfx.wFormatTag != WAVE_FORMAT_PCM)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            return S_OK;
        }

        public int EndOfStream()
        {
            CloseFile();
            NotifyEvent(EventCode.Complete, (IntPtr)((int)S_OK), Marshal.GetIUnknownForObject(this));
            return S_OK;
        }

        public int OnReceive(ref IMediaSampleImpl _sample)
        {
            lock (m_Lock)
            {
                if (m_Stream == null)
                {
                    OpenFile();
                }

                int _length = _sample.GetActualDataLength();
                if (m_Stream != null && _length > 0)
                {
                    byte[] _data = new byte[_length];
                    IntPtr _ptr;
                    _sample.GetPointer(out _ptr);
                    Marshal.Copy(_ptr, _data, 0, _length);
                    m_Stream.Write(_data, 0, _length);
                }
            }
            return S_OK;
        }

        #endregion

        #region Helper Methods

        protected int OpenFile()
        {
            if (m_Stream == null && m_sFileName != "" && Pins[0].IsConnected)
            {
                m_Stream = new FileStream(m_sFileName, FileMode.Create, FileAccess.Write, FileShare.Read);

                WaveFormatEx _wfx = Pins[0].CurrentMediaType;

                int _size;
                byte[] _buffer;
                IntPtr _ptr;

                OUTPUT_FILE_HEADER _header = new OUTPUT_FILE_HEADER();

                _header.dwRiff = RIFF_TAG;
                _header.dwFileSize = 0;
                _header.dwWave = WAVE_TAG;
                _header.dwFormat = FMT__TAG;
                _header.dwFormatLength = (uint)Marshal.SizeOf(_wfx);

                _size = Marshal.SizeOf(_header);
                _buffer = new byte[_size];
                _ptr = Marshal.AllocCoTaskMem(_size);
                Marshal.StructureToPtr(_header, _ptr, true);
                Marshal.Copy(_ptr, _buffer, 0, _size);
                m_Stream.Write(_buffer, 0, _size);
                Marshal.FreeCoTaskMem(_ptr);

                _size = Marshal.SizeOf(_wfx);
                _buffer = new byte[_size];
                _ptr = Marshal.AllocCoTaskMem(_size);
                Marshal.StructureToPtr(_wfx, _ptr, true);
                Marshal.Copy(_ptr, _buffer, 0, _size);
                m_Stream.Write(_buffer, 0, _size);
                Marshal.FreeCoTaskMem(_ptr);

                OUTPUT_DATA_HEADER _data = new OUTPUT_DATA_HEADER();
                _data.dwData = DATA_TAG;
                _data.dwDataLength = 0;

                _size = Marshal.SizeOf(_data);
                _buffer = new byte[_size];
                _ptr = Marshal.AllocCoTaskMem(_size);
                Marshal.StructureToPtr(_data, _ptr, true);
                Marshal.Copy(_ptr, _buffer, 0, _size);
                m_Stream.Write(_buffer, 0, _size);
                Marshal.FreeCoTaskMem(_ptr);

                return NOERROR;
            }
            return S_FALSE;
        }

        protected int CloseFile()
        {
            lock (m_Lock)
            {
                if (m_Stream != null)
                {
                    WaveFormatEx _wfx = Pins[0].CurrentMediaType;

                    int _size;
                    byte[] _buffer;
                    IntPtr _ptr;

                    OUTPUT_FILE_HEADER _header = new OUTPUT_FILE_HEADER();

                    _header.dwRiff = RIFF_TAG;
                    _header.dwFileSize = (uint)m_Stream.Length - 2 * 4;
                    _header.dwWave = WAVE_TAG;
                    _header.dwFormat = FMT__TAG;
                    _header.dwFormatLength = (uint)Marshal.SizeOf(_wfx);

                    _size = Marshal.SizeOf(_header);
                    _buffer = new byte[_size];
                    _ptr = Marshal.AllocCoTaskMem(_size);
                    Marshal.StructureToPtr(_header, _ptr, true);
                    Marshal.Copy(_ptr, _buffer, 0, _size);
                    m_Stream.Write(_buffer, 0, _size);
                    Marshal.FreeCoTaskMem(_ptr);

                    _size = Marshal.SizeOf(_wfx);
                    _buffer = new byte[_size];
                    _ptr = Marshal.AllocCoTaskMem(_size);
                    Marshal.StructureToPtr(_wfx, _ptr, true);
                    Marshal.Copy(_ptr, _buffer, 0, _size);
                    m_Stream.Write(_buffer, 0, _size);
                    Marshal.FreeCoTaskMem(_ptr);

                    OUTPUT_DATA_HEADER _data = new OUTPUT_DATA_HEADER();
                    _data.dwData = DATA_TAG;
                    _data.dwDataLength = (uint)(m_Stream.Length - Marshal.SizeOf(_header) - _header.dwFormatLength - Marshal.SizeOf(_data));

                    _size = Marshal.SizeOf(_data);
                    _buffer = new byte[_size];
                    _ptr = Marshal.AllocCoTaskMem(_size);
                    Marshal.StructureToPtr(_data, _ptr, true);
                    Marshal.Copy(_ptr, _buffer, 0, _size);
                    m_Stream.Write(_buffer, 0, _size);
                    Marshal.FreeCoTaskMem(_ptr);

                    m_Stream.Dispose();
                    m_Stream = null;
                }
            }
            return NOERROR;
        }

        #endregion

        #region IFileSinkFilter Members

        public int SetFileName(string pszFileName, AMMediaType pmt)
        {
            if (string.IsNullOrEmpty(pszFileName)) return E_POINTER;
            if (IsActive) return VFW_E_WRONG_STATE;
            m_sFileName = pszFileName;
            return NOERROR;
        }

        public int GetCurFile(out string pszFileName, AMMediaType pmt)
        {
            pszFileName = m_sFileName;
            if (pmt != null)
            {
                pmt.Set(Pins[0].CurrentMediaType);
            }
            return NOERROR;
        }

        #endregion

        #region IAMFilterMiscFlags Members

        public int GetMiscFlags()
        {
            return 1;
        }

        #endregion
    }

    #endregion

    #region Channel Output

    public enum AudioChannel : int
    {
        FRONT_LEFT = 0x1,
        FRONT_RIGHT = 0x2,
        FRONT_CENTER = 0x4,
        LOW_FREQUENCY = 0x8,
        BACK_LEFT = 0x10,
        BACK_RIGHT = 0x20,
        SIDE_LEFT = 0x200,
        SIDE_RIGHT = 0x400,
    }

    [ComVisible(true)]
    [System.Security.SuppressUnmanagedCodeSecurity]
    [Guid("29D64CCD-D271-4390-8CF2-89D445E7814B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioChannel
    {
        [PreserveSig]
        int put_ActiveChannel([In] AudioChannel _channel);

        [PreserveSig]
        int get_ActiveChannel([Out] out AudioChannel _channel);
    }

    [ComVisible(true)]
    [Guid("701F5A6E-CE48-4dd8-A619-3FEB42E4AC77")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AudioChannelForm),typeof(AboutForm))]
    public class AudioChannelFilter : TransformFilter, IAudioChannel
    {
        #region Constants

        private const uint WAVE_FORMAT_PCM = 0x0001;
        private const uint WAVE_FORMAT_EXTENSIBLE = 0xFFFE;
        private static readonly Guid KSDATAFORMAT_SUBTYPE_PCM = new Guid("00000001-0000-0010-8000-00aa00389b71");

        #endregion

        #region Variables

        protected AudioChannel m_Channel = AudioChannel.FRONT_LEFT;

        #endregion

        #region Constructor

        public AudioChannelFilter()
            : base("CSharp Audio Channel Filter")
        {
        }

        #endregion

        #region Overridden Methods

        public override int CheckInputType(AMMediaType pmt)
        {
            if (pmt.majorType != MediaType.Audio)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (pmt.formatType != FormatType.WaveEx)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            WaveFormatEx _wfx = pmt;
            if (_wfx == null)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (_wfx.wFormatTag != WAVE_FORMAT_PCM)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (_wfx.nChannels == 0)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (_wfx.wBitsPerSample != 16 && _wfx.wBitsPerSample != 8)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            return NOERROR;
        }

        public override int GetMediaType(int iPosition, ref AMMediaType pMediaType)
        {
            if (!Input.IsConnected) return E_INVALIDARG;
            if (iPosition < 0) return E_INVALIDARG;
            if (iPosition > 0) return VFW_S_NO_MORE_ITEMS;

            WaveFormatEx _wfx = Input.CurrentMediaType;

            WaveFormatExtensible _wfxOut = new WaveFormatExtensible();

            _wfxOut.Format.wFormatTag = (ushort)WAVE_FORMAT_EXTENSIBLE;
            _wfxOut.Format.cbSize = (ushort)(Marshal.SizeOf(_wfxOut) - Marshal.SizeOf(typeof(WaveFormatEx)));
            _wfxOut.Format.wBitsPerSample = _wfx.wBitsPerSample;
            _wfxOut.Format.nChannels = 1;

            _wfxOut.Format.nSamplesPerSec = _wfx.nSamplesPerSec;

            _wfxOut.Format.nBlockAlign = (ushort)(_wfxOut.Format.nChannels * _wfxOut.Format.wBitsPerSample / 8);
            _wfxOut.Format.nAvgBytesPerSec = _wfxOut.Format.nBlockAlign * _wfxOut.Format.nSamplesPerSec;
            _wfxOut.wReserved = 0;

            _wfxOut.dwChannelMask = (SPEAKER)m_Channel;
            _wfxOut.SubFormat = KSDATAFORMAT_SUBTYPE_PCM;

            pMediaType.majorType = MediaType.Audio;
            pMediaType.subType = MediaSubType.PCM;
            pMediaType.formatType = FormatType.WaveEx;

            pMediaType.fixedSizeSamples = true;
            pMediaType.sampleSize = _wfxOut.Format.nBlockAlign;

            pMediaType.formatSize = Marshal.SizeOf(_wfxOut);
            pMediaType.formatPtr = Marshal.AllocCoTaskMem(pMediaType.formatSize);
            Marshal.StructureToPtr(_wfxOut, pMediaType.formatPtr, true);

            return NOERROR;
        }

        public override int CheckTransform(AMMediaType mtIn, AMMediaType mtOut)
        {
            return NOERROR;
        }

        public override int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            if (!Output.IsConnected) return VFW_E_NOT_CONNECTED;
            AllocatorProperties _actual = new AllocatorProperties();

            WaveFormatExtensible _wfx = (WaveFormatExtensible)Marshal.PtrToStructure(Output.CurrentMediaType.formatPtr, typeof(WaveFormatExtensible));
            if (_wfx == null) return VFW_E_INVALIDMEDIATYPE;
            prop.cbBuffer = _wfx.Format.nAvgBytesPerSec;
            if (prop.cbBuffer < _wfx.Format.nBlockAlign * _wfx.Format.nSamplesPerSec)
            {
                prop.cbBuffer = _wfx.Format.nBlockAlign * _wfx.Format.nSamplesPerSec;
            }
            prop.cbAlign = _wfx.Format.nBlockAlign;
            prop.cBuffers = 3;
            int hr = pAlloc.SetProperties(prop, _actual);
            return hr;
        }

        public override int Transform(ref IMediaSampleImpl pIn, ref IMediaSampleImpl pOut)
        {
            WaveFormatEx _wfx = Input.CurrentMediaType;

            int iSize = pIn.GetActualDataLength();
            IntPtr pBuffer;
            pIn.GetPointer(out pBuffer);
            IntPtr pOutBuffer;
            pOut.GetPointer(out pOutBuffer);
            pOut.SetActualDataLength(iSize / _wfx.nChannels);

            int iCount = (_wfx.wBitsPerSample == 8 ? 1 : 2);
            float fMax = _wfx.wBitsPerSample == 8 ? 127.0f : 32767.0f;
            float fMin = _wfx.wBitsPerSample == 8 ? -127.0f : -32767.0f;

            float fTemp;
            if (_wfx.wBitsPerSample == 8)
            {
                while (iSize > 0)
                {
                    fTemp = 0;
                    int nChannel = 0;
                    while (nChannel++ < _wfx.nChannels)
                    {
                        fTemp += ((float)(Marshal.ReadByte(pBuffer)) - 128.0f);
                        iSize -= iCount;
                        pBuffer = new IntPtr(pBuffer.ToInt32() + 1);
                    }

                    fTemp /= _wfx.nChannels;

                    if (fTemp < fMin) fTemp = fMin;
                    if (fTemp > fMax) fTemp = fMax;

                    Marshal.WriteByte(pOutBuffer, (byte)(fTemp + 128.0f));
                    pOutBuffer = new IntPtr(pOutBuffer.ToInt32() + 1);
                }
            }
            else
                if (_wfx.wBitsPerSample == 16)
                {
                    while (iSize > 0)
                    {
                        fTemp = 0;
                        int nChannel = 0;
                        while (nChannel++ < _wfx.nChannels)
                        {
                            fTemp += (float)Marshal.ReadInt16(pBuffer);
                            iSize -= iCount;
                            pBuffer = new IntPtr(pBuffer.ToInt32() + 2);
                        }

                        fTemp /= _wfx.nChannels;

                        if (fTemp < fMin) fTemp = fMin;
                        if (fTemp > fMax) fTemp = fMax;

                        Marshal.WriteInt16(pOutBuffer, (short)(fTemp + 128.0f));
                        pOutBuffer = new IntPtr(pOutBuffer.ToInt32() + 2);
                    }
                }

            return NOERROR;
        }

        #endregion

        #region IAudioChannel Members

        public int put_ActiveChannel(AudioChannel _channel)
        {
            if (IsActive) return VFW_E_WRONG_STATE;
            if (m_Channel != _channel)
            {
                m_Channel = _channel;
                if (Output.IsConnected)
                {
                    Output.ReconnectPin();
                }
            }
            return NOERROR;
        }

        public int get_ActiveChannel(out AudioChannel _channel)
        {
            _channel = m_Channel;
            return NOERROR;
        }

        #endregion
    }

    #endregion

    #region Inf Tee

    [ComVisible(true)]
    public class InfTeeInputPin: BaseInputPin
    {
        #region Variables

        private bool m_bInsideCheckMediaType = false;

        #endregion

        #region Constructor

        public InfTeeInputPin(string _name, BaseFilter _filter)
            : base(_name, _filter)
        {
        }

        #endregion

        #region Overridden Methods

        public override int CheckMediaType(AMMediaType pmt)
        {
            lock (m_Lock)
            {
                if (m_bInsideCheckMediaType) return NOERROR;
                m_bInsideCheckMediaType = true;
                for (int i = 1; i < m_Filter.Pins.Count; i++)
                {
                    InfTeeOutputPin _pin = (m_Filter.Pins[i] as InfTeeOutputPin);
                    if (_pin.IsConnected)
                    {
                        HRESULT hr = (HRESULT)_pin.QueryAccept(pmt);
                        if (hr != NOERROR)
                        {
                            m_bInsideCheckMediaType = false;
                            return VFW_E_TYPE_NOT_ACCEPTED;
                        }
                    }
                }
                m_bInsideCheckMediaType = false;
            }
            return NOERROR;
        }

        public override int EndOfStream()
        {
            lock (m_Lock)
            {
                for (int i = 1; i < m_Filter.Pins.Count; i++)
                {
                    HRESULT hr = (HRESULT)(m_Filter.Pins[i] as InfTeeOutputPin).DeliverEndOfStream();
                    if (hr.Failed) return hr;
                }
            }
            return NOERROR;
        }

        public override int BeginFlush()
        {
            lock (m_Lock)
            {
                for (int i = 1; i < m_Filter.Pins.Count; i++)
                {
                    HRESULT hr = (HRESULT)(m_Filter.Pins[i] as InfTeeOutputPin).DeliverBeginFlush();
                    if (hr.Failed) return hr;
                }
            }
            return NOERROR;
        }

        public override int EndFlush()
        {
            lock (m_Lock)
            {
                for (int i = 1; i < m_Filter.Pins.Count; i++)
                {
                    HRESULT hr = (HRESULT)(m_Filter.Pins[i] as InfTeeOutputPin).DeliverEndFlush();
                    if (hr.Failed) return hr;
                }
            }
            return NOERROR;
        }

        public override int NewSegment(long tStart, long tStop, double dRate)
        {
            lock (m_Lock)
            {
                for (int i = 1; i < m_Filter.Pins.Count; i++)
                {
                    HRESULT hr = (HRESULT)(m_Filter.Pins[i] as InfTeeOutputPin).DeliverNewSegment(tStart, tStop, dRate);
                    if (hr.Failed) return hr;
                }
            }
            return NOERROR;
        }

        public override int CompleteConnect(ref IPinImpl pReceivePin)
        {
            HRESULT hr = (HRESULT)base.CompleteConnect(ref pReceivePin);
            if (hr.Failed) return hr;
            for (int i = 1; i < m_Filter.Pins.Count; i++)
            {
                InfTeeOutputPin _pin = (m_Filter.Pins[i] as InfTeeOutputPin);
                if (_pin.IsConnected && _pin.CurrentMediaType != m_mt)
                {
                    m_Filter.ReconnectPin(_pin, m_mt);
                }
            }

            return hr;
        }

        public override int OnReceive(ref IMediaSampleImpl _sample)
        {
            HRESULT hr;
            lock (m_Lock)
            {
                hr = (HRESULT)base.OnReceive(ref _sample);
                if (hr != S_OK) return hr;
                for (int i = 1; i < m_Filter.Pins.Count; i++)
                {
                    hr = (HRESULT)(m_Filter.Pins[i] as InfTeeOutputPin).Deliver(ref _sample);
                    if (hr.Failed) return hr;
                }
            }
            return hr;
        }

        #endregion
    }

    [ComVisible(true)]
    public class InfTeeOutputPin : BaseOutputPin
    {
        #region Constants

        private const int INFTEE_MAX_PINS = 1000;

        #endregion

        #region Valiables

        private OutputQueue m_pOutputQueue = null;
        private bool m_bInsideCheckMediaType = false;

        #endregion

        #region Constructor

        public InfTeeOutputPin(string _name, BaseFilter _filter)
            : base(_name, _filter)
        {
        }

        #endregion

        #region Overriden Methods

        public override int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            return NOERROR;
        }

        public override int DecideAllocator(IMemInputPinImpl pPin, out IntPtr ppAlloc)
        {
            ppAlloc = IntPtr.Zero;

            HRESULT hr = (HRESULT)pPin.NotifyAllocator((m_Filter as InfTeeFilter).Input.AllocatorPtr, true);
            if (hr.Failed) return hr;

            ppAlloc = (m_Filter as InfTeeFilter).Input.AllocatorPtr;
            Marshal.AddRef(ppAlloc);

            return hr;
        }

        public override int CheckMediaType(AMMediaType pmt)
        {
            lock (m_Lock)
            {
                HRESULT hr = NOERROR;
                if (m_bInsideCheckMediaType == true) return hr;

                m_bInsideCheckMediaType = true;

                if (!(m_Filter as InfTeeFilter).Input.IsConnected)
                {
                    m_bInsideCheckMediaType = false;
                    return VFW_E_NOT_CONNECTED;
                }

                hr = (HRESULT)(m_Filter as InfTeeFilter).Input.Connected.QueryAccept(pmt);
                if (hr != NOERROR)
                {
                    m_bInsideCheckMediaType = false;
                    return VFW_E_TYPE_NOT_ACCEPTED;
                }

                for (int i = 1; i < m_Filter.Pins.Count; i++)
                {
                    InfTeeOutputPin _pin = (m_Filter.Pins[i] as InfTeeOutputPin);
                    if (_pin.IsConnected && !object.ReferenceEquals(this, _pin))
                    {
                        hr = (HRESULT)_pin.QueryAccept(pmt);
                        if (hr != NOERROR)
                        {
                            m_bInsideCheckMediaType = false;
                            return VFW_E_TYPE_NOT_ACCEPTED;
                        }
                    }
                }

            }
            m_bInsideCheckMediaType = false;
            return NOERROR;
        }

        public override int EnumMediaTypes(out IntPtr ppEnum)
        {
            lock (m_Lock)
            {
                ppEnum = IntPtr.Zero;
                if (!(m_Filter as InfTeeFilter).Input.IsConnected)
                {
                    return VFW_E_NOT_CONNECTED;
                }
                return (m_Filter as InfTeeFilter).Input.Connected.EnumMediaTypes(out ppEnum);
            }
        }

        public override int SetMediaType(AMMediaType mt)
        {
            lock (m_Lock)
            {
                if (!(m_Filter as InfTeeFilter).Input.IsConnected)
                {
                    return VFW_E_NOT_CONNECTED;
                }
                return base.SetMediaType(mt);
            }
        }

        public override int CompleteConnect(ref IPinImpl pReceivePin)
        {
            lock (m_Lock)
            {
                HRESULT hr = (HRESULT)base.CompleteConnect(ref pReceivePin);
                if (hr.Failed) return hr;

                if (m_mt != (m_Filter as InfTeeFilter).Input.CurrentMediaType)
                {
                    hr = (HRESULT)m_Filter.ReconnectPin((m_Filter as InfTeeFilter).Input.Connected.UnknownPtr, m_mt);
                    if (FAILED(hr))
                    {
                        return hr;
                    }
                }

                int nCount = m_Filter.Pins.Count - 1;
                for (int i = 1; i < m_Filter.Pins.Count; i++)
                {
                    if (m_Filter.Pins[i].IsConnected) nCount--;
                }
                if (nCount == 0)
                {
                    (m_Filter as InfTeeFilter).AddOutputPin();
                }
                return NOERROR;
            }
        }

        public override int Active()
        {
            lock (m_Lock)
            {
                if (!IsConnected) return NOERROR;
                if (m_pOutputQueue == null)
                {
                    m_pOutputQueue = new OutputQueue(m_ConnectedPin);
                }
                return base.Active();
            }
        }

        public override int Inactive()
        {
            lock (m_Lock)
            {
                if (m_pOutputQueue != null)
                {
                    m_pOutputQueue.Dispose();
                    m_pOutputQueue = null;
                }
                return base.Inactive();
            }
        }

        public override int Deliver(ref IMediaSampleImpl _sample)
        {
            if (m_pOutputQueue == null) return NOERROR;

            return m_pOutputQueue.Receive(ref _sample);
        }

        public override int Notify(IntPtr pSelf, Quality q)
        {
            if (m_Filter.Pins.IndexOf(this) == 1)
            {
                if ((m_Filter as InfTeeFilter).Input.IsConnected)
                {
                    IntPtr _ptr;
                    Guid _guid = typeof(IQualityControl).GUID;
                    if (S_OK == (m_Filter as InfTeeFilter).Input.Connected._QueryInterface(ref _guid,out _ptr))
                    {
                        IQualityControl _qcontrol = (IQualityControl)Marshal.GetObjectForIUnknown(_ptr);
                        _qcontrol.Notify(Marshal.GetIUnknownForObject(m_Filter), q);
                        Marshal.Release(_ptr);
                    }
                }
            }
            return NOERROR;
        }

        public override int DeliverEndOfStream()
        {
            if (m_pOutputQueue == null) return NOERROR;
            m_pOutputQueue.EOS();
            return NOERROR;
        }

        public override int DeliverBeginFlush()
        {
            if (m_pOutputQueue == null) return NOERROR;
            m_pOutputQueue.BeginFlush();
            return NOERROR;
        }

        public override int DeliverEndFlush()
        {
            if (m_pOutputQueue == null) return NOERROR;
            m_pOutputQueue.EndFlush();
            return NOERROR;
        }

        public override int DeliverNewSegment(long tStart, long tStop, double dRate)
        {
            if (m_pOutputQueue == null) return NOERROR;
            m_pOutputQueue.NewSegment(tStart, tStop, dRate);
            return NOERROR;
        }

        #endregion
    }

    [ComVisible(true)]
    [Guid("F45BC9DF-C9FD-42ce-8BED-9A2DDE053DF9")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AboutForm))]
    public class InfTeeFilter : BaseFilter
    {
        #region Properties

        public InfTeeInputPin Input
        {
            get { return (InfTeeInputPin)Pins[0]; }
        }

        #endregion

        #region Constructor

        public InfTeeFilter()
            : base("CSharp Inf Tee Filter")
        {

        }

        #endregion

        #region Overridden Methods

        protected override int OnInitializePins()
        {
            AddPin(new InfTeeInputPin("In", this));
            AddOutputPin();
            return NOERROR;
        }

        public override int Run(long tStart)
        {
            lock (m_Lock)
            {
                int hr = base.Run(tStart);
                if (!Input.IsConnected)
                {
                    Input.EndOfStream();
                }
                return hr;
            }
        }

        public override int Pause()
        {
            lock (m_Lock)
            {
                int hr = base.Pause();
                if (!Input.IsConnected)
                {
                    Input.EndOfStream();
                }
                return hr;
            }
        }

        public override int Stop()
        {
            int hr = base.Stop();
            m_State = FilterState.Stopped;
            return hr;
        }

        #endregion

        #region Methods

        public void AddOutputPin()
        {
            AddPin(new InfTeeOutputPin("Output", this));
        }

        #endregion
    }

    #endregion

    #region Null Renderer

    [ComVisible(true)]
    [Guid("8DE31E85-10FC-4088-8861-E0EC8E70744A")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AboutForm))]
    public class NullRendererFilter : BaseRendererFilter
    {
        #region Constructor

        public NullRendererFilter()
            : base("CSharp Null Renderer Filter")
        {

        }

        #endregion

        #region Overridden Methods

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

        #endregion
    }

    #endregion

    #region WAVE Splitter

    [ComVisible(false)]
    public class WaveTrack : DemuxTrack
    {
        #region Variables

        protected AMMediaType m_mt = null;
        protected long m_ullReadPosition = 0;
        protected int m_lSampleSize = 0;
        protected long m_rtMediaPosition = 0;

        #endregion

        #region Constructor

        public WaveTrack(WaveParser _parser, AMMediaType mt)
            :base (_parser,TrackType.Audio)
        {
            m_mt = mt;
            WaveFormatEx _wfx = m_mt;
            m_lSampleSize = _wfx.nAvgBytesPerSec / 2;
        }

        #endregion

        #region Overridden Methods

        public override HRESULT SetMediaType(AMMediaType pmt)
        {
            if (pmt.majorType != m_mt.majorType) return VFW_E_TYPE_NOT_ACCEPTED;
            if (pmt.formatPtr == IntPtr.Zero) return VFW_E_INVALIDMEDIATYPE;
            if (pmt.subType != m_mt.subType) return VFW_E_TYPE_NOT_ACCEPTED;
            if (pmt.formatType != m_mt.formatType) return VFW_E_TYPE_NOT_ACCEPTED;

            return NOERROR;
        }

        public override HRESULT GetMediaType(int iPosition, ref AMMediaType pmt)
        {
            if (iPosition < 0) return E_INVALIDARG;
            if (iPosition > 0) return VFW_S_NO_MORE_ITEMS;
            pmt.Set(m_mt);
            return NOERROR;
        }

        public override HRESULT SeekTrack(long _time)
        {
            WaveParser pParser = (WaveParser)m_pParser;
            if (_time <= 0 || _time > pParser.Duration)
            {
                m_ullReadPosition = pParser.DataOffset;
            }
            else
            {
                WaveFormatEx _wfx = m_mt;
                if (pParser.Duration > 0)
                {
                    m_ullReadPosition = (pParser.Stream.TotalSize - pParser.DataOffset) * _time / pParser.Duration;
                    if (_wfx.nBlockAlign != 0)
                    {
                        m_ullReadPosition -= m_ullReadPosition % _wfx.nBlockAlign;
                    }
                }
            }
	        m_rtMediaPosition = _time;
            return base.SeekTrack(_time);
        }

        public override PacketData GetNextPacket()
        {
            if (m_ullReadPosition < m_pParser.Stream.TotalSize)
            {
                PacketData _data = new PacketData();
                _data.Position = m_ullReadPosition;
                _data.Size = m_lSampleSize;
                _data.SyncPoint = true;
                _data.Start = m_rtMediaPosition;
                _data.Stop = _data.Start + UNITS / 2;
                m_ullReadPosition += m_lSampleSize;
                m_rtMediaPosition = _data.Stop;
                return _data;
            }
            return null;
        }

        #endregion
    }

    [ComVisible(false)]
    public class WaveParser : FileParser
    {
        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        private class OUTPUT_DATA_HEADER
        {
            public uint dwData = 0;
            public uint dwDataLength = 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private class OUTPUT_FILE_HEADER
        {
            public uint dwRiff = 0;
            public uint dwFileSize = 0;
            public uint dwWave = 0;
            public uint dwFormat = 0;
            public uint dwFormatLength = 0;
        }

        #endregion

        #region Constants

        private const uint RIFF_TAG = 0x46464952;
        private const uint WAVE_TAG = 0x45564157;
        private const uint FMT__TAG = 0x20746D66;
        private const uint DATA_TAG = 0x61746164;
        private const uint WAVE_FORMAT_PCM = 0x01;

        #endregion

        #region Variables

        protected long	m_llDataOffset = 0;

        #endregion

        #region Constructor

        public WaveParser()
            : base(false)
        {

        }

        #endregion

        #region Properties

        public long DataOffset
        {
            get { return m_llDataOffset; }
        }

        #endregion

        #region Overridden Methods

        protected override HRESULT CheckFile()
        {
            m_Stream.Seek(0);
            OUTPUT_FILE_HEADER _header = (OUTPUT_FILE_HEADER)m_Stream.ReadValue(typeof(OUTPUT_FILE_HEADER));
            if (_header != null)
            {
                if (_header.dwRiff == RIFF_TAG && _header.dwWave == WAVE_TAG && _header.dwFormat == FMT__TAG)
                {
                    return NOERROR;
                }
            }
            return S_FALSE;
        }

        protected override HRESULT LoadTracks()
        {
            m_Stream.Seek(0);
            OUTPUT_FILE_HEADER _header = (OUTPUT_FILE_HEADER)m_Stream.ReadValue(typeof(OUTPUT_FILE_HEADER));
            if (_header.dwRiff == RIFF_TAG && _header.dwWave == WAVE_TAG && _header.dwFormat == FMT__TAG)
            {
                WaveFormatEx pwfx = (WaveFormatEx)m_Stream.ReadValue<WaveFormatEx>((int)_header.dwFormatLength);
                if (pwfx == null) return E_UNEXPECTED;
                if (pwfx.nBlockAlign == 0)
                {
                    pwfx.nBlockAlign = (ushort)(pwfx.nChannels * pwfx.wBitsPerSample / 8);
                }
                if (pwfx.nAvgBytesPerSec == 0)
                {
                    pwfx.nAvgBytesPerSec = pwfx.nSamplesPerSec * pwfx.nBlockAlign;
                }
                AMMediaType mt = new AMMediaType();
                mt.majorType = MediaType.Audio;
                mt.subType = MediaSubType.PCM;
                mt.sampleSize = pwfx.nBlockAlign;
                mt.fixedSizeSamples = true;
                mt.SetFormat(pwfx);
                m_Tracks.Add(new WaveTrack(this, mt));
                HRESULT hr = E_UNEXPECTED;
                OUTPUT_DATA_HEADER _data = (OUTPUT_DATA_HEADER)m_Stream.ReadValue<OUTPUT_DATA_HEADER>();
                if (_data.dwData == DATA_TAG)
                {
                    hr = NOERROR;
                    m_llDataOffset = m_Stream.Position;
                    if (pwfx.nAvgBytesPerSec != 0)
                    {
                        m_rtDuration = (UNITS * (m_Stream.TotalSize - m_llDataOffset)) / pwfx.nAvgBytesPerSec;
                    }
                }
                return hr;
            }
            return S_FALSE;
        }

        #endregion
    }

    [ComVisible(true)]
    [Guid("E097F784-8D20-4dd7-A2C8-D54350DBBE99")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AboutForm))]
    public class WAVESplitterFilter : BaseSplitterFilterTemplate<WaveParser>
    {
        public WAVESplitterFilter()
            : base("CSharp WAVE Splitter")
        {
        }
    }

    [ComVisible(true)]
    [Guid("0C9925E8-F4D6-4d27-9253-A09F9D9123A6")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(AboutForm))]
    public class WAVESourceFilter : BaseSourceFilterTemplate<WaveParser>
    {
        public WAVESourceFilter()
            : base("CSharp WAVE Source")
        {
        }
    }

    #endregion

    #region DSNetwork

    [ComVisible(true)]
    [System.Security.SuppressUnmanagedCodeSecurity]
    [Guid("96D8A0B7-8EE7-4325-98D4-BB7C66F27B1A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface INetworkConfig
    {
        string IP { get; set; }
        int Port { get; set; }
    }

    [ComVisible(true)]
    [Guid("E15A7277-13C6-4b05-A50C-C6E85E4348C8")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(NetworkForm),typeof(AboutForm))]
    public class NetworkSyncFilter : BaseRendererFilter, INetworkConfig
    {
        #region Variables

        private object m_csLockNetwork = new object();
        private string m_sIP = "224.0.0.1";
        private int m_iPort = 1234;
        private IPEndPoint m_EndPoint = null;
        private Socket m_Socket = null;
        private MemoryStream m_Stream = null;

        #endregion

        #region Constructor

        public NetworkSyncFilter()
            : base("CSharp Network Sync Filter")
        {
        }

        ~NetworkSyncFilter()
        {
            if (m_Socket != null)
            {
                m_Socket.Close();
            }
        }

        #endregion

        #region Overridden Methods

        public override int Pause()
        {
            if (m_State == FilterState.Stopped)
            {
                m_Stream = new MemoryStream();
                Connect();
            }
            return base.Pause();
        }

        public override int Stop()
        {
            int hr = base.Stop();
            if (m_Stream != null)
            {
                m_Stream.Dispose();
                m_Stream = null;
            }
            return hr;
        }

        public override int CheckMediaType(AMMediaType pmt)
        {
            if (pmt.majorType != MediaType.Video)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (pmt.subType != MediaSubType.RGB24)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (pmt.formatType != FormatType.VideoInfo)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            if (pmt.formatPtr == IntPtr.Zero)
            {
                return VFW_E_TYPE_NOT_ACCEPTED;
            }
            return NOERROR;
        }

        public override int DoRenderSample(ref IMediaSampleImpl pMediaSample)
        {
            if (m_Stream != null)
            {
                IntPtr _ptr;
                pMediaSample.GetPointer(out _ptr);
                BitmapInfoHeader _bmi = (BitmapInfoHeader)InputPin.CurrentMediaType;
                PixelFormat _format = (_bmi.BitCount == 32 ? PixelFormat.Format32bppRgb : PixelFormat.Format24bppRgb);
                Bitmap _bmp = new Bitmap(_bmi.Width, Math.Abs(_bmi.Height), _bmi.Width * _bmi.BitCount / 8, _format, _ptr);
                m_Stream.SetLength(0);
                _bmp.Save(m_Stream, ImageFormat.Jpeg);
                lock (m_csLockNetwork)
                {
                    if (m_EndPoint != null)
                    {
                        m_Stream.Position = 0;
                        try
                        {
                            m_Socket.SendBufferSize = (int)m_Stream.Length;
                            m_Socket.SendTo(m_Stream.ToArray(), m_EndPoint);
                        }
                        catch (Exception _exception)
                        {
                            TRACE(_exception.Message);
                            m_EndPoint = null;
                        }
                    }
                }
            }
            return NOERROR;
        }

        #endregion

        #region Methods

        public void Connect()
        {
            lock (m_csLockNetwork)
            {
                if (m_Socket != null)
                {
                    m_Socket.Close();
                    m_Socket = null;
                }
                m_EndPoint = null;
                try
                {
                    m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                    m_Socket.MulticastLoopback = true;
                    m_Socket.EnableBroadcast = true;
                    IPAddress _ip = IPAddress.Parse(m_sIP);
                    m_Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(_ip));
                    m_Socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 5);
                    m_EndPoint = new IPEndPoint(_ip, m_iPort);
                }
                catch (Exception _exception)
                {
                    TRACE(_exception.Message);
                }
            }
        }

        #endregion

        #region INetworkConfig Members

        public string IP
        {
            get
            {
                return m_sIP;
            }
            set
            {
                if (m_sIP != value)
                {
                    m_sIP = value;
                    if (m_State != FilterState.Stopped)
                    {
                        Connect();
                    }
                }
            }
        }

        public int Port
        {
            get
            {
                return m_iPort;
            }
            set
            {
                if (m_iPort != value)
                {
                    m_iPort = value;
                    if (m_State != FilterState.Stopped)
                    {
                        Connect();
                    }
                }
            }
        }

        #endregion
    }

    [ComVisible(true)]
    public class NetworkStream : SourceStream
    {
        #region Constructor

        public NetworkStream(string _name, NetworkSourceFilter _filter)
            :base(_name,_filter)
        {
        }

        #endregion

        #region Overridden Methods

        public override int GetMediaType(ref AMMediaType pMediaType)
        {
            return (m_Filter as NetworkSourceFilter).GetMediaType(ref pMediaType);
        }

        public override int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            if (!IsConnected) return VFW_E_NOT_CONNECTED;
            return (m_Filter as NetworkSourceFilter).DecideBufferSize(ref pAlloc, ref prop);
        }

        public override int FillBuffer(ref IMediaSampleImpl pSample)
        {
            return (m_Filter as NetworkSourceFilter).FillBuffer(ref pSample);
        }

        #endregion
    }

    [ComVisible(true)]
    [Guid("FDE01FF1-D110-46a8-AB87-2341E4809DE5")]
    [AMovieSetup(true)]
    [PropPageSetup(typeof(NetworkForm), typeof(AboutForm))]
    public class NetworkSourceFilter : BaseSourceFilter, INetworkConfig
    {
        #region Variables

        private string m_sIP = "224.0.0.1";
        private int m_iPort = 1234;
        protected object m_csLockNetwork = new object();
        protected Image m_pBitmap = null;
        protected long m_nAvgTimePerFrame = UNITS / 20;
        protected long m_lLastSampleTime = 0;
        protected Thread m_ReceiveThread = null;
        protected ManualResetEvent m_evQuit = new ManualResetEvent(false);

        #endregion

        #region Constructor

        public NetworkSourceFilter()
            :base("CSharp Network Source Filter")
        {

        }

        ~NetworkSourceFilter()
        {
            if (m_pBitmap != null)
            {
                m_pBitmap.Dispose();
                m_pBitmap = null;
            }
        }

        #endregion

        #region Overridden Methods

        protected override int OnInitializePins()
        {
            AddPin(new NetworkStream("Output", this));
            return NOERROR;
        }

        public override int Pause()
        {
            if (m_State == FilterState.Stopped)
            {
                m_lLastSampleTime = 0;
            }
            return base.Pause();
        }

        public override int JoinFilterGraph(IntPtr pGraph, string pName)
        {
            if (pGraph != IntPtr.Zero)
            {
                StartThread();
            }
            else
            {
                StopThread();
            }
            return base.JoinFilterGraph(pGraph, pName);
        }

        #endregion

        #region Methods

        public void StartThread()
        {
            if (m_ReceiveThread == null || !m_ReceiveThread.IsAlive)
            {
                m_evQuit.Reset();
                m_ReceiveThread = new Thread(new ThreadStart(ReceiveThreadProc));
                m_ReceiveThread.Start();
            }
        }

        public void StopThread()
        {
            if (m_ReceiveThread != null)
            {
                m_evQuit.Set();
                if (!m_ReceiveThread.Join(1000))
                {
                    m_ReceiveThread.Abort();
                }
                m_ReceiveThread = null;
            }
        }

        public void Connect()
        {
            StopThread();
            StartThread();
        }

        private void ReceiveThreadProc()
        {
            string _ip = m_sIP;
            int _port = m_iPort;
            Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            IPEndPoint _EndPoint = new IPEndPoint(IPAddress.Any, _port);
            _socket.Bind(_EndPoint);
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddress.Parse(_ip)));
            Stream _stream = new MemoryStream();
            byte[] _data = new byte[1920 * 1080 * 4];
            try
            {
                while (!m_evQuit.WaitOne(0, false))
                {
                    EndPoint ep = _EndPoint;
                    int iLength = _socket.ReceiveFrom(_data, SocketFlags.None, ref ep);
                    _stream.SetLength(0);
                    _stream.Write(_data, 0, _data.Length);
                    _stream.Position = 0;
                    Image img = Image.FromStream(_stream);
                    if (img != null)
                    {
                        lock (m_csLockNetwork)
                        {
                            if (m_pBitmap != null)
                            {
                                m_pBitmap.Dispose();
                            }
                            m_pBitmap = img;
                        }
                    }
                }
            }
            catch (Exception _exception)
            {
                if (!(_exception is ThreadAbortException))
                {
                    TRACE(_exception.Message);
                }
            }
            _socket.Close();
            _socket = null;
            _stream.Dispose();
        }

        public int GetMediaType(ref AMMediaType pMediaType)
        {
            lock (m_csLockNetwork)
            {
                if (m_pBitmap == null) return E_UNEXPECTED;
                pMediaType.majorType = DirectShow.MediaType.Video;
                pMediaType.subType = DirectShow.MediaSubType.RGB32;
                pMediaType.formatType = DirectShow.FormatType.VideoInfo;

                VideoInfoHeader vih = new VideoInfoHeader();
                vih.AvgTimePerFrame = m_nAvgTimePerFrame;
                vih.BmiHeader = new BitmapInfoHeader();
                vih.BmiHeader.Size = Marshal.SizeOf(typeof(BitmapInfoHeader));
                vih.BmiHeader.Compression = 0;
                vih.BmiHeader.BitCount = 32;
                vih.BmiHeader.Width = m_pBitmap.Width;
                vih.BmiHeader.Height = m_pBitmap.Height;
                vih.BmiHeader.Planes = 1;
                vih.BmiHeader.ImageSize = vih.BmiHeader.Width * vih.BmiHeader.Height * vih.BmiHeader.BitCount / 8;
                vih.SrcRect = new DsRect();
                vih.TargetRect = new DsRect();
                AMMediaType.SetFormat(ref pMediaType, ref vih);
                pMediaType.fixedSizeSamples = true;
                pMediaType.sampleSize = vih.BmiHeader.ImageSize;
            }
            return NOERROR;
        }

        public int DecideBufferSize(ref IMemAllocatorImpl pAlloc, ref AllocatorProperties prop)
        {
            AllocatorProperties _actual = new AllocatorProperties();

            BitmapInfoHeader _bmi = (BitmapInfoHeader)Pins[0].CurrentMediaType;
            prop.cbBuffer = _bmi.GetBitmapSize();
            if (prop.cbBuffer < _bmi.ImageSize)
            {
                prop.cbBuffer = _bmi.ImageSize;
            }
            prop.cBuffers = 1;

            int hr = pAlloc.SetProperties(prop, _actual);
            return hr;
        }

        public int FillBuffer(ref IMediaSampleImpl _sample)
        {
            BitmapInfoHeader _bmi = (BitmapInfoHeader)Pins[0].CurrentMediaType;
            
            IntPtr _ptr;
            _sample.GetPointer(out _ptr);
            Bitmap _bmp = new Bitmap(_bmi.Width, _bmi.Height, _bmi.Width * 4, PixelFormat.Format32bppRgb, _ptr);
            Graphics _graphics = Graphics.FromImage(_bmp);
            lock (m_csLockNetwork)
            {
                _graphics.DrawImage(m_pBitmap, new Rectangle(0, 0, _bmp.Width, _bmp.Height), 0, 0, m_pBitmap.Width, m_pBitmap.Height, GraphicsUnit.Pixel);
            }
            _graphics.Dispose();
            _bmp.Dispose();
            _sample.SetActualDataLength(_bmi.ImageSize);
            _sample.SetSyncPoint(true);
            long _stop = m_lLastSampleTime + m_nAvgTimePerFrame;
            _sample.SetTime((DsLong)m_lLastSampleTime, (DsLong)_stop);
            m_lLastSampleTime = _stop;
            return NOERROR;
        }

        #endregion

        #region INetworkConfig Members

        public string IP
        {
            get
            {
                return m_sIP;
            }
            set
            {
                if (m_sIP != value)
                {
                    m_sIP = value;
                    Connect();
                }
            }
        }

        public int Port
        {
            get
            {
                return m_iPort;
            }
            set
            {
                if (m_iPort != value)
                {
                    m_iPort = value;
                    Connect();
                }
            }
        }

        #endregion
    }

    #endregion
}
*/