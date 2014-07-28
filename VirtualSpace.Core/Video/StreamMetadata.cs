namespace VirtualSpace.Core.Video
{
    public class StreamMetadata
    {
        public int TrackNumber { get; set; }
        public string Name { get; set; }
        public string Language { get; set; }
        public bool IsActive { get; set; }
        public StreamType Type { get; set; }

        public bool IsSupported { get; set; }
    }

    public enum StreamType
    {
        Video = 0,
        Audio,
        Subtitle,
        Other
    }
}
