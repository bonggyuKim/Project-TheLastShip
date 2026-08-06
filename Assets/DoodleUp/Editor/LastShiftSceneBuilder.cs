using System.IO;
using System.Linq;
using DoodleUp.Runtime;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoodleUp.Editor
{
    public static class LastShiftSceneBuilder
    {
        /// <summary>이 프로젝트의 유일한 레벨. 씬을 짓는 것은 <see cref="LastShiftNetworkSceneBuilder"/> 다.</summary>
        public const string ScenePath = LastShiftNetworkSceneBuilder.ScenePath;
        // 구역 이름 정본은 Runtime 의 LastShiftSceneZones 다. 런타임 연출(손상 구역 표시)이
        // 같은 문자열로 구역을 찾아야 하므로 여기서는 그것을 재노출만 한다.
        public const string CockpitZoneName = LastShiftSceneZones.CockpitZoneName;
        public const string PowerZoneName = LastShiftSceneZones.PowerZoneName;
        public const string CoolingZoneName = LastShiftSceneZones.CoolingZoneName;
        public const string LifeSupportZoneName = LastShiftSceneZones.LifeSupportZoneName;

        private static Material hullMaterial;
        private static Material floorMaterial;
        private static Material cockpitMaterial;
        private static Material powerMaterial;
        private static Material coolingMaterial;
        private static Material lifeSupportMaterial;
        private static Material ceilingMaterial;
        private static Material ductMaterial;
        private static Material panelMaterial;
        private static Material starMaterial;
        private static Material voidMaterial;
        private static Material compartmentMaterial;

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

        /// <summary>
        /// 선체 프리팹. <b>SP01 과 SP02A 가 같은 배를 두 벌 들고 있던 것이 이 프리팹이 생긴 이유다.</b>
        ///
        /// 예전에는 선체가 씬 안에 직접 구워져 있었고, SP02A 는 SP01 을 열어 변형해 저장하는
        /// 파생물이었다(<see cref="LastShiftNetworkSceneBuilder"/>). 그래서 SP01 만 다시 굽고
        /// SP02A 를 안 구우면 4인 씬이 조용히 옛 선체로 남았다 — 그레이박스 구획 11개가 SP01 에만
        /// 들어가고 SP02A 에는 0개였던 것이 실제로 그렇게 났다. 두 씬 어느 쪽 테스트도 안 걸렸다.
        ///
        /// 프리팹 인스턴스는 씬에 참조와 override 만 저장하므로, 프리팹만 다시 구우면 그것을 쓰는
        /// 씬은 손대지 않아도 최신 선체를 쓴다. 한쪽만 굽는 상태 자체가 만들어지지 않는다.
        ///
        /// 절차적 생성을 프리팹으로 바꾼 것이 아니라 <b>절차적 생성의 출력 대상만 바꿨다.</b>
        /// 좌표는 여전히 <see cref="LastShiftShipDimensions"/> 와 <see cref="LastShiftCompartments"/>
        /// 에서 파생한다 — 손으로 authoring 한 프리팹으로 바꾸면 전장 36m→38m 개정이 프리팹 수동
        /// 편집이 되어, 치수 정본을 한 곳에 모아 둔 이유가 사라진다. 같은 패턴이 이미
        /// <see cref="LastShiftNetworkSceneBuilder"/> 의 <c>CreatePlayerPrefab</c> 에 있다.
        ///
        /// 조명은 안 들어간다. 등과 <c>RenderSettings</c> 는 씬 소관으로 남긴다.
        /// </summary>
        public const string ShipPrefabPath = "Assets/DoodleUp/Prefabs/LastShiftShipGraybox.prefab";

        /// <summary>
        /// 선체 프리팹을 다시 굽는다. <b>지우고 만들지 않는다</b> — 지우면 GUID 가 새로 찍혀
        /// 이 프리팹을 참조하던 씬이 전부 끊긴 참조가 된다. 덮어쓰면 대응되는 오브젝트의
        /// fileID 까지 유지되어 재빌드 diff 가 조용하다(<c>CreatePlayerPrefab</c> 주석과 같은 근거).
        ///
        /// <c>NetworkObject</c> 가 없으므로 그쪽 프리팹이 겪은 <c>GlobalObjectIdHash 0</c> 함정은
        /// 여기 해당하지 않는다. 나중에 선체에 네트워크 오브젝트가 붙으면 그 검사도 같이 와야 한다.
        /// </summary>
        public static GameObject RebuildShipPrefab()
        {
            Directory.CreateDirectory("Assets/DoodleUp/Prefabs");
            var ship = BuildShipGrayboxHierarchy();
            PrefabUtility.SaveAsPrefabAsset(ship, ShipPrefabPath);
            Object.DestroyImmediate(ship);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ShipPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath);
            if (prefab == null)
                throw new System.InvalidOperationException($"{ShipPrefabPath} failed to save or import.");
            Debug.Log($"[LAST_SHIFT_SHIP_PREFAB] path={ShipPrefabPath} compartments={LastShiftCompartments.Count} result=PASS");
            return prefab;
        }

        private static GameObject BuildShipGrayboxHierarchy()
        {
            var ship = new GameObject("ShipGraybox");
            CreateZone(CockpitZoneName, ship.transform, LastShiftZone.Cockpit, cockpitMaterial ??= CreateMaterial("LS_Cockpit", new Color(0.24f, 0.38f, 0.50f)));
            CreateZone(PowerZoneName, ship.transform, LastShiftZone.Power, powerMaterial ??= CreateMaterial("LS_Power", new Color(0.42f, 0.38f, 0.28f)));
            CreateZone(CoolingZoneName, ship.transform, LastShiftZone.Cooling, coolingMaterial ??= CreateMaterial("LS_Cooling", new Color(0.26f, 0.42f, 0.50f)));
            CreateZone(LifeSupportZoneName, ship.transform, LastShiftZone.LifeSupport, lifeSupportMaterial ??= CreateMaterial("LS_LifeSupport", new Color(0.26f, 0.48f, 0.36f)));
            // 벽 높이는 천장 내면(CeilingInnerHeight)까지 올린다. 예전 3.0 을 유지하면
            // 벽과 천장 사이에 0.2m 띠 구멍이 남아 저중력에서 뜬 물건이 그 틈으로 빠진다.
            //
            // Left/Right 는 전장 축(x)의 두 끝벽이고 Back/Front 는 전폭 축(z)의 긴 벽이다.
            // 이름은 예전 배치에서 굳은 것이라 그대로 두되, 좌표는 전부 치수 정본에서 파생한다.
            CreateCube("OuterHull_Left", ship.transform, new Vector3(-EndWallX, CeilingInnerHeight * 0.5f, 0f), new Vector3(LastShiftShipDimensions.HullThickness, CeilingInnerHeight, LastShiftShipDimensions.EndWallSpan), hullMaterial ??= CreateMaterial("LS_Hull", new Color(0.18f, 0.20f, 0.23f)));
            CreateAftEndWall(ship.transform);
            CreateCube("OuterHull_Back", ship.transform, new Vector3(0f, CeilingInnerHeight * 0.5f, HullBackZ), new Vector3(LastShiftShipDimensions.SideWallSpan, CeilingInnerHeight, LastShiftShipDimensions.HullThickness), hullMaterial);
            CreateCube("OuterHull_FrontLower", ship.transform, new Vector3(0f, WindowSillHeight * 0.5f, HullFrontZ), new Vector3(LastShiftShipDimensions.SideWallSpan, WindowSillHeight, LastShiftShipDimensions.HullThickness), hullMaterial);
            CreatePassage(ship.transform, 0);
            CreatePassage(ship.transform, 1);
            // 경계마다 벌크헤드 한 장. 3 -> 4 구역이 되며 셋이 됐다(§3).
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                CreateBulkheadWithDoor($"B{boundary}", ship.transform, boundary);
            CreateShipCeiling(ship.transform);
            CreateForwardWindows(ship.transform);
            CreateInstrumentPanels(ship.transform);
            CreateDucts(ship.transform);
            CreateCompartments(ship.transform);
            CreateBypassDuct(ship.transform);
            CreateCube("CockpitConsole", ship.transform, new Vector3(LastShiftShipDimensions.CockpitCenterX - 1.3f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 2.5f), cockpitMaterial);
            CreateCube("TetherRack", ship.transform, TetherRackPosition, TetherRackScale, cockpitMaterial);
            CreateCube("BusCabinet", ship.transform, new Vector3(LastShiftShipDimensions.PowerCenterX, 0.65f, BackWallInnerZ - 0.55f), new Vector3(1.6f, 1.3f, 0.5f), powerMaterial);
            CreateCube("LifeSupportRack", ship.transform, new Vector3(LastShiftShipDimensions.LifeSupportCenterX + 1.1f, 0.75f, BackWallInnerZ - 0.75f), new Vector3(0.8f, 1.5f, 0.8f), lifeSupportMaterial);
            CreateZoneLabel(ship.transform, "COCKPIT", new Vector3(LastShiftShipDimensions.CockpitCenterX, 2.25f, BackWallInnerZ - 0.13f), cockpitMaterial.color);
            CreateZoneLabel(ship.transform, "POWER / BUS", new Vector3(LastShiftShipDimensions.PowerCenterX, 2.25f, BackWallInnerZ - 0.13f), powerMaterial.color);
            CreateZoneLabel(ship.transform, "COOLING", new Vector3(LastShiftShipDimensions.CoolingCenterX, 2.25f, BackWallInnerZ - 0.13f), coolingMaterial.color);
            CreateZoneLabel(ship.transform, "LIFE SUPPORT", new Vector3(LastShiftShipDimensions.LifeSupportCenterX, 2.25f, BackWallInnerZ - 0.13f), lifeSupportMaterial.color);
            return ship;
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
            CreateWallPanel("Panel_Power", ship, new Vector3(LastShiftShipDimensions.PowerCenterX, 1.55f, panelZ), new Vector3(3.2f, 1.1f, 0.12f), powerMaterial.color);
            CreateWallPanel("Panel_Cooling", ship, new Vector3(LastShiftShipDimensions.CoolingCenterX, 1.55f, panelZ), new Vector3(3.2f, 1.1f, 0.12f), coolingMaterial.color);
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
        // ── 그레이박스 구획 (docs/corridor-4p-redesign-v1.md §17.4) ─────────────
        // 좌표 정본은 Runtime 의 LastShiftCompartments 다. 여기서 하는 것은 그 표를 판으로
        // 세우는 일뿐이고, 숫자는 하나도 다시 적지 않는다.
        //
        // 면 소유 규칙이 이 블록 전체를 지탱한다: <b>구획은 자기 안쪽 문이 놓인 면을 안 세운다.</b>
        // 그 면은 부모 구획(또는 선체)이 세우고 구멍도 거기서 뚫는다. 양쪽이 다 세우면 같은
        // 평면에 판이 두 장 겹쳐 z-fighting 이 나고, 양쪽이 다 안 세우면 구획이 안 닫힌다.
        // 소유자를 "문을 가진 쪽" 이 아니라 "문이 향하는 쪽" 으로 정한 이유는 선체다 —
        // 선체 판은 이미 서 있으므로 구획이 그 자리에 또 세울 수 없다.

        /// <summary>
        /// 선미 끝벽. 통짜 한 장이 아닌 이유는 생활공간(§9)이 여기에 문 하나로 붙기 때문이다.
        /// 선수 끝벽은 화물칸이 붙지만 그쪽은 잠긴 구획(§15.2)이라 구멍을 안 뚫는다 —
        /// 잠김은 그레이박스에서 "판으로 메운 자리" 이고, 해치 표식만 붙여 존재를 알린다.
        /// </summary>
        private static void CreateAftEndWall(Transform ship)
        {
            var doorways = LastShiftCompartments.Specs
                .Where(spec => LastShiftCompartments.ConnectsToHull(spec) && spec.IsPassable &&
                               spec.DoorPlane == LastShiftDoorPlane.AlongX &&
                               Mathf.Abs(spec.DoorPlaneCoordinate - HalfLength) < 0.001f)
                .Select(spec => spec.DoorCenter)
                .ToArray();

            const float span = LastShiftShipDimensions.EndWallSpan;
            CreateWallWithOpenings("OuterHull_Right", ship, true, EndWallX,
                -span * 0.5f, span * 0.5f, CeilingInnerHeight,
                LastShiftShipDimensions.HullThickness, hullMaterial, doorways);
        }

        /// <summary>
        /// 구획 열한 개(§17.4). 에어록은 없다 — 우회 통로 z 경로가 미결이라 좌표가 안 나온다(§17.5).
        ///
        /// 이 지오메트리는 압력존에 안 들어간다(§17.6). <see cref="LastShiftZoneDoor"/> 를 쓰지
        /// 않는 것이 그 경계를 코드에서 지키는 자리다 — 그 컴포넌트를 여기에 달면 씬 검증기의
        /// "문 개수 = 구역 경계 수" 가 깨지고, 깨진 것을 고치려다 구획이 압력 위상에 조용히
        /// 편입된다. 구획 문은 지금 단계에서 <b>판이거나 구멍이거나</b> 둘 중 하나다.
        /// </summary>
        private static void CreateCompartments(Transform ship)
        {
            compartmentMaterial ??= CreateMaterial("LS_Compartment", new Color(0.31f, 0.29f, 0.33f));

            var root = new GameObject("Compartments");
            root.transform.SetParent(ship, false);

            foreach (var spec in LastShiftCompartments.Specs)
                CreateCompartment(root.transform, spec);
        }

        /// <summary>
        /// 갑판 하부 우회 통로와 에어록(§5, §23). 좌표 정본은 Runtime 의
        /// <see cref="LastShiftBypassDuct"/> 이고 여기서는 판으로 세우기만 한다.
        ///
        /// <b>여기서 통행이 열린다.</b> 3단계까지는 솔리드 큐브 겉면만 세우고 콜라이더를 뺐다 —
        /// 승강구 해치가 없어 승무원이 못 들어오는데 콜라이더를 남기면 갑판 아래 보이지 않는
        /// 벽이 되기 때문이었다. 해치가 생긴 지금은 반대로 <b>속이 빈 관</b>이어야 한다:
        /// 솔리드 큐브에 콜라이더를 붙이면 덕트가 통째로 막힌 블록이 되고, 안 붙이면 해치를
        /// 열자마자 허공으로 떨어진다.
        ///
        /// 그래서 판 넷(바닥·천장·옆벽 둘)으로 세운다. 판 두께는 슬래브 아래 여유
        /// (<see cref="LastShiftBypassDuct.PanelThickness"/>)라 내부 <c>0.9m</c> 가 그대로 남는다.
        /// </summary>
        private static void CreateBypassDuct(Transform ship)
        {
            ductMaterial ??= CreateMaterial("LS_Duct", new Color(0.34f, 0.33f, 0.30f));
            var root = new GameObject("BypassDuct");
            root.transform.SetParent(ship, false);

            const float section = LastShiftBypassDuct.Section;
            const float half = section * 0.5f;
            const float thickness = LastShiftBypassDuct.PanelThickness;
            var floor = LastShiftBypassDuct.FloorY;
            var ceiling = LastShiftBypassDuct.CeilingY;
            var runZ = LastShiftBypassDuct.RunZ;
            var foreX = LastShiftBypassDuct.ForeShaftX;
            var aftX = LastShiftBypassDuct.AftShaftX;
            var foreZ = LastShiftBypassDuct.ForeShaftZ;

            // ── 선미로 달리는 긴 구간(x 축). 꺾임 모서리는 이쪽이 갖는다(§23.4) ──────────
            var runMinX = foreX - half;
            var runMaxX = aftX + half;
            CreateDuctPlate(root.transform, "Run_Floor", runMinX, runMaxX, runZ - half, runZ + half, floor - thickness, floor);
            // 천장은 선미 승강구 자리를 비운다 — 안 비우면 올라갈 구멍이 천장 판에 막힌다.
            CreateDuctPlate(root.transform, "Run_Ceiling", runMinX, aftX - half, runZ - half, runZ + half, ceiling, ceiling + thickness);
            CreateDuctPlate(root.transform, "Run_WallStarboard", runMinX, runMaxX, runZ + half, runZ + half + thickness, floor, ceiling);
            // 안쪽 옆벽은 선수 다리가 붙는 구간을 비운다. 안 비우면 L 자 모서리가 벽으로 막혀
            // 꺾임이 형상으로만 남고 실제로는 두 개의 막다른 관이 된다.
            CreateDuctPlate(root.transform, "Run_WallPort", foreX + half, runMaxX, runZ - half - thickness, runZ - half, floor, ceiling);
            CreateDuctPlate(root.transform, "Run_EndFore", runMinX - thickness, runMinX, runZ - half, runZ + half, floor, ceiling);
            CreateDuctPlate(root.transform, "Run_EndAft", runMaxX, runMaxX + thickness, runZ - half, runZ + half, floor, ceiling);

            // ── 선수 쪽으로 꺾이는 짧은 다리(z 축) ────────────────────────────────────
            var legMinZ = foreZ - half;
            var legMaxZ = runZ - half;
            CreateDuctPlate(root.transform, "Leg_Floor", foreX - half, foreX + half, legMinZ, legMaxZ, floor - thickness, floor);
            // 천장은 선수 승강구 자리를 비운다.
            CreateDuctPlate(root.transform, "Leg_Ceiling", foreX - half, foreX + half, foreZ + half, legMaxZ, ceiling, ceiling + thickness);
            CreateDuctPlate(root.transform, "Leg_WallFore", foreX - half - thickness, foreX - half, legMinZ, legMaxZ, floor, ceiling);
            CreateDuctPlate(root.transform, "Leg_WallAft", foreX + half, foreX + half + thickness, legMinZ, legMaxZ, floor, ceiling);
            CreateDuctPlate(root.transform, "Leg_End", foreX - half, foreX + half, legMinZ - thickness, legMinZ, floor, ceiling);

            // ── 양 끝 수직 승강구 ─────────────────────────────────────────────────────
            for (var shaft = 0; shaft < LastShiftBypassDuct.ShaftCount; shaft++)
                CreateShaft(root.transform, shaft);

            CreateAirlock(root.transform);
        }

        /// <summary>
        /// 덕트 판 한 장. 세 축 전부 구간으로 받는다 — 관 하나가 판 여섯 장이고 그중 셋이 승강구·
        /// 모서리 때문에 잘려 있어서, 중심·크기로 적으면 어느 판이 어디서 끊기는지가 안 읽힌다.
        /// </summary>
        private static void CreateDuctPlate(Transform parent, string name,
            float minX, float maxX, float minZ, float maxZ, float minY, float maxY)
        {
            CreateCube(name, parent,
                new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, maxY - minY, maxZ - minZ), ductMaterial);
        }

        /// <summary>
        /// 수직 승강구 하나. 덕트 천장(<c>-0.3</c>)에서 갑판 슬래브 밑면(<c>-0.2</c>)까지의 짧은
        /// 목만 세운다 — 그 아래는 덕트 본체이고 그 위는 슬래브에 뚫은 구멍이라 이미 벽이 있다.
        /// 목을 안 두면 <c>0.1m</c> 띠가 옆으로 열려 물건이 슬래브 밑으로 끼어든다.
        ///
        /// 바닥의 단은 §23.6 의 권고다. <c>CharacterController.stepOffset</c> 기본값과 같아 걸어서
        /// 오르내리고, 상승이 <c>1.2 → 0.9m</c> 로 줄어 점프 여유가 두 배가 된다.
        /// </summary>
        private static void CreateShaft(Transform parent, int shaft)
        {
            const float half = LastShiftBypassDuct.Section * 0.5f;
            const float thickness = LastShiftBypassDuct.PanelThickness;
            var mouth = LastShiftBypassDuct.ShaftMouth(shaft);
            var name = shaft == LastShiftBypassDuct.ForeShaft ? "Fore" : "Aft";
            var neckMin = LastShiftBypassDuct.CeilingY;
            var neckMax = -LastShiftShipDimensions.HullThickness;

            CreateDuctPlate(parent, $"ShaftNeck_{name}_Fore",
                mouth.x - half - thickness, mouth.x - half, mouth.z - half, mouth.z + half, neckMin, neckMax);
            CreateDuctPlate(parent, $"ShaftNeck_{name}_Aft",
                mouth.x + half, mouth.x + half + thickness, mouth.z - half, mouth.z + half, neckMin, neckMax);
            CreateDuctPlate(parent, $"ShaftNeck_{name}_Port",
                mouth.x - half - thickness, mouth.x + half + thickness, mouth.z - half - thickness, mouth.z - half, neckMin, neckMax);
            CreateDuctPlate(parent, $"ShaftNeck_{name}_Starboard",
                mouth.x - half - thickness, mouth.x + half + thickness, mouth.z + half, mouth.z + half + thickness, neckMin, neckMax);

            CreateCube($"Step_{name}", parent,
                new Vector3(mouth.x, LastShiftBypassDuct.FloorY + LastShiftBypassDuct.StepHeight * 0.5f, mouth.z),
                new Vector3(LastShiftBypassDuct.Section, LastShiftBypassDuct.StepHeight, half), ductMaterial);

            CreateDeckHatch(parent, shaft);
        }

        /// <summary>
        /// 에어록(§17.4 <c>3x3x3</c>, §23.5). 덕트 바닥에 천장을 붙여 <b>안쪽 해치</b>가 되고,
        /// 그 <c>3m</c> 아래 바닥이 배 밑면의 <b>바깥 해치</b>다. 같은 층에 뒀다면 이중 해치
        /// 자리를 따로 만들어야 했는데 갑판 하부에서는 위아래로 나뉜다.
        ///
        /// 압력존에는 안 들어간다(§24) — <see cref="LastShiftZoneDoor"/> 를 안 붙이는 것이
        /// 그 경계를 코드에서 지키는 자리다. 구획 그레이박스와 같은 원칙이다.
        /// </summary>
        private static void CreateAirlock(Transform parent)
        {
            const float size = LastShiftBypassDuct.AirlockSize;
            var centre = new Vector3(LastShiftBypassDuct.AirlockCenterX,
                (LastShiftBypassDuct.AirlockFloorY + LastShiftBypassDuct.AirlockCeilingY) * 0.5f,
                LastShiftBypassDuct.AirlockCenterZ);
            CreateDecorCube("Airlock", parent, centre, new Vector3(size, size, size), ductMaterial);

            // 해치 두 짝. 형상으로만 존재하고 아직 안 열린다 — EVA 감압 시퀀스는 §24.7-2 가
            // 별도 카드로 남긴 항목이다.
            //
            // 안쪽 해치는 덕트 바닥 판 <b>바로 아래</b>에 매단다. 예전처럼 덕트 바닥과 같은 y 에
            // 두면 판과 겹쳐 z-fighting 이 나고, 무엇보다 이 카드에서 중요한 것은 그 판이
            // 에어록 천장을 통째로 막고 있다는 사실이다 — 갑판 구멍으로 떨어진 물건이 닿는
            // 최저점이 덕트 바닥(-1.2)이지 에어록 바닥(-4.2)이 아닌 근거가 그것이고,
            // LastShiftBypassDuct.DeepestFallY 가 같은 것을 코드로 말한다.
            var hatchThickness = 0.08f;
            var hatch = new Vector3(LastShiftZoneDoor.OpeningWidth, hatchThickness, LastShiftZoneDoor.OpeningWidth);
            CreateDecorCube("Hatch_Inner", parent,
                new Vector3(centre.x,
                    LastShiftBypassDuct.AirlockCeilingY - LastShiftBypassDuct.PanelThickness - hatchThickness * 0.5f,
                    centre.z), hatch, ductMaterial);
            CreateDecorCube("Hatch_Outer", parent,
                new Vector3(centre.x, LastShiftBypassDuct.AirlockFloorY, centre.z), hatch, ductMaterial);
        }

        /// <summary>
        /// 갑판 승강구의 해치 한 짝. 판은 갑판 위로 미끄러지고, 통행 차단은 별도 콜라이더가 맡는다 —
        /// 움직이는 콜라이더로 막으면 CharacterController 와 떠 있는 물건이 판에 끼거나 밀려나서
        /// 확인하려는 것("닫힌 해치는 못 지나가고 물건도 안 빠진다")이 아니라 밀림이 먼저 보인다.
        /// <see cref="CreateZoneDoor"/> 와 같은 이유·같은 구조다.
        ///
        /// 테두리 띠를 두르는 것은 <b>구멍이 발밑에서 읽혀야</b> 하기 때문이다. 갑판에 뚫린
        /// <c>0.9m</c> 구멍은 눈높이(<c>1.65</c>)에서 잘 안 보이고, 저중력에서 뒷걸음질로 빠지면
        /// 다시 올라오는 데 우회로 왕복이 든다.
        /// </summary>
        private static void CreateDeckHatch(Transform parent, int shaft)
        {
            const float span = LastShiftDeckHatch.OpeningSpan;
            const float thickness = LastShiftDeckHatch.PanelThickness;
            var name = $"DeckHatch_{(shaft == LastShiftBypassDuct.ForeShaft ? "Fore" : "Aft")}";
            var hatchMaterial = CreateMaterial($"LS_Hatch_{shaft}", new Color(0.50f, 0.42f, 0.26f));

            var hatch = new GameObject(name);
            hatch.transform.SetParent(parent, false);
            // 해치 오브젝트를 구멍 중심에 놓는다. 판·띠·차단 콜라이더가 전부 이 아래에서 로컬로
            // 잡히고, LastShiftDeckHatch 가 매 프레임 다시 쓰는 판 위치도 로컬이라 그대로 따라온다.
            hatch.transform.localPosition = LastShiftBypassDuct.ShaftMouth(shaft);

            var panel = CreateCube($"{name}_Panel", hatch.transform, Vector3.zero,
                new Vector3(span, thickness, span), hatchMaterial);
            Object.DestroyImmediate(panel.GetComponent<Collider>());

            panelMaterial ??= CreateMaterial("LS_Panel", new Color(0.14f, 0.16f, 0.19f));
            foreach (var sign in new[] { -1f, 1f })
            {
                var rimZ = CreateCube($"{name}_Rim_{(sign < 0f ? "Port" : "Starboard")}", hatch.transform,
                    new Vector3(0f, 0.015f, sign * (span * 0.5f + 0.06f)),
                    new Vector3(span + 0.24f, 0.03f, 0.12f), panelMaterial);
                Object.DestroyImmediate(rimZ.GetComponent<Collider>());
                var rimX = CreateCube($"{name}_Rim_{(sign < 0f ? "Fore" : "Aft")}", hatch.transform,
                    new Vector3(sign * (span * 0.5f + 0.06f), 0.015f, 0f),
                    new Vector3(0.12f, 0.03f, span), panelMaterial);
                Object.DestroyImmediate(rimX.GetComponent<Collider>());
            }

            var blockerObject = new GameObject($"{name}_Blocker");
            blockerObject.transform.SetParent(hatch.transform, false);
            // 차단면은 갑판 슬래브 자리를 그대로 메운다. 얇게 얹으면 떠 있는 물건이 얇은 판을
            // 뚫고 지나가는 터널링이 나온다.
            blockerObject.transform.localPosition = new Vector3(0f, -LastShiftShipDimensions.HullThickness * 0.5f, 0f);
            var blocker = blockerObject.AddComponent<BoxCollider>();
            blocker.size = new Vector3(span, LastShiftShipDimensions.HullThickness, span);

            hatch.AddComponent<LastShiftDeckHatch>().Configure(shaft, panel.transform, blocker);
        }

        private static void CreateCompartment(Transform parent, LastShiftCompartmentSpec spec)
        {
            const float thickness = LastShiftCompartments.PanelThickness;
            const float height = LastShiftCompartments.InteriorHeight;

            var root = new GameObject(LastShiftCompartments.NameOf(spec.Compartment));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(spec.CenterX, 0f, spec.CenterZ);

            var halfX = spec.LengthX * 0.5f;
            var halfZ = spec.WidthZ * 0.5f;
            var slabX = spec.LengthX + 2f * thickness;
            var slabZ = spec.WidthZ + 2f * thickness;

            CreateCube("Floor", root.transform, new Vector3(0f, -thickness * 0.5f, 0f),
                new Vector3(slabX, thickness, slabZ), floorMaterial);
            CreateCube("Ceiling", root.transform, new Vector3(0f, height + thickness * 0.5f, 0f),
                new Vector3(slabX, thickness, slabZ), ceilingMaterial);

            foreach (var alongX in new[] { true, false })
            foreach (var atMax in new[] { false, true })
            {
                if (IsOwnDoorFace(spec, alongX, atMax)) continue;

                var half = alongX ? halfX : halfZ;
                var freeHalf = alongX ? halfZ : halfX;
                var plane = (atMax ? half : -half) + (atMax ? thickness * 0.5f : -thickness * 0.5f);
                var openings = ChildDoorwaysOn(spec, alongX, atMax);
                CreateWallWithOpenings(
                    $"Wall_{(alongX ? "X" : "Z")}{(atMax ? "Max" : "Min")}", root.transform,
                    alongX, plane, -freeHalf - thickness, freeHalf + thickness, height,
                    thickness, compartmentMaterial, openings);
            }

            CreateCompartmentLabel(spec, root.transform);

            // 등은 여기서 안 단다. 조명은 씬 소관이라 선체 프리팹에 안 들어가고,
            // 구획 등도 CreateLighting 이 같이 세운다 — 프리팹 안에 등이 섞이면
            // "씬 조명은 씬에서 고친다" 가 한 군데서만 깨진다.
        }

        /// <summary>이 면이 구획 자기 안쪽 문이 놓인 면인가. 그 면은 부모(또는 선체)가 세운다.</summary>
        private static bool IsOwnDoorFace(LastShiftCompartmentSpec spec, bool alongX, bool atMax)
        {
            if (spec.DoorPlane != (alongX ? LastShiftDoorPlane.AlongX : LastShiftDoorPlane.AlongZ)) return false;
            var face = alongX
                ? (atMax ? spec.MaxX : spec.MinX)
                : (atMax ? spec.MaxZ : spec.MinZ);
            return Mathf.Abs(spec.DoorPlaneCoordinate - face) < 0.001f;
        }

        private static LastShiftCompartmentSpec[] ChildrenOn(LastShiftCompartmentSpec spec, bool alongX, bool atMax)
        {
            var face = alongX
                ? (atMax ? spec.MaxX : spec.MinX)
                : (atMax ? spec.MaxZ : spec.MinZ);
            return LastShiftCompartments.Specs
                .Where(child => child.ParentIndex == (int)spec.Compartment &&
                                child.DoorPlane == (alongX ? LastShiftDoorPlane.AlongX : LastShiftDoorPlane.AlongZ) &&
                                Mathf.Abs(child.DoorPlaneCoordinate - face) < 0.001f)
                .ToArray();
        }

        /// <summary>이 면에 뚫어야 하는 구멍의 자유축 로컬 좌표. 잠긴 자식은 구멍을 안 낸다.</summary>
        private static float[] ChildDoorwaysOn(LastShiftCompartmentSpec spec, bool alongX, bool atMax)
        {
            var origin = alongX ? spec.CenterZ : spec.CenterX;
            return ChildrenOn(spec, alongX, atMax)
                .Where(child => child.IsPassable)
                .Select(child => child.DoorCenter - origin)
                .ToArray();
        }

        /// <summary>
        /// 판 한 장. <paramref name="openings"/> 는 이 면의 자유축 위 문 중심이고, 비어 있으면
        /// 통짜다. 구멍이 있으면 구간을 잘라 세우고 그 위에 인방을 얹는다 — 인방이 없으면
        /// 문 높이(2.2)에서 천장까지가 그대로 뚫려 그림과 통행 가능 범위가 어긋난다.
        /// </summary>
        private static void CreateWallWithOpenings(string name, Transform parent, bool alongX,
            float plane, float freeMin, float freeMax, float height, float thickness,
            Material material, float[] openings)
        {
            const float doorWidth = LastShiftZoneDoor.OpeningWidth;
            const float doorHeight = LastShiftZoneDoor.OpeningHeight;

            var edges = new System.Collections.Generic.List<float> { freeMin };
            foreach (var opening in openings.OrderBy(value => value))
            {
                edges.Add(opening - doorWidth * 0.5f);
                edges.Add(opening + doorWidth * 0.5f);
            }
            edges.Add(freeMax);

            // 짝수 index 로 시작하는 구간이 판, 그 사이가 구멍이다.
            for (var segment = 0; segment + 1 < edges.Count; segment += 2)
            {
                var min = edges[segment];
                var max = edges[segment + 1];
                if (max - min <= 0.0001f) continue;
                CreateSlab($"{name}_{segment / 2}", parent, alongX, plane,
                    (min + max) * 0.5f, max - min, height, 0f, thickness, material);
            }

            for (var index = 0; index < openings.Length; index++)
                CreateSlab($"{name}_Lintel_{index}", parent, alongX, plane,
                    openings[index], doorWidth, height - doorHeight, doorHeight, thickness, material);
        }

        private static void CreateSlab(string name, Transform parent, bool alongX, float plane,
            float freeCenter, float freeSize, float height, float bottom, float thickness, Material material)
        {
            var position = alongX
                ? new Vector3(plane, bottom + height * 0.5f, freeCenter)
                : new Vector3(freeCenter, bottom + height * 0.5f, plane);
            var scale = alongX
                ? new Vector3(thickness, height, freeSize)
                : new Vector3(freeSize, height, thickness);
            CreateCube(name, parent, position, scale, material);
        }

        private static void CreateCompartmentLabel(LastShiftCompartmentSpec spec, Transform root)
        {
            var text = spec.Compartment.ToString().ToUpperInvariant();
            // 라벨은 +z 를 보는 면에 글자를 그린다. 구획 선수 쪽 벽 안쪽에 붙여 문으로 들어오는
            // 방향에서 읽히게 둔다.
            CreateZoneLabel(root.parent, text,
                new Vector3(spec.CenterX, LastShiftCompartments.InteriorHeight - 0.75f, spec.MinZ + 0.12f),
                compartmentMaterial.color);
        }

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
            CreateZoneFloor(zone.transform, zoneId, zoneLength);
            CreateZoneStrip(zone.transform, zoneLength, material);
        }

        /// <summary>
        /// 구역 바닥. 승강구가 있는 구역(조종석·산소실)은 슬래브에 <c>0.9m</c> 구멍을 뚫는다 —
        /// 우회 통로 3단계에서 미뤄 둔 "갑판에 구멍" 이 여기서 열린다.
        ///
        /// <b>액자형 넉 장으로 두른다.</b> 구멍을 뺀 나머지를 x 두 장 + z 두 장으로 덮는 방식이고,
        /// 넷 다 구멍 좌표에서 파생하므로 진입점이 옮겨가도 판이 따라온다. 리터럴로 쪼개 두면
        /// 선체 확대 때 구멍만 옮겨 가고 판이 제자리에 남아 바닥에 엉뚱한 틈이 생긴다.
        ///
        /// 구멍이 뚫려도 <see cref="LastShiftDeckHatch"/> 의 차단 콜라이더가 닫혀 있는 동안 그 자리를
        /// 메우므로, 아무도 열지 않은 상태에서는 예전과 똑같이 막힌 바닥이다.
        /// </summary>
        private static void CreateZoneFloor(Transform zone, LastShiftZone zoneId, float zoneLength)
        {
            const float thickness = LastShiftShipDimensions.HullThickness;
            const float span = LastShiftShipDimensions.EndWallSpan;
            floorMaterial ??= CreateMaterial("LS_Floor", new Color(0.30f, 0.32f, 0.35f));

            if (!LastShiftBypassDuct.TryShaftInZone(zoneId, out var mouth))
            {
                CreateCube("Floor", zone, new Vector3(0f, -thickness * 0.5f, 0f),
                    new Vector3(zoneLength, thickness, span), floorMaterial);
                return;
            }

            // 구역 오브젝트가 ZoneCenterX 에 놓이므로 구멍도 로컬 x 로 바꿔서 쓴다.
            var holeX = mouth.x - LastShiftShipDimensions.ZoneCenterX(zoneId);
            var holeZ = mouth.z;
            var half = LastShiftDeckHatch.OpeningSpan * 0.5f;

            // 선수·선미 쪽 두 장은 전폭을 그대로 덮고, 좌우 두 장이 구멍의 x 폭 안에서 z 를 메운다.
            CreateFloorSlab(zone, "Floor_Fore", -zoneLength * 0.5f, holeX - half, -span * 0.5f, span * 0.5f);
            CreateFloorSlab(zone, "Floor_Aft", holeX + half, zoneLength * 0.5f, -span * 0.5f, span * 0.5f);
            CreateFloorSlab(zone, "Floor_ShaftPort", holeX - half, holeX + half, -span * 0.5f, holeZ - half);
            CreateFloorSlab(zone, "Floor_ShaftStarboard", holeX - half, holeX + half, holeZ + half, span * 0.5f);
        }

        /// <summary>바닥 판 한 장. x·z 구간으로 받는다 — 구멍을 두르는 넉 장이 전부 구간 계산이라 중심·크기로 받으면 읽기 어렵다.</summary>
        private static void CreateFloorSlab(Transform zone, string name, float minX, float maxX, float minZ, float maxZ)
        {
            const float thickness = LastShiftShipDimensions.HullThickness;
            CreateCube(name, zone, new Vector3((minX + maxX) * 0.5f, -thickness * 0.5f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, thickness, maxZ - minZ), floorMaterial);
        }

        private static void CreateZoneStrip(Transform zone, float zoneLength, Material material)
        {
            // 구역 색 띠. 뒷벽 앞 바닥에 깔아 어느 구역에 서 있는지가 발밑에서 읽힌다.
            var strip = CreateCube("ZoneStrip", zone, new Vector3(0f, 0.015f, BackWallInnerZ - 0.8f),
                new Vector3(zoneLength - 0.3f, 0.03f, 0.25f), material);
            Object.DestroyImmediate(strip.GetComponent<Collider>());
        }

        /// <summary>
        /// Tether 는 어떤 프리셋에서도 loose 로 유지되는 유일한 상시 grab 대상이라 시작 위치에서
        /// 보이면서 GrabDistance(2.2m) 안이어야 한다. 바닥 높이 아이템은 카메라(y≈1.65, 수직 FOV 72°)
        /// 기준 사거리 안으로 당길수록 화면 밖으로 내려가 조준 자체가 불가능하다. 그래서
        /// 받침대(TetherRack) 위에 올린다 — loose 상태의 Rigidbody 는 kinematic 이 아니라
        /// 공중 배치는 낙하한다.
        /// </summary>
        public static Vector3 TetherRackPosition => LastShiftShipDimensions.TetherRackPosition;

        public static Vector3 TetherRackScale => LastShiftShipDimensions.TetherRackScale;

        public static Vector3 TetherSpawnPosition => LastShiftShipDimensions.TetherNominal;

        /// <summary>
        /// 아이템 하나의 제원. 정위치(<c>Position</c>)만 프리팹 밖에 남는다 — 프리팹은 "무엇인가"를
        /// 들고 씬이 "어디인가"를 정한다. <see cref="LastShiftGrabbable.NominalPosition"/> 이
        /// <c>Configure</c> 시점의 <c>transform.position</c> 을 그대로 잡아 두므로, 프리팹을
        /// 원점에서 구워 두고 씬에서 놓은 뒤 다시 <c>Configure</c> 해야 정위치가 맞는다.
        /// </summary>
        private readonly struct ItemSpec
        {
            public ItemSpec(string name, LastShiftItemRole role, Vector3 position, Vector3 scale, Color color)
            {
                Name = name;
                Role = role;
                Position = position;
                Scale = scale;
                Color = color;
            }

            public string Name { get; }
            public LastShiftItemRole Role { get; }
            public Vector3 Position { get; }
            public Vector3 Scale { get; }
            public Color Color { get; }
        }

        private static ItemSpec[] ItemSpecs => new[]
        {
            new ItemSpec("Battery", LastShiftItemRole.Battery, LastShiftShipDimensions.BatteryNominal, new Vector3(0.65f, 0.65f, 0.9f), new Color(0.95f, 0.65f, 0.12f)),
            new ItemSpec("CoolingCanister", LastShiftItemRole.CoolingCanister, LastShiftShipDimensions.CoolingNominal, new Vector3(0.55f, 1.1f, 0.55f), new Color(0.15f, 0.72f, 0.95f)),
            new ItemSpec("PatchPlate", LastShiftItemRole.PatchPlate, LastShiftShipDimensions.PatchPlateNominal, new Vector3(1.15f, 1.15f, 0.18f), new Color(0.78f, 0.82f, 0.88f)),
            new ItemSpec("Tether", LastShiftItemRole.Tether, TetherSpawnPosition, new Vector3(0.25f, 0.25f, 1.2f), new Color(0.95f, 0.30f, 0.22f))
        };

        public static string ItemPrefabPath(LastShiftItemRole role) =>
            $"Assets/DoodleUp/Prefabs/LastShiftItem_{role}.prefab";

        /// <summary>
        /// 아이템 프리팹 넷. 선체와 같은 이유로 씬 밖으로 뺀다 — 씬에 직접 구우면 씬이 둘일 때
        /// 아이템도 두 벌이 되고, 한쪽만 다시 구우면 조용히 어긋난다.
        /// 프리팹은 지우지 않고 덮어쓴다(<c>CreatePlayerPrefab</c> 주석과 같은 근거).
        /// </summary>
        public static void RebuildItemPrefabs()
        {
            Directory.CreateDirectory("Assets/DoodleUp/Prefabs");
            foreach (var spec in ItemSpecs)
            {
                var item = BuildItemHierarchy(spec);
                PrefabUtility.SaveAsPrefabAsset(item, ItemPrefabPath(spec.Role));
                Object.DestroyImmediate(item);
            }

            AssetDatabase.SaveAssets();
            foreach (var spec in ItemSpecs)
            {
                var path = ItemPrefabPath(spec.Role);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var identity = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<NetworkObject>();
                EditorUtility.SetDirty(identity);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (identity.PrefabIdHash == 0u)
                    throw new System.InvalidOperationException(
                        $"{path} saved with GlobalObjectIdHash 0 — NGO cannot tell it from the other items.");
            }

            Debug.Log($"[LAST_SHIFT_ITEM_PREFABS] count={ItemSpecs.Length} result=PASS");
        }

        /// <summary>
        /// 프리팹을 씬에 놓고 정위치를 다시 잡는다. <c>Configure</c> 를 여기서 한 번 더 부르는
        /// 이유는 정위치가 호출 시점의 좌표를 잡기 때문이다 — 프리팹 안의 값은 원점이라
        /// 그대로 두면 물건이 전부 배 한가운데로 되돌아간다.
        /// </summary>
        public static LastShiftGrabbable[] CreateItems()
        {
            return ItemSpecs.Select(spec =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath(spec.Role));
                if (prefab == null)
                    throw new System.InvalidOperationException($"{ItemPrefabPath(spec.Role)} missing — call RebuildItemPrefabs first.");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = spec.Name;
                instance.transform.position = spec.Position;
                var grabbable = instance.GetComponent<LastShiftGrabbable>();
                grabbable.Configure(spec.Role, true);
                return grabbable;
            }).ToArray();
        }

        /// <summary>
        /// 프리팹으로 구울 아이템 하나. 원점에 세운다 — 정위치는 씬이 정한다(<see cref="CreateItems"/>).
        /// </summary>
        private static GameObject BuildItemHierarchy(ItemSpec spec)
        {
            var name = spec.Name;
            var role = spec.Role;
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.position = Vector3.zero;
            item.transform.localScale = spec.Scale;
            item.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial($"LS_{name}", spec.Color);
            var body = item.AddComponent<Rigidbody>();
            body.mass = role == LastShiftItemRole.Battery ? 8f : 3f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            // 저중력은 씬 직렬화 값에도 반영한다. Awake 의 ConfigureItemBody 만 의존하면
            // 씬을 열어 첫 물리 스텝이 도는 사이 한 프레임 동안 지구 중력으로 떨어진다.
            LastShiftShipPhysics.ConfigureItemBody(body);
            item.AddComponent<LastShiftGrabbable>().Configure(role, true);
            // 네트워크 부품은 프리팹이 들고 있어야 한다. 씬 인스턴스에 나중에 AddComponent 하면
            // NetworkObject.OnValidate 가 안 돌아 GlobalObjectIdHash 가 0 으로 남고, 0 끼리는
            // 서로 같은 값이라 NGO 가 "이미 같은 해시가 등록됐다" 며 두 번째 아이템부터 죽는다.
            // 씬은 멀쩡히 저장되고 빌드 로그도 PASS 라 조용히 지나간다 — 플레이어 프리팹이
            // 겪은 것과 같은 함정이다(CreatePlayerPrefab 주석).
            item.AddComponent<NetworkObject>().DontDestroyWithOwner = true;
            item.AddComponent<LastShiftOwnerNetworkTransform>();
            item.AddComponent<LastShiftNetworkGrabbable>();
            return item;
        }

        public static void CreateMeteorStimulus()
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

        /// <summary>
        /// 구역·구획 라벨. 선체 프리팹 안에 들어가야 하므로 부모를 받는다 — 씬 루트에 두면
        /// 프리팹을 다시 구워도 라벨만 옛 위치에 남아, 배는 움직였는데 이름표는 안 움직인다.
        /// </summary>
        private static void CreateZoneLabel(Transform parent, string text, Vector3 position, Color color)
        {
            var label = new GameObject($"Label_{text}");
            label.transform.SetParent(parent, false);
            label.transform.localPosition = position;
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
        public static void CreateLighting()
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
            CreateZoneLights("Power", LastShiftZone.Power, new Color(1f, 0.86f, 0.62f), 2.3f);
            CreateZoneLights("Cooling", LastShiftZone.Cooling, new Color(0.66f, 0.86f, 1f), 2.3f);
            CreateZoneLights("LifeSupport", LastShiftZone.LifeSupport, new Color(0.66f, 1f, 0.80f), 2.3f);

            // 드나들 수 있는 구획만 등을 단다. 잠긴 구획은 들어갈 수 없으므로 등이 낭비고,
            // 잠긴 문틈으로 빛이 새면 §17.7 이 미결로 남긴 "차폐 수준" 을 코드가 먼저 정해 버린다.
            foreach (var spec in LastShiftCompartments.Specs)
            {
                if (!spec.IsPassable) continue;
                CreateZoneLight($"Light_{LastShiftCompartments.NameOf(spec.Compartment)}",
                    new Vector3(spec.CenterX, LastShiftCompartments.InteriorHeight - 0.35f, spec.CenterZ),
                    new Color(0.78f, 0.80f, 0.86f), 2.0f);
            }
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
            powerMaterial = null;
            coolingMaterial = null;
            lifeSupportMaterial = null;
            ceilingMaterial = null;
            ductMaterial = null;
            panelMaterial = null;
            starMaterial = null;
            voidMaterial = null;
            compartmentMaterial = null;
        }

        /// <summary>
        /// 머티리얼 에셋 폴더. 예전에는 <c>new Material(shader)</c> 로 만들어 어디에도 저장하지
        /// 않았고, 그러면 유니티가 그것을 <b>씬 파일 안에</b> 직렬화한다 — 프로젝트에 <c>.mat</c>
        /// 파일이 하나도 없는데 씬에는 머티리얼이 24개 들어 있던 이유다.
        ///
        /// 그 상태로는 선체를 프리팹으로 뺄 수 없다. 프리팹 에셋은 씬에 묻힌 머티리얼을 참조할
        /// 수 없어서, 옮기는 순간 회색 기본 머티리얼이 되거나 프리팹 파일 안에 사본이 또 생긴다.
        /// 씬 하나로 합치는 작업(SP01 폐기)의 선행 조건이 여기다.
        ///
        /// 부수적으로 아트 소관이 생긴다 — 드레싱 단계에서 색을 바꾸려면 만질 파일이 있어야 하는데,
        /// 지금까지는 씬을 다시 굽는 것 말고는 방법이 없었다.
        /// </summary>
        private const string MaterialFolder = "Assets/DoodleUp/Materials";

        private static void EnsureMaterialFolder()
        {
            if (AssetDatabase.IsValidFolder(MaterialFolder)) return;
            Directory.CreateDirectory(MaterialFolder);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 이름으로 머티리얼 에셋을 찾아 값을 갱신하고, 없으면 만든다.
        ///
        /// <b>지우고 다시 만들지 않는다.</b> 프리팹에서 이미 겪은 것과 같은 이유다
        /// (<see cref="LastShiftNetworkSceneBuilder"/> 의 <c>CreatePlayerPrefab</c> 주석) —
        /// 에셋을 지우면 GUID 가 새로 찍히고, 그 머티리얼을 참조하던 씬·프리팹이 전부
        /// 끊긴 참조로 바뀐다. 덮어쓰면 GUID 가 유지되어 재빌드 diff 가 조용하다.
        ///
        /// 발광 상태는 매번 껐다가 <see cref="CreateEmissiveMaterial"/> 가 다시 켠다. 안 그러면
        /// 발광이던 이름이 비발광으로 바뀔 때 에셋에 남은 키워드가 조용히 따라온다.
        /// </summary>
        private static Material CreateMaterial(string name, Color color)
        {
            EnsureMaterialFolder();
            var shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.shader != shader) material.shader = shader;
            material.color = color;
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
            return material;
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
            // CreateMaterial 이 발광을 끈 뒤 dirty 를 찍었으므로 여기서 다시 찍어야 한다.
            // 안 그러면 발광 설정이 메모리에만 남고 에셋 파일에는 꺼진 상태가 저장된다.
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
