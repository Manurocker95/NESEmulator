using TMPro;
using UnityEngine;
namespace NES
{
    public class Framerate : MonoBehaviour
    {
        [SerializeField]
        private float UpdateTime = 1f;
        [SerializeField]
        private TextMeshProUGUI text;

        private float nextUpdate;
        private long lastFrameCount;

        private Ppu ppu;

        private void Start()
        {
            nextUpdate = UpdateTime;
        }

        private void Update()
        {
            if(ppu == null)
            {
                return;
            }

            var deltaTime = Time.deltaTime;

            nextUpdate -= deltaTime;
            if(nextUpdate > 0)
            {
                return;
            }

            var currentFrame = ppu.TotalFrames;

            text.text = $"{(int)((currentFrame - lastFrameCount) / UpdateTime)} FPS";

            lastFrameCount = currentFrame;
            nextUpdate = UpdateTime;
        }

        public void StartFramerate(Ppu newPpu)
        {
            ppu = newPpu;
        }
    }
}