using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Stroke
{
    public readonly struct Du03ADrawIntent
    {
        public readonly bool DrawPressed;
        public readonly bool DrawReleased;
        public readonly bool ConfirmPressed;
        public readonly bool CancelPressed;
        public readonly bool HasCandidate;
        public readonly Vector3 Candidate;

        public Du03ADrawIntent(
            bool drawPressed,
            bool drawReleased,
            bool confirmPressed,
            bool cancelPressed,
            bool hasCandidate,
            Vector3 candidate)
        {
            DrawPressed = drawPressed;
            DrawReleased = drawReleased;
            ConfirmPressed = confirmPressed;
            CancelPressed = cancelPressed;
            HasCandidate = hasCandidate;
            Candidate = candidate;
        }
    }

    public readonly struct Du03ALateUpdateEvidence
    {
        public readonly int RenderFrame;
        public readonly long LateUpdateSequence;
        public readonly string SamplePhase;
        public readonly int CandidateCountThisFrame;
        public readonly string EventOrder;
        public readonly Du03ACandidateResult CandidateResult;

        public Du03ALateUpdateEvidence(
            int renderFrame,
            long lateUpdateSequence,
            int candidateCountThisFrame,
            string eventOrder,
            Du03ACandidateResult candidateResult)
        {
            RenderFrame = renderFrame;
            LateUpdateSequence = lateUpdateSequence;
            SamplePhase = "LATE_UPDATE";
            CandidateCountThisFrame = candidateCountThisFrame;
            EventOrder = eventOrder;
            CandidateResult = candidateResult;
        }
    }

    public interface IDu03ADrawIntentSource
    {
        Du03ADrawIntent ReadIntent();
    }

    [DefaultExecutionOrder(100)]
    public sealed class Du03AStrokeDriver : MonoBehaviour
    {
        [SerializeField] private Transform handMarker;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private string ownerId = "player-1";
        [SerializeField] private Du03AStrokeMode mode = Du03AStrokeMode.Trajectory;
        [SerializeField] private LineRenderer previewLine;
        [SerializeField] private Transform committedStrokeParent;
        [SerializeField] private MonoBehaviour intentSourceBehaviour;
        [SerializeField] private bool verboseSamplingLogging;

        private static readonly Color DrawingPreviewColor = new(0.15f, 0.95f, 1f, 1f);
        private static readonly Color PendingPreviewColor = new(1f, 0.72f, 0.12f, 1f);
        private static readonly Color InvalidPreviewColor = new(1f, 0.12f, 0.10f, 1f);
        private const float DrawingPreviewWidth = 0.12f;
        private const float PendingPreviewWidth = 0.16f;
        private const float InvalidPreviewWidth = 0.12f;

        private readonly List<GameObject> committedRoots = new();
        private IDu03ADrawIntentSource intentSource;
        private LineRenderer invalidPreviewLine;
        private Material previewMaterial;
        private Material invalidPreviewMaterial;

        public event Action<Du03ALateUpdateEvidence> LateUpdateProcessed;

        public Du03AStrokeSession Session { get; private set; }
        public Transform HandMarker => handMarker;
        public Du03AStrokeMode Mode => mode;
        public long LateUpdateSequence { get; private set; }
        public Du03AStrokeGeometryResult LastGeometryResult { get; private set; }
        public int CommittedColliderCount
        {
            get
            {
                var count = 0;
                foreach (var root in committedRoots)
                {
                    if (root != null) count += root.GetComponentsInChildren<CapsuleCollider>(true).Length;
                }
                return count;
            }
        }
        public bool PreviewVisible => previewLine != null && previewLine.enabled && previewLine.positionCount >= 2;

        public void Configure(
            Transform marker,
            Camera cameraComponent,
            IDu03ADrawIntentSource source,
            string strokeOwnerId,
            Du03AStrokeMode strokeMode,
            LineRenderer preview = null,
            Transform committedParent = null)
        {
            handMarker = marker;
            targetCamera = cameraComponent;
            intentSource = source;
            intentSourceBehaviour = source as MonoBehaviour;
            ownerId = strokeOwnerId;
            mode = strokeMode;
            previewLine = preview;
            committedStrokeParent = committedParent != null ? committedParent : transform;
            Session ??= new Du03AStrokeSession();
            ConfigurePreviewRendering();
            RefreshPreview();
        }

        public void SetModeForProbe(Du03AStrokeMode strokeMode)
        {
            if (Session != null && Session.State != Du03AStrokeSessionState.Idle)
                throw new InvalidOperationException("DU-03A mode can only change while Idle.");
            mode = strokeMode;
        }

        public void ResetSession()
        {
            Session ??= new Du03AStrokeSession();
            Session.Reset();
            LateUpdateSequence = 0;
            LastGeometryResult = default;
            DisableInvalidPreview();
            foreach (var root in committedRoots)
            {
                if (root != null) DestroyImmediate(root);
            }
            committedRoots.Clear();
            RefreshPreview();
            Debug.Log($"[DU03A_RESET] state={Session.State} live={Session.LiveCommittedCount} pending={Session.PendingCount} ink={Session.AvailableInk:F6} colliders={CommittedColliderCount}");
        }

        public Du03ACandidateResult ProcessIntent(in Du03ADrawIntent intent)
        {
            return ProcessIntentCore(intent, null);
        }

        private Du03ACandidateResult ProcessIntentCore(in Du03ADrawIntent intent, List<string> eventOrder)
        {
            Session ??= new Du03AStrokeSession();

            if (intent.CancelPressed)
            {
                var before = Session.State;
                if (Session.Cancel())
                {
                    DisableInvalidPreview();
                    eventOrder?.Add("CANCEL");
                    LogTransition(before, Session.State, "CANCEL_INPUT");
                    RefreshPreview();
                }
                else
                {
                    eventOrder?.Add("CANCEL_REJECTED");
                }
                return default;
            }

            if (intent.ConfirmPressed)
            {
                var before = Session.State;
                var committed = TryConfirmWithGeometry();
                if (committed != null)
                {
                    DisableInvalidPreview();
                    eventOrder?.Add("CONFIRM_COMMIT");
                    LogTransition(before, Session.State, "CONFIRM");
                    Debug.Log($"[DU03A_COMMIT] owner={committed.OwnerId} mode={committed.Mode} chargedLength={committed.ChargedLength:F6} simplifiedPoints={committed.SimplifiedPoints.Count} segments={LastGeometryResult.SegmentCount} colliders={LastGeometryResult.ColliderCount} renderers={LastGeometryResult.RendererCount} degenerateSkipped={LastGeometryResult.DegenerateSkipped} maxSharedEndpointGap={LastGeometryResult.MaximumSharedEndpointGap:F9}");
                    RefreshPreview();
                    return default;
                }

                eventOrder?.Add("CONFIRM_REJECTED");
            }

            if (intent.DrawPressed)
            {
                if (Session.State == Du03AStrokeSessionState.Idle)
                {
                    if (handMarker == null || targetCamera == null)
                        throw new InvalidOperationException("DU-03A driver requires HandMarker and Camera.");
                    var before = Session.State;
                    if (Session.TryBegin(handMarker.position, Vector3.forward, ownerId, mode))
                    {
                        eventOrder?.Add("PRESS");
                        LogTransition(before, Session.State, "DRAW_PRESS");
                    }
                }
                else
                {
                    eventOrder?.Add("PRESS_REJECTED");
                }
            }

            var result = default(Du03ACandidateResult);
            if (intent.HasCandidate && Session.State == Du03AStrokeSessionState.Drawing)
            {
                result = Session.SubmitCandidate(intent.Candidate);
                eventOrder?.Add("CANDIDATE");
                RefreshPreview(result);
                if (verboseSamplingLogging)
                    Debug.Log($"[DU03A_CANDIDATE] seq={LateUpdateSequence} valid={result.CandidateValid} appended={result.AcceptedAppended} reason={result.Reason} appendedCount={result.AppendedPointCount} requiredInk={result.RequiredInk:F6} lengthBefore={result.LengthBefore:F6} lengthAfter={result.LengthAfter:F6} inkBefore={result.AvailableInkBefore:F6} inkAfter={result.AvailableInkAfter:F6}");
            }

            if (intent.DrawReleased && Session.State == Du03AStrokeSessionState.Drawing)
            {
                var before = Session.State;
                Session.Release();
                DisableInvalidPreview();
                eventOrder?.Add("RELEASE");
                if (Session.State == Du03AStrokeSessionState.Pending)
                {
                    var committed = TryConfirmWithGeometry();
                    if (committed == null)
                        throw new InvalidOperationException("DU-03A release auto-commit failed after entering Pending.");
                    eventOrder?.Add("AUTO_COMMIT");
                    LogTransition(before, Session.State, "DRAW_RELEASE_COMMIT");
                    Debug.Log($"[DU03A_COMMIT] owner={committed.OwnerId} mode={committed.Mode} trigger=LMB_RELEASE chargedLength={committed.ChargedLength:F6} simplifiedPoints={committed.SimplifiedPoints.Count} segments={LastGeometryResult.SegmentCount} colliders={LastGeometryResult.ColliderCount} renderers={LastGeometryResult.RendererCount} degenerateSkipped={LastGeometryResult.DegenerateSkipped} maxSharedEndpointGap={LastGeometryResult.MaximumSharedEndpointGap:F9}");
                }
                else
                {
                    LogTransition(before, Session.State, "DRAW_RELEASE_CANCELLED");
                }
                RefreshPreview();
            }

            return result;
        }

        private Du03AStrokeData TryConfirmWithGeometry()
        {
            if (Session.State != Du03AStrokeSessionState.Pending || Session.PendingStroke == null)
                return null;

            var prepared = Du03AStrokeGeometry.Create(
                Session.PendingStroke,
                committedStrokeParent != null ? committedStrokeParent : transform,
                Session.LiveCommittedCount + 1);
            try
            {
                var committed = Session.Confirm();
                if (committed == null) throw new InvalidOperationException("DU-03A Confirm state changed during geometry preparation.");
                Du03AStrokeGeometry.Activate(prepared);
                committedRoots.Add(prepared.Root);
                LastGeometryResult = prepared;
                return committed;
            }
            catch
            {
                if (prepared.Root != null) DestroyImmediate(prepared.Root);
                throw;
            }
        }

        private void Awake()
        {
            if (intentSource == null && intentSourceBehaviour is IDu03ADrawIntentSource serializedSource)
                intentSource = serializedSource;
        }

        private void LateUpdate()
        {
            if (intentSource == null && intentSourceBehaviour is IDu03ADrawIntentSource serializedSource)
                intentSource = serializedSource;
            if (intentSource == null) return;
            LateUpdateSequence++;
            var intent = intentSource.ReadIntent();
            var order = new List<string>(4);
            var result = ProcessIntentCore(intent, order);
            var candidateCount = order.Contains("CANDIDATE") ? 1 : 0;
            var evidence = new Du03ALateUpdateEvidence(
                Time.frameCount,
                LateUpdateSequence,
                candidateCount,
                order.Count == 0 ? "NONE" : string.Join(">", order),
                result);
            LateUpdateProcessed?.Invoke(evidence);
            if (verboseSamplingLogging && evidence.EventOrder != "NONE")
                Debug.Log($"[DU03A_LATE_UPDATE] renderFrame={evidence.RenderFrame} sequence={evidence.LateUpdateSequence} samplePhase={evidence.SamplePhase} candidateCount={evidence.CandidateCountThisFrame} order={evidence.EventOrder}");
        }

        private void ConfigurePreviewRendering()
        {
            if (previewLine == null) return;
            previewLine.useWorldSpace = true;
            previewLine.numCapVertices = 6;
            previewLine.numCornerVertices = 4;
            previewLine.widthMultiplier = DrawingPreviewWidth;
            previewLine.startColor = DrawingPreviewColor;
            previewLine.endColor = DrawingPreviewColor;

            if (previewMaterial == null)
            {
                var previewShader = Shader.Find("Sprites/Default");
                if (previewShader == null) previewShader = Shader.Find("Standard");
                if (previewShader == null) throw new InvalidOperationException("DU-03A preview shader is unavailable.");
                previewMaterial = new Material(previewShader)
                {
                    name = "DU03AStrokePreviewMaterial",
                    color = Color.white
                };
                previewLine.sharedMaterial = previewMaterial;
            }

            if (invalidPreviewLine != null) return;
            var invalidObject = new GameObject("DU03A_InvalidPreview");
            invalidObject.transform.SetParent(previewLine.transform.parent != null ? previewLine.transform.parent : transform, false);
            invalidPreviewLine = invalidObject.AddComponent<LineRenderer>();
            invalidPreviewLine.useWorldSpace = true;
            invalidPreviewLine.numCapVertices = 6;
            invalidPreviewLine.widthMultiplier = InvalidPreviewWidth;
            invalidPreviewLine.startColor = InvalidPreviewColor;
            invalidPreviewLine.endColor = InvalidPreviewColor;
            invalidPreviewLine.positionCount = 0;
            invalidPreviewLine.enabled = false;

            var invalidPreviewShader = Shader.Find("Sprites/Default");
            if (invalidPreviewShader == null) invalidPreviewShader = Shader.Find("Standard");
            if (invalidPreviewShader == null) throw new InvalidOperationException("DU-03A preview shader is unavailable.");
            invalidPreviewMaterial = new Material(invalidPreviewShader)
            {
                name = "DU03AInvalidPreviewMaterial",
                color = Color.white
            };
            invalidPreviewLine.sharedMaterial = invalidPreviewMaterial;
        }

        private void RefreshPreview(Du03ACandidateResult candidateResult = default)
        {
            if (previewLine == null || Session == null) return;
            ConfigurePreviewRendering();
            var pending = Session.State == Du03AStrokeSessionState.Pending && Session.PendingStroke != null;
            var points = pending ? Session.PendingStroke.SimplifiedPoints : Session.AcceptedPoints;
            previewLine.positionCount = points.Count;
            for (var index = 0; index < points.Count; index++) previewLine.SetPosition(index, points[index]);
            previewLine.widthMultiplier = pending ? PendingPreviewWidth : DrawingPreviewWidth;
            var color = pending ? PendingPreviewColor : DrawingPreviewColor;
            previewLine.startColor = color;
            previewLine.endColor = color;
            previewLine.enabled = Session.State == Du03AStrokeSessionState.Drawing || pending;

            if (invalidPreviewLine == null) return;
            var showInvalid = Session.State == Du03AStrokeSessionState.Drawing
                && !candidateResult.CandidateValid
                && (candidateResult.Reason == Du03ACandidateReason.ReachInvalid
                    || candidateResult.Reason == Du03ACandidateReason.InkInvalid);
            if (showInvalid)
            {
                invalidPreviewLine.positionCount = 2;
                invalidPreviewLine.SetPosition(0, Session.AcceptedPoints[Session.AcceptedPoints.Count - 1]);
                invalidPreviewLine.SetPosition(1, candidateResult.ProjectedCandidate);
            }
            else
            {
                invalidPreviewLine.positionCount = 0;
            }
            invalidPreviewLine.enabled = showInvalid;
        }

        private void DisableInvalidPreview()
        {
            if (invalidPreviewLine == null) return;
            invalidPreviewLine.positionCount = 0;
            invalidPreviewLine.enabled = false;
        }

        private void OnDestroy()
        {
            if (previewMaterial != null) DestroyImmediate(previewMaterial);
            if (invalidPreviewMaterial != null) DestroyImmediate(invalidPreviewMaterial);
        }

        private static void LogTransition(Du03AStrokeSessionState before, Du03AStrokeSessionState after, string reason)
        {
            Debug.Log($"[DU03A_STATE] before={before} after={after} reason={reason}");
        }
    }
}
