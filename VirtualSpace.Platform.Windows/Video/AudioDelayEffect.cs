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

        private int _startFrame;
        private int _storedFrames;
        private float[] _delayedData;

        public AudioDelayEffect(float maxDelay)
        {
            _maxDelay = maxDelay;
            _startFrame = 0;
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
            var endPointer = _startFrame + _storedFrames;

            using (var input = new DataStream(inputProcessParameters[0].Buffer, frameCount * InputFormatLocked.BlockAlign, true, false))
            using (var output = new DataStream(inputProcessParameters[0].Buffer, frameCount * InputFormatLocked.BlockAlign, false, true))
            {
                for (int i = 0; i < frameCount; i++)
                {
                    if (endPointer >= _delayedData.Length)
                    {
                        endPointer -= _delayedData.Length;
                    }

                    _delayedData[endPointer] = input.Read<float>();
                    _delayedData[endPointer + 1] = input.Read<float>();
                    endPointer += 2;
                    _storedFrames += 2;

                    if (_storedFrames < delaySampleAmount)
                    {
                        output.Write(0f);
                        output.Write(0f);
                    }
                    else
                    {
                        // We might need to skip frames if we had a change in delay...
                        var skipFrames = _storedFrames - delaySampleAmount - 2;
                        if (skipFrames > 0)
                        {
                            _startFrame += skipFrames;
                            _storedFrames -= skipFrames;
                            if (_startFrame >= _delayedData.Length)
                            {
                                _startFrame -= _delayedData.Length;
                            }
                        }

                        output.Write(_delayedData[_startFrame]);
                        output.Write(_delayedData[_startFrame + 1]);

                        _storedFrames -= 2;
                        _startFrame += 2;
                        if (_startFrame >= _delayedData.Length)
                        {
                            _startFrame -= _delayedData.Length;
                        }
                    }
                }
            }
        }
    }
}
