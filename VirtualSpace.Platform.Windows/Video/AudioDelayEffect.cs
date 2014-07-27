using SharpDX;
using SharpDX.XAPO;
using System;
using System.Runtime.InteropServices;

namespace VirtualSpace.Platform.Windows.Video
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioDelayParam
    {
        public float Delay { get; set; }
    }

    internal class AudioDelayEffect : AudioProcessorBase<AudioDelayParam>
    {
        private readonly float _maxDelay;

        private int _lastFrame;
        private int _storedFrames;
        private float[] _delayedData;
        private int _lastDelayAmount;

        public AudioDelayEffect(float maxDelay)
        {
            _maxDelay = maxDelay;
            _lastFrame = 0;
            _storedFrames = 0;
            _lastDelayAmount = 0;

            RegistrationProperties = new RegistrationProperties()
            {
                Clsid = Utilities.GetGuidFromType(typeof(AudioDelayEffect)),
                CopyrightInfo = "Test",
                FriendlyName = "AudioDelay",
                MaxInputBufferCount = 1,
                MaxOutputBufferCount = 1,
                MinInputBufferCount = 1,
                MinOutputBufferCount = 1,
                Flags = PropertyFlags.Default
            };
        }

        public override void Process(BufferParameters[] inputProcessParameters, BufferParameters[] outputProcessParameters, bool isEnabled)
        {
            int frameCount = inputProcessParameters[0].ValidFrameCount;
            float delay = Parameters.Delay;
            if (delay > _maxDelay || delay < 0)
            {
                throw new InvalidOperationException("Delay is out of range");
            }

            if (_delayedData == null)
            {
                _delayedData = new float[2 * (int)(InputFormatLocked.SampleRate * _maxDelay / 1000)];
            }

            var delaySampleAmount = 2 * (int)(InputFormatLocked.SampleRate * delay / 1000);
            var delayDiff = (_lastDelayAmount - delaySampleAmount) / 2;

            using (var input = new DataStream(inputProcessParameters[0].Buffer, frameCount * InputFormatLocked.BlockAlign, true, false))
            using (var output = new DataStream(inputProcessParameters[0].Buffer, frameCount * InputFormatLocked.BlockAlign, false, true))
            {
                var lastStartFrame = _lastFrame - _lastDelayAmount;
                if (lastStartFrame < 0)
                {
                    lastStartFrame += _delayedData.Length;
                }

                for (int i = 0; i < frameCount; i++)
                {
                    var leftInput = _delayedData[_lastFrame] = input.Read<float>();
                    var rightInput = _delayedData[_lastFrame + 1] = input.Read<float>();
                    
                    _lastFrame += 2;
                    if (_lastFrame >= _delayedData.Length)
                    {
                        _lastFrame -= _delayedData.Length;
                    }

                    if (_storedFrames < _delayedData.Length)
                    {
                        _storedFrames += 2;
                    }

                    // If not enabled then just pass the data through
                    // (We still store so that you can enable/disable easily)
                    if (!isEnabled)
                    {
                        output.Write(leftInput);
                        output.Write(rightInput);
                        continue;
                    }

                    if (_storedFrames < delaySampleAmount)
                    {
                        output.Write(0f);
                        output.Write(0f);
                    }
                    else
                    {
                        // work out the frame we need to get
                        var startFrame = _lastFrame - _lastDelayAmount + 2 * (i * delayDiff / frameCount);
                        if(startFrame < 0)
                        {
                            startFrame += _delayedData.Length;
                        }

                        if (lastStartFrame == startFrame)
                        {
                            // Interpolate between current and next frame
                            var nextFrame = (startFrame + 2) % _delayedData.Length;
                            output.Write((_delayedData[startFrame] + _delayedData[nextFrame]) / 2);
                            output.Write((_delayedData[startFrame + 1] + _delayedData[nextFrame + 1]) / 2);
                        }
                        else
                        {
                            var left = 0f;
                            var right = 0f;
                            var count = 0f;
                            var direction = lastStartFrame > startFrame ? -1 : 1;

                            while (lastStartFrame != startFrame)
                            {
                                lastStartFrame += 2 * direction;
                                if (lastStartFrame >= _delayedData.Length)
                                {
                                    lastStartFrame -= _delayedData.Length;
                                }
                                else if (lastStartFrame < 0)
                                {
                                    lastStartFrame += _delayedData.Length;
                                }
                                left += _delayedData[lastStartFrame];
                                right += _delayedData[lastStartFrame + 1];
                                count++;
                            }

                            output.Write(count == 0 ? 0 : left / count);
                            output.Write(count == 0 ? 0 : right / count);
                        }
                    }
                }
            }

            _lastDelayAmount = delaySampleAmount;
        }
    }
}
