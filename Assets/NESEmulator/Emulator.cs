using System;

namespace NES
{
    public class Emulator
    {
        // NTSC Constants
        public const int ScreenWidth = 256;
        public const int ScreenHeight = 240;
        public const int ApuFrameCounterRate = 60;
        public const int PpuDotsPerCpuCycle = 3;

        public Action onBeforeStep;

        public Cpu Cpu { get; private set; }
        public Ppu Ppu { get; private set; }
        public Apu Apu { get; private set; }
        public Controller ControllerOne { get; private set; }
        public Controller ControllerTwo { get; private set; }
        public Mapper Mapper { get; private set; }
        public bool IsValid { get; private set; }

        private bool isRunning;
        private bool shouldReset;
        private bool stepMode;
        private bool shouldStep;

        public Emulator(Cartridge cartridge)
        {
            LoadMapper(cartridge);
            if (Mapper == null)
            {
                return;
            }

            Cpu = new Cpu(this);
            Ppu = new Ppu(this);
            Apu = new Apu(this);
            ControllerOne = new Controller();
            ControllerTwo = new Controller();

            IsValid = true;
        }

        public void Init()
        {
            if (!IsValid)
            {
                return;
            }

            isRunning = true;
        }

        public void Frame()
        {
            var originalOddFrame = Ppu.OddFrame;

            while (isRunning && originalOddFrame == Ppu.OddFrame)
            {
                if (stepMode & !shouldStep)
                {
                    return;
                }

                shouldStep = false;

                if (shouldReset)
                {
                    Cpu.Reset();
                    Ppu.Reset();
                    Apu.Reset();

                    originalOddFrame = Ppu.OddFrame;

                    shouldReset = false;
                }

                onBeforeStep?.Invoke();

                var cycles = Cpu.Step();

                for (var i = 0; i < cycles * PpuDotsPerCpuCycle; i++)
                {
                    Ppu.Step();
                }

                for (var i = 0; i < cycles; i++)
                {
                    Apu.Step();
                }
            }
        }

        private void LoadMapper(Cartridge cartridge)
        {
            Mapper = cartridge.Mapper switch
            {
                000 => new Nrom(cartridge),
                // 001 or 105 or 155 => new Mmc1(cartridge),
                002 or 094 or 180 => new UxRom(cartridge),
                _ => null,
            };
        }

        public void StepMode(bool newStepMode)
        {
            stepMode = newStepMode;
        }

        public void Step()
        {
            shouldStep = true;
        }

        public void Reset()
        {
            shouldReset = true;
        }

        public void Stop()
        {
            isRunning = false;
        }
    }
}
