using System;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleUp.Editor
{
    /// <summary>
    /// 래그돌 프로토타입 전용 테스트맵을 굽는다.
    ///
    /// <b>본 게임 씬을 안 건드린다.</b> <c>LAST_SHIFT_SP02A_NETWORK</c>·<c>LAST_SHIFT_SOLO</c> 는
    /// 열지도 않는다 — 그 둘은 여는 것만으로 프리팹이 재직렬화돼 fileID 연결이 흔들린 전례가 있다.
    /// 여기서 만드는 씬은 바닥·벽 두 장·상자 하나·승무원 하나가 전부다.
    ///
    /// 방 배치는 <c>ship-orientation-and-room-brief-v1.md</c> 의 문 앞 진입 띠(R-1)를 흉내만 냈다 —
    /// 정본 좌표를 복제하면 정본이 두 벌이 되므로, 폭만 같고 위치는 원점 기준으로 새로 잡았다.
    /// </summary>
    public static class LastShiftRagdollLabScene
    {
        public const string ScenePath = "Assets/Scenes/LAST_SHIFT_RAGDOLL_LAB.unity";

        public const string CharacterPrefabPath =
            "Assets/DoodleUp/Art/Characters/LastShiftLimeAlien/LastShiftLimeAlien_Animated.prefab";

        /// <summary>문 통과 폭. 승무원 둘이 동시에 들어가려다 부딪히는 자리를 만드는 값이다.</summary>
        public const float DoorwayGap = 1.2f;

        /// <summary>바닥 한 변(m). 저중력에서는 한 번 밀린 몸이 꽤 멀리 흐르므로 넉넉히 잡는다.</summary>
        public const float FloorSize = 40f;

        /// <summary>
        /// 승무원 크기 배율. <b>게임과 같은 값이어야 한다</b> —
        /// <see cref="LastShiftNetworkSceneBuilder"/> 가 이 프리팹을 1.5배로 세워 두고 있고,
        /// 여기서 1배로 두면 같은 임펄스가 전혀 다른 그림을 만든다.
        /// </summary>
        /// <summary>문 벽 한 장의 길이(m). 문틀로 읽히되 카메라를 막지 않을 만큼만.</summary>
        public const float WallLength = 3f;

        /// <summary>
        /// 바닥·소품 머티리얼. 프리미티브 기본 흰 머티리얼은 조명에 하얗게 날아가
        /// 승무원 실루엣을 지웠다 — 배에서 쓰는 머티리얼을 그대로 빌려 온다.
        /// </summary>
        public const string FloorMaterialPath = "Assets/DoodleUp/Materials/LS_Floor.mat";
        public const string PropMaterialPath = "Assets/DoodleUp/Materials/LS_Hull.mat";

        public const float CrewScale = 1.5f;

        [MenuItem("Last Shift/Prototype/Rebuild Ragdoll Lab Scene")]
        public static void Rebuild()
        {
            var scene = Build();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LAST_SHIFT_RAGDOLL_LAB_BUILD] scene={ScenePath} parts={LastShiftRagdollRig.Bones.Length} result=PASS");
        }

        /// <summary>
        /// 씬 내용을 만들고 <b>저장은 안 한다.</b> 캡처 자동화가 디스크를 안 건드리고 같은 씬을
        /// 세우려고 이 형태를 쓴다 — 증거를 뽑느라 씬 파일이 매번 바뀌면 diff 가 못 읽힌다.
        /// </summary>
        public static Scene Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(FloorMaterialPath);
            var propMaterial = AssetDatabase.LoadAssetAtPath<Material>(PropMaterialPath);

            var floor = Box("Floor", new Vector3(0f, -0.5f, 0f), new Vector3(FloorSize, 1f, FloorSize), floorMaterial);
            floor.isStatic = true;

            // 문 벽 두 장. 사이 틈이 DoorwayGap 이고, 그 앞에서 부딪히는 게 R-1 상황이다.
            // <b>벽은 짧게 둔다.</b> 바닥 폭만큼 길게 세웠더니 밀려간 승무원을 따라가는 카메라가
            // 벽 뒤로 들어가 열 장 중 절반이 벽만 찍혔다 — 문틀로 읽힐 만큼만 남긴다.
            var wallOffset = (DoorwayGap + WallLength) * 0.5f;
            Box("DoorWall_Port", new Vector3(-wallOffset, 1.6f, 3.0f), new Vector3(WallLength, 3.2f, 0.35f), propMaterial);
            Box("DoorWall_Starboard", new Vector3(wallOffset, 1.6f, 3.0f), new Vector3(WallLength, 3.2f, 0.35f), propMaterial);

            // 걸려 넘어질 상자. 부위별 반응이 바닥 말고 다른 것에도 걸리는지 보는 용도다.
            // 카메라가 지나는 우현(+x) 쪽을 비워 두고 좌현 뒤편에 놓는다.
            Box("Crate", new Vector3(-1.1f, 0.35f, -2.4f), new Vector3(0.7f, 0.7f, 0.7f), propMaterial);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException($"승무원 프리팹을 못 찾았다: {CharacterPrefabPath}");

            var crew = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            crew.name = "RagdollSubject";
            crew.transform.SetPositionAndRotation(new Vector3(0f, 0f, 0f), Quaternion.identity);
            crew.transform.localScale = Vector3.one * CrewScale;
            crew.AddComponent<LastShiftRagdoll>();
            crew.AddComponent<LastShiftRagdollLab>();

            var camera = Camera.main;
            if (camera != null)
            {
                LastShiftRagdollLab.FrameSubject(camera, crew.transform.position + Vector3.up * 0.2f);
                camera.fieldOfView = 55f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 80f;
            }

            var light = UnityEngine.Object.FindAnyObjectByType<Light>();
            if (light != null)
            {
                light.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
                // 흰 기본 머티리얼 바닥이 1.15 에서는 하얗게 날아가 승무원 실루엣이 안 읽혔다.
                light.intensity = 0.85f;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return scene;
        }

        private static GameObject Box(string name, Vector3 center, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = center;
            box.transform.localScale = size;
            if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }
    }
}
