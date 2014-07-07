using MediaFoundation;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace VideoDecoders.MediaFoundation
{
    public class IMFByteStreamWrapper : Stream
    {
        private readonly IMFByteStream _byteStream;
        private readonly MFByteStreamCapabilities _capabilities;
        private readonly StreamMetadata _metadata;

        public IMFByteStreamWrapper(IMFByteStream byteStream)
        {
            _byteStream = byteStream;
            TestSuccess("Could not read byte stream capabilities", _byteStream.GetCapabilities(out _capabilities));

            _metadata = new StreamMetadata
            {
                CanPause = true,
                CanSeek = _capabilities.HasFlag(MFByteStreamCapabilities.IsSeekable),
                HasSlowSeek = _capabilities.HasFlag(MFByteStreamCapabilities.HasSlowSeek)
            };
        }

        public StreamMetadata Metadata { get { return _metadata; } }

        public override bool CanRead
        {
            get { return _capabilities.HasFlag(MFByteStreamCapabilities.IsReadable); }
        }

        public override bool CanSeek
        {
            get { return _capabilities.HasFlag(MFByteStreamCapabilities.IsSeekable); }
        }

        public override bool CanWrite
        {
            get { return _capabilities.HasFlag(MFByteStreamCapabilities.IsWritable); }
        }

        public override long Position
        {
            get
            {
                long position;
                TestSuccess("Could not get bytestream current position", _byteStream.GetCurrentPosition(out position));
                return position;
            }
            set
            {
                TestSuccess("Could not set bytestream current position", _byteStream.SetCurrentPosition(value));
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int readAmount;
            var pinned = GCHandle.Alloc(buffer , GCHandleType.Pinned);
            var address = pinned.AddrOfPinnedObject();
            var result = _byteStream.Read(IntPtr.Add(address, offset), count, out readAmount);
            pinned.Free();
            TestSuccess("Could not read bytestream", result);
            return readAmount;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            MFByteStreamSeekOrigin ori;
            switch(origin)
            {
                case SeekOrigin.Begin:
                    ori = MFByteStreamSeekOrigin.Begin;
                    break;
                case SeekOrigin.Current:
                    ori = MFByteStreamSeekOrigin.Current;
                    break;
                default:
                    throw new ArgumentException("Can only seek IMFByteStream from start or current");
            }

            long position;
            TestSuccess("Could not seek IMFByteStream", _byteStream.Seek(ori, offset, MFByteStreamSeekingFlags.None, out position));
            return position;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        private void TestSuccess(string message, int hResult)
        {
            if(hResult < 0)
            {
                throw new COMException(message, hResult);
            }
        }
    }
}
