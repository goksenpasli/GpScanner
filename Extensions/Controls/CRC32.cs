using System;
using System.Security.Cryptography;

namespace Extensions
{
    public class CRC32 : HashAlgorithm
    {
        private const uint polynomial = 0xedb88320;
        private readonly uint[] table;
        private uint hashValue;

        public CRC32()
        {
            table = InitializeTable(polynomial);
            hashValue = 0xFFFFFFFF;
        }

        public override int HashSize => 32;

        public override void Initialize() => hashValue = 0xFFFFFFFF;

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            uint crc = hashValue;
            int end = ibStart + cbSize;

            for (int i = ibStart; i < end; i += 4)
            {
                uint chunk = array[i] | ((uint)array[i + 1] << 8) | ((uint)array[i + 2] << 16) | ((uint)array[i + 3] << 24);

                crc ^= chunk;

                crc = table[(byte)crc] ^ table[(byte)(crc >> 8)] ^ table[(byte)(crc >> 16)] ^ table[(byte)(crc >> 24)] ^ (crc >> 8);
            }

            hashValue = crc;
        }

        protected override byte[] HashFinal()
        {
            hashValue = ~hashValue;
            return BitConverter.GetBytes(hashValue);
        }

        private static uint[] InitializeTable(uint polynomial)
        {
            uint[] table = new uint[256];

            for (uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                {
                    crc = (crc & 1) == 1 ? (crc >> 1) ^ polynomial : crc >> 1;
                }
                table[i] = crc;
            }

            return table;
        }
    }
}
