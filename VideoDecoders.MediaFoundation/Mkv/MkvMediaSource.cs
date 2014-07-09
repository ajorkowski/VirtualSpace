using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace VideoDecoders.MediaFoundation.Mkv
{
    [ComVisible(true)]
    [Guid("F7C75FFE-06FE-4C7A-98DF-D7FBD1BD9642")]
    public class MkvMediaSource : COMBase, IMFMediaSource
    {
        private readonly MkvDecoder _decoder;
        private readonly List<IMkvTrack> _tracks;
        private readonly List<int> _ignoredTrackNumbers;
        private readonly Dictionary<int, Queue<IMFSample>> _queuedBuffers;
        private readonly IMFPresentationDescriptor _descriptor;
        private readonly ConcurrentQueue<MkvStateCommand> _commands;
        private readonly IMFMediaEventQueue _eventQueue;
        private readonly ManualResetEvent _commandReset;
        private readonly Task _commandProcess;

        private MkvState _currentState;

        public MkvState CurrentState { get { return _currentState; } }

        public MkvMediaSource(MkvDecoder decoder)
        {
            _decoder = decoder;
            _tracks = new List<IMkvTrack>();
            _commands = new ConcurrentQueue<MkvStateCommand>();
            _commandReset = new ManualResetEvent(true);
            _currentState = MkvState.Stop;
            _ignoredTrackNumbers = new List<int>();
            _queuedBuffers = new Dictionary<int, Queue<IMFMediaBuffer>>();

            foreach (var t in _decoder.Metadata.Tracks)
            {
                if (t.TrackType == TrackType.Video)
                {
                    var newTrack = new MkvVideoTrack(t, this);
                    _tracks.Add(newTrack);
                }
            }

            TestSuccess("Could not create presentation descriptor", MFExtern.MFCreatePresentationDescriptor(_tracks.Count, _tracks.Select(t => t.Descriptor).ToArray(), out _descriptor));

            for (int i = 0; i < _tracks.Count; i++)
            {
                var t = _tracks[i];
                if (t.Metadata.FlagDefault)
                {
                    TestSuccess("Could not select track for presentation", _descriptor.SelectStream(i));
                }
                else
                {
                    TestSuccess("Could not deselect track for presentation", _descriptor.DeselectStream(i));
                }
            }

            TestSuccess("Could not create event queue", MFExtern.MFCreateEventQueue(out _eventQueue));
            _commandProcess = Task.Run(() => ProcessCommandQueue());
        }

        public int CreatePresentationDescriptor(out IMFPresentationDescriptor ppPresentationDescriptor)
        {
            // Recommended that this should clone...
            return _descriptor.Clone(out ppPresentationDescriptor);
        }

        public int Start(IMFPresentationDescriptor pPresentationDescriptor, Guid pguidTimeFormat, ConstPropVariant pvarStartPosition)
        {
            if (_currentState == MkvState.Shutdown) { return MFError.MF_E_SHUTDOWN; }
            if (pguidTimeFormat != Guid.Empty) { return MFError.MF_E_UNSUPPORTED_TIME_FORMAT; }
            if (pvarStartPosition.GetVariantType() != ConstPropVariant.VariantType.None) { return MFError.MF_E_INVALIDREQUEST; }

            SetEvent(new MkvStateCommand { State = MkvState.Play, Descriptor = pPresentationDescriptor, Prop = pvarStartPosition });
            return S_Ok;
        }

        public int Stop()
        {
            if (_currentState == MkvState.Shutdown) { return MFError.MF_E_SHUTDOWN; }

            SetEvent(new MkvStateCommand { State = MkvState.Stop });
            return S_Ok;
        }

        public int Pause()
        {
            if (_currentState == MkvState.Shutdown) { return MFError.MF_E_SHUTDOWN; }

            SetEvent(new MkvStateCommand { State = MkvState.Pause });
            return S_Ok;
        }

        public int Shutdown()
        {
            if (_currentState == MkvState.Shutdown) { return MFError.MF_E_SHUTDOWN; }

            MkvStateCommand command;
            while (_commands.TryDequeue(out command)) ;
            SetEvent(new MkvStateCommand { State = MkvState.Shutdown });
            _commandProcess.Wait();
            _commandProcess.Dispose();
            _commandReset.Dispose();

            foreach (var t in _tracks)
            {
                t.Dispose();
            }
            _tracks.Clear();

            return _eventQueue.Shutdown();
        }

        public int BeginGetEvent(IMFAsyncCallback pCallback, object o)
        {
            return _eventQueue.BeginGetEvent(pCallback, o);
        }

        public int GetEvent(MFEventFlag dwFlags, out IMFMediaEvent ppEvent)
        {
            return _eventQueue.GetEvent(dwFlags, out ppEvent);
        }

        public int QueueEvent(MediaEventType met, Guid guidExtendedType, int hrStatus, global::MediaFoundation.Misc.ConstPropVariant pvValue)
        {
            return _eventQueue.QueueEventParamVar(met, guidExtendedType, hrStatus, pvValue);
        }

        public int EndGetEvent(IMFAsyncResult pResult, out IMFMediaEvent ppEvent)
        {
            return _eventQueue.EndGetEvent(pResult, out ppEvent);
        }

        public int GetCharacteristics(out MFMediaSourceCharacteristics pdwCharacteristics)
        {
            pdwCharacteristics = MFMediaSourceCharacteristics.None;
            if (_decoder.StreamMetadata.CanPause)
            {
                pdwCharacteristics &= MFMediaSourceCharacteristics.CanPause;
            }

            if (_decoder.StreamMetadata.CanSeek)
            {
                pdwCharacteristics &= MFMediaSourceCharacteristics.CanSeek;
            }

            if (_decoder.StreamMetadata.HasSlowSeek)
            {
                pdwCharacteristics &= MFMediaSourceCharacteristics.HasSlowSeek;
            }

            return S_Ok;
        }

        public IMFSample LoadNextSample(int track)
        {
            if (_queuedBuffers[track].Count > 0)
            {
                return _queuedBuffers[track].Dequeue();
            }

            while (true)
            {
                MkvBlockHeader header;
                if (!_decoder.SeekNextBlock(_ignoredTrackNumbers, out header))
                {
                    return null;
                }

                IMFSample sample;
                TestSuccess("Could not media sample", MFExtern.MFCreateSample(out sample));
            }
        }

        private void ProcessCommandQueue()
        {
            while (_currentState != MkvState.Shutdown)
            {
                MkvStateCommand command;
                if (_commands.Count > 0 && _commands.TryDequeue(out command))
                {
                    switch (command.State)
                    {
                        case MkvState.Play:
                            OnStart(command);
                            break;
                        case MkvState.Shutdown:
                            _currentState = MkvState.Shutdown;
                            break;
                        default:
                            throw new InvalidOperationException("Unsupported state");
                    }
                    continue;
                }

                if(_currentState == MkvState.Play)
                {
                    LoadNextFrame();
                    continue;
                }

                // We are stopped pretty much... we have nothing to do... so just wait instead of wasting cycles
                _commandReset.Reset();
                _commandReset.WaitOne();
            }
        }

        private void OnStart(MkvStateCommand command)
        {
            if (_currentState == MkvState.Play) { return; }
            _currentState = MkvState.Play;

            // Work out the right tracks to play...
            var selectedTracks = new List<int>();
            int count;
            TestSuccess("Could not get count", command.Descriptor.GetStreamDescriptorCount(out count));

            _ignoredTrackNumbers.Clear();
            _queuedBuffers.Clear();
            for(int i=0; i<count; i++)
            {
                bool selected;
                IMFStreamDescriptor desc;
                TestSuccess("Could not get stream descriptor", command.Descriptor.GetStreamDescriptorByIndex(i, out selected, out desc));

                int trackId;
                TestSuccess("Could not get stream identifier", desc.GetStreamIdentifier(out trackId));

                if (selected)
                {
                    selectedTracks.Add(trackId);
                    _queuedBuffers[trackId] = new Queue<IMFSample>();
                }
                else
                {
                    _ignoredTrackNumbers.Add(trackId);
                }
            }

            // Send the right events out in the right order...
            foreach (var t in _tracks)
            {
                bool willSelect = selectedTracks.Contains((int)t.Metadata.TrackNumber);
                if (willSelect)
                {
                    QueueEvent(t.IsSelected ? MediaEventType.MEUpdatedStream : MediaEventType.MENewStream, Guid.Empty, S_Ok, new PropVariant(t));
                }
                t.IsSelected = willSelect;
            }

            QueueEvent(MediaEventType.MESourceStarted, Guid.Empty, S_Ok, command.Prop);

            foreach(var t in _tracks)
            {
                if (t.IsSelected)
                {
                    t.QueueEvent(MediaEventType.MEStreamStarted, Guid.Empty, S_Ok, command.Prop);
                }
            }
        }

        private void LoadNextFrame()
        {
            foreach (var t in _tracks)
            {
                if (t.IsSelected)
                {
                    t.ProcessSample();
                }
            }
        }

        private void SetEvent(MkvStateCommand command)
        {
            _commands.Enqueue(command);
            _commandReset.Set();
        }

        private void TestSuccess(string message, int hResult)
        {
            if (hResult < 0)
            {
                throw new COMException(message, hResult);
            }
        }
    }
}
