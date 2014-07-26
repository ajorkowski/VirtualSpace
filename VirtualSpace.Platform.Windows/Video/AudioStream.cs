using SharpDX;
using SharpDX.MediaFoundation;
using SharpDX.Multimedia;
using SharpDX.XAudio2;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using VirtualSpace.Core.Video;

namespace VirtualSpace.Platform.Windows.Video
{
    internal sealed class AudioStream : IDisposable
    {
        private const int MaxNumberOfFramesToQueue = 25;

        private readonly SourceReader _reader;
        private readonly int _sourceIndex;
        private readonly List<IDisposable> _toDispose;

        private ConcurrentBag<AudioFrame> _unusedFrames;
        private ConcurrentQueue<AudioFrame> _bufferedFrames;
        private ConcurrentQueue<AudioFrame> _playingFrames;

        private WaveFormat _waveFormat;
        private SourceVoice _sourceVoice;
        private bool _isPlaying;

        public AudioStream(SourceReader reader, int sourceIndex)
        {
            _reader = reader;
            _sourceIndex = sourceIndex;
            _unusedFrames = new ConcurrentBag<AudioFrame>();
            _bufferedFrames = new ConcurrentQueue<AudioFrame>();
            _playingFrames = new ConcurrentQueue<AudioFrame>();
            _isPlaying = false;

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

        public SourceVoice GetOutputSourceVoice()
        {
            _sourceVoice = AddDisposable(new SourceVoice(MediaAndDeviceManager.Current.AudioEngine, _waveFormat));

            for (int i = 0; i < MaxNumberOfFramesToQueue; i++)
            {
                _unusedFrames.Add(new AudioFrame
                {
                    Buffer = new AudioBuffer(),
                    MemBuffer = new DataPointer(Utilities.AllocateMemory(32 * 1024), 32 * 1024)  // 32kb default size
                });
            }

            return _sourceVoice;
        }

        public void UpdateFrame(ManualResetEvent frameResetEvent, ref VideoState state)
        {
            if (_sourceVoice != null && (_bufferedFrames.Count > 0 || _playingFrames.Count > _sourceVoice.State.BuffersQueued))
            {
                AudioFrame peeked = null;
                AudioFrame dequeued = null;
                TimeSpan? lastTimestamp = null;
                while (_bufferedFrames.TryPeek(out peeked))
                {
                    lastTimestamp = peeked.Timestamp;

                    // We have a frame to update
                    if (_bufferedFrames.TryDequeue(out dequeued))
                    {
                        _sourceVoice.SubmitSourceBuffer(dequeued.Buffer, null);
                        _playingFrames.Enqueue(dequeued);
                        frameResetEvent.Set();
                    }
                }

                // Free up any played items...
                while(_playingFrames.Count > _sourceVoice.State.BuffersQueued && _playingFrames.TryDequeue(out dequeued))
                {
                    _unusedFrames.Add(dequeued);
                }

                if (_unusedFrames.Count > 0)
                {
                    // We do not have any buffered, make sure that we are decoding more!
                    frameResetEvent.Set();
                }
            }

            if (state == VideoState.Playing)
            {
                state = _playingFrames.Count > 0 ? VideoState.Playing : VideoState.Buffering;
            }

            if(state == VideoState.Playing && !_isPlaying)
            {
                _sourceVoice.Start();
                _isPlaying = true;
            }

            if(state != VideoState.Playing && _isPlaying)
            {
                _sourceVoice.Stop();
                _isPlaying = false;
            }
        }

        public bool TryEnqueue(ref bool isBuffering)
        {
            AudioFrame unused;
            if (_unusedFrames.TryTake(out unused))
            {
                int streamIndex;
                SourceReaderFlags flags;
                long timeStamp;
                using (var sample = _reader.ReadSample(_sourceIndex, SourceReaderControlFlags.None, out streamIndex, out flags, out timeStamp))
                {
                    if (flags.HasFlag(SourceReaderFlags.StreamTick))
                    {
                        _unusedFrames.Add(unused);
                        return true;
                    }

                    if (flags.HasFlag(SourceReaderFlags.Endofstream))
                    {
                        _unusedFrames.Add(unused);
                        isBuffering = false;
                        return true;
                    }

                    if (sample != null)
                    {
                        unused.Timestamp = TimeSpan.FromMilliseconds(timeStamp / 10000.0); // timestamps are in 100 nanoseconds
                        using (var buffer = sample.ConvertToContiguousBuffer())
                        {
                            int maxLength;
                            int currentLength;
                            var data = buffer.Lock(out maxLength, out currentLength);

                            if (currentLength > unused.MemBuffer.Size)
                            {
                                if (unused.MemBuffer.Pointer != IntPtr.Zero)
                                {
                                    Utilities.FreeMemory(unused.MemBuffer.Pointer);
                                }

                                unused.MemBuffer.Pointer = Utilities.AllocateMemory(currentLength);
                                unused.MemBuffer.Size = currentLength;
                            }

                            // Copy the memory from MediaFoundation AudioDecoder to the buffer that is going to be played.
                            Utilities.CopyMemory(unused.MemBuffer.Pointer, data, currentLength);

                            // Set the pointer to the data.
                            unused.Buffer.AudioDataPointer = unused.MemBuffer.Pointer;
                            unused.Buffer.AudioBytes = currentLength;

                            buffer.Unlock();
                        }

                        _bufferedFrames.Enqueue(unused);
                    }
                    else
                    {
                        _unusedFrames.Add(unused);
                    }
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public void Dispose()
        {
            if(_sourceVoice != null)
            {
                _sourceVoice.FlushSourceBuffers();
                _sourceVoice.DestroyVoice();
            }

            AudioFrame f;
            while (_bufferedFrames.TryDequeue(out f) || _unusedFrames.TryTake(out f) || _playingFrames.TryDequeue(out f))
            {
                Utilities.FreeMemory(f.MemBuffer.Pointer);
            }

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

        private class AudioFrame
        {
            public TimeSpan Timestamp;
            public AudioBuffer Buffer;
            public DataPointer MemBuffer;
        }
    }
}
