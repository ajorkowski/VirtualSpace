using SharpDX;
using SharpDX.MediaFoundation;
using SharpDX.Toolkit;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using VirtualSpace.Core.Video;
using VirtualSpace.Platform.Windows.Rendering.Screen;

namespace VirtualSpace.Platform.Windows.Video
{
    internal sealed class VideoSource : IVideo, IScreenSource
    {
        private const int MF_SOURCE_READER_FIRST_VIDEO_STREAM = unchecked((int)(0xFFFFFFFC));
        private const int MF_SOURCE_READER_FIRST_AUDIO_STREAM = unchecked((int)(0xFFFFFFFD));
        private const int MF_SOURCE_READER_ALL_STREAMS = unchecked((int)(0xFFFFFFFE));

        private readonly List<IDisposable> _toDispose;
        private readonly List<VirtualSpace.Core.Video.StreamMetadata> _streamMetadata;

        private SourceReader _reader;
        private VideoState _state;
        private Task _decodeLoop;
        private TimeSpan _currentTime;
        private ManualResetEvent _waitForUnusedFrame;

        private VideoDevice _videoDevice;
        private VideoStream _videoStream;
        private AudioStream _audioStream;

        private bool _canSeek;
        private TimeSpan _duration;
        private bool _isBuffering;
        private bool _isAudioBuffering;

        public bool CanSeek { get { return _canSeek; } }
        public TimeSpan Duration { get { return _duration; } }
        public IEnumerable<StreamMetadata> Metadata { get { return _streamMetadata; } }

        public VideoSource(string file)
        {
            _toDispose = new List<IDisposable>();
            _streamMetadata = new List<Core.Video.StreamMetadata>();
            _isBuffering = true;
            _isAudioBuffering = true;

            // Creates an URL to the file
            var url = new Uri(file, UriKind.RelativeOrAbsolute);

            // Create Source Reader
            _videoDevice = AddDisposable(MediaAndDeviceManager.Current.CreateVideoDevice());
            using(var attr = new MediaAttributes())
            {
                if (_videoDevice.D3DManager != null)
                {
                    attr.Set(SourceReaderAttributeKeys.D3DManager, _videoDevice.D3DManager);
                }
                if (_videoDevice.VideoMode == VideoMode.Dx11)
                {
                    attr.Set(SourceReaderAttributeKeys.EnableAdvancedVideoProcessing, true);
                }
                if (_videoDevice.VideoMode == VideoMode.Software)
                {
                    // This allows output format of rgb32 when software rendering
                    // NOTE: This is NOT RECOMMENDED, so I need to find a better way of doing this... 
                    // (ie pump Yv12 format into texture and do Yv12 -> rgb conversion in shader)
                    attr.Set(SourceReaderAttributeKeys.EnableVideoProcessing, 1); 
                }
                _reader = AddDisposable(new SourceReader(url.AbsoluteUri, attr));
            }

            // Enable default streams...
            _reader.SetStreamSelection(MF_SOURCE_READER_ALL_STREAMS, false);
            _reader.SetStreamSelection(MF_SOURCE_READER_FIRST_VIDEO_STREAM, true);
            _reader.SetStreamSelection(MF_SOURCE_READER_FIRST_AUDIO_STREAM, true);

            // Grab out some metadata...
            _canSeek = GetCanSeek(_reader);
            _duration = GetDuration(_reader);

            int currentIndex = 0;
            while (true)
            {
                try
                {
                    Bool isSelected;
                    _reader.GetStreamSelection(currentIndex, out isSelected);

                    var mediaType = _reader.GetCurrentMediaType(currentIndex);
                    var majorType = mediaType.Get(MediaTypeAttributeKeys.MajorType);
                    StreamType type;
                    if(majorType == MediaTypeGuids.Video)
                    {
                        type = StreamType.Video;
                    }
                    else if(majorType == MediaTypeGuids.Audio)
                    {
                        type = StreamType.Audio;
                    }
                    else if (majorType == MediaTypeGuids.Sami)
                    {
                        type = StreamType.Subtitle;
                    }
                    else
                    {
                        type = StreamType.Other;
                    }

                    string language = null;
                    try
                    {
                        language = (string)_reader.GetPresentationAttribute(currentIndex, StreamDescriptorAttributeKeys.Language.Guid).Value;
                    }
                    catch(SharpDXException) {}

                    string name = null;
                    try
                    {
                        name = (string)_reader.GetPresentationAttribute(currentIndex, StreamDescriptorAttributeKeys.StreamName.Guid).Value;
                    }
                    catch (SharpDXException) { }

                    _streamMetadata.Add(new Core.Video.StreamMetadata
                    {
                        Language = language == "und" ? null : language,
                        Name = name,
                        TrackNumber = currentIndex,
                        Type = type,
                        IsActive = isSelected
                    });
                }
                catch (SharpDXException)
                {
                    break;
                }

                currentIndex++;
            }

            // Create the default video stream...
            _videoStream = AddDisposable(new VideoStream(_reader, _videoDevice, MF_SOURCE_READER_FIRST_VIDEO_STREAM));

            try
            {
                _audioStream = AddDisposable(new AudioStream(_reader, MF_SOURCE_READER_FIRST_AUDIO_STREAM));
            }
            catch (NotSupportedException e)
            {
                _reader.SetStreamSelection(MF_SOURCE_READER_FIRST_AUDIO_STREAM, false);
            }
            
            _state = VideoState.Paused;
        }

        public ScreenOutput GetOutput(SharpDX.Direct3D11.Device device)
        {
            var output = new ScreenOutput
            {
                 Texture = _videoStream.GetOutputRenderTexture(device),
                 Audio = _audioStream == null ? null : _audioStream.GetOutputSourceVoice()
            };
            
            // Make sure we let the decoder know we have frames to use!
            if (_waitForUnusedFrame != null)
            {
                _waitForUnusedFrame.Set();
            }

            return output;
        }

        public void Update(GameTime time)
        {
            if (_state != VideoState.Playing && _state != VideoState.Buffering) { return; }

            _currentTime = _currentTime.Add(time.ElapsedGameTime);
            _videoStream.UpdateFrame(_waitForUnusedFrame, ref _currentTime, ref _state);

            if (_audioStream != null)
            {
                _audioStream.UpdateFrame(_waitForUnusedFrame, ref _state);
            }

            if (!_videoStream.HasFrames && !_isBuffering)
            {
                _state = VideoState.Finished;

                // Let the decode loop finish
                _waitForUnusedFrame.Set();
            }
        }

        public VideoState State { get { return _state; } }

        public void Play()
        {
            if (_decodeLoop == null)
            {
                _state = VideoState.Buffering;
                _waitForUnusedFrame = new ManualResetEvent(true);
                _decodeLoop = Task.Run(() => DecodeLoop());
            }
            else if(_state == VideoState.Paused)
            {
                _state = _videoStream.HasFrames ? VideoState.Playing : VideoState.Buffering;
            }
            else if (_state == VideoState.Finished)
            {
                throw new NotImplementedException();
            }
        }

        public void Stop()
        {
            if (_state == VideoState.Buffering || _state == VideoState.Playing)
            {
                _state = VideoState.Paused;
            }
        }

        private void DecodeLoop()
        {
            _currentTime = TimeSpan.FromMilliseconds(0);
            _isBuffering = true;
            _isAudioBuffering = true;
            while (_state != VideoState.Finished)
            {
                if ((!_isBuffering || !_videoStream.TryEnqueue(ref _isBuffering))
                    && (_audioStream == null || !_isAudioBuffering || !_audioStream.TryEnqueue(ref _isAudioBuffering)))
                {
                    _waitForUnusedFrame.Reset();
                    _waitForUnusedFrame.WaitOne();
                }
            }
        }

        private T AddDisposable<T>(T toDisopse)
            where T: IDisposable
        {
            _toDispose.Add(toDisopse);
            return toDisopse;
        }

        public void Dispose()
        {
            _state = VideoState.Finished;
            _reader.Dispose();
            if (_decodeLoop.Status == TaskStatus.Running)
            {
                _waitForUnusedFrame.Set();
                _decodeLoop.Wait();
            }
            _decodeLoop.Dispose();
            _waitForUnusedFrame.Dispose();

            foreach(var d in _toDispose)
            {
                d.Dispose();
            }
            _toDispose.Clear();

            GC.SuppressFinalize(this);
        }

        private static TimeSpan GetDuration(SourceReader reader)
        {
            try
            {
                var duration = reader.GetPresentationAttribute(SourceReaderIndex.MediaSource, PresentationDescriptionAttributeKeys.Duration);
                return TimeSpan.FromMilliseconds(duration / 10000.0);
            }
            catch (SharpDXException)
            {
                return TimeSpan.Zero;
            }
        }

        private static bool GetCanSeek(SourceReader reader)
        {
            try
            {
                var ch = (MediaSourceCharacteristics)reader.GetPresentationAttribute(SourceReaderIndex.MediaSource, SourceReaderAttributeKeys.MediaSourceCharacteristics);
                return (ch & MediaSourceCharacteristics.CanSeek) == MediaSourceCharacteristics.CanSeek;
            }
            catch (SharpDXException)
            {
                return false;
            }
        }
    }
}
