using System.Text;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NES
{
    public class PPUDebug : MonoBehaviour
    {
        [SerializeField]
        private RawImage[] paletteImages = default;
        [SerializeField]
        private RawImage[] patternImages = default;
        [SerializeField]
        private TextMeshProUGUI oamText = default;
        [SerializeField]
        private TextMeshProUGUI framesText = default;

        private Texture2D[] paletteTextures;
        private Texture2D[] patternTextures;
        private Color32[][] paletteBuffers;
        private Color32[][] patternBuffers;
        private StringBuilder oamBuilder;
        private int currentPalette = 0;
        byte[,] patternBuffer;
        byte[] paletteBuffer;

        private NESManager manager;
        private Emulator emulator;
        private Ppu ppu;

        private void Update()
        {
            if (ppu == null)
            {
                return;
            }

            if (paletteImages != null)
            {
                var palette = manager.Palette;
                var palettes = new byte[paletteTextures.Length][];

                var width = paletteTextures[0].width;
                var height = paletteTextures[0].height;
                var bufferLength = width * height;

                for (int i = 0; i < paletteTextures.Length; i++)
                {
                    FillPaletteBuffer(i);
                    palettes[i] = paletteBuffer;

                    for (int j = 0; j < bufferLength; j++)
                    {
                        paletteBuffers[i][j] = palette[palettes[i][j]];
                    }

                    var nativePaletteTexture = paletteTextures[i].GetRawTextureData<Color32>();
                    var nativePaletteBuffer = new NativeArray<Color32>(paletteBuffers[i], Allocator.Temp);

                    nativePaletteBuffer.CopyTo(nativePaletteTexture);
                    nativePaletteBuffer.Dispose();

                    paletteTextures[i].Apply(false);
                }
            }

            if (patternImages != null)
            {
                var palette = manager.Palette;
                var patternTables = new byte[patternTextures.Length][,];

                var width = patternTextures[0].width;
                var height = patternTextures[0].height;
                var bufferLength = width * height;

                for (int i = 0; i < patternTextures.Length; i++)
                {
                    FillPatternBuffer(i);
                    patternTables[i] = patternBuffer;

                    for (int j = 0; j < bufferLength; j++)
                    {
                        patternBuffers[i][j] = palette[patternTables[i][j % width, height - j / width - 1]];
                    }

                    var nativePatternTexture = patternTextures[i].GetRawTextureData<Color32>();
                    var nativePatternBuffer = new NativeArray<Color32>(patternBuffers[i], Allocator.Temp);

                    nativePatternBuffer.CopyTo(nativePatternTexture);
                    nativePatternBuffer.Dispose();

                    patternTextures[i].Apply(false);
                }
            }

            if (oamText != null)
            {
                oamBuilder.Clear();

                for (int i = 0; i < 64; i++)
                {
                    var oamSprite = ppu.GetOamSprite(i);
                    oamBuilder.Append($"{i,2} X:{oamSprite.x,3} Y:{oamSprite.y,3} I:{(byte)oamSprite.tileIndex:X2} A:{(byte)oamSprite.attributes:X2}\n");
                }

                oamText.text = oamBuilder.ToString();
            }

            if (framesText != null)
            {
                framesText.text = $"Frames:{ppu.TotalFrames}";
            }
        }
        public void SetCurrentPalette(int paletteNum)
        {
            currentPalette = paletteNum;
        }

        private void FillPaletteBuffer(int paletteNum)
        {
            for (int i = 0; i < 4; i++)
            {
                paletteBuffer[i] = ppu.ReadMemory((ushort)(Ppu.PalleteAddressStart + paletteNum * 4 + i));
            }
        }

        private void FillPatternBuffer(int tableNum)
        {
            for (var tileY = 0; tileY < 16; tileY++)
            {
                for (var tileX = 0; tileX < 16; tileX++)
                {
                    var offset = tileY * 256 + tileX * 16;

                    for (var row = 0; row < 8; row++)
                    {
                        var tile_lsb = ppu.ReadMemory((ushort)(tableNum * 0x1000 + offset + row));
                        var tile_msb = ppu.ReadMemory((ushort)(tableNum * 0x1000 + offset + row + 8));

                        for (var col = 7; col >= 0; col--)
                        {
                            var pixel = (tile_lsb & 0x01) + (tile_msb & 0x01);

                            tile_lsb >>= 1;
                            tile_msb >>= 1;

                            patternBuffer[tileX * 8 + col, tileY * 8 + row] = ppu.ReadMemory((ushort)(Ppu.PalleteAddressStart + currentPalette * 4 + pixel));
                        }
                    }
                }
            }
        }

        public void StartPpuDebug(NESManager newManager)
        {
            manager = newManager;
            emulator = manager.Emulator;
            ppu = emulator.Ppu;

            paletteTextures = new Texture2D[paletteImages.Length];
            paletteBuffers = new Color32[paletteImages.Length][];
            for (int i = 0; i < paletteTextures.Length; i++)
            {
                paletteTextures[i] = new Texture2D(4, 1, TextureFormat.RGBA32, false);
                paletteTextures[i].filterMode = FilterMode.Point;
                paletteImages[i].texture = paletteTextures[i];
                paletteBuffers[i] = new Color32[paletteTextures[i].width * paletteTextures[i].height];
            }

            patternTextures = new Texture2D[patternImages.Length];
            patternBuffers = new Color32[patternImages.Length][];
            for (int i = 0; i < patternTextures.Length; i++)
            {
                patternTextures[i] = new Texture2D(16 * 8, 16 * 8, TextureFormat.RGBA32, false);
                paletteTextures[i].filterMode = FilterMode.Point;
                patternImages[i].texture = patternTextures[i];
                patternBuffers[i] = new Color32[patternTextures[i].width * patternTextures[i].height];
            }

            patternBuffer = new byte[patternTextures[0].width, patternTextures[0].height];
            paletteBuffer = new byte[4];

            oamBuilder = new StringBuilder();
        }
    }
}
