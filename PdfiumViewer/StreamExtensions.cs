using System;
using System.IO;

namespace PdfiumViewer
{
    internal static class StreamExtensions
    {
        public static void CopyStream(this Stream from, Stream to)
        {
            if (@from == null)
            {
                throw new ArgumentNullException(nameof(from));
            }

            if (to == null)
            {
                throw new ArgumentNullException(nameof(to));
            }

            byte[] buffer = new byte[4096];

            while (true)
            {
                int read = from.Read(buffer, 0, buffer.Length);

                if (read == 0)
                {
                    return;
                }

                to.Write(buffer, 0, read);
            }
        }

        public static byte[] ToByteArray(this Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            return stream is MemoryStream memoryStream ? memoryStream.ToArray() : stream.CanSeek ? ReadBytesFast(stream) : ReadBytesSlow(stream);
        }

        private static byte[] ReadBytesFast(Stream stream)
        {
            byte[] data = new byte[stream.Length];
            int offset = 0;

            while (offset < data.Length)
            {
                int read = stream.Read(data, offset, data.Length - offset);

                if (read <= 0)
                {
                    break;
                }

                offset += read;
            }

            return offset < data.Length ? throw new InvalidOperationException("Incorrect length reported") : data;
        }

        private static byte[] ReadBytesSlow(Stream stream)
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                CopyStream(stream, memoryStream);

                return memoryStream.ToArray();
            }
        }
    }
}