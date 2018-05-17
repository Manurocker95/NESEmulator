namespace NES
{
    public class UxRom : Mapper
    {
        private const ushort BankSize = 0x4000; // 16KB
        private const ushort PrgRomAddress = 0x8000;
        private const ushort PrgRomLastBankAddress = 0xC000;
        private const ushort PrgRamAddress = 0x6000;

        private int bankSelect;

        public UxRom(Cartridge cartridge) : base(cartridge) { }

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
            if(address >= PrgRomAddress)
            {
                bankSelect = data & 0x0F;
            }
            else if (address >= PrgRamAddress && address < PrgRomAddress)
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
            if (address < PrgRomLastBankAddress)
            {
                return address + BankSize * bankSelect;
            }
            else
            {
                return address - PrgRomLastBankAddress;
            }
        }

        private int GetPrgRamIndex(ushort address)
        {
            return address - PrgRamAddress;
        }
    }
}
