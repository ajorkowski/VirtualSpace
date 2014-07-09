using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public struct MkvBlockHeader
    {
        public int TrackNumber { get; set; }
        public long TimeCode { get; set; } 
    }
}
