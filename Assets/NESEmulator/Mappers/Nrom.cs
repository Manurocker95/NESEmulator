namespace NES
{
    public class Nrom : Mapper
    {
        private const ushort PrgRomAddress = 0x8000;
        private const ushort PrgRomMirrorAddress = 0xC000;
        private const ushort PrgRamAddress = 0x6000;

        public Nrom(Cartridge cartridge) : base(cartridge) { }

        public override byte Read(ushort address)
        {
            byte data = 0;

            if (address >= PrgRomAddress)
            {
                data = cartridge.PrgRom[GetPrgRomIndex(address)];
            }
            else if (address >= PrgRamAddress)
            {
                data = cartridge.PrgRAM[GetPrgRamIndex(address)];
            }
            else
            {
                data = base.Read(address);
            }
            return data;
        }

        public override void Write(ushort address, byte data)
        {
            if (address >= PrgRamAddress && address < PrgRomAddress)
            {
                cartridge.PrgRAM[GetPrgRamIndex(address)] = data;
            }
            else
            {
                base.Write(address, data);
            }
        }

        private int GetPrgRomIndex(ushort address)
        {
            if (cartridge.PrgRomBanks > 1)
            {
                return address - PrgRomAddress;
            }
            else
            {
                return address - PrgRomMirrorAddress;
            }
        }

        private int GetPrgRamIndex(ushort address)
        {
            return address - PrgRamAddress;
        }
    }
}
