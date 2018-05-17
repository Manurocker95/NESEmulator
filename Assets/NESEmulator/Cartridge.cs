using System.IO;
using static NES.Constants;

namespace NES
{
    public class Cartridge
    {
        private const uint iNesHeader = 0x1A53454E; // NES<EOF>

        private const byte HeaderSize = 16;
        private const ushort TrainerSize = 512;
        private const byte TrainerBit = 0x04;

        public byte[] PrgRAM { get; private set; }
        public byte[] PrgRom { get; private set; }
        public byte[] Chr { get; private set; }
        public byte PrgRomBanks { get; private set; }
        public byte ChrBanks { get; private set; }
        public byte Flag6 { get; private set; }
        public byte Flag7 { get; private set; }
        public byte Mapper { get; private set; }
        public bool Mirror { get; private set; }
        public bool IsValid { get; private set; }

        public Cartridge(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(stream);

            // Parse Header
            if (iNesHeader != reader.ReadUInt32())
            {
                IsValid = false;
                return;
            }

            PrgRomBanks = reader.ReadByte();
            ChrBanks = reader.ReadByte();
            Flag6 = reader.ReadByte();
            Flag7 = reader.ReadByte();

            // Load PRGROM
            var prgRomSize = SixteenKB * PrgRomBanks;
            PrgRom = new byte[prgRomSize];
            var seekOffset = Flag6.IsBitSet(TrainerBit) ? HeaderSize + TrainerSize : HeaderSize;
            reader.BaseStream.Seek(seekOffset, SeekOrigin.Begin);
            reader.Read(PrgRom, 0, prgRomSize);

            // Load CHR
            if (ChrBanks != 0)
            {
                // ROM
                var chrRomSize = EightKB * ChrBanks;
                Chr = new byte[chrRomSize];
                reader.Read(Chr, 0, chrRomSize);
            }
            else
            {
                // RAM
                Chr = new byte[EightKB];
            }

            PrgRAM = new byte[EightKB];

            Mapper = (byte)(Flag7 & 0xF0 | Flag6 >> 4 & 0xF);

            IsValid = true;
        }
    }
}
