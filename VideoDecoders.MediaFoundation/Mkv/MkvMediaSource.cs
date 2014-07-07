using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections.Concurrent;
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
        private readonly IMFMediaEventQueue _eventQueue;
        private readonly ConcurrentQueue<MkvStateCommand> _commands;
        private readonly ManualResetEvent _commandReset;
        private readonly Task _commandProcess;

        private MkvState _currentState;

        public MkvMediaSource(MkvDecoder decoder)
        {
            _decoder = decoder;
            _tracks = new List<IMkvTrack>();
            _commands = new ConcurrentQueue<MkvStateCommand>();
            _commandReset = new ManualResetEvent(true);
            _currentState = MkvState.Stop;

            foreach (var t in _decoder.Metadata.Tracks)
            {
                if (t.TrackType == TrackType.Video)
                {
                    _tracks.Add(new MkvVideoTrack(t));
                }
            }

            TestSuccess("Could not create event queue", MFExtern.MFCreateEventQueue(out _eventQueue));
            _commandProcess = Task.Run(() => ProcessCommandQueue());
        }

        public int CreatePresentationDescriptor(out IMFPresentationDescriptor ppPresentationDescriptor)
        {
            TestSuccess("Could not create presentation descriptor", MFExtern.MFCreatePresentationDescriptor(_tracks.Count, _tracks.Select(t => t.Descriptor).ToArray(), out ppPresentationDescriptor));

            for (int i = 0; i < _tracks.Count; i++)
            {
                var t = _tracks[i];
                if (t.Metadata.FlagDefault)
                {
                    TestSuccess("Could not select track for presentation", ppPresentationDescriptor.SelectStream(i));
                }
                else
                {
                    TestSuccess("Could not deselect track for presentation", ppPresentationDescriptor.DeselectStream(i));
                }
            }

            return S_Ok;
        }

        public int Start(IMFPresentationDescriptor pPresentationDescriptor, Guid pguidTimeFormat, global::MediaFoundation.Misc.ConstPropVariant pvarStartPosition)
        {
            if (_currentState == MkvState.Shutdown)
            {
                throw new ObjectDisposedException("MkvMediaSource");
            }

            SetEvent(new MkvStateCommand { State = MkvState.Play, Descriptor = pPresentationDescriptor });
            return S_Ok;
        }

        public int Stop()
        {
            if (_currentState == MkvState.Shutdown)
            {
                throw new ObjectDisposedException("MkvMediaSource");
            }

            SetEvent(new MkvStateCommand { State = MkvState.Stop });
            return S_Ok;
        }

        public int Pause()
        {
            if (_currentState == MkvState.Shutdown)
            {
                throw new ObjectDisposedException("MkvMediaSource");
            }

            SetEvent(new MkvStateCommand { State = MkvState.Pause });
            return S_Ok;
        }

        public int Shutdown()
        {
            MkvStateCommand command;
            while (_commands.TryDequeue(out command)) ;
            SetEvent(new MkvStateCommand { State = MkvState.Shutdown });
            _commandProcess.Wait();
            _commandProcess.Dispose();
            _commandReset.Dispose();

            return S_Ok;
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

        private void SetEvent(MkvStateCommand command)
        {
            _commands.Enqueue(command);
            _commandReset.Set();
        }

        private void ProcessCommandQueue()
        {
            bool isRunning = true;
            while (isRunning)
            {
                MkvStateCommand command;
                if (_commands.Count > 0 && _commands.TryDequeue(out command))
                {
                    switch (command.State)
                    {
                        case MkvState.Shutdown:
                            _currentState = MkvState.Shutdown;
                            isRunning = false;
                            break;
                    }
                }
                else
                {
                    _commandReset.Reset();
                    _commandReset.WaitOne();
                }
            }
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
