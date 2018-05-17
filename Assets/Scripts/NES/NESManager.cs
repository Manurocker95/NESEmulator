using System.Diagnostics;
using UnityEngine;

namespace NES
{
    public class NESManager : MonoBehaviour
    {
        [SerializeField]
        private NESScreen screen;
        [SerializeField]
        private StandardController controller;
        [SerializeField]
        private PaletteScriptableObject palletteSO;

#if UNITY_EDITOR
        [Header("Debugging")]
        [SerializeField]
        private CPUDebug cpuDebug;
        [SerializeField]
        private PPUDebug ppuDebug;
        [SerializeField]
        private Framerate framerate;
        [SerializeField]
        private Log log;
        [SerializeField]
        private bool stepMode;
#endif

        public Color32[] Palette => palletteSO.palette;
        public long FrameCount { get; private set; }
        public Emulator Emulator { get; private set; }
        private double nextUpdate;

#if !UNITY_EDITOR
        private void Start()
        {
            StartEmulator(@"D:\Projects\NESEmulator\Roms\donkey kong.nes");
        }
#endif

        private void Update()
        {
            if (!(Emulator?.IsValid ?? false))
            {
                return;
            }

            nextUpdate -= Time.deltaTime;

            if (nextUpdate > 0)
            {
                return;
            }

            Emulator.Frame();

            FrameCount++;

            nextUpdate += (1d / Emulator.ApuFrameCounterRate);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateStepMode();
        }

        private void OnDestroy()
        {
            StopEmulator();
        }
#endif

        public void StartEmulator(string path)
        {
            var cartridge = new Cartridge(path);
            if (!cartridge.IsValid)
            {
                return;
            }

            Emulator = new Emulator(cartridge);
            if (!Emulator.IsValid)
            {
                return;
            }

            screen.StartOutput(this);
            controller.StartController(Emulator);

#if UNITY_EDITOR
            UpdateStepMode();

            if (cpuDebug != null)
            {
                cpuDebug.StartCpuDebug(Emulator);
            }
            if (ppuDebug != null)
            {
                ppuDebug.StartPpuDebug(this);
            }
            if(framerate != null)
            {
                framerate.StartFramerate(Emulator.Ppu);
            }
            if (log != null)
            {
                log.StartLog(Emulator);
            }
#endif
            Emulator.Init();
        }

        public void ResetEmulator() => Emulator?.Reset();
        public void StopEmulator() => Emulator?.Stop();

#if UNITY_EDITOR
        private void UpdateStepMode() => Emulator?.StepMode(stepMode);
        public void StepEmulator() => Emulator?.Step();
#endif
    }
}
