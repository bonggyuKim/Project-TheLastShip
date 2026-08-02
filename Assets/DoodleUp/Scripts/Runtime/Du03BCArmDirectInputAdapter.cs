using DoodleUp.Input;
using DoodleUp.Stroke;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Runtime
{
    public sealed class Du03BCArmDirectInputAdapter : Du03BCInputAdapterBase
    {
        public const string ProfileId = "PRETEST_ARM_DIRECT_V1";
        public const float PlaneDepth = 1.25f;
        public const float UnitsPerPixel = 0.0025f;
        public static readonly Vector3 NeutralHandLocalPosition = new(0f, 1.20f, PlaneDepth);
        public static readonly Vector3 NeutralHandPitchLocalPosition =
            NeutralHandLocalPosition - Du02CameraRig.ArmPitchAnchorLocalPosition;

        private Vector2 integratedOffset;
        private Vector3 lastValidHandPosition;
        private bool hasSnapshot;
        private bool hasProbeMouseDelta;
        private Vector2 probeMouseDelta;

        public override Du03AStrokeMode Mode => Du03AStrokeMode.Spatial;
        public Vector3 DesiredTip { get; private set; }
        public Vector3 LastValidHandPosition => lastValidHandPosition;

        public void SetProbeMouseDelta(Vector2 delta)
        {
            probeMouseDelta = delta;
            hasProbeMouseDelta = true;
        }

        public override void ResetAdapter()
        {
            base.ResetAdapter();
            integratedOffset = Vector2.zero;
            hasSnapshot = false;
            hasProbeMouseDelta = false;
            probeMouseDelta = default;
            DesiredTip = default;
            RestoreNeutralHand();
        }

        protected override bool TryMapCandidate(
            in Du03BCInputSnapshot input,
            out Vector3 candidate,
            out Du03BCMappingEvidence evidence)
        {
            if (input.DrawPressed || !hasSnapshot)
            {
                lastValidHandPosition = handMarker.position;
                integratedOffset = Vector2.zero;
                hasSnapshot = true;
            }

            var delta = hasProbeMouseDelta ? probeMouseDelta : Vector2.zero;
            hasProbeMouseDelta = false;
            probeMouseDelta = default;
            integratedOffset += delta * UnitsPerPixel;

            var neutralTip = handMarker.parent != null
                ? handMarker.parent.TransformPoint(GetNeutralHandLocalPosition(handMarker))
                : handMarker.position;
            DesiredTip = neutralTip
                + targetCamera.transform.right * integratedOffset.x
                + targetCamera.transform.up * integratedOffset.y;
            candidate = DesiredTip;

            handMarker.position = DesiredTip;
            lastValidHandPosition = DesiredTip;

            evidence = new Du03BCMappingEvidence(
                Mode,
                Time.frameCount,
                input,
                "LATE_UPDATE",
                "CAMERA_LOOK_ARM_SPATIAL",
                delta,
                null,
                null,
                handMarker.position,
                handMarker.localPosition,
                planeOrigin,
                planeNormal,
                DesiredTip,
                DesiredTip,
                0f,
                Du03BCMappingInvalidReason.None);

            if (input.DrawReleased)
            {
                hasSnapshot = false;
                integratedOffset = Vector2.zero;
                RestoreNeutralHand();
            }

            return true;
        }

        protected override void OnStrokeEnded()
        {
            hasSnapshot = false;
            integratedOffset = Vector2.zero;
            RestoreNeutralHand();
        }

        public static Vector3 GetNeutralHandLocalPosition(Transform marker)
        {
            return marker != null && marker.parent != null
                && marker.parent.name == Du02CameraRig.ArmPitchAnchorName
                ? NeutralHandPitchLocalPosition
                : NeutralHandLocalPosition;
        }

        private void RestoreNeutralHand()
        {
            if (handMarker == null) return;
            handMarker.SetLocalPositionAndRotation(GetNeutralHandLocalPosition(handMarker), Quaternion.identity);
            handMarker.localScale = Vector3.one;
            lastValidHandPosition = handMarker.position;
        }
    }
}
