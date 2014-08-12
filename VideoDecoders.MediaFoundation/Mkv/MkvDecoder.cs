using NEbml.Core;
using NEbml.MkvTitleEdit.Matroska;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VideoDecoders.MediaFoundation.Mkv
{
    public class MkvDecoder : IDisposable
    {
        private readonly EbmlReader _reader;
        private readonly MatroskaElementDescriptorProvider _medp;
        private readonly MkvMetadata _metadata;
        private readonly StreamMetadata _streamMetadata;

        private bool _hasStarted;
        private bool _hasFinished;
        
        // Cluster/block tracking
        private ulong _currentClusterTimecode;
        private bool _isInBlockGroup;
        private byte[] _blockHeaderBuffer;

        public MkvDecoder(Stream stream, StreamMetadata streamMetadata)
        {
            _reader = new EbmlReader(stream);
            _medp = new MatroskaElementDescriptorProvider();
            _currentClusterTimecode = ulong.MaxValue;

            _metadata = GetMetadata();
            _streamMetadata = streamMetadata;
            _blockHeaderBuffer = new byte[21]; // 21 is the max, but could be variable...
        }

        public MkvMetadata Metadata { get { return _metadata; } }
        public StreamMetadata StreamMetadata { get { return _streamMetadata; } }

        public bool SeekNextBlock(List<int> validTracks, ref MkvBlockHeader header)
        {
            if(_hasFinished)
            {
                return false;
            }

            while (true)
            {
                if (!_hasStarted)
                {
                    if (!_reader.LocateElement(MatroskaElementDescriptorProvider.Cluster))
                    {
                        // No more clusters
                        _hasFinished = true;
                        return false;
                    }

                    _reader.EnterContainer();
                    _hasStarted = true;
                }

                while (_reader.ReadNext())
                {
                    var descriptor = _medp.GetElementDescriptor(_reader.ElementId);
                    if (descriptor == null) continue;

                    if (_isInBlockGroup)
                    {
                        // Block Group reading
                        switch (descriptor.Name)
                        {
                            case "Block":
                                ReadBlockHeader(false, ref header);
                                if (!validTracks.Contains(header.TrackNumber))
                                {
                                    continue;
                                }
                                return true;
                        }
                    }
                    else
                    {
                        // Cluster reads
                        switch (descriptor.Name)
                        {
                            case "Timecode":
                                _currentClusterTimecode = _reader.ReadUInt() * Metadata.Info.TimecodeScale;
                                break;
                            case "SimpleBlock":
                                ReadBlockHeader(true, ref header);
                                if (!validTracks.Contains(header.TrackNumber))
                                {
                                    continue;
                                }
                                return true;
                            case "BlockGroup":
                                _isInBlockGroup = true;
                                _reader.EnterContainer();
                                break;
                        }
                    }
                }

                if (_isInBlockGroup)
                {
                    _reader.LeaveContainer();
                    continue;
                }

                _reader.LeaveContainer();
                _currentClusterTimecode = ulong.MaxValue;
                _hasStarted = false;
            }
        }

        public long RemainingBlockSize
        {
            get 
            {
                return _reader.RemainingBytes;
            }
        }

        public int ReadBlock(byte[] buffer, int offset, int length)
        {
            return _reader.ReadBinary(buffer, offset, length);
        }

        private void ReadBlockHeader(bool isSimple, ref MkvBlockHeader header)
        {
            if (_currentClusterTimecode == ulong.MaxValue)
            {
                throw new InvalidOperationException("The cluster does not have a timecode - invalid");
            }

            if (!isSimple)
            {
                // TODO: Implement Block reading
                throw new NotImplementedException();
            }

            var trackNumber = (int)_reader.ReadVarIntInline(8).Value;
            var timecode = _reader.ReadSignedIntegerInline(2) * (long)Metadata.Info.TimecodeScale;

            var track = Metadata.Tracks.FirstOrDefault(t => (int)t.TrackNumber == trackNumber);
            if (track == null)
            {
                throw new InvalidOperationException("Cannot find associated track for this simple block");
            }

            header.TrackNumber = trackNumber;
            if(timecode < 0)
            {
                var posTimecode = (ulong)Math.Abs(-timecode);
                if(posTimecode > _currentClusterTimecode)
                {
                    throw new InvalidOperationException("Timecode for simple block is less than 0");
                }
                header.TimeCode = _currentClusterTimecode - posTimecode;
            }
            else
            {
                header.TimeCode = _currentClusterTimecode + (ulong)timecode;
            }
            header.Duration = track.DefaultDuration;
            
            // Read a byte for flags
            _reader.ReadBinary(_blockHeaderBuffer, 0, 1);
            byte flags = _blockHeaderBuffer[0];
            header.KeyFrame = (flags & (1 << 0)) != 0;
            header.Invisible = (flags & (1 << 4)) != 0;
            bool firstLace = (flags & (1 << 5)) != 0;
            bool secondLace = (flags & (1 << 6)) != 0;
            header.Discardable = (flags & (1 << 7)) != 0;

            header.Lacing = firstLace ? (secondLace ? Lacing.EBML : Lacing.FixedSize) : (secondLace ? Lacing.Xiph : Lacing.None);

            if(header.Lacing != Lacing.None)
            {
                throw new NotImplementedException();
            }
        }

        private MkvMetadata GetMetadata()
        {
            var tracks = new List<TrackEntry>();
            var info = new SegmentInfo();

            double duration = 0;
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
                                    duration = r.ReadFloat();
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
                                        case "MaxCache":
                                            track.MaxCache = r.ReadUInt();
                                            break;
                                        case "DefaultDuration":
                                            track.DefaultDuration = r.ReadUInt();
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
                                        case "CodecPrivate":
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

            if (info.TimecodeScale == 0)
            {
                throw new InvalidOperationException("Timecode scale must be defined");
            }

            info.Duration = (ulong)(duration * info.TimecodeScale);

            return new MkvMetadata { Info = info, Tracks = tracks };
        }

        public void Dispose()
        {
            _reader.Dispose();
        }
    }
}
