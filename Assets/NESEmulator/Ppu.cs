using System;
using System.Runtime.InteropServices;

using static NES.Constants;

namespace NES
{
    public class Ppu
    {
        [Flags]
        public enum OamTileIndex : byte
        {
            Bank = 1 << 0,
            Priority = 127 << 1
        }

        [Flags]
        public enum OamSpriteAttributes : byte
        {
            Palette = 3 << 0,
            Priority = 1 << 5,
            FlipHorizontal = 1 << 6,
            FlipVertical = 1 << 7,
        }

        public readonly struct OamSprite
        {
            public readonly byte y;
            public readonly OamTileIndex tileIndex;
            public readonly OamSpriteAttributes attributes;
            public readonly byte x;
            public readonly byte GetTileIndexBit(OamTileIndex flag)
            {
                return (byte)((tileIndex & flag) > 0 ? 1 : 0);
            }

            public readonly bool IsSpriteAttributesBitSet(OamSpriteAttributes flag)
            {
                return (attributes & flag) > 0;
            }
        }

        [Flags]
        private enum Control : byte
        {
            NameTableSelect = 3 << 0,
            IncrementMode = 1 << 2,
            SpriteTileSelect = 1 << 3,
            BackgroundTileSelect = 1 << 4,
            SpriteHeight = 1 << 5,
            MasterSlave = 1 << 6,
            NMIEnable = 1 << 7,
        }

        [Flags]
        private enum Mask : byte
        {
            Greyscale = 1 << 0,
            BackgroundLeftCol = 1 << 1,
            SpriteLeftCol = 1 << 2,
            BackgroundEnable = 1 << 3,
            SpriteEnable = 1 << 4,
            EmRed = 1 << 5,
            EmGreen = 1 << 6,
            EmBlue = 1 << 7,
        }

        [Flags]
        private enum Status : byte
        {
            SpriteOverflow = 1 << 5,
            SpriteZeroHit = 1 << 6,
            VBlank = 1 << 7
        }

        private struct Register
        {
            public ushort data;

            public byte CoarseX
            {
                get => (byte)(data & 0x1F);
                set => data = (ushort)(data & ~0x1F | value & 0x1F);
            }
            public byte CoarseY
            {
                get => (byte)(data >> 5 & 0x1F);
                set => data = (ushort)(data & ~(0x1F << 5) | (value & 0x1F) << 5);
            }
            public byte NametableX
            {
                get => (byte)(data >> 10 & 0x01);
                set => data = (ushort)(data & ~(0x01 << 10) | (value & 0x01) << 10);
            }
            public byte NametableY
            {
                get => (byte)(data >> 11 & 0x01);
                set => data = (ushort)(data & ~(0x01 << 11) | (value & 0x01) << 11);
            }
            public byte FineY
            {
                get => (byte)(data >> 12 & 0x07);
                set => data = (ushort)(data & ~(0x07 << 12) | (value & 0x07) << 12);
            }

            public byte AddrHi
            {
                set => data = (ushort)(data & ~(0x3F << ByteLength) | (value & 0x3F) << ByteLength);
            }
            public byte AddrLo
            {
                set => data = (ushort)(data & ~LowByteMask | value);
            }

            public Register(ushort newData)
            {
                data = newData;
            }

            public static Register operator +(Register r, int i) => new Register((ushort)(r.data + i));
        }

        // NTSC
        private const int Scanlines = 262;
        private const int Cycles = 341;
        private const int VisibleScanlines = 240;

        // Register Addresses
        private const ushort PpuCtrlAddress = 0x2000;
        private const ushort PpuMaskAddress = 0x2001;
        private const ushort PpuStatusAddress = 0x2002;
        private const ushort OamAddrAddress = 0x2003;
        private const ushort OamDataAddress = 0x2004;
        private const ushort PpuScrollAddress = 0x2005;
        private const ushort PpuAddrAddress = 0x2006;
        private const ushort PpuDataAddress = 0x2007;
        public const ushort OamDmaAddress = 0x4014;

        // Memory Sizes
        private const ushort VRamSize = 0x1000;
        private const ushort NametableSize = 0x400;
        private const ushort PalleteSize = 0x0020;
        private const int OamSize = 64 * 4;
        private const int SecondaryOamSize = 8;
        private readonly int BytesPerSprite = Marshal.SizeOf(typeof(OamSprite));

        // Memory Addreses
        private const ushort PatternTableAddressEnd = 0x1FFF;
        private const ushort NametableAddressStart = 0x2000;
        private const ushort AttributeTableStart = 0x23C0;
        private const ushort NametableAddressEnd = 0x2FFF;
        private const ushort NametableMirrorAddressStart = 0x3000;
        public const ushort PalleteAddressStart = 0x3F00;
        private const ushort PalleteMirrorAddressStart = 0x3F20;

        // Memory
        private readonly byte[] vRam;
        private readonly byte[] paletteRam;
        private readonly byte[] oam;
        private readonly byte[] secondaryOam;

        // State
        private int currentCycle;
        public int CurrentCycle => currentCycle;

        private int currentScanline;
        public int CurrentScanline => currentScanline;

        private int totalFrames;
        public int TotalFrames => totalFrames;

        private bool oddFrame;
        public bool OddFrame => oddFrame;

        // Screen
        private byte[] screenPixels;
        public byte[] ScreenPixels => screenPixels;

        // OAM
        private byte oamData;
        public byte OamData;
        private byte oamAddr;
        public byte OamAddr => oamAddr;

        // Registers
        private Control ppuCtrl;
        private Mask ppuMask;
        private Status ppuStatus;

        // Internal Registers
        private Register v; // VRAM Address
        private Register t; // Temporary VRAM Address
        private byte x;     // Fine X Scroll
        private bool w;     // First/Second Write Toggle

        // Internal Buffers
        private byte nametableBuffer;
        private byte attributeTableByte;
        private byte patternTableLSBBuffer;
        private byte patternTableMSBBuffer;
        private byte readbuffer;
        private byte ioBus;

        // Background Shift Registers
        private ushort backgroundShifterPatternLo;
        private ushort backgroundShifterPatternHi;
        private byte backgroundShifterAttributeLo;
        private byte backgroundShifterAttributeHi;

        // Sprite Shift Registers
        private byte[] spriteShifterPatternLo;
        private byte[] spriteShifterPatternHi;
        private byte[] spriteXCounter;
        private OamSpriteAttributes[] spriteAttributes;

        // Secondary OAM Clear
        private bool clearingSecondaryOAM;

        // Sprite Evaluation
        private int n, m;
        private int spriteCount;
        private bool copySprite;
        private bool secondaryOamFull;
        private bool oamChecked;

        // Sprite Zero
        private bool spriteZeroHitPossible;
        private bool spriteZeroRendering;

        // Emulator
        private readonly Emulator emulator;

        public Ppu(Emulator emulator)
        {
            this.emulator = emulator;

            screenPixels = new byte[Emulator.ScreenWidth * Emulator.ScreenHeight];

            v = new Register();
            t = new Register();

            vRam = new byte[VRamSize];
            paletteRam = new byte[PalleteSize];

            oam = new byte[OamSize * BytesPerSprite];
            secondaryOam = new byte[SecondaryOamSize * BytesPerSprite];

            spriteShifterPatternLo = new byte[8];
            spriteShifterPatternHi = new byte[8];
            spriteXCounter = new byte[8];
            spriteAttributes = new OamSpriteAttributes[8];

            currentCycle = 0;
        }

        public void Reset()
        {

        }

        public OamSprite GetOamSprite(int i)
        {
            unsafe
            {
                fixed (byte* pOam = &oam[0])
                {
                    return new ReadOnlySpan<OamSprite>(pOam, oam.Length)[i];
                }
            }
        }

        public OamSprite GetSecondaryOamSprite(int i)
        {
            unsafe
            {
                fixed (byte* pOam = &secondaryOam[0])
                {
                    return new ReadOnlySpan<OamSprite>(pOam, oam.Length)[i];
                }
            }
        }

        private bool IsControlFlagSet(Control flag)
        {
            return (ppuCtrl & flag) > 0;
        }

        private ushort GetControlFlag(Control flag)
        {
            return (ushort)(IsControlFlagSet(flag) ? 1 : 0);
        }

        private bool IsMaskFlagSet(Mask flag)
        {
            return (ppuMask & flag) > 0;
        }

        private void SetStatusFlag(Status flag, bool value)
        {
            ppuStatus = value ? ppuStatus | flag : ppuStatus & ~flag;
        }

        private bool IsStatusFlagSet(Status flag)
        {
            return (ppuStatus & flag) > 0;
        }

        public void Step()
        {
            // Pre-render Scanline
            if (currentScanline == -1)
            {
                // Reset Emulator State
                if (currentCycle == 1)
                {
                    SetStatusFlag(Status.SpriteOverflow, false);
                    SetStatusFlag(Status.SpriteZeroHit, false);
                    SetStatusFlag(Status.VBlank, false);

                    spriteZeroHitPossible = false;

                    for (int i = 0; i < 8; i++)
                    {
                        spriteShifterPatternLo[i] = 0;
                        spriteShifterPatternHi[i] = 0;
                        spriteXCounter[i] = 0;
                        spriteAttributes[i] = 0;
                    }
                }
                // Copy Y
                else if (currentCycle >= 220 && currentCycle <= 304)
                {
                    if (ShouldRender())
                    {
                        v.CoarseY = t.CoarseY;
                        v.FineY = t.FineY;
                        v.NametableY = t.NametableY;
                    }
                }
            }

            // Pre-Render and Visible Scanlines
            if (currentScanline < VisibleScanlines)
            {
                // Odd Frame Idle
                if (currentCycle == 0)
                {
                    // Idle
                }
                else if (currentCycle <= 256)
                {
                    UpdateShifters();

                    FetchTile((byte)((currentCycle - 1) % 8));

                    // Scroll Y
                    if (currentCycle == 256)
                    {
                        if (ShouldRender())
                        {
                            if (v.FineY < 7)
                            {
                                v.FineY++;
                            }
                            else
                            {
                                v.FineY = 0;
                                if (v.CoarseY == 29) // Start of Attribute Table
                                {
                                    v.CoarseY = 0;
                                    v.NametableY ^= 1;
                                }
                                else if (v.CoarseY == 31) // In Attribute Table
                                {
                                    v.CoarseY = 0;
                                }
                                else
                                {
                                    v.CoarseY++;
                                }
                            }
                        }
                    }
                }
                else if (currentCycle == 257)
                {
                    LoadBackgroundShifters();

                    // Copy X
                    if (ShouldRender())
                    {
                        v.CoarseX = t.CoarseX;
                        v.NametableX = t.NametableX;
                    }
                }
                else if (currentCycle > 320 && currentCycle <= 336)
                {
                    UpdateShifters();

                    FetchTile((byte)((currentCycle - 1) % 8));
                }
                else if (currentCycle == 337 || currentCycle == 339)
                {
                    nametableBuffer = FetchNametable();
                }

                // Secondary OAM Clear
                if (currentScanline > 0 && currentCycle >= 1 && currentCycle <= 64)
                {
                    if (currentCycle == 1)
                    {
                        clearingSecondaryOAM = true;
                    }

                    // Read
                    if (currentCycle % 2 == 0)
                    {
                        oamData = ReadRegister(OamDataAddress);
                    }
                    // Write
                    else
                    {
                        secondaryOam[currentCycle / 2] = oamData;
                    }

                    if (currentCycle == 64)
                    {
                        clearingSecondaryOAM = false;
                    }
                }
                // Sprite Evaluation
                else if (currentScanline > 0 && currentCycle > 64 && currentCycle <= 256)
                {
                    // Reset Evaluation State
                    if (currentCycle == 65)
                    {
                        n = 0;
                        m = 0;
                        spriteCount = 0;
                        copySprite = false;
                        secondaryOamFull = false;
                        oamChecked = false;
                        spriteZeroRendering = false;
                    }

                    // Read
                    if (currentCycle % 2 == 1)
                    {
                        oamData = oam[n * BytesPerSprite + m];
                    }
                    // Write
                    else
                    {
                        if(!oamChecked)
                        {
                            int difference = currentScanline - oamData;

                            if(!secondaryOamFull)
                            {
                                secondaryOam[BytesPerSprite * spriteCount + m] = oamData;

                                if (difference >= 0 && difference < (IsControlFlagSet(Control.SpriteHeight) ? 16 : 8))
                                {
                                    copySprite = true;
                                }
                            }
                            else
                            {
                                if (spriteCount == 8  && !IsStatusFlagSet(Status.SpriteOverflow))
                                {
                                    if (difference > 0 && difference < (IsControlFlagSet(Control.SpriteHeight) ? 16 : 8))
                                    {
                                        copySprite = true;
                                        SetStatusFlag(Status.SpriteOverflow, true);
                                    }
                                    else
                                    {
                                        n++;
                                        m++;
                                        if (m == 3)
                                        {
                                            m = 0;
                                        }
                                    }
                                }
                            }

                            if (copySprite)
                            {
                                if (m < 3)
                                {
                                    m++;
                                }
                                else
                                {
                                    m = 0;
                                    copySprite = false;
                                    if (!IsStatusFlagSet(Status.SpriteOverflow))
                                    {
                                        spriteCount++;
                                    }

                                    if (n == 0)
                                    {
                                        spriteZeroHitPossible = true;
                                    }
                                }
                            }

                            if (!copySprite)
                            {
                                n++;

                                if (n == 64)
                                {
                                    n = 0;
                                    oamChecked = true;
                                }

                                if (spriteCount == 8)
                                {
                                    secondaryOamFull = true;
                                }
                            }
                        }
                        else
                        {
                            n++;
                        }
                    }
                }
                // Fetch Sprite
                else if (currentCycle > 256 && currentCycle <= 320)
                {
                    if (currentCycle == 257)
                    {
                        oamAddr = 0;
                    }
                }
                // Background render pipe initialization
                else if (currentCycle > 320 && currentCycle <= 340 || currentCycle == 0)
                {
                    if (currentCycle == 340)
                    {
                        for (int i = 0; i < spriteCount; i++)
                        {
                            byte spritePatternLo;
                            byte spritePatternHi;

                            var sprite = GetSecondaryOamSprite(i);

                            var tileIndex = (byte)(sprite.tileIndex);

                            if (!IsControlFlagSet(Control.SpriteHeight))
                            {
                                // 8x8
                                var row = (byte)(sprite.IsSpriteAttributesBitSet(OamSpriteAttributes.FlipVertical) ? 7 - (currentScanline - sprite.y) :
                                                                                                                          currentScanline - sprite.y);
                                spritePatternLo = ReadPatternTableSprite(tileIndex, row, false);
                                spritePatternHi = ReadPatternTableSprite(tileIndex, row, true);
                            }
                            else
                            {
                                // 8x16
                                var bank = sprite.GetTileIndexBit(OamTileIndex.Bank);
                                var row = (byte)((sprite.IsSpriteAttributesBitSet(OamSpriteAttributes.FlipVertical) ? 7 - (currentScanline - sprite.y) :
                                                                                                                           currentScanline - sprite.y) & 0x07);
                                var topTile = currentScanline - sprite.y < 8;

                                spritePatternLo = ReadPatternTableLargeSprites(bank, tileIndex, row, topTile, false);
                                spritePatternHi = ReadPatternTableLargeSprites(bank, tileIndex, row, topTile, true);
                            }

                            if (sprite.IsSpriteAttributesBitSet(OamSpriteAttributes.FlipHorizontal))
                            {
                                spritePatternLo = spritePatternLo.ReverseBits();
                                spritePatternHi = spritePatternHi.ReverseBits();
                            }

                            spriteShifterPatternLo[i] = spritePatternLo;
                            spriteShifterPatternHi[i] = spritePatternHi;
                            spriteXCounter[i] = sprite.x;
                            spriteAttributes[i] = sprite.attributes;
                        }
                    }
                }
            }
            // Post Render
            else if (currentScanline == VisibleScanlines)
            {
                // Idle
            }
            // VBlank
            else if (currentScanline == VisibleScanlines + 1)
            {
                if (currentCycle == 1)
                {
                    SetStatusFlag(Status.VBlank, true);

                    if (IsControlFlagSet(Control.NMIEnable))
                    {
                        emulator.Cpu.RaiseNmi();
                    }
                }
            }

            // Background Rendering
            var bgPixel = default(byte);
            var bgPalette = default(byte);
            if (ShouldRenderBackground())
            {
                byte mux = (byte)(0x80 >> x);

                byte pixelLSB = (byte)((backgroundShifterPatternLo & mux) > 0 ? 1 : 0);
                byte pixelMSB = (byte)((backgroundShifterPatternHi & mux) > 0 ? 1 : 0);
                bgPixel = (byte)(pixelMSB << 1 | pixelLSB);

                byte paletteLSB = (byte)((backgroundShifterAttributeLo & mux) > 0 ? 1 : 0);
                byte paletteMSB = (byte)((backgroundShifterAttributeHi & mux) > 0 ? 1 : 0);
                bgPalette = (byte)(paletteMSB << 1 | paletteLSB);
            }

            // Sprite Rendering
            var spPixel = default(byte);
            var spPalette = default(byte);
            var spPriority = default(bool);
            if (ShouldRenderSprites())
            {
                spriteZeroRendering = false;

                for (int i = 0; i < spriteCount; i++)
                {
                    if (spriteXCounter[i] == 0)
                    {
                        byte mux = (byte)0x80;

                        byte pixelLSB = (byte)((spriteShifterPatternLo[i] & mux) > 0 ? 1 : 0);
                        byte pixelMSB = (byte)((spriteShifterPatternHi[i] & mux) > 0 ? 1 : 0);
                        spPixel = (byte)(pixelMSB << 1 | pixelLSB);

                        spPalette = (byte)((spriteAttributes[i] & OamSpriteAttributes.Palette) + 4);

                        spPriority = (spriteAttributes[i] & OamSpriteAttributes.Priority) > 0;

                        if(spPixel != 0)
                        {
                            if(i == 0)
                            {
                                spriteZeroRendering = true;
                            }
                            break;
                        }
                    }
                }
            }

            // Priority
            var pixel = default(byte);
            var palette = default(byte);

            if(bgPixel == 0 && spPixel == 0)
            {
                pixel = 0;
                palette = 0;
            }
            else if (bgPixel == 0 && spPixel > 0)
            {
                pixel = spPixel;
                palette = spPalette;
            }
            else if (bgPixel > 0 && spPixel == 0)
            {
                pixel = bgPixel;
                palette = bgPalette;
            }
            else if(bgPixel > 0 && spPixel > 0)
            {
                if(spPriority)
                {
                    pixel = spPixel;
                    palette = spPalette;
                }
                else
                {
                    pixel = bgPixel;
                    palette = bgPalette;
                }

                // Sprite Zero Hit
                if (spriteZeroHitPossible && spriteZeroRendering)
                {
                    if (ShouldRenderBackground() && ShouldRenderSprites())
                    {
                        if (IsMaskFlagSet(Mask.BackgroundLeftCol) || IsMaskFlagSet(Mask.SpriteLeftCol))
                        {
                            if (currentCycle >= 1 && currentCycle < 258)
                            {
                                SetStatusFlag(Status.SpriteZeroHit, true);
                            }
                        }
                        else
                        {
                            if (currentCycle >= 9 && currentCycle < 258)
                            {
                                SetStatusFlag(Status.SpriteZeroHit, true);
                            }
                        }
                    }
                }
            }

            if (currentCycle > 0 && currentCycle <= Emulator.ScreenWidth &&
                currentScanline >= 0 && currentScanline < VisibleScanlines)
            {
                screenPixels[currentCycle - 1 + currentScanline * Emulator.ScreenWidth] = ReadMemory((ushort)(PalleteAddressStart + palette * 4 + pixel));
            }

            currentCycle++;
            if (currentCycle > Cycles)
            {
                currentCycle = 0;
                currentScanline++;
                if (currentScanline >= Scanlines - 1)
                {
                    currentScanline = -1;
                    if (oddFrame)
                    {
                        currentCycle = 1;
                    }
                    oddFrame = !oddFrame;
                    totalFrames++;
                }
            }
        }

        private byte ReadPatternTableBackground(byte tileIndex, byte row, bool upper)
        {
            return ReadMemory((ushort)(GetControlFlag(Control.BackgroundTileSelect) << 12 |
                                       tileIndex << 4 |
                                       (byte)(upper ? 8 : 0) |
                                       row));
        }

        private byte ReadPatternTableSprite(byte tileIndex, byte row, bool upper)
        {
            return ReadMemory((ushort)(GetControlFlag(Control.SpriteTileSelect) << 12 |
                                       tileIndex << 4 |
                                       (byte)(upper ? 8 : 0) |
                                       row));
        }

        private byte ReadPatternTableLargeSprites(byte bank, byte tileIndex, byte row, bool topTile, bool upper)
        {
            return ReadMemory((ushort)(bank << 12 |
                                      (topTile ? tileIndex & ~bank: tileIndex & ~bank + 1) << 4 |
                                      (byte)(upper ? 8 : 0) |
                                      row));
        }

        private bool ShouldRender()
        {
            return ShouldRenderBackground() || ShouldRenderSprites();
        }

        private bool ShouldRenderBackground()
        {
            return (ppuMask & Mask.BackgroundEnable) > 0;
        }

        private bool ShouldRenderSprites()
        {
            return (ppuMask & Mask.SpriteEnable) > 0;
        }

        private void UpdateShifters()
        {
            // Background Shifters
            if (ShouldRenderBackground())
            {
                backgroundShifterPatternLo <<= 1;
                backgroundShifterPatternHi <<= 1;
                backgroundShifterAttributeLo <<= 1;
                backgroundShifterAttributeHi <<= 1;
            }

            // Sprite Shifters / Counters
            if (ShouldRenderSprites())
            {
                for (int i = 0; i < spriteXCounter.Length; i++)
                {
                    if (spriteXCounter[i] > 0)
                    {
                        spriteXCounter[i]--;
                    }
                    else
                    {
                        spriteShifterPatternLo[i] <<= 1;
                        spriteShifterPatternHi[i] <<= 1;
                    }
                }
            }
        }

        private void FetchTile(int cycle)
        {
            // Nametable Byte
            if (cycle == 0)
            {
                LoadBackgroundShifters();

                nametableBuffer = FetchNametable();
            }
            // Attribute Byte
            else if (cycle == 2)
            {
                attributeTableByte = ReadMemory((ushort)(AttributeTableStart |
                                                         v.data & 0x0C00 |     // Nametable
                                                         v.data >> 4 & 0x38 |  // Coarse Y / 4
                                                         v.data >> 2 & 0x07)); // Coarse X / 4
                if ((v.CoarseY & 0x02) > 0)
                {
                    attributeTableByte >>= 4;
                }

                if ((v.CoarseX & 0x02) > 0)
                {
                    attributeTableByte >>= 2;
                }

                attributeTableByte &= 0x03;
            }
            // Pattern Table Tile LSB
            else if (cycle == 4)
            {
                patternTableLSBBuffer = ReadPatternTableBackground(nametableBuffer, v.FineY, false);
            }
            // Pattern Table Tile MSB
            else if (cycle == 6)
            {
                patternTableMSBBuffer = ReadPatternTableBackground(nametableBuffer, v.FineY, true);
            }
            // Scroll Coarse X
            else if (cycle == 7)
            {
                if ((ppuMask & Mask.BackgroundEnable) > 0 || (ppuMask & Mask.SpriteEnable) > 0)
                {
                    if (v.CoarseX == 31)
                    {
                        v.CoarseX = 0;
                        v.NametableX ^= 1;
                    }
                    else
                    {
                        v.CoarseX++;
                    }
                }
            }
        }

        private byte FetchNametable()
        {
            return ReadMemory((ushort)(NametableAddressStart | v.data & 0x0FFF));
        }

        private void LoadBackgroundShifters()
        {
            backgroundShifterPatternLo = (ushort)(backgroundShifterPatternLo & HighByteMask | patternTableLSBBuffer);
            backgroundShifterPatternHi = (ushort)(backgroundShifterPatternHi & HighByteMask | patternTableMSBBuffer);
            backgroundShifterAttributeLo = (byte)((attributeTableByte & 0x01) > 0 ? 0xFF : 0x00);
            backgroundShifterAttributeHi = (byte)((attributeTableByte & 0x02) > 0 ? 0xFF : 0x00);
        }

        public byte ReadRegister(ushort address)
        {
            if (address == PpuCtrlAddress)
            {
                // Write Only
            }
            else if (address == PpuMaskAddress)
            {
                // Write Only
            }
            else if (address == PpuStatusAddress)
            {
                ioBus = (byte)((byte)ppuStatus & 0xE0);
                SetStatusFlag(Status.VBlank, false);
                w = false;
            }
            else if (address == OamAddrAddress)
            {
                // Write Only
            }
            else if (address == OamDataAddress)
            {
                if(clearingSecondaryOAM)
                {
                    ioBus = 0XFF;
                }
                else
                {
                    ioBus = oam[oamAddr];
                }
            }
            else if (address == PpuScrollAddress)
            {
                // Write Only
            }
            else if (address == PpuAddrAddress)
            {
                // Write Only
            }
            else if (address == PpuDataAddress)
            {
                ioBus = readbuffer;
                readbuffer = ReadMemory(v.data);

                if (v.data >= PalleteAddressStart)
                {
                    ioBus = readbuffer;
                }

                IncrementPpuAddr();
            }
            else if (address == OamDmaAddress)
            {
                // Write Only
            }

            return ioBus;
        }

        public void WriteRegister(ushort address, byte value)
        {
            ioBus = value;

            if (address == PpuCtrlAddress)
            {
                ppuCtrl = (Control)ioBus;
                t.NametableX = (byte)((byte)ppuCtrl & 0x0001);
                t.NametableY = (byte)(((byte)ppuCtrl >> 1) & 0x0001);
            }
            else if (address == PpuMaskAddress)
            {
                ppuMask = (Mask)ioBus;
            }
            else if (address == PpuStatusAddress)
            {
                // Read Only
            }
            else if (address == OamAddrAddress)
            {
                oamAddr = ioBus;
            }
            else if (address == OamDataAddress)
            {
                WriteOam(ioBus);
            }
            else if (address == PpuScrollAddress)
            {
                if (!w) // First Write
                {
                    t.CoarseX = (byte)(ioBus >> 3);
                    x = (byte)(value & 0x7);
                }
                else  // Second Write
                {
                    t.CoarseY = (byte)(ioBus >> 3);
                    t.FineY = (byte)(ioBus & 0x7);
                }

                w = !w;
            }
            else if (address == PpuAddrAddress)
            {
                if (!w) // First Write
                {
                    t.AddrHi = ioBus;
                }
                else   // Second Write
                {
                    t.AddrLo = ioBus;
                    v = t;
                }

                w = !w;
            }
            else if (address == PpuDataAddress)
            {
                WriteMemory(v.data, ioBus);

                IncrementPpuAddr();
            }
            else if (address == OamDmaAddress)
            {
                emulator.Cpu.StartOamDma(ioBus);
            }
        }

        public void WriteOam(byte oamValue)
        {
            oam[oamAddr] = oamValue;
            oamAddr++;
        }

        private void IncrementPpuAddr()
        {
            v += IsControlFlagSet(Control.IncrementMode) ? 32 : 1;
        }

        public byte ReadMemory(ushort address)
        {
            var value = default(byte);

            if (address <= PatternTableAddressEnd)
            {
                value = emulator.Mapper.Read(address);
            }
            else if (address <= NametableAddressEnd)
            {
                value = vRam[GetNametableAddress(address)];
            }
            else if (address >= PalleteAddressStart)
            {
                value = paletteRam[GetPaletteRamAddress(address)];
            }

            return value;
        }

        private void WriteMemory(ushort address, byte value)
        {
            if (address <= PatternTableAddressEnd)
            {
                emulator.Mapper.Write(address, value);
            }
            else if (address <= NametableAddressEnd)
            {
                vRam[GetNametableAddress(address)] = value;
            }
            else if (address >= PalleteAddressStart)
            {
                paletteRam[GetPaletteRamAddress(address)] = value;
            }
        }

        private ushort GetNametableAddress(ushort address)
        {
            if (address >= NametableMirrorAddressStart && address < PalleteAddressStart)
            {
                address = (ushort)(address - VRamSize);
            }

            Mapper.MirrorMode mode = emulator.Mapper.mirrorMode;
            ushort nametableAddress = (ushort)(address - NametableAddressStart);
            if (mode == Mapper.MirrorMode.Horizontal)
            {
                if (nametableAddress >= NametableSize && nametableAddress < NametableSize * 2 ||
                    nametableAddress >= NametableSize * 3)
                {
                    nametableAddress -= NametableSize;
                }
            }
            else if (mode == Mapper.MirrorMode.Vertical)
            {
                if (nametableAddress >= NametableSize * 2)
                {
                    nametableAddress -= NametableSize * 2;
                }
            }

            return nametableAddress;
        }

        private static ushort GetPaletteRamAddress(ushort address)
        {
            if (address >= PalleteMirrorAddressStart)
            {
                address = (ushort)((address - PalleteAddressStart) % PalleteSize + PalleteAddressStart);
            }

            if (address == 0x3F10 || address == 0x3F14 || address == 0x3F18 || address == 0x3F1C)
            {
                address = (ushort)(address - 0x0010);
            }

            return (ushort)(address - PalleteAddressStart);
        }
    }
}
