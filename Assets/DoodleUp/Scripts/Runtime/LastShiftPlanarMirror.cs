using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>작은 실내 거울용 planar reflection. 물리·상호작용 없이 카메라 한 대만 빌린다.</summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class LastShiftPlanarMirror : MonoBehaviour
    {
        [SerializeField, Range(128, 1024)] private int resolution = 512;
        [SerializeField] private LayerMask reflectionMask = ~0;
        [SerializeField] private float clipPlaneOffset = 0.03f;

        private Camera reflectionCamera;
        private RenderTexture reflectionTexture;
        private Material mirrorMaterial;
        private Renderer mirrorRenderer;
        private bool isRendering;

        private void Awake()
        {
            mirrorRenderer = GetComponent<Renderer>();
            mirrorMaterial = mirrorRenderer.material;
        }

        private void OnWillRenderObject()
        {
            var source = Camera.current;
            if (source == null || source == reflectionCamera || isRendering || !enabled) return;
            EnsureResources();
            isRendering = true;
            try { RenderReflection(source); }
            finally { isRendering = false; }
        }

        private void EnsureResources()
        {
            if (reflectionTexture == null)
            {
                reflectionTexture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32)
                {
                    name = "LastShiftCockpitMirrorRT",
                    hideFlags = HideFlags.HideAndDontSave,
                    useMipMap = false,
                };
                reflectionTexture.Create();
            }

            if (reflectionCamera != null) return;
            var cameraObject = new GameObject("CockpitMirrorReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
            reflectionCamera = cameraObject.AddComponent<Camera>();
            reflectionCamera.enabled = false;
        }

        private void RenderReflection(Camera source)
        {
            var normal = transform.forward;
            var position = transform.position;
            var d = -Vector3.Dot(normal, position) - clipPlaneOffset;
            var reflection = CalculateReflectionMatrix(new Vector4(normal.x, normal.y, normal.z, d));

            reflectionCamera.CopyFrom(source);
            // 거울 자신은 반사에서 뺀다. 안 빼면 반사 안에 자기 면이 한 겹 더 서고,
            // 그 면은 아직 칠해지기 전이라 검게 남는다.
            reflectionCamera.cullingMask = reflectionMask & ~(1 << gameObject.layer);
            reflectionCamera.targetTexture = reflectionTexture;
            reflectionCamera.transform.position = reflection.MultiplyPoint(source.transform.position);
            // <b>회전도 반사시킨다.</b> 위치만 거울 너머로 보내고 회전을 원본 그대로 두면
            // 반사 카메라가 벽 안쪽을 향한 것으로 판단돼 컬링이 조종석 기하를 통째로 버린다 —
            // worldToCameraMatrix 는 렌더 행렬만 정하고 절두체 컬링은 transform 을 본다.
            // 그 상태가 "거울이 검다" 로 나온다.
            reflectionCamera.transform.rotation = Quaternion.LookRotation(
                reflection.MultiplyVector(source.transform.forward),
                reflection.MultiplyVector(source.transform.up));
            reflectionCamera.worldToCameraMatrix = source.worldToCameraMatrix * reflection;
            var clipPlane = CameraSpacePlane(reflectionCamera, position, normal, 1f);
            reflectionCamera.projectionMatrix = source.CalculateObliqueMatrix(clipPlane);

            var previous = GL.invertCulling;
            GL.invertCulling = true;
            reflectionCamera.Render();
            GL.invertCulling = previous;

            if (mirrorMaterial.HasProperty("_BaseMap")) mirrorMaterial.SetTexture("_BaseMap", reflectionTexture);
            if (mirrorMaterial.HasProperty("_MainTex")) mirrorMaterial.SetTexture("_MainTex", reflectionTexture);
        }

        private Vector4 CameraSpacePlane(Camera camera, Vector3 position, Vector3 normal, float sideSign)
        {
            var offsetPosition = position + normal * clipPlaneOffset;
            var matrix = camera.worldToCameraMatrix;
            var cameraPosition = matrix.MultiplyPoint(offsetPosition);
            var cameraNormal = matrix.MultiplyVector(normal).normalized * sideSign;
            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPosition, cameraNormal));
        }

        private static Matrix4x4 CalculateReflectionMatrix(Vector4 plane)
        {
            var matrix = Matrix4x4.identity;
            matrix.m00 = 1f - 2f * plane[0] * plane[0]; matrix.m01 = -2f * plane[0] * plane[1]; matrix.m02 = -2f * plane[0] * plane[2]; matrix.m03 = -2f * plane[3] * plane[0];
            matrix.m10 = -2f * plane[1] * plane[0]; matrix.m11 = 1f - 2f * plane[1] * plane[1]; matrix.m12 = -2f * plane[1] * plane[2]; matrix.m13 = -2f * plane[3] * plane[1];
            matrix.m20 = -2f * plane[2] * plane[0]; matrix.m21 = -2f * plane[2] * plane[1]; matrix.m22 = 1f - 2f * plane[2] * plane[2]; matrix.m23 = -2f * plane[3] * plane[2];
            return matrix;
        }

        private void OnDisable() => ReleaseResources();
        private void OnDestroy() => ReleaseResources();

        private void ReleaseResources()
        {
            if (reflectionTexture != null) { reflectionTexture.Release(); Destroy(reflectionTexture); reflectionTexture = null; }
            if (reflectionCamera != null) { Destroy(reflectionCamera.gameObject); reflectionCamera = null; }
        }
    }
}
