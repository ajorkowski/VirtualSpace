using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.Transform;
using System;
using System.Runtime.InteropServices;

namespace VideoDecoders.MediaFoundation.DirectShowAudio
{
    // http://msdn.microsoft.com/en-us/library/windows/desktop/aa965264(v=vs.85).aspx
    [ComVisible(true)]
    [Guid("9D6F50A9-8D55-4ED2-B5F6-04A397C52689")]
    public class DirectShowAudioDecoderTransform : COMBase, IMFTransform
    {
        public readonly static MFTRegisterTypeInfo[] Inputs = new MFTRegisterTypeInfo[]
        {
            new MFTRegisterTypeInfo { guidMajorType = MFMediaType.Audio, guidSubtype = MediaSubTypes.MEDIASUBTYPE_DTS }
        };

        public readonly static MFTRegisterTypeInfo[] Outputs = new MFTRegisterTypeInfo[]
        {
            new MFTRegisterTypeInfo { guidMajorType = MFMediaType.Audio, guidSubtype = MediaSubTypes.MEDIASUBTYPE_IEEE_FLOAT },
            new MFTRegisterTypeInfo { guidMajorType = MFMediaType.Audio, guidSubtype = MediaSubTypes.MEDIASUBTYPE_PCM }
        };

        private IMFMediaType _inputType;
        private IMFMediaType _outputType;

        public int ProcessMessage(MFTMessageType eMessage, IntPtr ulParam)
        {
            if(_inputType == null || _outputType == null)
            {
                return MFError.MF_E_TRANSFORM_TYPE_NOT_SET;
            }

            switch(eMessage)
            {
                case MFTMessageType.NotifyBeginStreaming:
                    Initialise();
                    break;
                // These are messages we can safely ignore
                case MFTMessageType.SetD3DManager:
                    break;
                // We have not implemented these messages
                default:
                    return E_NotImplemented;
            }

            return S_Ok;
        }

        private void Initialise()
        {

        }

        public int GetStreamCount(MFInt pcInputStreams, MFInt pcOutputStreams)
        {
            pcInputStreams.Assign(1);
            pcOutputStreams.Assign(1);

            return S_Ok;
        }

        public int GetStreamIDs(int dwInputIDArraySize, int[] pdwInputIDs, int dwOutputIDArraySize, int[] pdwOutputIDs)
        {
            return E_NotImplemented;
        }

        public int GetStreamLimits(MFInt pdwInputMinimum, MFInt pdwInputMaximum, MFInt pdwOutputMinimum, MFInt pdwOutputMaximum)
        {
            pdwInputMinimum.Assign(1);
            pdwInputMaximum.Assign(1);
            pdwOutputMinimum.Assign(1);
            pdwOutputMaximum.Assign(1);
            return S_Ok;
        }

        public int AddInputStreams(int cStreams, int[] adwStreamIDs)
        {
            return E_NotImplemented;
        }

        public int DeleteInputStream(int dwStreamID)
        {
            return E_NotImplemented;
        }

        public int GetInputCurrentType(int dwInputStreamID, out IMFMediaType ppType)
        {
            ppType = null;
            if (dwInputStreamID != 0) { return MFError.MF_E_INVALIDSTREAMNUMBER; }

            if (_inputType == null) { return MFError.MF_E_TRANSFORM_TYPE_NOT_SET; }

            TestSuccess("Could not create media type", MFExtern.MFCreateMediaType(out ppType));
            TestSuccess("Could not copy media type attributes", _inputType.CopyAllItems(ppType));
            return S_Ok;
        }

        public int GetInputAvailableType(int dwInputStreamID, int dwTypeIndex, out IMFMediaType ppType)
        {
            ppType = null;
            if (dwInputStreamID != 0) 
            {
                return MFError.MF_E_INVALIDSTREAMNUMBER; 
            }

            if(dwTypeIndex >= Inputs.Length || dwTypeIndex < 0)
            {
                return MFError.MF_E_NO_MORE_TYPES;
            }

            TestSuccess("Could not create media type", MFExtern.MFCreateMediaType(out ppType));

            TestSuccess("Could not set audio type", ppType.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, Inputs[dwTypeIndex].guidMajorType));
            TestSuccess("Could not set audio subtype", ppType.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, Inputs[dwTypeIndex].guidSubtype));

            return S_Ok;
        }

        public int SetInputType(int dwInputStreamID, IMFMediaType pType, MFTSetTypeFlags dwFlags)
        {
            if (dwInputStreamID != 0)
            {
                return MFError.MF_E_INVALIDSTREAMNUMBER; 
            }

            if(dwFlags == MFTSetTypeFlags.TestOnly && pType == null)
            {
                return MFError.MF_E_INVALIDMEDIATYPE;
            }

            if(_inputType != null || pType == null)
            {
                // TODO: handle changes?
                throw new NotImplementedException();
            }

            // Test major/minor types first
            Guid majorType;
            TestSuccess("Could not get major type", pType.GetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, out majorType));
            Guid subType;
            TestSuccess("Could not get major type", pType.GetGUID(MFAttributesClsid.MF_MT_SUBTYPE, out subType));

            bool isValid = false;
            foreach(var i in Inputs)
            {
                if(i.guidMajorType == majorType && i.guidSubtype == subType)
                {
                    isValid = true;
                    break;
                }
            }

            if (!isValid) { return MFError.MF_E_INVALIDMEDIATYPE; }
            if (dwFlags == MFTSetTypeFlags.TestOnly) { return S_Ok; }

            // Looks like we are supposed to copy reference this type...
            _inputType = pType;
            return S_Ok;
        }

        public int GetInputStreamInfo(int dwInputStreamID, out MFTInputStreamInfo pStreamInfo)
        {
            throw new NotImplementedException();
        }

        public int GetOutputCurrentType(int dwOutputStreamID, out IMFMediaType ppType)
        {
            ppType = null;
            if (dwOutputStreamID != 0) { return MFError.MF_E_INVALIDSTREAMNUMBER; }

            if (_outputType == null) { return MFError.MF_E_TRANSFORM_TYPE_NOT_SET; }

            TestSuccess("Could not create media type", MFExtern.MFCreateMediaType(out ppType));
            TestSuccess("Could not copy media type attributes", _outputType.CopyAllItems(ppType));
            return S_Ok;
        }

        public int GetOutputAvailableType(int dwOutputStreamID, int dwTypeIndex, out IMFMediaType ppType)
        {
            ppType = null;
            if (dwOutputStreamID != 0)
            {
                return MFError.MF_E_INVALIDSTREAMNUMBER;
            }

            if (dwTypeIndex >= Outputs.Length || dwTypeIndex < 0)
            {
                return MFError.MF_E_NO_MORE_TYPES;
            }

            TestSuccess("Could not create media type", MFExtern.MFCreateMediaType(out ppType));

            TestSuccess("Could not set audio type", ppType.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, Outputs[dwTypeIndex].guidMajorType));
            TestSuccess("Could not set audio subtype", ppType.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, Outputs[dwTypeIndex].guidSubtype));

            return S_Ok;
        }

        public int SetOutputType(int dwOutputStreamID, IMFMediaType pType, MFTSetTypeFlags dwFlags)
        {
            if (dwOutputStreamID != 0)
            {
                return MFError.MF_E_INVALIDSTREAMNUMBER;
            }

            if (dwFlags == MFTSetTypeFlags.TestOnly && pType == null)
            {
                return MFError.MF_E_INVALIDMEDIATYPE;
            }

            // Need input type first... so we can copy details!
            if(_inputType == null)
            {
                return MFError.MF_E_TRANSFORM_TYPE_NOT_SET;
            }

            if (pType == null)
            {
                // TODO: handle changes?
                throw new NotImplementedException();
            }

            Guid majorType;
            TestSuccess("Could not get major type", pType.GetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, out majorType));
            Guid subType;
            TestSuccess("Could not get major type", pType.GetGUID(MFAttributesClsid.MF_MT_SUBTYPE, out subType));

            bool isValid = false;
            foreach (var i in Outputs)
            {
                if (i.guidMajorType == majorType && i.guidSubtype == subType)
                {
                    isValid = true;
                    break;
                }
            }

            if (!isValid) { return MFError.MF_E_INVALIDMEDIATYPE; }
            if (dwFlags == MFTSetTypeFlags.TestOnly) { return S_Ok; }

            // Looks like we are supposed to copy reference this type
            _outputType = pType;

            // Calculate derived values.
            // http://msdn.microsoft.com/en-us/library/windows/desktop/ff485864(v=vs.85).aspx
            int cChannels;
            if(_outputType.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_NUM_CHANNELS, out cChannels) != S_Ok && _inputType.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_NUM_CHANNELS, out cChannels) != S_Ok)
            {
                return MFError.MF_E_INVALIDMEDIATYPE;
            }

            int samplesPerSec;
            if (_outputType.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_SAMPLES_PER_SECOND, out samplesPerSec) != S_Ok && _inputType.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_SAMPLES_PER_SECOND, out samplesPerSec) != S_Ok)
            {
                return MFError.MF_E_INVALIDMEDIATYPE;
            }

            int bitsPerSample;
            if (_outputType.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_BITS_PER_SAMPLE, out bitsPerSample) != S_Ok && _inputType.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_BITS_PER_SAMPLE, out bitsPerSample) != S_Ok)
            {
                bitsPerSample = subType == MediaSubTypes.MEDIASUBTYPE_IEEE_FLOAT ? 32 : 16; // Apparently a good default
            }

            var blockAlign = cChannels * (bitsPerSample / 8);
            var bytesPerSecond = blockAlign * samplesPerSec;

            // Set all the expected audio types... this will override existing values... but I guess other transforms will take care of type transforms?
            _outputType.SetUINT32(MFAttributesClsid.MF_MT_AUDIO_NUM_CHANNELS, cChannels);
            _outputType.SetUINT32(MFAttributesClsid.MF_MT_AUDIO_SAMPLES_PER_SECOND, samplesPerSec);
            _outputType.SetUINT32(MFAttributesClsid.MF_MT_AUDIO_BLOCK_ALIGNMENT, blockAlign);
            _outputType.SetUINT32(MFAttributesClsid.MF_MT_AUDIO_AVG_BYTES_PER_SECOND, bytesPerSecond);
            _outputType.SetUINT32(MFAttributesClsid.MF_MT_AUDIO_BITS_PER_SAMPLE, bitsPerSample);
            _outputType.SetUINT32(MFAttributesClsid.MF_MT_ALL_SAMPLES_INDEPENDENT, 1);

            int channelMask;
            if (_outputType.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_CHANNEL_MASK, out channelMask) == S_Ok || _inputType.GetUINT32(MFAttributesClsid.MF_MT_AUDIO_CHANNEL_MASK, out channelMask) == S_Ok)
            {
                _outputType.SetUINT32(MFAttributesClsid.MF_MT_AUDIO_CHANNEL_MASK, channelMask);
            }

            return S_Ok;
        }

        public int GetOutputStreamInfo(int dwOutputStreamID, out MFTOutputStreamInfo pStreamInfo)
        {
            throw new NotImplementedException();
        }

        public int GetInputStatus(int dwInputStreamID, out MFTInputStatusFlags pdwFlags)
        {
            throw new NotImplementedException();
        }

        public int GetOutputStatus(out MFTOutputStatusFlags pdwFlags)
        {
            throw new NotImplementedException();
        }

        public int GetOutputStreamAttributes(int dwOutputStreamID, out IMFAttributes pAttributes)
        {
            pAttributes = null;
            return E_NotImplemented;
        }

        public int GetInputStreamAttributes(int dwInputStreamID, out IMFAttributes pAttributes)
        {
            pAttributes = null;
            return E_NotImplemented;
        }

        public int GetAttributes(out IMFAttributes pAttributes)
        {
            pAttributes = null;
            return E_NotImplemented;
        }

        public int ProcessEvent(int dwInputStreamID, IMFMediaEvent pEvent)
        {
            throw new NotImplementedException();
        }

        public int ProcessInput(int dwInputStreamID, IMFSample pSample, int dwFlags)
        {
            throw new NotImplementedException();
        }

        public int ProcessOutput(MFTProcessOutputFlags dwFlags, int cOutputBufferCount, MFTOutputDataBuffer[] pOutputSamples, out ProcessOutputStatus pdwStatus)
        {
            throw new NotImplementedException();
        }

        public int SetOutputBounds(long hnsLowerBound, long hnsUpperBound)
        {
            throw new NotImplementedException();
        }

        private void TestSuccess(string message, int hResult)
        {
            if (hResult < 0)
            {
                throw new COMException(message, hResult);
            }
        }
    }
}
