namespace NES
{
    public class Mapper
    {
        public enum MirrorMode
        {
            Horizontal,
            Vertical
        };

        public MirrorMode mirrorMode;

        protected Cartridge cartridge;

        public Mapper(Cartridge cartridge)
        {
            this.cartridge = cartridge;

            mirrorMode = (cartridge.Flag6 & 1) == 1 ? MirrorMode.Vertical : MirrorMode.Horizontal;
        }

        public virtual byte Read(ushort address)
        {
            byte data = 0;
            if (address < 0x2000)
            {
                data = cartridge.Chr[address];
            }
            return data;
        }

        public virtual void Write(ushort address, byte data)
        {
            if(address < 0x2000 && cartridge.ChrBanks == 0)
            {
                // CHRRAM
                cartridge.Chr[address] = data;
            }
        }
    }
}
