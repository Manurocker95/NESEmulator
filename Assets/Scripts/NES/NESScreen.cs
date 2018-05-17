using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace NES
{
    [RequireComponent(typeof(RawImage))]
    public class NESScreen : MonoBehaviour
    {
        private RawImage screenImage;
        private Texture2D screenTexture;
        private Color32[] screenBuffer;
        private int bufferLength;
        private NativeArray<Color32> nativeScreenTexture;

        private NESManager manager;
        private Ppu ppu;

        private void Start()
        {
            screenImage = GetComponent<RawImage>();
        }

        private void LateUpdate()
        {
            if (screenTexture == null || ppu == null)
            {
                return;
            }

            var palette = manager.Palette;
            var screenPixels = ppu.ScreenPixels;

            for (int i = 0; i < bufferLength; i++)
            {
                screenBuffer[i] = palette[screenPixels[i]];
            }
            var nativeScreenBuffer = new NativeArray<Color32>(screenBuffer, Allocator.Temp);

            nativeScreenBuffer.CopyTo(nativeScreenTexture);
            nativeScreenBuffer.Dispose();

            screenTexture.Apply(false);
        }

        public void StartOutput(NESManager newManager)
        {
            manager = newManager;
            ppu = manager.Emulator.Ppu;

            screenTexture = new Texture2D(Emulator.ScreenWidth, Emulator.ScreenHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            screenImage.texture = screenTexture;
            bufferLength = screenTexture.width * screenTexture.height;
            screenBuffer = new Color32[bufferLength];

            // RGBA32 texture format data layout exactly matches Color32 struct
            nativeScreenTexture = screenTexture.GetRawTextureData<Color32>();
        }
    }
}
