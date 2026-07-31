using UnityEngine;

namespace DoodleUp.Stroke
{
    public readonly struct Du02CandidateObservation
    {
        public readonly int RenderFrame;
        public readonly long Sequence;
        public readonly Vector3 HandPosition;
        public readonly Vector3 PlaneOrigin;
        public readonly Vector3 PlaneNormal;

        public Du02CandidateObservation(int renderFrame, long sequence, Vector3 handPosition, Vector3 planeOrigin, Vector3 planeNormal)
        {
            RenderFrame = renderFrame;
            Sequence = sequence;
            HandPosition = handPosition;
            PlaneOrigin = planeOrigin;
            PlaneNormal = planeNormal;
        }
    }

    public interface IDu02CandidateSamplingSink
    {
        void Observe(in Du02CandidateObservation observation);
    }

    public static class Du02SamplingExpectation
    {
        public static int ExpectedSamples(int frameRate, float durationSeconds)
        {
            return Mathf.RoundToInt(frameRate * durationSeconds);
        }
    }
}
