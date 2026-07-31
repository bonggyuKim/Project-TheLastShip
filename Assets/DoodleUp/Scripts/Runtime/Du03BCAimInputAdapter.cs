using DoodleUp.Input;
using DoodleUp.Stroke;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Runtime
{
    public sealed class Du03BCAimInputAdapter : Du03BCInputAdapterBase
    {
        private bool hasProbeScreenPosition;
        private Vector2 probeScreenPosition;
        private bool hasProbeRay;
        private Ray probeRay;

        public override Du03AStrokeMode Mode => Du03AStrokeMode.Aim;

        public void SetProbeScreenPosition(Vector2 screenPosition)
        {
            hasProbeScreenPosition = true;
            probeScreenPosition = screenPosition;
            hasProbeRay = false;
        }

        public void SetProbeRay(Ray ray)
        {
            hasProbeRay = true;
            probeRay = ray;
            hasProbeScreenPosition = false;
        }

        public override void ResetAdapter()
        {
            base.ResetAdapter();
            hasProbeScreenPosition = false;
            hasProbeRay = false;
            probeScreenPosition = default;
            probeRay = default;
        }

        protected override bool TryMapCandidate(
            in Du03BCInputSnapshot input,
            out Vector3 candidate,
            out Du03BCMappingEvidence evidence)
        {
            var screen = hasProbeScreenPosition
                ? probeScreenPosition
                : Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var ray = hasProbeRay ? probeRay : targetCamera.ScreenPointToRay(screen);
            var invalidReason = Du03BCMappingInvalidReason.None;
            float? intersectionDistance = null;
            Vector3? rawCandidate = null;
            Vector3? expected = null;
            float? mappingError = null;

            if (!IsFinite(screen) || !IsFinite(ray.origin) || !IsFinite(ray.direction))
            {
                invalidReason = Du03BCMappingInvalidReason.NonFinite;
            }
            else
            {
                var denominator = Vector3.Dot(ray.direction, planeNormal);
                if (Mathf.Abs(denominator) <= Du03AStrokeProfile.PlaneEpsilon)
                {
                    invalidReason = Du03BCMappingInvalidReason.NoPlaneIntersection;
                }
                else
                {
                    var distance = Vector3.Dot(planeOrigin - ray.origin, planeNormal) / denominator;
                    var intersection = ray.origin + ray.direction * distance;
                    if (!IsFinite(distance) || !IsFinite(intersection))
                    {
                        invalidReason = Du03BCMappingInvalidReason.NonFinite;
                    }
                    else
                    {
                        intersectionDistance = distance;
                        rawCandidate = intersection;
                        expected = IndependentIntersection(ray, planeOrigin, planeNormal);
                        mappingError = Vector3.Distance(intersection, expected.Value);
                    }
                }
            }

            candidate = rawCandidate ?? new Vector3(float.NaN, float.NaN, float.NaN);
            evidence = new Du03BCMappingEvidence(
                Mode,
                Time.frameCount,
                input,
                "LATE_UPDATE",
                "MOUSE_RAY",
                screen,
                ray,
                intersectionDistance,
                handMarker.position,
                handMarker.localPosition,
                planeOrigin,
                planeNormal,
                rawCandidate,
                expected,
                mappingError,
                invalidReason);
            return rawCandidate.HasValue;
        }

        private static Vector3 IndependentIntersection(Ray ray, Vector3 origin, Vector3 normal)
        {
            var distance = Vector3.Dot(origin - ray.origin, normal) / Vector3.Dot(ray.direction, normal);
            return ray.origin + ray.direction * distance;
        }
    }
}
