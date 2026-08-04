using System.IO;
using System.Linq;
using DoodleUp.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleUp.Editor
{
    public static class LastShiftSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/LAST_SHIFT_SP01.unity";
        // 구역 이름 정본은 Runtime 의 LastShiftSceneZones 다. 런타임 연출(손상 구역 표시)이
        // 같은 문자열로 구역을 찾아야 하므로 여기서는 그것을 재노출만 한다.
        public const string CockpitZoneName = LastShiftSceneZones.CockpitZoneName;
        public const string UtilityZoneName = LastShiftSceneZones.UtilityZoneName;
        public const string LifeSupportZoneName = LastShiftSceneZones.LifeSupportZoneName;

        private static Material hullMaterial;
        private static Material floorMaterial;
        private static Material cockpitMaterial;
        private static Material utilityMaterial;
        private static Material lifeSupportMaterial;
        private static Material ceilingMaterial;
        private static Material ductMaterial;
        private static Material panelMaterial;
        private static Material starMaterial;
        private static Material voidMaterial;

        /// <summary>
        /// 천장 내면 높이. 정본은 Runtime 의 LastShiftShipPhysics 다. 점프 정점이 이 값을
        /// 넘으면 카메라가 선체 밖으로 나가므로 두 값은 반드시 같은 상수를 봐야 한다.
        /// </summary>
        private const float CeilingInnerHeight = LastShiftShipPhysics.CeilingInnerHeight;

        private const float CeilingThickness = 0.2f;
        private const float HullFrontZ = -2.45f;
        private const float WindowSillHeight = 0.6f;

        [MenuItem("Last Shift/SP-01/Rebuild Sandbox")]
        public static void RebuildSandbox()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[LAST_SHIFT_BUILD] cancelled=true reason=active-scene-not-saved");
                return;
            }

            BuildAndSaveSandbox();
        }

        public static void RebuildSandboxForAutomation()
        {
            var activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
                throw new System.InvalidOperationException("Refusing to replace a dirty active scene during automated SP-01 rebuild.");

            BuildAndSaveSandbox();
        }

        public static bool HasUnsavedActiveSceneChanges()
        {
            var activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() && activeScene.isDirty;
        }

        private static void BuildAndSaveSandbox()
        {
            ResetCachedMaterials();
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "LAST_SHIFT_SP01";
            CreateLighting();
            CreateShipGraybox();
            var player = CreatePlayer();
            var items = CreateItems();
            var runtime = new GameObject("LAST_SHIFT_SP01_Runtime");
            runtime.AddComponent<LastShiftImpactFeedback>();
            runtime.AddComponent<LastShiftSandboxController>().Configure(player, items);
            CreateMeteorStimulus();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureSceneInBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"[LAST_SHIFT_BUILD] scene={ScenePath} zones=3 players=1 items={items.Length} buildScene=1 result=PASS");
        }

        private static void EnsureSceneInBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FirstOrDefault(scene => scene.path == ScenePath);
            if (existing != null)
            {
                existing.enabled = true;
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void CreateShipGraybox()
        {
            var ship = new GameObject("ShipGraybox");
            CreateZone(CockpitZoneName, ship.transform, new Vector3(-4f, 0f, 0f), cockpitMaterial ??= CreateMaterial("LS_Cockpit", new Color(0.24f, 0.38f, 0.50f)));
            CreateZone(UtilityZoneName, ship.transform, Vector3.zero, utilityMaterial ??= CreateMaterial("LS_Utility", new Color(0.42f, 0.38f, 0.28f)));
            CreateZone(LifeSupportZoneName, ship.transform, new Vector3(4f, 0f, 0f), lifeSupportMaterial ??= CreateMaterial("LS_LifeSupport", new Color(0.26f, 0.48f, 0.36f)));
            // 벽 높이는 천장 내면(CeilingInnerHeight)까지 올린다. 예전 3.0 을 유지하면
            // 벽과 천장 사이에 0.2m 띠 구멍이 남아 저중력에서 뜬 물건이 그 틈으로 빠진다.
            CreateCube("OuterHull_Left", ship.transform, new Vector3(-6.15f, CeilingInnerHeight * 0.5f, 0f), new Vector3(0.2f, CeilingInnerHeight, 5f), hullMaterial ??= CreateMaterial("LS_Hull", new Color(0.18f, 0.20f, 0.23f)));
            CreateCube("OuterHull_Right", ship.transform, new Vector3(6.15f, CeilingInnerHeight * 0.5f, 0f), new Vector3(0.2f, CeilingInnerHeight, 5f), hullMaterial);
            CreateCube("OuterHull_Back", ship.transform, new Vector3(0f, CeilingInnerHeight * 0.5f, 2.45f), new Vector3(12.5f, CeilingInnerHeight, 0.2f), hullMaterial);
            CreateCube("OuterHull_FrontLower", ship.transform, new Vector3(0f, WindowSillHeight * 0.5f, HullFrontZ), new Vector3(12.5f, WindowSillHeight, 0.2f), hullMaterial);
            CreateCube("Bulkhead_Left", ship.transform, new Vector3(-2f, CeilingInnerHeight * 0.5f, 0f), new Vector3(0.15f, CeilingInnerHeight, 3.2f), hullMaterial);
            CreateCube("Bulkhead_Right", ship.transform, new Vector3(2f, CeilingInnerHeight * 0.5f, 0f), new Vector3(0.15f, CeilingInnerHeight, 3.2f), hullMaterial);
            CreateShipCeiling(ship.transform);
            CreateForwardWindows(ship.transform);
            CreateInstrumentPanels(ship.transform);
            CreateDucts(ship.transform);
            CreateCube("CockpitConsole", ship.transform, new Vector3(-5.1f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 2.5f), cockpitMaterial);
            CreateCube("TetherRack", ship.transform, TetherRackPosition, TetherRackScale, cockpitMaterial);
            CreateCube("BusCabinet", ship.transform, new Vector3(0f, 0.65f, 1.8f), new Vector3(1.6f, 1.3f, 0.5f), utilityMaterial);
            CreateCube("LifeSupportRack", ship.transform, new Vector3(5.1f, 0.75f, 1.6f), new Vector3(0.8f, 1.5f, 0.8f), lifeSupportMaterial);
            CreateZoneLabel("COCKPIT", new Vector3(-4f, 2.25f, 2.25f), cockpitMaterial.color);
            CreateZoneLabel("UTILITY / BUS", new Vector3(0f, 2.25f, 2.25f), utilityMaterial.color);
            CreateZoneLabel("LIFE SUPPORT", new Vector3(4f, 2.25f, 2.25f), lifeSupportMaterial.color);
        }

        /// <summary>
        /// 천장을 닫는다. 닫아야 하는 이유는 두 가지다. 하나는 "우주선 안"이 읽히려면 위가
        /// 막혀 있어야 한다는 것이고, 다른 하나는 저중력에서 뜬 물건이 위로 빠져나가
        /// ItemSafetyBounds 의 above-world 복구를 계속 밟는 것을 막는 것이다.
        /// </summary>
        private static void CreateShipCeiling(Transform ship)
        {
            ceilingMaterial ??= CreateMaterial("LS_Ceiling", new Color(0.21f, 0.23f, 0.26f));
            CreateCube("Ceiling", ship, new Vector3(0f, CeilingInnerHeight + CeilingThickness * 0.5f, 0f), new Vector3(12.5f, CeilingThickness, 5f), ceilingMaterial);
            // 천장 리브. 평평한 판만 있으면 실내가 아니라 뚜껑처럼 보인다.
            for (var index = 0; index < 7; index++)
            {
                var x = -5.4f + index * 1.8f;
                CreateDecorCube($"CeilingRib_{index}", ship, new Vector3(x, CeilingInnerHeight - 0.06f, 0f), new Vector3(0.18f, 0.12f, 4.9f), hullMaterial);
            }
        }

        /// <summary>
        /// 앞쪽 창과 그 너머 별. 별은 실제 스카이박스 대신 창 밖에 놓은 점 격자다.
        /// 스카이박스 자산을 요구하지 않고도 "밖은 우주"가 읽히고, 창 프레임이
        /// 시야를 잘라 주므로 격자라는 것이 드러나지 않는다.
        /// </summary>
        private static void CreateForwardWindows(Transform ship)
        {
            voidMaterial ??= CreateMaterial("LS_Void", new Color(0.012f, 0.016f, 0.030f));
            // 별은 발광이어야 한다. 실내 조명이 창 밖까지 닿지 않으므로 일반 재질로 두면
            // 검은 벽과 구분되지 않는다(첫 렌더에서 확인). 자기발광으로 두면 조명과 무관하게 보인다.
            starMaterial ??= CreateEmissiveMaterial("LS_Star", new Color(0.92f, 0.95f, 1f), 2.2f);
            panelMaterial ??= CreateMaterial("LS_Panel", new Color(0.14f, 0.16f, 0.19f));

            // 창 위 상부 선체(창 높이만큼 비운 자리를 메운다)
            const float windowTop = 2.1f;
            CreateCube("OuterHull_FrontUpper", ship, new Vector3(0f, (CeilingInnerHeight + windowTop) * 0.5f, HullFrontZ), new Vector3(12.5f, CeilingInnerHeight - windowTop, 0.2f), hullMaterial);
            // 창 사이 기둥
            foreach (var x in new[] { -4f, 0f, 4f })
                CreateCube($"WindowMullion_{x:F0}", ship, new Vector3(x, (WindowSillHeight + windowTop) * 0.5f, HullFrontZ), new Vector3(0.35f, windowTop - WindowSillHeight, 0.22f), panelMaterial);

            // 창 밖 우주. 창보다 크게 두어 창틀 사이로 선체 밖 회색이 보이지 않게 한다.
            CreateDecorCube("SpaceVoid", ship, new Vector3(0f, 1.6f, HullFrontZ - 6f), new Vector3(34f, 18f, 0.2f), voidMaterial);
            var starRandom = new System.Random(20260804);
            var stars = new GameObject("StarField");
            stars.transform.SetParent(ship, false);
            for (var index = 0; index < 90; index++)
            {
                var x = (float)(starRandom.NextDouble() * 30.0 - 15.0);
                var y = (float)(starRandom.NextDouble() * 14.0 - 4.0);
                var z = HullFrontZ - 3.2f - (float)(starRandom.NextDouble() * 2.4);
                // 창까지 거리가 3~6m 라 0.05 짜리는 화면에서 1~2픽셀로 사라진다. 0.10~0.24 로 키운다.
                var size = 0.10f + (float)starRandom.NextDouble() * 0.14f;
                CreateDecorCube($"Star_{index}", stars.transform, new Vector3(x, y, z), Vector3.one * size, starMaterial);
            }
        }

        /// <summary>
        /// 계기·콘솔 패널. 벽면이 완전히 비어 있으면 큐브 상자로 읽히므로, 각 구역 벽에
        /// 패널과 발광 계기 띠를 붙여 "장비가 있는 실내"로 만든다.
        /// </summary>
        private static void CreateInstrumentPanels(Transform ship)
        {
            panelMaterial ??= CreateMaterial("LS_Panel", new Color(0.14f, 0.16f, 0.19f));
            CreateWallPanel("Panel_Cockpit", ship, new Vector3(-4f, 1.55f, 2.32f), new Vector3(3.2f, 1.1f, 0.12f), cockpitMaterial.color);
            CreateWallPanel("Panel_Utility", ship, new Vector3(0f, 1.55f, 2.32f), new Vector3(3.2f, 1.1f, 0.12f), utilityMaterial.color);
            CreateWallPanel("Panel_LifeSupport", ship, new Vector3(4f, 1.55f, 2.32f), new Vector3(3.2f, 1.1f, 0.12f), lifeSupportMaterial.color);
            CreateWallPanel("Panel_PortWall", ship, new Vector3(-6.02f, 1.7f, -0.9f), new Vector3(0.12f, 1.0f, 2.2f), cockpitMaterial.color);
            CreateWallPanel("Panel_StarboardWall", ship, new Vector3(6.02f, 1.7f, -0.9f), new Vector3(0.12f, 1.0f, 2.2f), lifeSupportMaterial.color);
        }

        private static void CreateWallPanel(string name, Transform ship, Vector3 position, Vector3 scale, Color readoutColor)
        {
            CreateDecorCube(name, ship, position, scale, panelMaterial);
            // 발광 계기 띠. 조명이 어두운 구역에서도 패널 위치가 읽히게 한다.
            var readout = CreateEmissiveMaterial($"{name}_Readout", readoutColor, 1.4f);
            var isVertical = scale.x < scale.z;
            var stripScale = isVertical
                ? new Vector3(scale.x * 1.2f, 0.07f, scale.z * 0.72f)
                : new Vector3(scale.x * 0.72f, 0.07f, scale.z * 1.2f);
            for (var index = 0; index < 3; index++)
            {
                var offsetY = 0.30f - index * 0.30f;
                CreateDecorCube($"{name}_Readout_{index}", ship, position + new Vector3(0f, offsetY, 0f), stripScale, readout);
            }
        }

        /// <summary>
        /// 배관·덕트. 천장 아래를 가로지르는 관은 "선체 설비"라는 신호가 가장 강한 요소다.
        /// 캡슐을 눕혀 쓰면 원통이 되므로 별도 메시 자산이 필요 없다.
        /// </summary>
        private static void CreateDucts(Transform ship)
        {
            ductMaterial ??= CreateMaterial("LS_Duct", new Color(0.34f, 0.33f, 0.30f));
            // 좌우로 길게 지나는 주 배관 두 줄
            CreatePipe("Duct_Main_Fore", ship, new Vector3(0f, CeilingInnerHeight - 0.42f, -1.55f), new Vector3(0f, 0f, 90f), 0.16f, 5.9f);
            CreatePipe("Duct_Main_Aft", ship, new Vector3(0f, CeilingInnerHeight - 0.42f, 1.62f), new Vector3(0f, 0f, 90f), 0.13f, 5.9f);
            // 벽으로 내려가는 수직 지관. x 는 패널 사이 빈 구간(패널 폭 3.2 가 x=-4/0/4 에
            // 놓이므로 경계는 ±2.0, ±5.6)에 둔다. 패널 위에 겹치면 발광 계기 띠를 가려
            // 정면에서 관이 계기판을 관통한 것처럼 보인다.
            foreach (var x in new[] { -5.85f, -2.05f, 2.05f, 5.85f })
                CreatePipe($"Duct_Riser_{x:F1}", ship, new Vector3(x, 1.5f, 2.24f), Vector3.zero, 0.11f, 1.5f);
        }

        private static void CreatePipe(string name, Transform ship, Vector3 position, Vector3 eulerAngles, float radius, float halfLength)
        {
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            pipe.name = name;
            pipe.transform.SetParent(ship, false);
            pipe.transform.localPosition = position;
            pipe.transform.localRotation = Quaternion.Euler(eulerAngles);
            pipe.transform.localScale = new Vector3(radius * 2f, halfLength, radius * 2f);
            pipe.GetComponent<MeshRenderer>().sharedMaterial = ductMaterial;
            // 장식물에 콜라이더를 남기면 저중력에서 뜬 물건이 배관에 끼어 회수가 어려워진다.
            Object.DestroyImmediate(pipe.GetComponent<Collider>());
        }

        /// <summary>콜라이더 없는 장식 큐브. 물건이 걸리지 않아야 하는 요소에 쓴다.</summary>
        private static GameObject CreateDecorCube(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var cube = CreateCube(name, parent, localPosition, scale, material);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            return cube;
        }

        private static void CreateZone(string name, Transform parent, Vector3 position, Material material)
        {
            var zone = new GameObject(name);
            zone.transform.SetParent(parent, false);
            zone.transform.position = position;
            CreateCube("Floor", zone.transform, new Vector3(0f, -0.1f, 0f), new Vector3(4f, 0.2f, 5f), floorMaterial ??= CreateMaterial("LS_Floor", new Color(0.30f, 0.32f, 0.35f)));
            var strip = CreateCube("ZoneStrip", zone.transform, new Vector3(0f, 0.015f, 2.2f), new Vector3(3.7f, 0.03f, 0.25f), material);
            Object.DestroyImmediate(strip.GetComponent<Collider>());
        }

        private static LastShiftPlayerController CreatePlayer()
        {
            var player = new GameObject("PlayerOne");
            player.transform.position = LastShiftSandboxController.PlayerSpawn;
            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.28f;
            controller.height = 1.7f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            var cameraObject = new GameObject("PlayerOne Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.fieldOfView = 72f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 80f;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            var socket = new GameObject("HoldSocket").transform;
            socket.SetParent(cameraObject.transform, false);
            socket.localPosition = new Vector3(0.45f, -0.30f, 1.1f);
            var playerController = player.AddComponent<LastShiftPlayerController>();
            playerController.Configure(camera, socket);
            CreatePlayerMarker(player.transform, new Color(0.2f, 0.65f, 1f));
            return playerController;
        }

        private static void CreatePlayerMarker(Transform player, Color identityColor)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            marker.name = "PlayerOne_Identity";
            marker.transform.SetParent(player, false);
            marker.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            marker.transform.localScale = new Vector3(0.32f, 0.48f, 0.32f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial("LS_PlayerOne", identityColor);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        /// <summary>
        /// Tether 는 어떤 프리셋에서도 loose 로 유지되는 유일한 상시 grab 대상이므로 시작 위치에서
        /// 보이면서 GrabDistance(2.2m) 안이어야 한다. 예전 (-3.1, 0.25, 1.55) 는 spawn 에서 2.85m 로
        /// 사거리 밖이었고, 바닥 높이 아이템은 카메라(y≈1.65, 수직 FOV 72°) 기준 사거리 안으로
        /// 당길수록 화면 밖으로 내려가 조준 자체가 불가능하다. 그래서 받침대(TetherRack) 위에 올린다.
        /// loose 상태의 Rigidbody 는 kinematic 이 아니므로 공중 배치는 낙하한다.
        /// </summary>
        public static readonly Vector3 TetherRackPosition = new(-2.62f, 0.60f, -1.28f);

        public static readonly Vector3 TetherRackScale = new(0.5f, 1.2f, 0.9f);

        public static readonly Vector3 TetherSpawnPosition = new(-2.62f, 1.325f, -1.28f);

        private static LastShiftGrabbable[] CreateItems()
        {
            return new[]
            {
                CreateItem("Battery", LastShiftItemRole.Battery, new Vector3(0.6f, 0.38f, 0.8f), new Vector3(0.65f, 0.65f, 0.9f), new Color(0.95f, 0.65f, 0.12f), true),
                CreateItem("CoolingCanister", LastShiftItemRole.CoolingCanister, new Vector3(0f, 0.55f, -1.3f), new Vector3(0.55f, 1.1f, 0.55f), new Color(0.15f, 0.72f, 0.95f), true),
                CreateItem("PatchPlate", LastShiftItemRole.PatchPlate, new Vector3(4.5f, 0.65f, -1.6f), new Vector3(1.15f, 1.15f, 0.18f), new Color(0.78f, 0.82f, 0.88f), true),
                CreateItem("Tether", LastShiftItemRole.Tether, TetherSpawnPosition, new Vector3(0.25f, 0.25f, 1.2f), new Color(0.95f, 0.30f, 0.22f), true)
            };
        }

        private static LastShiftGrabbable CreateItem(string name, LastShiftItemRole role, Vector3 position, Vector3 scale, Color color, bool secured)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial($"LS_{name}", color);
            var body = item.AddComponent<Rigidbody>();
            body.mass = role == LastShiftItemRole.Battery ? 8f : 3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // 저중력은 씬 직렬화 값에도 반영한다. Awake 의 ConfigureItemBody 만 의존하면
            // 씬을 열어 첫 물리 스텝이 도는 사이 한 프레임 동안 지구 중력으로 떨어진다.
            LastShiftShipPhysics.ConfigureItemBody(body);
            var grabbable = item.AddComponent<LastShiftGrabbable>();
            grabbable.Configure(role, secured);
            return grabbable;
        }

        private static void CreateMeteorStimulus()
        {
            var meteor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meteor.name = "CanonicalMeteorStimulus";
            meteor.transform.position = LastShiftMeteorStimulus.Canonical.ImpactPoint - LastShiftMeteorStimulus.Canonical.ImpactVector * 2f;
            meteor.transform.localScale = Vector3.one * 0.65f;
            meteor.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial("LS_Meteor", new Color(0.82f, 0.22f, 0.08f));
            Object.DestroyImmediate(meteor.GetComponent<Collider>());
        }

        private static GameObject CreateCube(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localScale = scale;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
            return cube;
        }

        private static void CreateZoneLabel(string text, Vector3 position, Color color)
        {
            var label = new GameObject($"Label_{text}");
            label.transform.position = position;
            // TextMesh 는 +Z 를 보는 면에 글자를 그린다. 라벨은 z=2.25 뒤쪽 벽에 붙어 낮은 z
            // 쪽의 플레이어를 향하므로 회전 없이 두어야 읽힌다. Euler(0,180,0) 을 주면
            // 글자가 좌우로 뒤집혀 "TROPPUS EFIL" 로 보인다.
            label.transform.rotation = Quaternion.identity;
            var textMesh = label.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.fontSize = 48;
            textMesh.characterSize = 0.08f;
            textMesh.color = color;
        }

        /// <summary>
        /// 실내 조명. 천장을 닫으면 Directional Light 가 차단되므로 예전 설정 그대로 두면
        /// 실내가 거의 검게 된다. 그래서 밝은 야외용 ambient/directional 을 낮추고 구역마다
        /// 천장 등을 둔다. 구역별 색을 달리해 어디 있는지 조명만으로도 구분되게 한다.
        /// </summary>
        private static void CreateLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            // 우주 실내라 하늘광이 없다. 형태를 잃지 않을 최소값만 남긴다.
            RenderSettings.ambientLight = new Color(0.10f, 0.11f, 0.14f);
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            // 천장에 막히므로 형태 보조용으로만 남긴다.
            light.intensity = 0.25f;
            light.color = new Color(0.72f, 0.78f, 0.95f);
            lightObject.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            CreateZoneLight("Light_Cockpit", new Vector3(-4f, CeilingInnerHeight - 0.35f, 0f), new Color(0.62f, 0.78f, 1f), 2.5f);
            CreateZoneLight("Light_Utility", new Vector3(0f, CeilingInnerHeight - 0.35f, 0f), new Color(1f, 0.86f, 0.62f), 2.3f);
            CreateZoneLight("Light_LifeSupport", new Vector3(4f, CeilingInnerHeight - 0.35f, 0f), new Color(0.66f, 1f, 0.80f), 2.3f);
        }

        private static void CreateZoneLight(string name, Vector3 position, Color color, float intensity)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.position = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            // 구역 폭 4m + 깊이 5m 를 덮되 옆 구역까지 흘러 구분이 사라지지 않는 반경.
            light.range = 7f;
            light.shadows = LightShadows.Soft;
        }

        private static void ResetCachedMaterials()
        {
            hullMaterial = null;
            floorMaterial = null;
            cockpitMaterial = null;
            utilityMaterial = null;
            lifeSupportMaterial = null;
            ceilingMaterial = null;
            ductMaterial = null;
            panelMaterial = null;
            starMaterial = null;
            voidMaterial = null;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            return new Material(shader) { name = name, color = color };
        }

        /// <summary>
        /// 자기발광 재질. 실내 조명이 닿지 않는 곳(창 밖 별)이나 조명과 무관하게 항상 읽혀야
        /// 하는 곳(계기 띠)에 쓴다. Standard 셰이더는 _EMISSION 키워드를 켜야 발광이 적용된다.
        /// </summary>
        private static Material CreateEmissiveMaterial(string name, Color color, float intensity)
        {
            var material = CreateMaterial(name, color);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return material;
        }
    }
}
