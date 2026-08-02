using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Stroke
{
    public static class Du03AStrokeProfile
    {
        public const float ReachRadius = 1.25f;
        public const float SampleSpacing = 0.08f;
        public const float DedupeThreshold = 0.02f;
        public const float MinimumStrokeLength = 0.20f;
        public const float SimplificationTolerance = 0.02f;
        public const float InitialInk = 5.00f;
        public const float PlaneEpsilon = 0.000001f;
    }

    public enum Du03AStrokeSessionState
    {
        Idle,
        Drawing,
        Pending,
        Committed,
        Cancelled
    }

    public enum Du03AStrokeMode
    {
        Aim,
        Trajectory,
        Spatial
    }

    public enum Du03ACandidateReason
    {
        Appended,
        SpacingNotReached,
        Dedupe,
        ReachInvalid,
        InkInvalid,
        NonFinite,
        WrongState
    }

    public readonly struct Du03ACandidateResult
    {
        public readonly bool CandidateValid;
        public readonly bool AcceptedAppended;
        public readonly Du03ACandidateReason Reason;
        public readonly Vector3 RawCandidate;
        public readonly Vector3 ProjectedCandidate;
        public readonly int AppendedPointCount;
        public readonly float RequiredInk;
        public readonly float LengthBefore;
        public readonly float LengthAfter;
        public readonly float AvailableInkBefore;
        public readonly float AvailableInkAfter;

        public Du03ACandidateResult(
            bool candidateValid,
            bool acceptedAppended,
            Du03ACandidateReason reason,
            Vector3 rawCandidate,
            Vector3 resolvedCandidateCandidate,
            int appendedPointCount,
            float requiredInk,
            float lengthBefore,
            float lengthAfter,
            float availableInkBefore,
            float availableInkAfter)
        {
            CandidateValid = candidateValid;
            AcceptedAppended = acceptedAppended;
            Reason = reason;
            RawCandidate = rawCandidate;
            ProjectedCandidate = resolvedCandidateCandidate;
            AppendedPointCount = appendedPointCount;
            RequiredInk = requiredInk;
            LengthBefore = lengthBefore;
            LengthAfter = lengthAfter;
            AvailableInkBefore = availableInkBefore;
            AvailableInkAfter = availableInkAfter;
        }
    }

    public readonly struct Du03AStrokeTransition
    {
        public readonly Du03AStrokeSessionState Before;
        public readonly Du03AStrokeSessionState After;
        public readonly string Reason;

        public Du03AStrokeTransition(Du03AStrokeSessionState before, Du03AStrokeSessionState after, string reason)
        {
            Before = before;
            After = after;
            Reason = reason;
        }
    }

    public sealed class Du03AStrokeData
    {
        private readonly Vector3[] simplifiedPoints;

        public IReadOnlyList<Vector3> SimplifiedPoints => Array.AsReadOnly(simplifiedPoints);
        public float ChargedLength { get; }
        public string OwnerId { get; }
        public Du03AStrokeMode Mode { get; }

        public Du03AStrokeData(IReadOnlyList<Vector3> points, float chargedLength, string ownerId, Du03AStrokeMode mode)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            simplifiedPoints = new Vector3[points.Count];
            for (var index = 0; index < points.Count; index++) simplifiedPoints[index] = points[index];
            ChargedLength = chargedLength;
            OwnerId = ownerId ?? throw new ArgumentNullException(nameof(ownerId));
            Mode = mode;
        }
    }

    public sealed class Du03AStrokeSession
    {
        private readonly List<Vector3> acceptedPoints = new();
        private readonly List<Du03AStrokeData> committedStrokes = new();
        private readonly List<Du03AStrokeTransition> transitions = new();
        private readonly float inkCapacity;

        private Vector3 planeOrigin;
        private Vector3 planeNormal;
        private string ownerId;
        private Du03AStrokeMode mode;
        private Du03AStrokeData pendingStroke;

        public Du03AStrokeSessionState State { get; private set; } = Du03AStrokeSessionState.Idle;
        public Du03AStrokeSessionState LastTerminalState { get; private set; } = Du03AStrokeSessionState.Idle;
        public IReadOnlyList<Vector3> AcceptedPoints => acceptedPoints;
        public IReadOnlyList<Du03AStrokeData> CommittedStrokes => committedStrokes;
        public IReadOnlyList<Du03AStrokeTransition> Transitions => transitions;
        public Du03AStrokeData PendingStroke => pendingStroke;
        public Vector3 PlaneOrigin => planeOrigin;
        public Vector3 PlaneNormal => planeNormal;
        public float AcceptedLength { get; private set; }
        public float AvailableInk { get; private set; }
        public float DrawingReservedLength { get; private set; }
        public float PendingReservedLength { get; private set; }
        public float CommittedChargedLength
        {
            get
            {
                var committed = 0f;
                foreach (var stroke in committedStrokes) committed += stroke.ChargedLength;
                return committed;
            }
        }
        public float LedgerTotal => AvailableInk + DrawingReservedLength + PendingReservedLength + CommittedChargedLength;
        public int PendingCount => pendingStroke == null ? 0 : 1;
        public int LiveCommittedCount => committedStrokes.Count;

        public Du03AStrokeSession(float initialInk = Du03AStrokeProfile.InitialInk)
        {
            if (!IsFinite(initialInk) || initialInk < 0f) throw new ArgumentOutOfRangeException(nameof(initialInk));
            inkCapacity = initialInk;
            AvailableInk = initialInk;
        }

        public bool TryBegin(Vector3 handOrigin, Vector3 cameraForward, string strokeOwnerId, Du03AStrokeMode strokeMode)
        {
            if (State != Du03AStrokeSessionState.Idle || pendingStroke != null) return false;
            if (!IsFinite(handOrigin) || !IsFinite(cameraForward) || string.IsNullOrWhiteSpace(strokeOwnerId)) return false;

            var yawNormal = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            if (yawNormal.sqrMagnitude < Du03AStrokeProfile.PlaneEpsilon * Du03AStrokeProfile.PlaneEpsilon) return false;

            planeOrigin = handOrigin;
            planeNormal = yawNormal.normalized;
            ownerId = strokeOwnerId;
            mode = strokeMode;
            acceptedPoints.Clear();
            acceptedPoints.Add(handOrigin);
            AcceptedLength = 0f;
            DrawingReservedLength = 0f;
            PendingReservedLength = 0f;
            LastTerminalState = Du03AStrokeSessionState.Idle;
            SetState(Du03AStrokeSessionState.Drawing, "DRAW_PRESS");
            ValidateLedger();
            return true;
        }

        public Du03ACandidateResult SubmitCandidate(Vector3 rawCandidate)
        {
            var lengthBefore = AcceptedLength;
            var inkBefore = AvailableInk;
            if (State != Du03AStrokeSessionState.Drawing)
                return Result(false, false, Du03ACandidateReason.WrongState, rawCandidate, default, 0, 0f, lengthBefore, inkBefore);
            if (!IsFinite(rawCandidate))
                return Result(false, false, Du03ACandidateReason.NonFinite, rawCandidate, default, 0, 0f, lengthBefore, inkBefore);

            var resolvedCandidate = mode == Du03AStrokeMode.Spatial
                ? rawCandidate
                : rawCandidate - Vector3.Dot(rawCandidate - planeOrigin, planeNormal) * planeNormal;
            if (!IsFinite(resolvedCandidate))
                return Result(false, false, Du03ACandidateReason.NonFinite, rawCandidate, default, 0, 0f, lengthBefore, inkBefore);
            if (mode != Du03AStrokeMode.Spatial
                && Vector3.Distance(planeOrigin, resolvedCandidate) > Du03AStrokeProfile.ReachRadius)
            {
                return Result(false, false, Du03ACandidateReason.ReachInvalid, rawCandidate, resolvedCandidate, 0, 0f, lengthBefore, inkBefore);
            }
            if (Vector3.Distance(acceptedPoints[^1], resolvedCandidate) < Du03AStrokeProfile.DedupeThreshold)
                return Result(true, false, Du03ACandidateReason.Dedupe, rawCandidate, resolvedCandidate, 0, 0f, lengthBefore, inkBefore);

            var prospective = BuildProspectivePoints(acceptedPoints[^1], resolvedCandidate);
            if (prospective.Count == 0)
            {
                return Result(true, false, Du03ACandidateReason.SpacingNotReached, rawCandidate, resolvedCandidate, 0, 0f, lengthBefore, inkBefore);
            }

            var requiredInk = prospective.Count * Du03AStrokeProfile.SampleSpacing;
            if (requiredInk > AvailableInk + 0.000001f)
                return Result(false, false, Du03ACandidateReason.InkInvalid, rawCandidate, resolvedCandidate, 0, requiredInk, lengthBefore, inkBefore);

            foreach (var point in prospective) acceptedPoints.Add(point);
            AcceptedLength += requiredInk;
            DrawingReservedLength += requiredInk;
            AvailableInk -= requiredInk;
            ValidateLedger();
            return Result(true, true, Du03ACandidateReason.Appended, rawCandidate, resolvedCandidate, prospective.Count, requiredInk, lengthBefore, inkBefore);
        }

        public bool Release()
        {
            if (State != Du03AStrokeSessionState.Drawing) return false;
            if (AcceptedLength < Du03AStrokeProfile.MinimumStrokeLength)
            {
                CancelInternal("RELEASE_BELOW_MINIMUM");
                return true;
            }

            PendingReservedLength = DrawingReservedLength;
            DrawingReservedLength = 0f;
            pendingStroke = new Du03AStrokeData(
                Simplify(acceptedPoints, Du03AStrokeProfile.SimplificationTolerance),
                AcceptedLength,
                ownerId,
                mode);
            SetState(Du03AStrokeSessionState.Pending, "DRAW_RELEASE");
            ValidateLedger();
            return true;
        }

        public Du03AStrokeData Confirm()
        {
            if (State != Du03AStrokeSessionState.Pending || pendingStroke == null) return null;
            var committed = pendingStroke;
            committedStrokes.Add(committed);
            pendingStroke = null;
            PendingReservedLength = 0f;
            LastTerminalState = Du03AStrokeSessionState.Committed;
            SetState(Du03AStrokeSessionState.Committed, "CONFIRM");
            SetState(Du03AStrokeSessionState.Idle, "COMMIT_COMPLETE");
            ClearWorkingStroke();
            ValidateLedger();
            return committed;
        }

        public bool Cancel()
        {
            if (State != Du03AStrokeSessionState.Drawing && State != Du03AStrokeSessionState.Pending) return false;
            CancelInternal("CANCEL_INPUT");
            return true;
        }

        public void Reset()
        {
            State = Du03AStrokeSessionState.Idle;
            LastTerminalState = Du03AStrokeSessionState.Idle;
            acceptedPoints.Clear();
            committedStrokes.Clear();
            transitions.Clear();
            pendingStroke = null;
            AcceptedLength = 0f;
            DrawingReservedLength = 0f;
            PendingReservedLength = 0f;
            AvailableInk = inkCapacity;
            ownerId = null;
            ValidateLedger();
        }

        public static IReadOnlyList<Vector3> Simplify(IReadOnlyList<Vector3> points, float tolerance)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count <= 2) return Copy(points);

            var keep = new bool[points.Count];
            keep[0] = true;
            keep[^1] = true;
            SimplifyRange(points, 0, points.Count - 1, tolerance * tolerance, keep);
            var result = new List<Vector3>();
            for (var index = 0; index < points.Count; index++)
            {
                if (keep[index]) result.Add(points[index]);
            }
            return result;
        }

        private void CancelInternal(string reason)
        {
            AvailableInk += DrawingReservedLength + PendingReservedLength;
            DrawingReservedLength = 0f;
            PendingReservedLength = 0f;
            pendingStroke = null;
            LastTerminalState = Du03AStrokeSessionState.Cancelled;
            SetState(Du03AStrokeSessionState.Cancelled, reason);
            SetState(Du03AStrokeSessionState.Idle, "CANCEL_COMPLETE");
            ClearWorkingStroke();
            ValidateLedger();
        }

        private void ClearWorkingStroke()
        {
            acceptedPoints.Clear();
            AcceptedLength = 0f;
            ownerId = null;
        }

        private List<Vector3> BuildProspectivePoints(Vector3 from, Vector3 target)
        {
            var distance = Vector3.Distance(from, target);
            var count = Mathf.FloorToInt((distance + 0.000001f) / Du03AStrokeProfile.SampleSpacing);
            var points = new List<Vector3>(count);
            if (count <= 0) return points;

            var direction = (target - from).normalized;
            for (var index = 1; index <= count; index++)
                points.Add(from + direction * (Du03AStrokeProfile.SampleSpacing * index));
            return points;
        }

        private Du03ACandidateResult Result(
            bool valid,
            bool appended,
            Du03ACandidateReason reason,
            Vector3 raw,
            Vector3 resolvedCandidate,
            int appendedCount,
            float requiredInk,
            float lengthBefore,
            float inkBefore)
        {
            return new Du03ACandidateResult(
                valid,
                appended,
                reason,
                raw,
                resolvedCandidate,
                appendedCount,
                requiredInk,
                lengthBefore,
                AcceptedLength,
                inkBefore,
                AvailableInk);
        }

        private void SetState(Du03AStrokeSessionState next, string reason)
        {
            transitions.Add(new Du03AStrokeTransition(State, next, reason));
            State = next;
        }

        private void ValidateLedger()
        {
            var total = LedgerTotal;
            if (Mathf.Abs(total - inkCapacity) > 0.0001f)
                throw new InvalidOperationException($"DU-03A ink invariant failed total={total:F6} capacity={inkCapacity:F6}");
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        private static IReadOnlyList<Vector3> Copy(IReadOnlyList<Vector3> points)
        {
            var copy = new Vector3[points.Count];
            for (var index = 0; index < points.Count; index++) copy[index] = points[index];
            return copy;
        }

        private static void SimplifyRange(IReadOnlyList<Vector3> points, int first, int last, float toleranceSquared, bool[] keep)
        {
            if (last <= first + 1) return;
            var maxDistanceSquared = -1f;
            var maxIndex = -1;
            for (var index = first + 1; index < last; index++)
            {
                var distanceSquared = DistanceToSegmentSquared(points[index], points[first], points[last]);
                if (distanceSquared <= maxDistanceSquared) continue;
                maxDistanceSquared = distanceSquared;
                maxIndex = index;
            }

            if (maxDistanceSquared <= toleranceSquared) return;
            keep[maxIndex] = true;
            SimplifyRange(points, first, maxIndex, toleranceSquared, keep);
            SimplifyRange(points, maxIndex, last, toleranceSquared, keep);
        }

        private static float DistanceToSegmentSquared(Vector3 point, Vector3 start, Vector3 end)
        {
            var segment = end - start;
            var denominator = segment.sqrMagnitude;
            if (denominator <= 0f) return (point - start).sqrMagnitude;
            var t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / denominator);
            return (point - (start + segment * t)).sqrMagnitude;
        }
    }
}
