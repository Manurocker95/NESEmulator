// SxROM Boards

namespace NES
{
    public class Mmc1: Mapper
    {
        public Mmc1(Cartridge cartridge) : base(cartridge) { }

        public override byte Read(ushort address)
        {
            byte data = 0;

            return data;
        }

        public override void Write(ushort address, byte data)
        {
         
        }

        private int GetPrgRomIndex(ushort address)
        {
            return 0;
        }

        private int GetPrgRamIndex(ushort address)
        {
            return 0;
        }
    }
}
