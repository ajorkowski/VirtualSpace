using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VideoDecoders.MediaFoundation.Mkv
{
    [ComVisible(true)]
    [Guid("3FD9C8B1-33C2-434D-9193-61B08D39B620")]
    public class MkvDecoderActivator : COMBase, IMFActivate
    {
        public int ActivateObject(Guid riid, out object ppv)
        {
            if (riid == typeof(IMFByteStreamHandler).GUID)
            {
                ppv = new MkvDecoderByteStreamHandler();
                return S_Ok;
            }

            ppv = null;
            return E_InvalidArgument;
        }

        public int ShutdownObject()
        {
            return S_Ok;
        }

        public int DetachObject()
        {
            return S_Ok;
        }

        /************************************************
         * Not implemented methods...
         * *********************************************/
        public int Compare(IMFAttributes pTheirs, MFAttributesMatchType MatchType, out bool pbResult)
        {
            throw new NotImplementedException();
        }

        public int CompareItem(Guid guidKey, ConstPropVariant Value, out bool pbResult)
        {
            throw new NotImplementedException();
        }

        public int CopyAllItems(IMFAttributes pDest)
        {
            throw new NotImplementedException();
        }

        public int DeleteAllItems()
        {
            throw new NotImplementedException();
        }

        public int DeleteItem(Guid guidKey)
        {
            throw new NotImplementedException();
        }

        public int GetAllocatedBlob(Guid guidKey, out IntPtr ip, out int pcbSize)
        {
            throw new NotImplementedException();
        }

        public int GetAllocatedString(Guid guidKey, out string ppwszValue, out int pcchLength)
        {
            throw new NotImplementedException();
        }

        public int GetBlob(Guid guidKey, byte[] pBuf, int cbBufSize, out int pcbBlobSize)
        {
            throw new NotImplementedException();
        }

        public int GetBlobSize(Guid guidKey, out int pcbBlobSize)
        {
            throw new NotImplementedException();
        }

        public int GetCount(out int pcItems)
        {
            throw new NotImplementedException();
        }

        public int GetDouble(Guid guidKey, out double pfValue)
        {
            throw new NotImplementedException();
        }

        public int GetGUID(Guid guidKey, out Guid pguidValue)
        {
            throw new NotImplementedException();
        }

        public int GetItem(Guid guidKey, PropVariant pValue)
        {
            throw new NotImplementedException();
        }

        public int GetItemByIndex(int unIndex, out Guid pguidKey, PropVariant pValue)
        {
            throw new NotImplementedException();
        }

        public int GetItemType(Guid guidKey, out MFAttributeType pType)
        {
            throw new NotImplementedException();
        }

        public int GetString(Guid guidKey, StringBuilder pwszValue, int cchBufSize, out int pcchLength)
        {
            throw new NotImplementedException();
        }

        public int GetStringLength(Guid guidKey, out int pcchLength)
        {
            throw new NotImplementedException();
        }

        public int GetUINT32(Guid guidKey, out int punValue)
        {
            throw new NotImplementedException();
        }

        public int GetUINT64(Guid guidKey, out long punValue)
        {
            throw new NotImplementedException();
        }

        public int GetUnknown(Guid guidKey, Guid riid, out object ppv)
        {
            throw new NotImplementedException();
        }

        public int LockStore()
        {
            throw new NotImplementedException();
        }

        public int SetBlob(Guid guidKey, byte[] pBuf, int cbBufSize)
        {
            throw new NotImplementedException();
        }

        public int SetDouble(Guid guidKey, double fValue)
        {
            throw new NotImplementedException();
        }

        public int SetGUID(Guid guidKey, Guid guidValue)
        {
            throw new NotImplementedException();
        }

        public int SetItem(Guid guidKey, ConstPropVariant Value)
        {
            throw new NotImplementedException();
        }

        public int SetString(Guid guidKey, string wszValue)
        {
            throw new NotImplementedException();
        }

        public int SetUINT32(Guid guidKey, int unValue)
        {
            throw new NotImplementedException();
        }

        public int SetUINT64(Guid guidKey, long unValue)
        {
            throw new NotImplementedException();
        }

        public int SetUnknown(Guid guidKey, object pUnknown)
        {
            throw new NotImplementedException();
        }

        public int UnlockStore()
        {
            throw new NotImplementedException();
        }
    }
}
