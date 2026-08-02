using DoodleUp.Stroke;
using UnityEngine;

namespace DoodleUp.Runtime
{
    public enum Du03BCArmVisualState
    {
        Neutral,
        Drawing
    }

    public sealed class Du03BCPlayabilityVisuals : MonoBehaviour
    {
        private const int ReachSegments = 64;
        private const float UpperArmWidth = 0.18f;
        private const float ForearmWidth = 0.15f;
        private static readonly Vector3 LegacyShoulderLocalPosition = new(0.34f, 0.92f, 0.18f);
        private static readonly Vector3 ElbowBendLocalOffset = new(0.20f, -0.16f, -0.04f);
        private static readonly Color ReachColor = new(0.25f, 0.90f, 1f, 0.45f);
        private static readonly Color BodyColor = new(0.18f, 0.28f, 0.62f, 1f);
        private static readonly Color ArmColor = new(0.30f, 0.42f, 0.95f, 1f);
        private static readonly Color HandColor = new(1f, 0.55f, 0.05f, 1f);
        private static readonly Color DrawingColor = new(0.10f, 0.90f, 1f, 1f);

        private static Material bodyMaterial;
        private static Material armMaterial;
        private static Material handMaterial;
        private static Material drawingMaterial;
        private static Material reachMaterial;

        [SerializeField] private Transform handMarker;
        [SerializeField] private Du03AStrokeDriver strokeDriver;
        [SerializeField] private LineRenderer reachLine;

        private Transform bodyVisual;
        private Transform armVisualRoot;
        private Transform upperArmVisual;
        private Transform forearmVisual;
        private MeshRenderer[] armRenderers;
        private MeshRenderer[] handRenderers;
        private Du03BCArmVisualState visualState = (Du03BCArmVisualState)(-1);

        public bool ReachVisible => reachLine != null && reachLine.enabled;
        public Du03BCArmVisualState VisualState => visualState;
        public Transform ArmVisualRoot => armVisualRoot;

        public void Configure(Transform marker, Du03AStrokeDriver driver, LineRenderer line)
        {
            handMarker = marker;
            strokeDriver = driver;
            reachLine = line;
            ResolveReferences();
            EnsureCharacterVisuals();
            EnsureReachLine();
            ConfigureReachLine();
            RefreshArmPose();
            RefreshVisualState();
            RefreshArmVisibility();
            RefreshReachCircle();
            RefreshReachVisibility();
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureCharacterVisuals();
            EnsureReachLine();
            ConfigureReachLine();
            RefreshArmPose();
            RefreshVisualState();
            RefreshArmVisibility();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            EnsureCharacterVisuals();
            EnsureReachLine();
            ConfigureReachLine();
            if (handMarker == null || reachLine == null) return;
            RefreshArmPose();
            RefreshVisualState();
            RefreshArmVisibility();
            RefreshReachCircle();
            RefreshReachVisibility();
        }

        private void RefreshReachVisibility()
        {
            if (reachLine == null) return;
            reachLine.enabled = strokeDriver != null
                && strokeDriver.Session != null
                && strokeDriver.Session.State == Du03AStrokeSessionState.Drawing
                && strokeDriver.Mode != Du03AStrokeMode.Spatial;
        }

        private void RefreshArmVisibility()
        {
            var spatialFirstPerson = strokeDriver != null
                && strokeDriver.Mode == Du03AStrokeMode.Spatial;
            SetRendererEnabled(bodyVisual, !spatialFirstPerson);
            SetRendererEnabled(upperArmVisual, true);
            SetRendererEnabled(forearmVisual, true);
            if (handRenderers == null) return;
            foreach (var renderer in handRenderers) renderer.enabled = true;

            var markerRenderer = handMarker != null
                ? handMarker.GetComponent<MeshRenderer>()
                : null;
            if (markerRenderer != null) markerRenderer.enabled = false;
        }

        private static void SetRendererEnabled(Transform visual, bool enabled)
        {
            if (visual == null) return;
            var renderer = visual.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.enabled = enabled;
        }

        private void ResolveReferences()
        {
            if (strokeDriver == null) strokeDriver = FindFirstObjectByType<Du03AStrokeDriver>();
            if (handMarker == null && strokeDriver != null) handMarker = strokeDriver.HandMarker;
            if (reachLine == null) reachLine = GetComponentInChildren<LineRenderer>(true);
        }

        private void EnsureCharacterVisuals()
        {
            if (handMarker == null || handMarker.parent == null) return;
            var pitchAnchor = handMarker.parent.name == Du02CameraRig.ArmPitchAnchorName
                ? handMarker.parent
                : null;
            var bodyAnchor = pitchAnchor != null && pitchAnchor.parent != null
                ? pitchAnchor.parent
                : handMarker.parent;
            bodyVisual = bodyAnchor.Find("BodyVisual");
            if (bodyVisual == null)
            {
                var body = CreatePrimitiveVisual(
                    PrimitiveType.Capsule,
                    "BodyVisual",
                    bodyAnchor,
                    new Vector3(0f, 0.5f, 0f),
                    Quaternion.identity,
                    new Vector3(0.5f, 0.5f, 0.5f));
                body.GetComponent<MeshRenderer>().sharedMaterial =
                    GetMaterial(ref bodyMaterial, "DU02PlayerBodyMaterial", BodyColor);
                bodyVisual = body.transform;
            }

            var armAnchor = pitchAnchor != null ? pitchAnchor : bodyAnchor;
            armVisualRoot = armAnchor.Find("ArmVisualRoot");
            if (armVisualRoot == null)
            {
                armVisualRoot = new GameObject("ArmVisualRoot").transform;
                armVisualRoot.SetParent(armAnchor, false);
            }

            upperArmVisual = EnsureSegment("UpperArmVisual");
            forearmVisual = EnsureSegment("ForearmVisual");
            EnsureHandVisuals();
            armRenderers = armVisualRoot.GetComponentsInChildren<MeshRenderer>(true);
            handRenderers = handMarker.GetComponentsInChildren<MeshRenderer>(true);
            ApplyVisualStateMaterials(visualState < 0 ? Du03BCArmVisualState.Neutral : visualState);
        }

        private Transform EnsureSegment(string name)
        {
            var segment = armVisualRoot.Find(name);
            if (segment != null) return segment;
            return CreatePrimitiveVisual(
                PrimitiveType.Capsule,
                name,
                armVisualRoot,
                Vector3.zero,
                Quaternion.identity,
                Vector3.one).transform;
        }

        private void EnsureHandVisuals()
        {
            var legacySphere = handMarker.Find("HandVisual");
            if (legacySphere != null) DestroyImmediate(legacySphere.gameObject);

            if (handMarker.Find("PalmVisual") == null)
            {
                CreatePrimitiveVisual(
                    PrimitiveType.Sphere,
                    "PalmVisual",
                    handMarker,
                    new Vector3(0f, -0.015f, -0.24f),
                    Quaternion.Euler(-8f, -8f, 0f),
                    new Vector3(0.24f, 0.14f, 0.30f));
            }

            EnsureFingerVisual("FingerIndexVisual", new Vector3(-0.075f, 0.035f, -0.07f));
            EnsureFingerVisual("FingerMiddleVisual", new Vector3(0f, 0.045f, -0.055f));
            EnsureFingerVisual("FingerRingVisual", new Vector3(0.075f, 0.025f, -0.075f));

            if (handMarker.Find("ThumbVisual") == null)
            {
                CreatePrimitiveVisual(
                    PrimitiveType.Capsule,
                    "ThumbVisual",
                    handMarker,
                    new Vector3(-0.16f, -0.045f, -0.18f),
                    Quaternion.Euler(70f, 15f, 42f),
                    new Vector3(0.055f, 0.10f, 0.055f));
            }

            var markerRenderer = handMarker.GetComponent<MeshRenderer>();
            if (markerRenderer != null) markerRenderer.enabled = false;
        }

        private void EnsureFingerVisual(string name, Vector3 localPosition)
        {
            if (handMarker.Find(name) != null) return;
            CreatePrimitiveVisual(
                PrimitiveType.Capsule,
                name,
                handMarker,
                localPosition,
                Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.045f, 0.13f, 0.045f));
        }

        private void RefreshArmPose()
        {
            if (handMarker == null || armVisualRoot == null || upperArmVisual == null || forearmVisual == null) return;
            var usesPitchAnchor = handMarker.parent != null
                && handMarker.parent.name == Du02CameraRig.ArmPitchAnchorName;
            var shoulder = usesPitchAnchor ? Vector3.zero : LegacyShoulderLocalPosition;
            var wrist = handMarker.localPosition + new Vector3(0f, -0.04f, -0.13f);
            var elbow = Vector3.Lerp(shoulder, wrist, 0.52f) + ElbowBendLocalOffset;
            SetCapsuleBetween(upperArmVisual, shoulder, elbow, UpperArmWidth);
            SetCapsuleBetween(forearmVisual, elbow, wrist, ForearmWidth);
        }

        private void RefreshVisualState()
        {
            var drawing = strokeDriver != null
                && strokeDriver.Session != null
                && strokeDriver.Session.State == Du03AStrokeSessionState.Drawing;
            var nextState = drawing
                ? Du03BCArmVisualState.Drawing
                : Du03BCArmVisualState.Neutral;
            if (nextState == visualState) return;
            visualState = nextState;
            ApplyVisualStateMaterials(nextState);
        }

        private void ApplyVisualStateMaterials(Du03BCArmVisualState state)
        {
            if (armRenderers == null || handRenderers == null) return;
            var stateMaterial = state == Du03BCArmVisualState.Drawing
                ? GetMaterial(ref drawingMaterial, "DU03BCArmDrawingMaterial", DrawingColor)
                : null;
            var armStateMaterial = stateMaterial != null
                ? stateMaterial
                : GetMaterial(ref armMaterial, "DU03BCArmMaterial", ArmColor);
            var handStateMaterial = stateMaterial != null
                ? stateMaterial
                : GetMaterial(ref handMaterial, "DU03BCHandMaterial", HandColor);
            foreach (var renderer in armRenderers) renderer.sharedMaterial = armStateMaterial;
            foreach (var renderer in handRenderers) renderer.sharedMaterial = handStateMaterial;
        }

        private void EnsureReachLine()
        {
            if (reachLine != null) return;
            var reachObject = new GameObject("DU03BC_ReachIndicator");
            reachObject.transform.SetParent(transform, false);
            reachLine = reachObject.AddComponent<LineRenderer>();
            reachLine.sharedMaterial = GetMaterial(ref reachMaterial, "DU03BCReachMaterial", Color.white);
        }

        private static GameObject CreatePrimitiveVisual(
            PrimitiveType primitiveType,
            string name,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            var visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.SetLocalPositionAndRotation(localPosition, localRotation);
            visual.transform.localScale = localScale;
            DestroyImmediate(visual.GetComponent<Collider>());
            return visual;
        }

        private static void SetCapsuleBetween(Transform segment, Vector3 start, Vector3 end, float width)
        {
            var direction = end - start;
            segment.localPosition = (start + end) * 0.5f;
            segment.localRotation = direction.sqrMagnitude > 0f
                ? Quaternion.FromToRotation(Vector3.up, direction.normalized)
                : Quaternion.identity;
            segment.localScale = new Vector3(width, direction.magnitude * 0.5f, width);
        }

        private static Material GetMaterial(ref Material material, string name, Color color)
        {
            if (material != null) return material;
            var shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = name, color = color };
            return material;
        }

        private void ConfigureReachLine()
        {
            if (reachLine == null || reachLine.positionCount == ReachSegments) return;
            reachLine.useWorldSpace = true;
            reachLine.loop = true;
            reachLine.widthMultiplier = 0.025f;
            reachLine.numCapVertices = 3;
            reachLine.startColor = ReachColor;
            reachLine.endColor = ReachColor;
            reachLine.positionCount = ReachSegments;
            reachLine.enabled = false;
        }

        private void RefreshReachCircle()
        {
            if (handMarker == null || reachLine == null) return;
            var origin = handMarker.position;
            for (var index = 0; index < ReachSegments; index++)
            {
                var angle = index * Mathf.PI * 2f / ReachSegments;
                reachLine.SetPosition(index, origin + new Vector3(
                    Mathf.Cos(angle) * Du03AStrokeProfile.ReachRadius,
                    Mathf.Sin(angle) * Du03AStrokeProfile.ReachRadius,
                    0f));
            }
        }
    }
}
