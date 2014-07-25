using SharpDX;
using SharpDX.MediaFoundation;
using SharpDX.Multimedia;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtualSpace.Platform.Windows.Video
{
    internal sealed class AudioStream : IDisposable
    {
        private const int MaxNumberOfFramesToQueue = 25;

        private readonly SourceReader _reader;
        private readonly int _sourceIndex;
        private readonly List<IDisposable> _toDispose;

        private WaveFormat _waveFormat;

        public AudioStream(SourceReader reader, int sourceIndex)
        {
            _reader = reader;
            _sourceIndex = sourceIndex;

            _toDispose = new List<IDisposable>();

            // Find supported audio types
            var supportedTypes = new List<Guid>();
            while (true)
            {
                try
                {
                    using (var nativeFormat = _reader.GetNativeMediaType(sourceIndex, supportedTypes.Count))
                    {
                        if (nativeFormat.Get(MediaTypeAttributeKeys.MajorType) != MediaTypeGuids.Audio)
                        {
                            throw new InvalidOperationException("The stream is not an audio type stream");
                        }

                        supportedTypes.Add(nativeFormat.Get(MediaTypeAttributeKeys.Subtype));
                    }
                }
                catch (SharpDXException)
                {
                    break;
                }
            }

            if (supportedTypes.Count == 0)
            {
                throw new InvalidOperationException("No output types for audio supported...");
            }

            using (var audioFormat = new MediaType())
            {
                audioFormat.Set(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
                audioFormat.Set(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Float); // for XAudio2
                _reader.SetCurrentMediaType(sourceIndex, audioFormat);
            }

            using (var currentFormat = _reader.GetCurrentMediaType(sourceIndex))
            {
                int waveFormatLength;
                _waveFormat = currentFormat.ExtracttWaveFormat(out waveFormatLength);
            }
        }

        public void Dispose()
        {
            foreach (var d in _toDispose)
            {
                d.Dispose();
            }
            _toDispose.Clear();

            GC.SuppressFinalize(this);
        }

        private T AddDisposable<T>(T toDisopse)
            where T : IDisposable
        {
            _toDispose.Add(toDisopse);
            return toDisopse;
        }
    }
}
