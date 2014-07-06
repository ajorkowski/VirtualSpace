using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public class MkvMetadata
    {
        public SegmentInfo Info { get; set; }
        public IEnumerable<TrackEntry> Tracks { get; set; }
    }

    public class SegmentInfo
    {
        public SegmentInfo()
        {
            TimecodeScale = 1000000;
        }

        public string SegmentFilename { get; set; }
        public ulong TimecodeScale { get; set; }
        public double Duration { get; set; }
        public DateTime? DateUTC { get; set; }
        public string Title { get; set; }
    }

    public class TrackEntry
    {
        // set all the defaults in the constructor
        public TrackEntry()
        {
            FlagEnabled = true;
            FlagDefault = true;
            FlagLacing = true;
            Language = "eng";
            CodecDecodeAll = true;
            Video = new VideoSettings();
            Audio = new AudioSettings();
        }

        public ulong TrackNumber { get; set; }
        public ulong TrackUID { get; set; }
        public TrackType TrackType { get; set; }
        public bool FlagEnabled { get; set; }
        public bool FlagDefault { get; set; }
        public bool FlagForced { get; set; }
        public bool FlagLacing { get; set; }
        public ulong MinCache { get; set; }
        public ulong MaxBlockAdditionID { get; set; }
        public string Name { get; set; }
        public string Language { get; set; }
        public string CodecID { get; set; }
        public byte[] CodecPrivate { get; set; }
        public string CodecName { get; set; }
        public bool CodecDecodeAll { get; set; }
        public ulong CodecDelay { get; set; }
        public ulong SeekPreRoll { get; set; }
        public VideoSettings Video { get; set; }
        public AudioSettings Audio { get; set; }
    }

    public class VideoSettings
    {
        public bool FlagInterlaced { get; set; }
        public StereoMode StereoMode { get; set; }
        public ulong AlphaMode { get; set; }
        public ulong PixelWidth { get; set; }
        public ulong PixelHeight { get; set; }
        public ulong PixelCropBottom { get; set; }
        public ulong PixelCropTop { get; set; }
        public ulong PixelCropLeft { get; set; }
        public ulong PixelCropRight { get; set; }
        public ulong DisplayWidth { get; set; }
        public ulong DisplayHeight { get; set; }
        public DisplayUnit DisplayUnit { get; set; }
        public AspectRatioType AspectRatioType { get; set; }
    }

    public class AudioSettings
    {
        public AudioSettings()
        {
            SamplingFrequency = 8000;
            Channels = 1;
        }

        public double SamplingFrequency { get; set; }
        public double OutputSamplingFrequency { get; set; }
        public ulong Channels { get; set; }
        public ulong BitDepth { get; set; }
    }

    public enum TrackType : ulong
    {
        Video = 1,
        Audio = 2,
        Complex = 3,
        Logo = 0x10,
        Subtitle = 0x11,
        Buttons = 0x12,
        Control = 0x20
    }

    public enum StereoMode : ulong
    {
        Mono = 0,
        SideBySideLeftFirst = 1,
        TopBottomRightFirst = 2,
        TopBottomLeftFirst = 3,
        CheckBoardRightFirst = 4,
        CheckBoardLeftFirst = 5,
        RowInterleavedRightFirst = 6,
        RowInterleavedLeftFirst = 7,
        ColumnInterleavedRightFirst = 8,
        ColumnInterleavedLeftFirst = 9,
        AnaglyphCyanRed = 10,
        SideBySideRightFirst = 11,
        AnaglyphGreenMagenta = 12,
        LacedLeftFirst = 13,
        LacedRightFirst = 14
    }

    public enum DisplayUnit : ulong
    {
        Pixels = 0,
        Centimeters = 1,
        Inches = 2,
        DisplayAspectRatio = 3
    }

    public enum AspectRatioType : ulong
    {
        FreeResizing = 0,
        KeepAspectRatio = 1,
        Fixed = 2
    }
}
