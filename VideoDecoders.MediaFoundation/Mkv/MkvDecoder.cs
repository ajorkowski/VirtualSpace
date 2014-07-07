using NEbml.Core;
using NEbml.MkvTitleEdit.Matroska;
using System;
using System.Collections.Generic;
using System.IO;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public class MkvDecoder : IDisposable
    {
        private readonly EbmlReader _reader;
        private readonly MatroskaElementDescriptorProvider _medp;
        private readonly Lazy<MkvMetadata> _metadata;
        private readonly StreamMetadata _streamMetadata;

        public MkvDecoder(Stream stream, StreamMetadata streamMetadata)
        {
            _reader = new EbmlReader(stream);
            _medp = new MatroskaElementDescriptorProvider();
            _metadata = new Lazy<MkvMetadata>(GetMetadata);
            _streamMetadata = streamMetadata;
        }

        public MkvMetadata Metadata { get { return _metadata.Value; } }
        public StreamMetadata StreamMetadata { get { return _streamMetadata; } }

        private MkvMetadata GetMetadata()
        {
            var tracks = new List<TrackEntry>();
            var info = new SegmentInfo();

            var r = _reader;
            var segmentFound = r.LocateElement(MatroskaElementDescriptorProvider.Segment);
            if (segmentFound)
            {
                r.EnterContainer();
                while (r.ReadNext())
                {
                    var descriptor = _medp.GetElementDescriptor(r.ElementId);
                    if (descriptor == null) continue;

                    if(descriptor.Name == "Info")
                    {
                        r.EnterContainer();
                        while (r.ReadNext())
                        {
                            var infoDescriptor = _medp.GetElementDescriptor(r.ElementId);
                            if (infoDescriptor == null) continue;
                            switch(infoDescriptor.Name)
                            {
                                case "SegmentFilename":
                                    info.SegmentFilename = r.ReadUtf();
                                    break;
                                case "TimecodeScale":
                                    info.TimecodeScale = r.ReadUInt();
                                    break;
                                case "Duration":
                                    info.Duration = r.ReadFloat();
                                    break;
                                case "DateUTC":
                                    info.DateUTC = r.ReadDate();
                                    break;
                                case "Title":
                                    info.Title = r.ReadUtf();
                                    break;
                            }
                        }
                        r.LeaveContainer();
                    }

                    if (descriptor.Name == "Tracks")
                    {
                        r.EnterContainer();
                        while (r.ReadNext())
                        {
                            var trackDescriptor = _medp.GetElementDescriptor(r.ElementId);
                            if (trackDescriptor == null) continue;

                            if (trackDescriptor.Name == "TrackEntry")
                            {
                                r.EnterContainer();
                                var track = new TrackEntry();

                                while (r.ReadNext())
                                {
                                    var trackEntryDescriptor = _medp.GetElementDescriptor(r.ElementId);
                                    if (trackEntryDescriptor == null) continue;

                                    switch(trackEntryDescriptor.Name)
                                    {
                                        case "TrackNumber":
                                            track.TrackNumber = r.ReadUInt();
                                            break;
                                        case "TrackUID":
                                            track.TrackUID = r.ReadUInt();
                                            break;
                                        case "TrackType":
                                            track.TrackType = (TrackType)r.ReadUInt();
                                            break;
                                        case "FlagEnabled":
                                            track.FlagEnabled = r.ReadUInt() == 1;
                                            break;
                                        case "FlagDefault":
                                            track.FlagDefault = r.ReadUInt() == 1;
                                            break;
                                        case "FlagForced":
                                            track.FlagForced = r.ReadUInt() == 1;
                                            break;
                                        case "FlagLacing":
                                            track.FlagLacing = r.ReadUInt() == 1;
                                            break;
                                        case "MinCache":
                                            track.MinCache = r.ReadUInt();
                                            break;
                                        case "MaxBlockAdditionID":
                                            track.MaxBlockAdditionID = r.ReadUInt();
                                            break;
                                        case "Name":
                                            track.Name = r.ReadUtf();
                                            break;
                                        case "Language":
                                            track.Language = r.ReadAscii();
                                            break;
                                        case "CodecID":
                                            track.CodecID = r.ReadAscii();
                                            break;
                                        case "CodecP":
                                            track.CodecPrivate = new byte[r.ElementSize];
                                            r.ReadBinary(track.CodecPrivate, 0, (int)r.ElementSize);
                                            break;
                                        case "CodecName":
                                            track.CodecName = r.ReadUtf();
                                            break;
                                        case "CodecDecodeAll":
                                            track.CodecDecodeAll = r.ReadUInt() == 1;
                                            break;
                                        case "CodecDelay":
                                            track.CodecDelay = r.ReadUInt();
                                            break;
                                        case "SeekPreRoll":
                                            track.SeekPreRoll = r.ReadUInt();
                                            break;
                                        case "Video":
                                            r.EnterContainer();
                                            while(r.ReadNext())
                                            {
                                                var videoDescriptor = _medp.GetElementDescriptor(r.ElementId);
                                                if (videoDescriptor == null) continue;
                                                switch(videoDescriptor.Name)
                                                {
                                                    case "FlagInterlaced":
                                                        track.Video.FlagInterlaced = r.ReadUInt() == 1;
                                                        break;
                                                    case "StereoMode":
                                                        track.Video.StereoMode = (StereoMode)r.ReadUInt();
                                                        break;
                                                    case "AlphaMode":
                                                        track.Video.AlphaMode = r.ReadUInt();
                                                        break;
                                                    case "PixelWidth":
                                                        track.Video.PixelWidth = r.ReadUInt();
                                                        break;
                                                    case "PixelHeight":
                                                        track.Video.PixelHeight = r.ReadUInt();
                                                        break;
                                                    case "PixelCropBottom":
                                                        track.Video.PixelCropBottom = r.ReadUInt();
                                                        break;
                                                    case "PixelCropTop":
                                                        track.Video.PixelCropTop = r.ReadUInt();
                                                        break;
                                                    case "PixelCropLeft":
                                                        track.Video.PixelCropLeft = r.ReadUInt();
                                                        break;
                                                    case "PixelCropRight":
                                                        track.Video.PixelCropRight = r.ReadUInt();
                                                        break;
                                                    case "DisplayWidth":
                                                        track.Video.DisplayWidth = r.ReadUInt();
                                                        break;
                                                    case "DisplayHeight":
                                                        track.Video.DisplayHeight = r.ReadUInt();
                                                        break;
                                                    case "DisplayUnit":
                                                        track.Video.DisplayUnit = (DisplayUnit)r.ReadUInt();
                                                        break;
                                                    case "AspectRatioType":
                                                        track.Video.AspectRatioType = (AspectRatioType)r.ReadUInt();
                                                        break;
                                                }
                                            }

                                            if(track.Video.DisplayUnit == DisplayUnit.Pixels)
                                            {
                                                if (track.Video.DisplayWidth == 0) { track.Video.DisplayWidth = track.Video.PixelWidth; }
                                                if (track.Video.DisplayHeight == 0) { track.Video.DisplayHeight = track.Video.PixelHeight; }
                                            }

                                            r.LeaveContainer();
                                            break;
                                        case "Audio":
                                            r.EnterContainer();
                                            while (r.ReadNext())
                                            {
                                                var audioDescriptor = _medp.GetElementDescriptor(r.ElementId);
                                                if (audioDescriptor == null) continue;
                                                switch (audioDescriptor.Name)
                                                {
                                                    case "SamplingFrequency":
                                                        track.Audio.SamplingFrequency = r.ReadFloat();
                                                        break;
                                                    case "OutputSamplingFrequency":
                                                        track.Audio.OutputSamplingFrequency = r.ReadFloat();
                                                        break;
                                                    case "Channels":
                                                        track.Audio.Channels = r.ReadUInt();
                                                        break;
                                                    case "BitDepth":
                                                        track.Audio.BitDepth = r.ReadUInt();
                                                        break;
                                                }
                                            }

                                            if (track.Audio.OutputSamplingFrequency == 0)
                                            {
                                                track.Audio.OutputSamplingFrequency = track.Audio.SamplingFrequency;
                                            }

                                            r.LeaveContainer();
                                            break;
                                    }
                                }

                                tracks.Add(track);
                                r.LeaveContainer();
                            }
                        }
                        r.LeaveContainer();
                        break;
                    }
                }
            }

            return new MkvMetadata { Info = info, Tracks = tracks };
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
