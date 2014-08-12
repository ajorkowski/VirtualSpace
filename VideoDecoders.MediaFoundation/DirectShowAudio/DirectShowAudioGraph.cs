using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoDecoders.MediaFoundation.DirectShowAudio
{
    public class DirectShowAudioGraph
    {
        private readonly IMFMediaType _inputType;
        private readonly IMFMediaType _outputType;

        public DirectShowAudioGraph(IMFMediaType inputType, IMFMediaType outputType)
        {
            _inputType = inputType;
            _outputType = outputType;
        }
    }
}
