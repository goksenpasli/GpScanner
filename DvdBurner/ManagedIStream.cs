using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace DvdBurner
{
    public class ManagedIStream : IStream
    {
        private Stream source;

        public static dynamic Create(Stream from) => new ManagedIStream { source = from };

        public void Clone(out IStream ppstm) => throw new NotImplementedException();

        public void Commit(int grfCommitFlags) => throw new NotImplementedException();

        public void CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten) => throw new NotImplementedException();

        public void LockRegion(long libOffset, long cb, int dwLockType) => throw new NotImplementedException();

        public void Read(byte[] pv, int cb, IntPtr pcbRead)
        {
            int read = source.Read(pv, 0, cb);
            Marshal.WriteInt32(pcbRead, read);
        }

        public void Revert() => throw new NotImplementedException();

        public void Seek(long dlibMove, int dwOrigin, IntPtr plibNewPosition)
        {
        }
        public void SetSize(long libNewSize) => throw new NotImplementedException();

        public void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG pstatstg, int grfStatFlag) => pstatstg =
        new System.Runtime.InteropServices.ComTypes.STATSTG { type = 2, cbSize = source.Length, grfMode = 0 };

        public void UnlockRegion(long libOffset, long cb, int dwLockType) => throw new NotImplementedException();

        public void Write(byte[] pv, int cb, IntPtr pcbWritten) => throw new NotImplementedException();
    }
}
