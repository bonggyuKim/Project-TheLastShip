using DoodleUp.Input;
using DoodleUp.Stroke;
using UnityEngine;

namespace DoodleUp.Runtime
{
    public sealed class Du03BCTrajectoryInputAdapter : Du03BCInputAdapterBase
    {
        public override Du03AStrokeMode Mode => Du03AStrokeMode.Trajectory;

        protected override bool TryMapCandidate(
            in Du03BCInputSnapshot input,
            out Vector3 candidate,
            out Du03BCMappingEvidence evidence)
        {
            var markerPosition = handMarker.position;
            var invalidReason = IsFinite(markerPosition)
                ? Du03BCMappingInvalidReason.None
                : Du03BCMappingInvalidReason.NonFinite;
            Vector3? rawCandidate = invalidReason == Du03BCMappingInvalidReason.None ? markerPosition : null;
            candidate = rawCandidate ?? new Vector3(float.NaN, float.NaN, float.NaN);
            evidence = new Du03BCMappingEvidence(
                Mode,
                Time.frameCount,
                input,
                "LATE_UPDATE",
                "HAND_MARKER",
                null,
                null,
                null,
                markerPosition,
                handMarker.localPosition,
                planeOrigin,
                planeNormal,
                rawCandidate,
                rawCandidate,
                rawCandidate.HasValue ? 0f : null,
                invalidReason);
            return rawCandidate.HasValue;
        }
    }
}
