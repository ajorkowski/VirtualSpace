using DirectShow.BaseClasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoDecoders.MediaFoundation.DirectShowAudio
{
    public class AudioRenderStream : RendererInputPin
    {
        public AudioRenderStream(string name, BaseRendererFilter filter)
            : base(name, filter)
        {
        }

        public override int GetMediaType(int iPosition, ref DirectShow.AMMediaType pMediaType)
        {
            if(iPosition != 0)
            {
                return E_UNEXPECTED;
            }

            return (m_Filter as AudioOutputFilter).GetMediaType(ref pMediaType);
        }
    }
}
