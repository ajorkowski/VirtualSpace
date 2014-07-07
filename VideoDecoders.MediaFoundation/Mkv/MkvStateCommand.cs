using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public class MkvStateCommand
    {
        public MkvState State { get; set; }
        public IMFPresentationDescriptor Descriptor { get; set; }
        public ConstPropVariant Prop { get; set; }
    }

    public enum MkvState
    {
        Stop = 0,
        Play,
        Pause,
        Restart,
        Shutdown
    }
}
