using DoodleUp.Stroke;
using UnityEngine;

namespace DoodleUp.Runtime
{
    public sealed class Du03BCPlayabilityVisuals : MonoBehaviour
    {
        private const int ReachSegments = 64;
        private static readonly Color ReachColor = new(0.25f, 0.90f, 1f, 0.45f);
        private static readonly Color BodyColor = new(0.30f, 0.42f, 0.95f, 1f);
        private static readonly Color HandColor = new(1f, 0.55f, 0.05f, 1f);

        private static Material bodyMaterial;
        private static Material handMaterial;
        private static Material reachMaterial;

        [SerializeField] private Transform handMarker;
        [SerializeField] private Du03AStrokeDriver strokeDriver;
        [SerializeField] private LineRenderer reachLine;

        public bool ReachVisible => reachLine != null && reachLine.enabled;

        public void Configure(Transform marker, Du03AStrokeDriver driver, LineRenderer line)
        {
            handMarker = marker;
            strokeDriver = driver;
            reachLine = line;
            EnsureCharacterVisuals();
            EnsureReachLine();
            ConfigureReachLine();
            RefreshReachCircle();
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureCharacterVisuals();
            EnsureReachLine();
            ConfigureReachLine();
        }

        private void LateUpdate()
        {
            ResolveReferences();
            EnsureCharacterVisuals();
            EnsureReachLine();
            ConfigureReachLine();
            if (handMarker == null || reachLine == null) return;
            RefreshReachCircle();
            reachLine.enabled = strokeDriver != null
                && strokeDriver.Session != null
                && strokeDriver.Session.State == Du03AStrokeSessionState.Drawing;
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
            var player = handMarker.parent;
            if (player.Find("BodyVisual") == null)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "BodyVisual";
                body.transform.SetParent(player, false);
                body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                body.transform.localRotation = Quaternion.identity;
                body.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                DestroyImmediate(body.GetComponent<Collider>());
                body.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial(ref bodyMaterial, "DU02PlayerBodyMaterial", BodyColor);
            }

            if (handMarker.Find("HandVisual") == null)
            {
                var hand = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                hand.name = "HandVisual";
                hand.transform.SetParent(handMarker, false);
                hand.transform.localPosition = Vector3.zero;
                hand.transform.localRotation = Quaternion.identity;
                hand.transform.localScale = Vector3.one * 0.22f;
                DestroyImmediate(hand.GetComponent<Collider>());
                hand.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial(ref handMaterial, "DU03BCHandMaterial", HandColor);
                var markerRenderer = handMarker.GetComponent<MeshRenderer>();
                if (markerRenderer != null) markerRenderer.enabled = false;
            }
        }

        private void EnsureReachLine()
        {
            if (reachLine != null) return;
            var reachObject = new GameObject("DU03BC_ReachIndicator");
            reachObject.transform.SetParent(transform, false);
            reachLine = reachObject.AddComponent<LineRenderer>();
            reachLine.sharedMaterial = GetMaterial(ref reachMaterial, "DU03BCReachMaterial", Color.white);
        }

        private static Material GetMaterial(ref Material material, string name, Color color)
        {
            if (material != null) return material;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");
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
