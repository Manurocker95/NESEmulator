using UnityEngine;

namespace NES
{
    [RequireComponent(typeof(AudioSource))]
    public class NESAudio : MonoBehaviour
    {
        private AudioSource audioSource;

        private NESManager manager;
        private Apu apu;

        private void Start()
        {
            audioSource = GetComponent<AudioSource>();
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if(apu == null)
            {
                return;
            }
        }

        public void StartOutput(NESManager newManager)
        {
            manager = newManager;
            apu = manager.Emulator.Apu;
        }
    }
}
