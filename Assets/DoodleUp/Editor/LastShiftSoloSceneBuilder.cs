using DoodleUp.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 네트워크 씬을 그대로 열어 승무원 하나를 세우는 솔로 씬으로 저장한다.
    ///
    /// <b>배를 다시 짓지 않는다.</b> 좌표 정본이 하나여야 하므로 솔로 전용 빌더를 따로 두지
    /// 않고, <see cref="LastShiftNetworkSceneBuilder"/> 가 구운 결과를 열어
    /// <see cref="LastShiftSoloBootstrap"/> 하나만 얹는다 — 배가 두 벌이 되면 여기서만 맞는
    /// 배가 생기고, 그 차이는 플레이로만 드러난다.
    /// </summary>
    public static class LastShiftSoloSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/LAST_SHIFT_SOLO.unity";

        [MenuItem("Last Shift/SP-02A/Rebuild Solo Scene")]
        public static void Rebuild()
        {
            var scene = EditorSceneManager.OpenScene(LastShiftNetworkSceneBuilder.ScenePath, OpenSceneMode.Single);

            var session = Object.FindAnyObjectByType<LastShiftNetworkSession>();
            var sandbox = Object.FindAnyObjectByType<LastShiftSandboxController>();
            if (session == null || sandbox == null)
                throw new System.InvalidOperationException(
                    $"{LastShiftNetworkSceneBuilder.ScenePath} 에 세션이나 샌드박스가 없다 — 네트워크 씬을 먼저 구워야 한다.");

            var existing = Object.FindAnyObjectByType<LastShiftSoloBootstrap>();
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject("SoloBootstrap");
            root.AddComponent<LastShiftSoloBootstrap>().Configure(session, sandbox);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[LAST_SHIFT_SOLO_BUILD] scene={ScenePath} crew=1 network=off result=PASS");
        }
    }
}
