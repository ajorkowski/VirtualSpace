using SharpDX;
using SharpDX.XAPO;
using System;
using System.Runtime.InteropServices;

namespace VirtualSpace.Platform.Windows.Video
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioDelayParam
    {
        public float[] Delays { get; set; }
    }

    internal class AudioDelayEffect : AudioProcessorBase<AudioDelayParam>
    {
        private readonly float _maxDelay;

        private int _lastFrame;
        private int _storedFrames;

        private float[] _delayedData;
        private int[] _lastDelayAmount;

        // Working arrays
        private int[] _delaySampleAmount;
        private int[] _delayDiff;
        private int[] _lastStartFrame;

        public AudioDelayEffect(float maxDelay)
        {
            _maxDelay = maxDelay;
            _lastFrame = 0;
            _storedFrames = 0;

            RegistrationProperties = new RegistrationProperties()
            {
                Clsid = Utilities.GetGuidFromType(typeof(AudioDelayEffect)),
                CopyrightInfo = "Test",
                FriendlyName = "AudioDelay",
                MaxInputBufferCount = 1,
                MaxOutputBufferCount = 1,
                MinInputBufferCount = 1,
                MinOutputBufferCount = 1,
                Flags = PropertyFlags.BitspersampleMustMatch | PropertyFlags.FrameRateMustMatch | PropertyFlags.InplaceSupported
            };
        }

        public override void Process(BufferParameters[] inputProcessParameters, BufferParameters[] outputProcessParameters, bool isEnabled)
        {
            int frameCount = inputProcessParameters[0].ValidFrameCount;
            int outputFrameCount = outputProcessParameters[0].ValidFrameCount;

            if (_delayedData == null)
            {
                if(InputFormatLocked.Channels != 1)
                {
                    throw new InvalidOperationException("Only supports one channel input");
                }

                _delayedData = new float[(int)(InputFormatLocked.SampleRate * _maxDelay / 1000)];
                _lastDelayAmount = new int[OutputFormatLocked.Channels];
                _delaySampleAmount = new int[OutputFormatLocked.Channels];
                _delayDiff = new int[OutputFormatLocked.Channels];
                _lastStartFrame = new int[OutputFormatLocked.Channels];
            }

            for (var i = 0; i < OutputFormatLocked.Channels; i++)
            {
                if(Parameters.Delays[i] >= _maxDelay || Parameters.Delays[i] < 0)
                {
                    throw new InvalidOperationException("Delay is too large or negative, not allowed");
                }

                _delaySampleAmount[i] = (int)(InputFormatLocked.SampleRate * Parameters.Delays[i] / 1000);
                _delayDiff[i] = _lastDelayAmount[i] - _delaySampleAmount[i];
                _lastStartFrame[i] = mod(_lastFrame - _lastDelayAmount[i], _delayedData.Length);
            }

            using (var input = new DataStream(inputProcessParameters[0].Buffer, frameCount * InputFormatLocked.BlockAlign, true, false))
            using (var output = new DataStream(inputProcessParameters[0].Buffer, outputFrameCount * OutputFormatLocked.BlockAlign, false, true))
            {
                for (int i = 0; i < frameCount; i++)
                {
                    var inputFrame = _delayedData[_lastFrame] = input.Read<float>();
                    
                    _lastFrame = mod(_lastFrame + 1, _delayedData.Length);
                    if (_storedFrames < _delayedData.Length) { _storedFrames++; }

                    for (var j = 0; j < OutputFormatLocked.Channels; j++)
                    {
                        // If not enabled then just pass the data through
                        // (We still store so that you can enable/disable easily)
                        if (!isEnabled)
                        {
                            output.Write(inputFrame);
                            continue;
                        }

                        if (_storedFrames < _delaySampleAmount[j])
                        {
                            output.Write(0f);
                        }
                        else
                        {
                            // work out the frame we need to get
                            var startFrame = _lastFrame - _lastDelayAmount[j] + (i * _delayDiff[j] / frameCount);
                            if (startFrame < 0)
                            {
                                startFrame += _delayedData.Length;
                            }

                            if (_lastStartFrame[j] == startFrame)
                            {
                                // Interpolate between current and next frame
                                var nextFrame = (startFrame + 1) % _delayedData.Length;
                                output.Write((_delayedData[startFrame] + _delayedData[nextFrame]) / 2);
                            }
                            else
                            {
                                var data = 0f;
                                var count = 0f;
                                var direction = _lastStartFrame[j] > startFrame ? -1 : 1;

                                while (_lastStartFrame[j] != startFrame)
                                {
                                    _lastStartFrame[j] = mod(_lastStartFrame[j] + direction, _delayedData.Length);
                                    data += _delayedData[_lastStartFrame[j]];
                                    count++;
                                }

                                output.Write(count == 0 ? 0 : data / count);
                            }
                        }
                    }
                }
            }

            for (var i = 0; i < OutputFormatLocked.Channels; i++)
            {
                _lastDelayAmount[i] = _delaySampleAmount[i];
            }
        }

        private int mod(int x, int m)
        {
            int r = x % m;
            return r < 0 ? r + m : r;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
