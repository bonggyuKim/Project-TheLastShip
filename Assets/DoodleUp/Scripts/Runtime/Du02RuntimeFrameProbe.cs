using UnityEngine;

namespace DoodleUp.Runtime
{
    [DefaultExecutionOrder(-100)]
    public sealed class Du02RuntimeFrameProbe : MonoBehaviour
    {
        public long ObservedFrameCount { get; private set; }
        public int LastFrame { get; private set; } = -1;
        public long DuplicateFrames { get; private set; }
        public long MissingFrames { get; private set; }

        public void ResetCounters()
        {
            ObservedFrameCount = 0;
            LastFrame = -1;
            DuplicateFrames = 0;
            MissingFrames = 0;
        }

        private void LateUpdate()
        {
            var frame = Time.frameCount;
            if (frame == LastFrame)
            {
                DuplicateFrames++;
                return;
            }

            if (LastFrame >= 0 && frame > LastFrame + 1)
            {
                MissingFrames += frame - LastFrame - 1;
            }

            LastFrame = frame;
            ObservedFrameCount++;
        }
    }
}
