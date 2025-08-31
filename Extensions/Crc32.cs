using System.IO;

namespace Extensions
{
    public static class Crc32
    {
        private static readonly uint[] Table;

        static Crc32()
        {
            const uint poly = 0xEDB88320;
            Table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint temp = i;
                for (int j = 0; j < 8; j++)
                {
                    if ((temp & 1) != 0)
                    {
                        temp = (temp >> 1) ^ poly;
                    }
                    else
                    {
                        temp >>= 1;
                    }
                }
                Table[i] = temp;
            }
        }

        public static uint ComputeFile(string filePath)
        {
            const int bufferSize = 8192;
            byte[] buffer = new byte[bufferSize];
            uint crc = 0xFFFFFFFF;

            using (FileStream fs = File.OpenRead(filePath))
            {
                int bytesRead;
                while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < bytesRead; i++)
                    {
                        crc = (crc >> 8) ^ Table[(byte)(crc ^ buffer[i])];
                    }
                }
            }

            return crc ^ 0xFFFFFFFF;
        }
    }
}
