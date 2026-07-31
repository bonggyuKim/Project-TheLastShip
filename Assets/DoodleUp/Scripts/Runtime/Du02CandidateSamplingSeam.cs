using DoodleUp.Core;
using DoodleUp.Stroke;
using UnityEngine;

namespace DoodleUp.Runtime
{
    [DefaultExecutionOrder(100)]
    public sealed class Du02CandidateSamplingSeam : MonoBehaviour
    {
        [SerializeField] private Transform handMarker;
        [SerializeField] private bool verboseSampling;

        private long sequence;
        private Vector3 planeOrigin;
        private Vector3 planeNormal = Vector3.forward;

        public long Sequence => sequence;

        public void Configure(Transform marker, Vector3 origin, Vector3 normal)
        {
            handMarker = marker;
            planeOrigin = origin;
            planeNormal = normal;
        }

        public void ResetCounter(Vector3 origin, Vector3 normal)
        {
            sequence = 0;
            planeOrigin = origin;
            planeNormal = normal;
        }

        private void LateUpdate()
        {
            if (handMarker == null) return;

            sequence++;
            var observation = new Du02CandidateObservation(
                Time.frameCount,
                sequence,
                handMarker.position,
                planeOrigin,
                planeNormal);

            if (verboseSampling)
            {
                Debug.Log($"[DU02_SAMPLE] frame={observation.RenderFrame} seq={observation.Sequence} phase=LATE_UPDATE hand={Du02LogFormat.Vector(observation.HandPosition)} planeOrigin={Du02LogFormat.Vector(observation.PlaneOrigin)} planeNormal={Du02LogFormat.Vector(observation.PlaneNormal)}");
            }
        }
    }
}
