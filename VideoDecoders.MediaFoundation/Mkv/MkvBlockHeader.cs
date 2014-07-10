namespace VideoDecoders.MediaFoundation.Mkv
{
    public struct MkvBlockHeader
    {
        public int TrackNumber { get; set; }
        public long TimeCode { get; set; }

        public bool KeyFrame { get; set; }
        public bool Invisible { get; set; }
        public Lacing Lacing { get; set; }
        public bool Discardable { get; set; }
    }

    public enum Lacing
    {
        None = 0,
        Xiph,
        EBML,
        FixedSize
    }
}
