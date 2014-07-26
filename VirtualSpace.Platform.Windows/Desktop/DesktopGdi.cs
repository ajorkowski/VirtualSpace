using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using VirtualSpace.Core;
using VirtualSpace.Core.Desktop;
using VirtualSpace.Platform.Windows.Rendering.Screen;

namespace VirtualSpace.Platform.Windows.Screen
{
    internal sealed class DesktopGdi : IDesktop, IScreenSource
    {
        private readonly List<IDisposable> _toDispose;

        private SharpDX.Direct3D11.Texture2D _gdiTexture;
        private SharpDX.Direct3D11.Texture2D _sharedTexture;

        private IntPtr _desktopDC;
        private IntPtr _memDC;
        private IntPtr _bitmap;
        private int _nScreenWidth;
        private int _nScreenHeight;

        private SharpDX.Direct3D11.Device _captureDevice;
        private KeyedMutex _mutex;

        private bool _isRunning;
        private Task _captureLoop;

        public DesktopGdi()
        {
            _toDispose = new List<IDisposable>();

            _nScreenWidth = GetSystemMetrics(SystemMetric.SM_CXSCREEN);
            _nScreenHeight = GetSystemMetrics(SystemMetric.SM_CYSCREEN);

            _desktopDC = GetDC(IntPtr.Zero);
            AddDisposable(new Disposable(() => { ReleaseDC(IntPtr.Zero, _desktopDC); }));

            _memDC = CreateCompatibleDC(_desktopDC);
            AddDisposable(new Disposable(() => { DeleteObject(_memDC); }));

            _bitmap = CreateCompatibleBitmap(_desktopDC, _nScreenWidth, _nScreenHeight);
            AddDisposable(new Disposable(() => { DeleteObject(_bitmap); }));

            var handle = SelectObject(_memDC, _bitmap);
            AddDisposable(new Disposable(() => { DeleteObject(handle); }));
        }

        public ScreenOutput GetOutput(SharpDX.Direct3D11.Device device)
        {
            // Base description
            var sharedDesc = new Texture2DDescription
            {
                CpuAccessFlags = CpuAccessFlags.None,
                BindFlags = BindFlags.ShaderResource,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                Height = _nScreenHeight,
                Width = _nScreenWidth,
                OptionFlags = ResourceOptionFlags.SharedKeyedmutex,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default
            };

            var renderTexture = AddDisposable(new SharpDX.Direct3D11.Texture2D(device, sharedDesc));

            _captureDevice = AddDisposable(new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware));
            using (var sharedResource = renderTexture.QueryInterface<SharpDX.DXGI.Resource1>())
            {
                _sharedTexture = AddDisposable(_captureDevice.OpenSharedResource<Texture2D>(sharedResource.SharedHandle));
            }
            _mutex = AddDisposable(_sharedTexture.QueryInterface<KeyedMutex>());

            // The shared Gdi textures
            sharedDesc.CpuAccessFlags = CpuAccessFlags.Write;
            sharedDesc.OptionFlags = ResourceOptionFlags.None;
            sharedDesc.Usage = ResourceUsage.Dynamic;
            _gdiTexture = AddDisposable(new SharpDX.Direct3D11.Texture2D(_captureDevice, sharedDesc));

            _isRunning = true;
            _captureLoop = Task.Run(() => CaptureLoop());

            return new ScreenOutput
            {
                Texture = renderTexture
            };
        }

        public void Update(SharpDX.Toolkit.GameTime gameTime)
        {
            // Do not need to do anything here, we are bitblt to another texture and then transfering to shared texture
        }

        private void CaptureLoop()
        {
            BITMAPINFO info = new BITMAPINFO();
            info.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            info.bmiHeader.biWidth = _nScreenWidth;
            info.bmiHeader.biHeight = -_nScreenHeight;
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;
            info.bmiHeader.biCompression = BitmapCompressionMode.BI_RGB;
            info.bmiHeader.biSizeImage = 0;
            info.bmiHeader.biXPelsPerMeter = 0;
            info.bmiHeader.biYPelsPerMeter = 0;
            info.bmiHeader.biClrUsed = 0;
            info.bmiHeader.biClrImportant = 0;

            byte[] data = new byte[((_nScreenWidth * info.bmiHeader.biBitCount + 31) / 32) * 4 * _nScreenHeight];

            using (var context = _captureDevice.ImmediateContext)
            {
                while (_isRunning)
                {
                    // BitBlt to memory... for some reason grabbing surfaceDC on a texture uses WAY too much memory
                    BitBlt(_memDC, 0, 0, _nScreenWidth, _nScreenHeight, _desktopDC, 0, 0, TernaryRasterOperations.SRCCOPY | TernaryRasterOperations.CAPTUREBLT);

                    // Get mouse info
                    CURSORINFO pci;
                    pci.cbSize = Marshal.SizeOf(typeof(CURSORINFO));
                    GetCursorInfo(out pci);

                    DrawIcon(_memDC, pci.ptScreenPos.x, pci.ptScreenPos.y, pci.hCursor);

                    // Get the bytes...
                    GetDIBits(_memDC, _bitmap, (uint)0, (uint)_nScreenHeight, data, ref info, DIB_Color_Mode.DIB_RGB_COLORS);

                    // Map data to the shared resource...
                    GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
                    IntPtr dataPointer = pinnedArray.AddrOfPinnedObject();

                    var box = context.MapSubresource(_gdiTexture, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
                    Utilities.CopyMemory(box.DataPointer, dataPointer, data.Length);
                    context.UnmapSubresource(_gdiTexture, 0);

                    pinnedArray.Free();

                    var result = _mutex.Acquire(0, 1000);
                    if (result != Result.WaitTimeout && result != Result.Ok)
                    {
                        throw new SharpDXException(result);
                    }

                    if (result == Result.Ok)
                    {
                        context.CopyResource(_gdiTexture, _sharedTexture);
                        _mutex.Release(0);
                    }
                }
            }
        }

        private T AddDisposable<T>(T toDisopse)
            where T : IDisposable
        {
            _toDispose.Add(toDisopse);
            return toDisopse;
        }

        public void Dispose()
        {
            _isRunning = false;
            if (_captureLoop != null)
            {
                if (_captureLoop.Status == TaskStatus.Running)
                {
                    _captureLoop.Wait();
                }
                _captureLoop.Dispose();
                _captureLoop = null;
            }

            foreach (var d in _toDispose)
            {
                d.Dispose();
            }
            _toDispose.Clear();

            GC.SuppressFinalize(this);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(SystemMetric smIndex);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public Int32 x;
            public Int32 y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORINFO
        {
            public Int32 cbSize;        // Specifies the size, in bytes, of the structure. 
            // The caller must set this to Marshal.SizeOf(typeof(CURSORINFO)).
            public Int32 flags;         // Specifies the cursor state. This parameter can be one of the following values:
            //    0             The cursor is hidden.
            //    CURSOR_SHOWING    The cursor is showing.
            public IntPtr hCursor;          // Handle to the cursor. 
            public POINT ptScreenPos;       // A POINT structure that receives the screen coordinates of the cursor. 
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        private static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);

        private const Int32 CURSOR_SHOWING = 0x00000001;

        [DllImport("gdi32.dll", EntryPoint = "BitBlt", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BitBlt([In] IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, [In] IntPtr hdcSrc, int nXSrc, int nYSrc, TernaryRasterOperations dwRop);

        [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleDC", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC([In] IntPtr hdc);

        [DllImport("gdi32.dll", EntryPoint = "CreateCompatibleBitmap")]
        private static extern IntPtr CreateCompatibleBitmap([In] IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll", EntryPoint = "SelectObject", SetLastError = true)]
        private static extern IntPtr SelectObject([In] IntPtr hdc, [In] IntPtr hgdiobj);

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        private static extern bool DeleteObject([In] IntPtr hObject);

        [DllImport("gdi32.dll", EntryPoint = "GetDIBits")]
        private static extern int GetDIBits([In] IntPtr hdc, [In] IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, ref BITMAPINFO lpbi, DIB_Color_Mode uUsage);

        [StructLayoutAttribute(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            /// <summary>
            /// A BITMAPINFOHEADER structure that contains information about the dimensions of color format.
            /// </summary>
            public BITMAPINFOHEADER bmiHeader;

            /// <summary>
            /// An array of RGBQUAD. The elements of the array that make up the color table.
            /// </summary>
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 1, ArraySubType = UnmanagedType.Struct)]
            public RGBQUAD[] bmiColors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RGBQUAD
        {
            public byte rgbBlue;
            public byte rgbGreen;
            public byte rgbRed;
            public byte rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public BitmapCompressionMode biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;

            public void Init()
            {
                biSize = (uint)Marshal.SizeOf(this);
            }
        }

        private enum DIB_Color_Mode : uint
        {
            DIB_RGB_COLORS = 0,
            DIB_PAL_COLORS = 1
        }

        private enum BitmapCompressionMode : uint
        {
            BI_RGB = 0,
            BI_RLE8 = 1,
            BI_RLE4 = 2,
            BI_BITFIELDS = 3,
            BI_JPEG = 4,
            BI_PNG = 5
        }

        private enum SystemMetric
        {
            SM_CXSCREEN = 0,  // 0x00
            SM_CYSCREEN = 1,  // 0x01
            SM_CXVSCROLL = 2,  // 0x02
            SM_CYHSCROLL = 3,  // 0x03
            SM_CYCAPTION = 4,  // 0x04
            SM_CXBORDER = 5,  // 0x05
            SM_CYBORDER = 6,  // 0x06
            SM_CXDLGFRAME = 7,  // 0x07
            SM_CXFIXEDFRAME = 7,  // 0x07
            SM_CYDLGFRAME = 8,  // 0x08
            SM_CYFIXEDFRAME = 8,  // 0x08
            SM_CYVTHUMB = 9,  // 0x09
            SM_CXHTHUMB = 10, // 0x0A
            SM_CXICON = 11, // 0x0B
            SM_CYICON = 12, // 0x0C
            SM_CXCURSOR = 13, // 0x0D
            SM_CYCURSOR = 14, // 0x0E
            SM_CYMENU = 15, // 0x0F
            SM_CXFULLSCREEN = 16, // 0x10
            SM_CYFULLSCREEN = 17, // 0x11
            SM_CYKANJIWINDOW = 18, // 0x12
            SM_MOUSEPRESENT = 19, // 0x13
            SM_CYVSCROLL = 20, // 0x14
            SM_CXHSCROLL = 21, // 0x15
            SM_DEBUG = 22, // 0x16
            SM_SWAPBUTTON = 23, // 0x17
            SM_CXMIN = 28, // 0x1C
            SM_CYMIN = 29, // 0x1D
            SM_CXSIZE = 30, // 0x1E
            SM_CYSIZE = 31, // 0x1F
            SM_CXSIZEFRAME = 32, // 0x20
            SM_CXFRAME = 32, // 0x20
            SM_CYSIZEFRAME = 33, // 0x21
            SM_CYFRAME = 33, // 0x21
            SM_CXMINTRACK = 34, // 0x22
            SM_CYMINTRACK = 35, // 0x23
            SM_CXDOUBLECLK = 36, // 0x24
            SM_CYDOUBLECLK = 37, // 0x25
            SM_CXICONSPACING = 38, // 0x26
            SM_CYICONSPACING = 39, // 0x27
            SM_MENUDROPALIGNMENT = 40, // 0x28
            SM_PENWINDOWS = 41, // 0x29
            SM_DBCSENABLED = 42, // 0x2A
            SM_CMOUSEBUTTONS = 43, // 0x2B
            SM_SECURE = 44, // 0x2C
            SM_CXEDGE = 45, // 0x2D
            SM_CYEDGE = 46, // 0x2E
            SM_CXMINSPACING = 47, // 0x2F
            SM_CYMINSPACING = 48, // 0x30
            SM_CXSMICON = 49, // 0x31
            SM_CYSMICON = 50, // 0x32
            SM_CYSMCAPTION = 51, // 0x33
            SM_CXSMSIZE = 52, // 0x34
            SM_CYSMSIZE = 53, // 0x35
            SM_CXMENUSIZE = 54, // 0x36
            SM_CYMENUSIZE = 55, // 0x37
            SM_ARRANGE = 56, // 0x38
            SM_CXMINIMIZED = 57, // 0x39
            SM_CYMINIMIZED = 58, // 0x3A
            SM_CXMAXTRACK = 59, // 0x3B
            SM_CYMAXTRACK = 60, // 0x3C
            SM_CXMAXIMIZED = 61, // 0x3D
            SM_CYMAXIMIZED = 62, // 0x3E
            SM_NETWORK = 63, // 0x3F
            SM_CLEANBOOT = 67, // 0x43
            SM_CXDRAG = 68, // 0x44
            SM_CYDRAG = 69, // 0x45
            SM_SHOWSOUNDS = 70, // 0x46
            SM_CXMENUCHECK = 71, // 0x47
            SM_CYMENUCHECK = 72, // 0x48
            SM_SLOWMACHINE = 73, // 0x49
            SM_MIDEASTENABLED = 74, // 0x4A
            SM_MOUSEWHEELPRESENT = 75, // 0x4B
            SM_XVIRTUALSCREEN = 76, // 0x4C
            SM_YVIRTUALSCREEN = 77, // 0x4D
            SM_CXVIRTUALSCREEN = 78, // 0x4E
            SM_CYVIRTUALSCREEN = 79, // 0x4F
            SM_CMONITORS = 80, // 0x50
            SM_SAMEDISPLAYFORMAT = 81, // 0x51
            SM_IMMENABLED = 82, // 0x52
            SM_CXFOCUSBORDER = 83, // 0x53
            SM_CYFOCUSBORDER = 84, // 0x54
            SM_TABLETPC = 86, // 0x56
            SM_MEDIACENTER = 87, // 0x57
            SM_STARTER = 88, // 0x58
            SM_SERVERR2 = 89, // 0x59
            SM_MOUSEHORIZONTALWHEELPRESENT = 91, // 0x5B
            SM_CXPADDEDBORDER = 92, // 0x5C
            SM_DIGITIZER = 94, // 0x5E
            SM_MAXIMUMTOUCHES = 95, // 0x5F

            SM_REMOTESESSION = 0x1000, // 0x1000
            SM_SHUTTINGDOWN = 0x2000, // 0x2000
            SM_REMOTECONTROL = 0x2001, // 0x2001


            SM_CONVERTABLESLATEMODE = 0x2003,
            SM_SYSTEMDOCKED = 0x2004,
        }

        private enum TernaryRasterOperations : uint
        {
            /// <summary>dest = source</summary>
            SRCCOPY = 0x00CC0020,
            /// <summary>dest = source OR dest</summary>
            SRCPAINT = 0x00EE0086,
            /// <summary>dest = source AND dest</summary>
            SRCAND = 0x008800C6,
            /// <summary>dest = source XOR dest</summary>
            SRCINVERT = 0x00660046,
            /// <summary>dest = source AND (NOT dest)</summary>
            SRCERASE = 0x00440328,
            /// <summary>dest = (NOT source)</summary>
            NOTSRCCOPY = 0x00330008,
            /// <summary>dest = (NOT src) AND (NOT dest)</summary>
            NOTSRCERASE = 0x001100A6,
            /// <summary>dest = (source AND pattern)</summary>
            MERGECOPY = 0x00C000CA,
            /// <summary>dest = (NOT source) OR dest</summary>
            MERGEPAINT = 0x00BB0226,
            /// <summary>dest = pattern</summary>
            PATCOPY = 0x00F00021,
            /// <summary>dest = DPSnoo</summary>
            PATPAINT = 0x00FB0A09,
            /// <summary>dest = pattern XOR dest</summary>
            PATINVERT = 0x005A0049,
            /// <summary>dest = (NOT dest)</summary>
            DSTINVERT = 0x00550009,
            /// <summary>dest = BLACK</summary>
            BLACKNESS = 0x00000042,
            /// <summary>dest = WHITE</summary>
            WHITENESS = 0x00FF0062,
            /// <summary>
            /// Capture window as seen on screen.  This includes layered windows 
            /// such as WPF windows with AllowsTransparency="true"
            /// </summary>
            CAPTUREBLT = 0x40000000
        }
    }
}
