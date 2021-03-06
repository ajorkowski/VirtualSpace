﻿namespace VideoDecoders.MediaFoundation.Mkv
{
    public struct MkvBlockHeader
    {
        public bool NeedsNextPhase { get; set; }

        public int TrackNumber { get; set; }
        public ulong TimeCode { get; set; }
        public ulong Duration { get; set; }

        public bool KeyFrame { get; set; }
        public bool Invisible { get; set; }
        public Lacing Lacing { get; set; }
        public bool Discardable { get; set; }

        public ulong ReferenceTimeCode { get; set; }
    }

    public enum Lacing
    {
        None = 0,
        Xiph,
        EBML,
        FixedSize
    }
}
