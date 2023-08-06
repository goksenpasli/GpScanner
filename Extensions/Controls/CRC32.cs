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

        public override void Initialize() { hashValue = 0xFFFFFFFF; }

        protected override void HashCore(byte[] array, int ibStart, int cbSize)
        {
            for(int i = ibStart; i < cbSize; i++)
            {
                hashValue = (hashValue >> 8) ^ table[array[i] ^ (hashValue & 0xFF)];
            }
        }

        protected override byte[] HashFinal()
        {
            hashValue = ~hashValue;
            byte[] hashBuffer = new byte[4];
            hashBuffer[0] = (byte)((hashValue >> 24) & 0xFF);
            hashBuffer[1] = (byte)((hashValue >> 16) & 0xFF);
            hashBuffer[2] = (byte)((hashValue >> 8) & 0xFF);
            hashBuffer[3] = (byte)(hashValue & 0xFF);
            return hashBuffer;
        }

        private static uint[] InitializeTable(uint polynomial)
        {
            uint[] table = new uint[256];

            for(uint i = 0; i < 256; i++)
            {
                uint crc = i;
                for(uint j = 8; j > 0; j--)
                {
                    if((crc & 1) == 1)
                    {
                        crc = (crc >> 1) ^ polynomial;
                    } else
                    {
                        crc >>= 1;
                    }
                }
                table[i] = crc;
            }

            return table;
        }
    }
}
