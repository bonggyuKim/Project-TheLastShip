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

        // 선체 치수 정본은 Runtime 의 LastShiftShipDimensions 다. 여기서는 짧은 별칭만 둔다 —
        // 이 파일에 치수 리터럴이 다시 쌓이면 다음 스케일 조정 때 또 35곳을 뒤져야 한다.
        private const float Length = LastShiftShipDimensions.InteriorLength;
        private const float Width = LastShiftShipDimensions.InteriorWidth;
        private const float HalfLength = LastShiftShipDimensions.HalfLength;
        private const float HalfWidth = LastShiftShipDimensions.HalfWidth;
        private const float EndWallX = LastShiftShipDimensions.EndWallX;
        private const float SideWallZ = LastShiftShipDimensions.SideWallZ;

        private const float CeilingThickness = LastShiftShipDimensions.HullThickness;

        /// <summary>창이 달린 선체 앞면(z-). 좌우 긴 벽 중 앞쪽이다.</summary>
        private const float HullFrontZ = -SideWallZ;

        /// <summary>뒤쪽 긴 벽(z+).</summary>
        private const float HullBackZ = SideWallZ;

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
            CreateZone(CockpitZoneName, ship.transform, LastShiftZone.Cockpit, cockpitMaterial ??= CreateMaterial("LS_Cockpit", new Color(0.24f, 0.38f, 0.50f)));
            CreateZone(UtilityZoneName, ship.transform, LastShiftZone.Utility, utilityMaterial ??= CreateMaterial("LS_Utility", new Color(0.42f, 0.38f, 0.28f)));
            CreateZone(LifeSupportZoneName, ship.transform, LastShiftZone.LifeSupport, lifeSupportMaterial ??= CreateMaterial("LS_LifeSupport", new Color(0.26f, 0.48f, 0.36f)));
            // 벽 높이는 천장 내면(CeilingInnerHeight)까지 올린다. 예전 3.0 을 유지하면
            // 벽과 천장 사이에 0.2m 띠 구멍이 남아 저중력에서 뜬 물건이 그 틈으로 빠진다.
            //
            // Left/Right 는 전장 축(x)의 두 끝벽이고 Back/Front 는 전폭 축(z)의 긴 벽이다.
            // 이름은 예전 배치에서 굳은 것이라 그대로 두되, 좌표는 전부 치수 정본에서 파생한다.
            CreateCube("OuterHull_Left", ship.transform, new Vector3(-EndWallX, CeilingInnerHeight * 0.5f, 0f), new Vector3(LastShiftShipDimensions.HullThickness, CeilingInnerHeight, LastShiftShipDimensions.EndWallSpan), hullMaterial ??= CreateMaterial("LS_Hull", new Color(0.18f, 0.20f, 0.23f)));
            CreateCube("OuterHull_Right", ship.transform, new Vector3(EndWallX, CeilingInnerHeight * 0.5f, 0f), new Vector3(LastShiftShipDimensions.HullThickness, CeilingInnerHeight, LastShiftShipDimensions.EndWallSpan), hullMaterial);
            CreateCube("OuterHull_Back", ship.transform, new Vector3(0f, CeilingInnerHeight * 0.5f, HullBackZ), new Vector3(LastShiftShipDimensions.SideWallSpan, CeilingInnerHeight, LastShiftShipDimensions.HullThickness), hullMaterial);
            CreateCube("OuterHull_FrontLower", ship.transform, new Vector3(0f, WindowSillHeight * 0.5f, HullFrontZ), new Vector3(LastShiftShipDimensions.SideWallSpan, WindowSillHeight, LastShiftShipDimensions.HullThickness), hullMaterial);
            CreatePassage(ship.transform, 0);
            CreatePassage(ship.transform, 1);
            CreateBulkheadWithDoor("Left", ship.transform, 0);
            CreateBulkheadWithDoor("Right", ship.transform, 1);
            CreateShipCeiling(ship.transform);
            CreateForwardWindows(ship.transform);
            CreateInstrumentPanels(ship.transform);
            CreateDucts(ship.transform);
            CreateCube("CockpitConsole", ship.transform, new Vector3(LastShiftShipDimensions.CockpitCenterX - 1.3f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 2.5f), cockpitMaterial);
            CreateCube("TetherRack", ship.transform, TetherRackPosition, TetherRackScale, cockpitMaterial);
            CreateCube("BusCabinet", ship.transform, new Vector3(LastShiftShipDimensions.UtilityCenterX, 0.65f, BackWallInnerZ - 0.55f), new Vector3(1.6f, 1.3f, 0.5f), utilityMaterial);
            CreateCube("LifeSupportRack", ship.transform, new Vector3(LastShiftShipDimensions.LifeSupportCenterX + 1.1f, 0.75f, BackWallInnerZ - 0.75f), new Vector3(0.8f, 1.5f, 0.8f), lifeSupportMaterial);
            CreateZoneLabel("COCKPIT", new Vector3(LastShiftShipDimensions.CockpitCenterX, 2.25f, BackWallInnerZ - 0.13f), cockpitMaterial.color);
            CreateZoneLabel("UTILITY / BUS", new Vector3(LastShiftShipDimensions.UtilityCenterX, 2.25f, BackWallInnerZ - 0.13f), utilityMaterial.color);
            CreateZoneLabel("LIFE SUPPORT", new Vector3(LastShiftShipDimensions.LifeSupportCenterX, 2.25f, BackWallInnerZ - 0.13f), lifeSupportMaterial.color);
        }

        /// <summary>뒤쪽 긴 벽의 안쪽 면. 벽에 붙이는 것들이 전부 이 면을 기준으로 놓인다.</summary>
        private const float BackWallInnerZ = HalfWidth;

        /// <summary>끝벽의 안쪽 면(선미 쪽). 부호를 바꾸면 선수 쪽이다.</summary>
        private const float EndWallInnerX = HalfLength;

        /// <summary>
        /// 통로 하나. 방 둘 사이 6m 구간이고, 통로 폭(3.6) 밖의 z 를 벽으로 메워 방과 방이
        /// 직선으로 마주보지 않게 한다. 통로 A 는 우현(+z)에, B 는 좌현(-z)에 붙는다.
        ///
        /// 통로가 방 끝 개구부와 경계 개구부를 z 로 어긋나게 잇는 것이 A3(구역끼리 서로 안
        /// 보임)의 1차 방어다. 다만 그것만으로는 비스듬한 시선이 남으므로 배플을 함께 세운다.
        /// </summary>
        private static void CreatePassage(Transform ship, int passage)
        {
            var minX = LastShiftShipDimensions.PassageMinX(passage);
            var maxX = LastShiftShipDimensions.PassageMaxX(passage);
            var centerX = LastShiftShipDimensions.PassageCenterX(passage);
            var length = LastShiftShipDimensions.PassageLength;
            var side = passage <= 0 ? "A" : "B";

            // 통로 폭 밖을 메우는 벽. 통로가 한쪽 벽에 붙으므로 반대쪽 한 장이면 된다.
            // 폭은 실측으로 뽑는다 — 리터럴 2.4 를 적으면 통로 폭이 바뀔 때 벽이 안 따라온다.
            var fillMin = passage <= 0 ? -HalfWidth : LastShiftShipDimensions.PassageMaxZ(passage);
            var fillMax = passage <= 0 ? LastShiftShipDimensions.PassageMinZ(passage) : HalfWidth;
            CreateCube($"PassageWall_{side}", ship,
                new Vector3(centerX, CeilingInnerHeight * 0.5f, (fillMin + fillMax) * 0.5f),
                new Vector3(length, CeilingInnerHeight, fillMax - fillMin), hullMaterial);

            // 방 끝 개구부(문이 없는 쪽)의 벌크헤드. 통로 폭 안에서 개구부를 뺀 나머지를 메운다.
            // 이게 없으면 방이 통로 폭 전체로 열려 통로가 꺾이지 않는다.
            var near = LastShiftShipDimensions.BaffleNearOpening(passage);
            var wallX = passage <= 0 ? minX : maxX;
            CreatePassageEndWall(ship, $"PassageEnd_{side}", wallX,
                LastShiftShipDimensions.PassageMinZ(passage), LastShiftShipDimensions.OpeningMinZ(near));
            CreatePassageEndWall(ship, $"PassageEnd_{side}", wallX,
                LastShiftShipDimensions.OpeningMaxZ(near), LastShiftShipDimensions.PassageMaxZ(passage));
            CreateCube($"PassageEnd_{side}_Lintel", ship,
                new Vector3(wallX, (CeilingInnerHeight + LastShiftZoneDoor.OpeningHeight) * 0.5f,
                    LastShiftShipDimensions.OpeningCenterZ(near)),
                new Vector3(LastShiftZoneDoor.PanelThickness,
                    CeilingInnerHeight - LastShiftZoneDoor.OpeningHeight,
                    LastShiftZoneDoor.OpeningWidth), hullMaterial);

            CreateSightlineBaffle(ship, passage);
        }

        private static void CreatePassageEndWall(Transform ship, string name, float x, float minZ, float maxZ)
        {
            if (maxZ - minZ <= 0.0001f) return;
            CreateCube($"{name}_{(minZ < 0f ? "Fore" : "Aft")}", ship,
                new Vector3(x, CeilingInnerHeight * 0.5f, (minZ + maxZ) * 0.5f),
                new Vector3(LastShiftZoneDoor.PanelThickness, CeilingInnerHeight, maxZ - minZ), hullMaterial);
        }

        /// <summary>
        /// 시선 차단 배플. <b>이 볼륨은 장식이 아니라 A3 성립 조건이다. 옮기면 T4 가 FAIL 한다.</b>
        ///
        /// 통로를 가로질러 세우는 판이며, 근거는 <see cref="LastShiftShipDimensions.BaffleOffsetT"/>
        /// 주석에 있다 — 두 개구부를 모두 지나는 직선은 그 x 평면에서 반드시 이 1.6m 구간을
        /// 지나므로, 그 구간을 바닥부터 천장까지 막으면 관통 직선이 하나도 남지 않는다.
        /// 한쪽에 남는 1.6m 차선은 문 쪽 개구부와 z 가 같아 물건을 들고 직진해 지나간다.
        ///
        /// 외형(랙·캐비닛)은 아트 CT-11 소관이다. 여기서 정하는 것은 존재와 위치·치수뿐이다.
        /// </summary>
        private static void CreateSightlineBaffle(Transform ship, int passage)
        {
            var side = passage <= 0 ? "A" : "B";
            CreateCube($"SightlineBaffle_{side}", ship,
                new Vector3(LastShiftShipDimensions.BaffleCenterX(passage),
                    CeilingInnerHeight * 0.5f,
                    LastShiftShipDimensions.BaffleCenterZ(passage)),
                new Vector3(LastShiftShipDimensions.BaffleThickness,
                    CeilingInnerHeight,
                    LastShiftShipDimensions.BaffleWidth), hullMaterial);
        }

        /// <summary>
        /// 벌크헤드 한 장 + 그 가운데 문 하나(N0b). 예전에는 벌크헤드 폭이 3.2 라 좌우로
        /// 0.75 씩 뚫려 있었고, 그 틈으로 걸어서 구역을 넘나들 수 있었다. 그 상태에서는 문을
        /// 닫아도 승무원은 그냥 옆으로 지나가므로 격리(§2.2.2)가 "압력만 끊고 사람은 안 막는"
        /// 반쪽이 된다. 좌우 벽 바깥면까지 덮고, 통과는 문으로만 시킨다.
        ///
        /// 문 구멍 규격은 <see cref="LastShiftZoneDoor"/> 의 상수를 그대로 쓴다. 씬과 런타임이
        /// 각자 숫자를 들고 있으면 "그림상 열려 있는데 못 지나가는" 문이 생긴다.
        /// </summary>
        private static void CreateBulkheadWithDoor(string side, Transform ship, int boundary)
        {
            const float fullWidth = LastShiftShipDimensions.EndWallSpan;
            const float thickness = LastShiftZoneDoor.PanelThickness;
            const float opening = LastShiftZoneDoor.OpeningWidth;
            const float openingHeight = LastShiftZoneDoor.OpeningHeight;
            var x = LastShiftZoneAtlas.BoundaryX(boundary);
            var centerZ = LastShiftZoneDoor.CenterZOf(boundary);

            // 구멍 좌우를 메우는 벽 두 짝. 구멍이 통로를 따라 한쪽으로 치우쳤으므로 두 짝의
            // 폭이 서로 다르다. 예전 대칭식 (fullWidth - opening) / 2 를 그대로 두면 구멍이
            // 옮겨간 만큼 한쪽 벽이 짧아져 그 옆으로 걸어서 지나갈 틈이 생긴다 — N0b 가
            // 막으려던 바로 그 상태다. 각 짝은 자기 쪽 선체 끝에서 구멍 가장자리까지를 메운다.
            var wallMin = -fullWidth * 0.5f;
            var wallMax = fullWidth * 0.5f;
            var openingMin = centerZ - opening * 0.5f;
            var openingMax = centerZ + opening * 0.5f;
            CreateCube($"Bulkhead_{side}_Fore", ship,
                new Vector3(x, CeilingInnerHeight * 0.5f, (wallMin + openingMin) * 0.5f),
                new Vector3(thickness, CeilingInnerHeight, openingMin - wallMin), hullMaterial);
            CreateCube($"Bulkhead_{side}_Aft", ship,
                new Vector3(x, CeilingInnerHeight * 0.5f, (openingMax + wallMax) * 0.5f),
                new Vector3(thickness, CeilingInnerHeight, wallMax - openingMax), hullMaterial);

            // 문 위 인방. 구멍 높이(2.2)에서 천장 내면(3.2)까지를 메운다. 이게 없으면 문을 닫아도
            // 머리 위 1m 가 그대로 뚫려 있어 압력 차단이 그림과 어긋난다.
            CreateCube($"Bulkhead_{side}_Lintel", ship,
                new Vector3(x, (CeilingInnerHeight + openingHeight) * 0.5f, centerZ),
                new Vector3(thickness, CeilingInnerHeight - openingHeight, opening), hullMaterial);

            CreateZoneDoor($"ZoneDoor_{side}", ship, boundary, x, centerZ);
        }

        /// <summary>
        /// 미닫이 문 하나. 판 두 짝이 가운데에서 만나 닫히고, 열리면 각각 옆벽 뒤로 물러난다.
        /// 판에는 콜라이더를 두지 않고 별도 차단 콜라이더 하나로 통행을 막는다 — 움직이는
        /// 콜라이더로 막으면 CharacterController 가 판에 끼거나 밀려나서, 확인하려는 것
        /// ("닫힌 문은 못 지나간다")이 아니라 밀림 현상이 먼저 보인다.
        /// </summary>
        private static void CreateZoneDoor(string name, Transform ship, int boundary, float x, float centerZ)
        {
            const float thickness = LastShiftZoneDoor.PanelThickness;
            const float opening = LastShiftZoneDoor.OpeningWidth;
            const float openingHeight = LastShiftZoneDoor.OpeningHeight;
            var doorMaterial = CreateMaterial($"LS_Door_{boundary}", new Color(0.46f, 0.44f, 0.30f));

            var door = new GameObject(name);
            door.transform.SetParent(ship, false);
            // 문 오브젝트 자체를 개구부 중심에 놓는다. 판·문틀·차단 콜라이더는 이 아래에서
            // 로컬 대칭으로 두면 되고, LastShiftZoneDoor 가 매 프레임 다시 쓰는 판 위치도
            // 로컬이라 그대로 따라온다. 자식마다 중심 z 를 더하는 방식으로 짜면 여섯 자리
            // 중 하나만 빠져도 그 조각이 구멍에서 어긋난 채 조용히 통과한다.
            door.transform.localPosition = new Vector3(x, 0f, centerZ);

            // 판은 구멍 절반씩 덮는다. 위치는 LastShiftZoneDoor 가 매 프레임 다시 쓰므로
            // 여기서는 크기와 재질만 정해 두면 된다.
            var panelScale = new Vector3(thickness * 1.1f, openingHeight, opening * 0.5f);
            var fore = CreateCube($"{name}_PanelFore", door.transform, Vector3.zero, panelScale, doorMaterial);
            var aft = CreateCube($"{name}_PanelAft", door.transform, Vector3.zero, panelScale, doorMaterial);
            Object.DestroyImmediate(fore.GetComponent<Collider>());
            Object.DestroyImmediate(aft.GetComponent<Collider>());

            // 문틀. 판이 물러난 자리를 감싸 "여기가 문" 이라는 것이 닫혀 있지 않을 때도 읽히게 한다.
            panelMaterial ??= CreateMaterial("LS_Panel", new Color(0.14f, 0.16f, 0.19f));
            foreach (var sign in new[] { -1f, 1f })
            {
                var jamb = CreateCube($"{name}_Jamb_{(sign < 0f ? "Fore" : "Aft")}", door.transform,
                    new Vector3(0f, openingHeight * 0.5f, sign * (opening * 0.5f + 0.06f)),
                    new Vector3(thickness * 1.4f, openingHeight, 0.12f), panelMaterial);
                Object.DestroyImmediate(jamb.GetComponent<Collider>());
            }

            var blockerObject = new GameObject($"{name}_Blocker");
            blockerObject.transform.SetParent(door.transform, false);
            blockerObject.transform.localPosition = new Vector3(0f, openingHeight * 0.5f, 0f);
            var blocker = blockerObject.AddComponent<BoxCollider>();
            blocker.size = new Vector3(thickness, openingHeight, opening);
            blocker.enabled = false;

            door.AddComponent<LastShiftZoneDoor>().Configure(boundary, fore.transform, aft.transform, blocker);
        }

        /// <summary>
        /// 천장을 닫는다. 닫아야 하는 이유는 두 가지다. 하나는 "우주선 안"이 읽히려면 위가
        /// 막혀 있어야 한다는 것이고, 다른 하나는 저중력에서 뜬 물건이 위로 빠져나가
        /// ItemSafetyBounds 의 above-world 복구를 계속 밟는 것을 막는 것이다.
        /// </summary>
        private static void CreateShipCeiling(Transform ship)
        {
            ceilingMaterial ??= CreateMaterial("LS_Ceiling", new Color(0.21f, 0.23f, 0.26f));
            CreateCube("Ceiling", ship, new Vector3(0f, CeilingInnerHeight + CeilingThickness * 0.5f, 0f), new Vector3(LastShiftShipDimensions.SideWallSpan, CeilingThickness, LastShiftShipDimensions.EndWallSpan), ceilingMaterial);
            // 천장 리브. 평평한 판만 있으면 실내가 아니라 뚜껑처럼 보인다.
            // 개수를 고정하지 않고 간격을 고정한다 — 전장이 바뀌었을 때 개수를 고정해 두면
            // 리브 간격이 늘어나 같은 배가 아니라 더 큰 배의 사진처럼 보인다.
            const float ribSpacing = 1.8f;
            var ribCount = Mathf.FloorToInt((Length - ribSpacing) / ribSpacing);
            var ribStart = -(ribCount - 1) * ribSpacing * 0.5f;
            for (var index = 0; index < ribCount; index++)
            {
                var x = ribStart + index * ribSpacing;
                CreateDecorCube($"CeilingRib_{index}", ship, new Vector3(x, CeilingInnerHeight - 0.06f, 0f), new Vector3(0.18f, 0.12f, Width), hullMaterial);
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
            CreateCube("OuterHull_FrontUpper", ship, new Vector3(0f, (CeilingInnerHeight + windowTop) * 0.5f, HullFrontZ), new Vector3(LastShiftShipDimensions.SideWallSpan, CeilingInnerHeight - windowTop, LastShiftShipDimensions.HullThickness), hullMaterial);
            // 창 사이 기둥. 간격을 고정해 두어야 전장이 바뀌어도 창 한 짝의 크기가 유지된다.
            // 기둥이 세 개로 고정돼 있으면 36m 에서는 창 하나가 12m 짜리 통유리가 된다.
            const float mullionSpacing = 3.2f;
            var mullionCount = Mathf.FloorToInt((Length - mullionSpacing) / mullionSpacing);
            var mullionStart = -(mullionCount - 1) * mullionSpacing * 0.5f;
            for (var index = 0; index < mullionCount; index++)
            {
                var x = mullionStart + index * mullionSpacing;
                CreateCube($"WindowMullion_{index}", ship, new Vector3(x, (WindowSillHeight + windowTop) * 0.5f, HullFrontZ), new Vector3(0.35f, windowTop - WindowSillHeight, 0.22f), panelMaterial);
            }

            // 창 밖 우주. 창보다 크게 두어 창틀 사이로 선체 밖 회색이 보이지 않게 한다.
            var voidWidth = Length + 12f;
            CreateDecorCube("SpaceVoid", ship, new Vector3(0f, 1.6f, HullFrontZ - 6f), new Vector3(voidWidth, 18f, 0.2f), voidMaterial);
            var starRandom = new System.Random(20260804);
            var stars = new GameObject("StarField");
            stars.transform.SetParent(ship, false);
            // 별 개수는 창 면적을 따라간다. 90개로 고정하면 36m 창에서 밀도가 1/3 로 떨어져
            // 밖이 우주가 아니라 검은 벽으로 읽힌다.
            var starCount = Mathf.RoundToInt(90f * Length / 12.5f);
            var starSpreadX = voidWidth * 0.44f;
            for (var index = 0; index < starCount; index++)
            {
                var x = (float)(starRandom.NextDouble() * (starSpreadX * 2.0) - starSpreadX);
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
            const float panelZ = BackWallInnerZ - 0.06f;
            const float endPanelX = EndWallInnerX - 0.06f;
            // 구역마다 뒷벽 패널 한 짝. 구역 중심에 두므로 전장이 바뀌면 따라 벌어진다.
            CreateWallPanel("Panel_Cockpit", ship, new Vector3(LastShiftShipDimensions.CockpitCenterX, 1.55f, panelZ), new Vector3(3.2f, 1.1f, 0.12f), cockpitMaterial.color);
            CreateWallPanel("Panel_Utility", ship, new Vector3(LastShiftShipDimensions.UtilityCenterX, 1.55f, panelZ), new Vector3(3.2f, 1.1f, 0.12f), utilityMaterial.color);
            CreateWallPanel("Panel_LifeSupport", ship, new Vector3(LastShiftShipDimensions.LifeSupportCenterX, 1.55f, panelZ), new Vector3(3.2f, 1.1f, 0.12f), lifeSupportMaterial.color);
            // 양 끝벽 패널. 배가 길어지면 이 둘 사이가 36m 가 되므로 각 구역 안에서만 보인다.
            CreateWallPanel("Panel_PortWall", ship, new Vector3(-endPanelX, 1.7f, -0.9f), new Vector3(0.12f, 1.0f, 2.2f), cockpitMaterial.color);
            CreateWallPanel("Panel_StarboardWall", ship, new Vector3(endPanelX, 1.7f, -0.9f), new Vector3(0.12f, 1.0f, 2.2f), lifeSupportMaterial.color);
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
            // 전장을 따라 길게 지나는 주 배관 두 줄. 캡슐 y 스케일이 반길이이므로 배 안쪽
            // 길이의 절반을 쓴다. 이 값이 고정이면 36m 배에서 배관이 조종석 근처에서 끊긴다.
            var mainHalfLength = HalfLength - 0.3f;
            CreatePipe("Duct_Main_Fore", ship, new Vector3(0f, CeilingInnerHeight - 0.42f, -HalfWidth * 0.62f), new Vector3(0f, 0f, 90f), 0.16f, mainHalfLength);
            CreatePipe("Duct_Main_Aft", ship, new Vector3(0f, CeilingInnerHeight - 0.42f, HalfWidth * 0.65f), new Vector3(0f, 0f, 90f), 0.13f, mainHalfLength);
            // 벽으로 내려가는 수직 지관. 뒷벽 패널(폭 3.2, 구역 중심) 사이 빈 구간에 둔다.
            // 패널 위에 겹치면 발광 계기 띠를 가려 정면에서 관이 계기판을 관통한 것처럼 보인다.
            var riserZ = BackWallInnerZ - 0.22f;
            foreach (LastShiftZone zone in System.Enum.GetValues(typeof(LastShiftZone)))
            {
                var center = LastShiftShipDimensions.ZoneCenterX(zone);
                foreach (var sign in new[] { -1f, 1f })
                    CreatePipe($"Duct_Riser_{zone}_{(sign < 0f ? "Fore" : "Aft")}", ship, new Vector3(center + sign * 2.05f, 1.5f, riserZ), Vector3.zero, 0.11f, 1.5f);
            }
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

        /// <summary>
        /// 구역 하나. 바닥은 그 구역이 실제로 차지하는 x 범위를 정확히 덮는다 — 예전처럼 폭을
        /// 4 로 고정해 두면 구역 판정(경계 ±7)과 바닥이 어긋나 구역 사이에 바닥 없는 틈이 생기고
        /// 승무원이 그리로 떨어진다.
        /// </summary>
        private static void CreateZone(string name, Transform parent, LastShiftZone zoneId, Material material)
        {
            var zone = new GameObject(name);
            zone.transform.SetParent(parent, false);
            zone.transform.position = new Vector3(LastShiftShipDimensions.ZoneCenterX(zoneId), 0f, 0f);
            var zoneLength = LastShiftShipDimensions.ZoneLength(zoneId);
            CreateCube("Floor", zone.transform, new Vector3(0f, -LastShiftShipDimensions.HullThickness * 0.5f, 0f),
                new Vector3(zoneLength, LastShiftShipDimensions.HullThickness, LastShiftShipDimensions.EndWallSpan),
                floorMaterial ??= CreateMaterial("LS_Floor", new Color(0.30f, 0.32f, 0.35f)));
            // 구역 색 띠. 뒷벽 앞 바닥에 깔아 어느 구역에 서 있는지가 발밑에서 읽힌다.
            var strip = CreateCube("ZoneStrip", zone.transform, new Vector3(0f, 0.015f, BackWallInnerZ - 0.8f),
                new Vector3(zoneLength - 0.3f, 0.03f, 0.25f), material);
            Object.DestroyImmediate(strip.GetComponent<Collider>());
        }

        private static LastShiftPlayerController CreatePlayer()
        {
            var player = new GameObject("PlayerOne");
            player.transform.position = LastShiftSandboxController.PlayerSpawn;
            var controller = player.AddComponent<CharacterController>();
            controller.radius = LastShiftShipPhysics.CrewRadius;
            controller.height = 1.7f;
            controller.center = new Vector3(0f, 0.85f, 0f);
            var cameraObject = new GameObject("PlayerOne Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, LastShiftShipPhysics.EyeHeight, 0f);
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
        public static Vector3 TetherRackPosition => LastShiftShipDimensions.TetherRackPosition;

        public static Vector3 TetherRackScale => LastShiftShipDimensions.TetherRackScale;

        public static Vector3 TetherSpawnPosition => LastShiftShipDimensions.TetherNominal;

        private static LastShiftGrabbable[] CreateItems()
        {
            return new[]
            {
                CreateItem("Battery", LastShiftItemRole.Battery, LastShiftShipDimensions.BatteryNominal, new Vector3(0.65f, 0.65f, 0.9f), new Color(0.95f, 0.65f, 0.12f), true),
                CreateItem("CoolingCanister", LastShiftItemRole.CoolingCanister, LastShiftShipDimensions.CoolingNominal, new Vector3(0.55f, 1.1f, 0.55f), new Color(0.15f, 0.72f, 0.95f), true),
                CreateItem("PatchPlate", LastShiftItemRole.PatchPlate, LastShiftShipDimensions.PatchPlateNominal, new Vector3(1.15f, 1.15f, 0.18f), new Color(0.78f, 0.82f, 0.88f), true),
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

            CreateZoneLights("Cockpit", LastShiftZone.Cockpit, new Color(0.62f, 0.78f, 1f), 2.5f);
            CreateZoneLights("Utility", LastShiftZone.Utility, new Color(1f, 0.86f, 0.62f), 2.3f);
            CreateZoneLights("LifeSupport", LastShiftZone.LifeSupport, new Color(0.66f, 1f, 0.80f), 2.3f);
        }

        /// <summary>
        /// 구역 조명. 구역 하나에 등 하나로 두면 11~14m 구역에서 가운데만 밝고 양 끝이 캄캄해진다
        /// (점광원 range 7 은 반경이다). 등 사이 간격을 고정하고 개수를 구역 길이에서 뽑는다.
        /// </summary>
        private static void CreateZoneLights(string name, LastShiftZone zone, Color color, float intensity)
        {
            const float lightSpacing = 5.5f;
            var length = LastShiftShipDimensions.ZoneLength(zone);
            var center = LastShiftShipDimensions.ZoneCenterX(zone);
            var count = Mathf.Max(1, Mathf.RoundToInt(length / lightSpacing));
            var start = center - (count - 1) * lightSpacing * 0.5f;
            for (var index = 0; index < count; index++)
                CreateZoneLight($"Light_{name}_{index}", new Vector3(start + index * lightSpacing, CeilingInnerHeight - 0.35f, 0f), color, intensity);
        }

        private static void CreateZoneLight(string name, Vector3 position, Color color, float intensity)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.position = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            // 등 간격(5.5)보다 넉넉해 사이가 어둡지 않고, 옆 구역까지 흘러 구분이 사라지지는 않는 반경.
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
