using System;
using UnityEngine;

namespace DoodleUp.Stroke
{
    public static class Du03AStrokeGeometryProfile
    {
        public const float Radius = 0.14f;
        public const float Diameter = Radius * 2f;
        public const float DegenerateEpsilon = 0.000001f;
    }

    public readonly struct Du03AStrokeGeometryResult
    {
        public readonly GameObject Root;
        public readonly int SegmentCount;
        public readonly int ColliderCount;
        public readonly int DegenerateSkipped;
        public readonly float MaximumSharedEndpointGap;
        public readonly bool GeometryValid;

        public Du03AStrokeGeometryResult(
            GameObject root,
            int segmentCount,
            int colliderCount,
            int degenerateSkipped,
            float maximumSharedEndpointGap,
            bool geometryValid)
        {
            Root = root;
            SegmentCount = segmentCount;
            ColliderCount = colliderCount;
            DegenerateSkipped = degenerateSkipped;
            MaximumSharedEndpointGap = maximumSharedEndpointGap;
            GeometryValid = geometryValid;
        }
    }

    public static class Du03AStrokeGeometry
    {
        public static Du03AStrokeGeometryResult Create(Du03AStrokeData stroke, Transform parent, int commitIndex)
        {
            if (stroke == null) throw new ArgumentNullException(nameof(stroke));
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            GameObject root = null;
            try
            {
                root = new GameObject($"DU03A_CommittedStroke_{commitIndex:D3}");
                root.SetActive(false);
                root.transform.SetParent(parent, false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                var segmentCount = Math.Max(0, stroke.SimplifiedPoints.Count - 1);
                var colliderCount = 0;
                var degenerateSkipped = 0;
                var maximumGap = 0f;
                var previousEnd = Vector3.zero;
                var hasPrevious = false;

                for (var index = 0; index < segmentCount; index++)
                {
                    var start = stroke.SimplifiedPoints[index];
                    var end = stroke.SimplifiedPoints[index + 1];
                    var segment = end - start;
                    var length = segment.magnitude;
                    if (length <= Du03AStrokeGeometryProfile.DegenerateEpsilon)
                    {
                        degenerateSkipped++;
                        Debug.Log($"[DU03A_CAPSULE_SKIP] owner={stroke.OwnerId} segment={index} length={length:F9} reason=DEGENERATE");
                        continue;
                    }

                    var child = new GameObject($"Capsule_{index:D3}");
                    child.transform.SetParent(root.transform, false);
                    child.transform.position = (start + end) * 0.5f;
                    child.transform.rotation = Quaternion.FromToRotation(Vector3.up, segment / length);
                    child.transform.localScale = Vector3.one;

                    var collider = child.AddComponent<CapsuleCollider>();
                    collider.direction = 1;
                    collider.radius = Du03AStrokeGeometryProfile.Radius;
                    collider.height = length + Du03AStrokeGeometryProfile.Diameter;
                    collider.center = Vector3.zero;
                    collider.isTrigger = false;
                    colliderCount++;

                    if (hasPrevious)
                    {
                        var centerlineGap = Vector3.Distance(previousEnd, start);
                        maximumGap = Mathf.Max(maximumGap, centerlineGap);
                    }

                    previousEnd = end;
                    hasPrevious = true;
                }

                var valid = Validate(root.transform, segmentCount, colliderCount, degenerateSkipped, maximumGap);
                if (!valid) throw new InvalidOperationException("DU-03A capsule geometry validation failed.");
                return new Du03AStrokeGeometryResult(root, segmentCount, colliderCount, degenerateSkipped, maximumGap, true);
            }
            catch
            {
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        public static void Activate(in Du03AStrokeGeometryResult result)
        {
            if (result.Root == null || !result.GeometryValid)
                throw new InvalidOperationException("Cannot activate invalid DU-03A stroke geometry.");
            result.Root.SetActive(true);
        }

        private static bool Validate(
            Transform root,
            int segmentCount,
            int colliderCount,
            int degenerateSkipped,
            float maximumGap)
        {
            if (root.localScale != Vector3.one || root.lossyScale != Vector3.one) return false;
            if (colliderCount + degenerateSkipped != segmentCount) return false;
            if (maximumGap > 0.000001f) return false;

            foreach (var collider in root.GetComponentsInChildren<CapsuleCollider>(true))
            {
                var segmentLength = collider.height - Du03AStrokeGeometryProfile.Diameter;
                if (collider.direction != 1
                    || Mathf.Abs(collider.radius - Du03AStrokeGeometryProfile.Radius) > 0.000001f
                    || segmentLength <= Du03AStrokeGeometryProfile.DegenerateEpsilon
                    || collider.center != Vector3.zero
                    || collider.isTrigger
                    || collider.transform.localScale != Vector3.one
                    || collider.transform.lossyScale != Vector3.one)
                    return false;
            }

            return root.GetComponentsInChildren<Rigidbody>(true).Length == 0;
        }
    }
}
