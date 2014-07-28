using SharpDX;
using SharpDX.XAPO;
using System;
using System.Runtime.InteropServices;

namespace VirtualSpace.Platform.Windows.Video
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioDelayParam
    {
        public float LeftDelay { get; set; }
        public float RightDelay { get; set; }
    }

    internal class AudioDelayEffect : AudioProcessorBase<AudioDelayParam>
    {
        private readonly float _maxDelay;

        private int _lastFrame;
        private int _storedFrames;

        private float[] _delayedData;
        private int _lastDelayAmountLeft;
        private int _lastDelayAmountRight;

        public AudioDelayEffect(float maxDelay)
        {
            _maxDelay = maxDelay;
            _lastFrame = 0;
            _storedFrames = 0;
            _lastDelayAmountLeft = 0;
            _lastDelayAmountRight = 0;

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
            if (Parameters.LeftDelay > _maxDelay || Parameters.LeftDelay < 0)
            {
                throw new InvalidOperationException("Left Delay is out of range");
            }
            if (Parameters.RightDelay > _maxDelay || Parameters.RightDelay < 0)
            {
                throw new InvalidOperationException("Right Delay is out of range");
            }

            if (_delayedData == null)
            {
                _delayedData = new float[2 * (int)(InputFormatLocked.SampleRate * _maxDelay / 1000)];
            }

            var delaySampleAmountLeft = 2 * (int)(InputFormatLocked.SampleRate * Parameters.LeftDelay / 1000);
            var delayDiffLeft = (_lastDelayAmountLeft - delaySampleAmountLeft) / 2;

            var delaySampleAmountRight = 2 * (int)(InputFormatLocked.SampleRate * Parameters.RightDelay / 1000);
            var delayDiffRight = (_lastDelayAmountRight - delaySampleAmountRight) / 2;

            using (var input = new DataStream(inputProcessParameters[0].Buffer, frameCount * InputFormatLocked.BlockAlign, true, false))
            using (var output = new DataStream(inputProcessParameters[0].Buffer, frameCount * InputFormatLocked.BlockAlign, false, true))
            {
                var lastStartFrameLeft = mod(_lastFrame - _lastDelayAmountLeft, _delayedData.Length);
                var lastStartFrameRight = mod(_lastFrame - _lastDelayAmountRight, _delayedData.Length);

                for (int i = 0; i < frameCount; i++)
                {
                    var leftInput = _delayedData[_lastFrame] = input.Read<float>();
                    var rightInput = _delayedData[_lastFrame + 1] = input.Read<float>();
                    
                    _lastFrame = mod(_lastFrame + 2, _delayedData.Length);
                    if (_storedFrames < _delayedData.Length) { _storedFrames += 2; }

                    // If not enabled then just pass the data through
                    // (We still store so that you can enable/disable easily)
                    if (!isEnabled)
                    {
                        output.Write(leftInput);
                        output.Write(rightInput);
                        continue;
                    }

                    OutputFrame(i, frameCount, output, 0, delaySampleAmountLeft, _lastDelayAmountLeft, delayDiffLeft, ref lastStartFrameLeft);
                    OutputFrame(i, frameCount, output, 1, delaySampleAmountRight, _lastDelayAmountRight, delayDiffRight, ref lastStartFrameRight);
                }
            }

            _lastDelayAmountLeft = delaySampleAmountLeft;
            _lastDelayAmountRight = delaySampleAmountRight;
        }

        private void OutputFrame(int i, int frameCount, DataStream output, int offset, int delayAmount, int lastDelayAmount, int delayDiff, ref int lastStartFrame)
        {
            if (_storedFrames < delayAmount)
            {
                output.Write(0f);
            }
            else
            {
                // work out the frame we need to get
                var startFrame = _lastFrame - lastDelayAmount + 2 * (i * delayDiff / frameCount);
                if (startFrame < 0)
                {
                    startFrame += _delayedData.Length;
                }

                if (lastStartFrame == startFrame)
                {
                    // Interpolate between current and next frame
                    var nextFrame = (startFrame + 2) % _delayedData.Length;
                    output.Write((_delayedData[startFrame + offset] + _delayedData[nextFrame + offset]) / 2);
                }
                else
                {
                    var data = 0f;
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
                        data += _delayedData[lastStartFrame + offset];
                        count++;
                    }

                    output.Write(count == 0 ? 0 : data / count);
                }
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
