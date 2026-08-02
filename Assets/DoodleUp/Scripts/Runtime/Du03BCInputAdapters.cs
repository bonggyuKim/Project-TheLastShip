using System;
using DoodleUp.Input;
using DoodleUp.Stroke;
using UnityEngine;

namespace DoodleUp.Runtime
{
    public enum Du03BCMappingInvalidReason
    {
        None,
        NoPlaneIntersection,
        NonFinite
    }

    public readonly struct Du03BCMappingEvidence
    {
        public readonly Du03AStrokeMode Mode;
        public readonly int RenderFrame;
        public readonly long InputEventSequence;
        public readonly string ExecutionPath;
        public readonly string SamplePhase;
        public readonly string MappingSource;
        public readonly Vector2? MouseScreen;
        public readonly Ray? Ray;
        public readonly float? IntersectionDistance;
        public readonly Vector3 HandPosition;
        public readonly Vector3 MarkerLocalPosition;
        public readonly Vector3 PlaneOrigin;
        public readonly Vector3 PlaneNormal;
        public readonly Vector3? RawCandidate;
        public readonly Vector3? IndependentExpected;
        public readonly float? MappingError;
        public readonly Du03BCMappingInvalidReason InvalidReason;
        public readonly Du03BCInputSnapshot Input;

        public Du03BCMappingEvidence(
            Du03AStrokeMode mode,
            int renderFrame,
            in Du03BCInputSnapshot input,
            string samplePhase,
            string mappingSource,
            Vector2? mouseScreen,
            Ray? ray,
            float? intersectionDistance,
            Vector3 handPosition,
            Vector3 markerLocalPosition,
            Vector3 planeOrigin,
            Vector3 planeNormal,
            Vector3? rawCandidate,
            Vector3? independentExpected,
            float? mappingError,
            Du03BCMappingInvalidReason invalidReason)
        {
            Mode = mode;
            RenderFrame = renderFrame;
            InputEventSequence = input.EventSequence;
            ExecutionPath = input.ExecutionPath;
            SamplePhase = samplePhase;
            MappingSource = mappingSource;
            MouseScreen = mouseScreen;
            Ray = ray;
            IntersectionDistance = intersectionDistance;
            HandPosition = handPosition;
            MarkerLocalPosition = markerLocalPosition;
            PlaneOrigin = planeOrigin;
            PlaneNormal = planeNormal;
            RawCandidate = rawCandidate;
            IndependentExpected = independentExpected;
            MappingError = mappingError;
            InvalidReason = invalidReason;
            Input = input;
        }
    }

    public interface IDu03BCInputAdapter : IDu03ADrawIntentSource
    {
        Du03AStrokeMode Mode { get; }
        Du03BCMappingEvidence LastMappingEvidence { get; }
        void ResetAdapter();
    }

    public abstract class Du03BCInputAdapterBase : MonoBehaviour, IDu03BCInputAdapter
    {
        [SerializeField] protected Du03BCInputEdgeLatch inputLatch;
        [SerializeField] protected Transform handMarker;
        [SerializeField] protected Camera targetCamera;
        [SerializeField] private bool verboseMappingLogging;

        protected Vector3 planeOrigin;
        protected Vector3 planeNormal;
        protected bool hasPlaneSnapshot;

        public abstract Du03AStrokeMode Mode { get; }
        public Du03BCMappingEvidence LastMappingEvidence { get; protected set; }

        public void Configure(Du03BCInputEdgeLatch latch, Transform marker, Camera cameraComponent)
        {
            inputLatch = latch;
            handMarker = marker;
            targetCamera = cameraComponent;
        }

        public Du03ADrawIntent ReadIntent()
        {
            if (inputLatch == null || handMarker == null || targetCamera == null)
                throw new InvalidOperationException($"DU-03BC {Mode} adapter is not configured.");

            var input = inputLatch.ConsumeStrokeEdges();
            if (input.CancelPressed)
            {
                hasPlaneSnapshot = false;
                OnStrokeEnded();
            }
            if (input.DrawPressed)
            {
                planeOrigin = handMarker.position;
                var gameplayNormal = Vector3.forward;
                hasPlaneSnapshot = IsFinite(gameplayNormal) && gameplayNormal.sqrMagnitude >= Du03AStrokeProfile.PlaneEpsilon * Du03AStrokeProfile.PlaneEpsilon;
                planeNormal = hasPlaneSnapshot ? gameplayNormal.normalized : default;
            }

            var receivesCandidate = input.DrawHeld || input.DrawPressed || input.DrawReleased;
            var candidate = default(Vector3);
            if (receivesCandidate && hasPlaneSnapshot)
            {
                TryMapCandidate(input, out candidate, out var evidence);
                LastMappingEvidence = evidence;
            }
            else
            {
                LastMappingEvidence = CreateInactiveEvidence(input);
            }

            if (verboseMappingLogging)
                LogMapping(LastMappingEvidence);
            return new Du03ADrawIntent(
                input.DrawPressed,
                input.DrawReleased,
                input.ConfirmPressed,
                input.CancelPressed,
                receivesCandidate && hasPlaneSnapshot,
                candidate);
        }

        public virtual void ResetAdapter()
        {
            hasPlaneSnapshot = false;
            planeOrigin = default;
            planeNormal = default;
            LastMappingEvidence = default;
            inputLatch?.ClearLatchedEdges("RESET");
            Debug.Log($"[DU03BC_RESET] mode={Mode} frame={Time.frameCount} planeSnapshot=False staleEdges=False result=PASS");
        }

        protected abstract bool TryMapCandidate(
            in Du03BCInputSnapshot input,
            out Vector3 candidate,
            out Du03BCMappingEvidence evidence);

        protected virtual void OnStrokeEnded()
        {
        }

        protected virtual Du03BCMappingEvidence CreateInactiveEvidence(in Du03BCInputSnapshot input)
        {
            return new Du03BCMappingEvidence(
                Mode,
                Time.frameCount,
                input,
                "NONE",
                Mode == Du03AStrokeMode.Aim ? "MOUSE_RAY" : "HAND_MARKER",
                null,
                null,
                null,
                handMarker.position,
                handMarker.localPosition,
                hasPlaneSnapshot ? planeOrigin : default,
                hasPlaneSnapshot ? planeNormal : default,
                null,
                null,
                null,
                Du03BCMappingInvalidReason.None);
        }

        protected static bool IsFinite(Vector2 value) => IsFinite(value.x) && IsFinite(value.y);
        protected static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        protected static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void LogMapping(in Du03BCMappingEvidence evidence)
        {
            if (evidence.SamplePhase == "NONE") return;
            var candidate = evidence.RawCandidate.HasValue ? Du02LogFormat.Vector(evidence.RawCandidate.Value) : "null";
            var expected = evidence.IndependentExpected.HasValue ? Du02LogFormat.Vector(evidence.IndependentExpected.Value) : "null";
            var error = evidence.MappingError.HasValue ? Du02LogFormat.Float(evidence.MappingError.Value) : "null";
            Debug.Log($"[DU03BC_SAMPLE] frame={evidence.RenderFrame} mode={evidence.Mode} source={evidence.MappingSource} phase={evidence.SamplePhase} sampleIndex=1 inputSeq={evidence.InputEventSequence} path={evidence.ExecutionPath}");
            Debug.Log($"[DU03BC_MAPPING] frame={evidence.RenderFrame} mode={evidence.Mode} source={evidence.MappingSource} candidate={candidate} expected={expected} error={error} reason={evidence.InvalidReason}");
        }
    }

}
