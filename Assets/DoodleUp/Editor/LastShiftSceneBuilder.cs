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
        private static Material galleryMaterial;
        private static Material observationMaterial;
        private static Material discHullMaterial;

        // ── 드레싱 재질 ─────────────────────────────────────────────────────────
        // 색 정본은 Runtime 의 LastShiftDressing 이고 여기서는 캐시만 든다.
        private static Material fixtureMaterial;
        private static Material hazardMaterial;
        private static Material laneMaterial;
        private static Material bypassMaterial;
        private static Material frostMaterial;
        private static Material scorchMaterial;
        private static Material growMaterial;
        private static Material indicatorMaterial;

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
        /// <summary>
        /// <c>-executeMethod DoodleUp.Editor.LastShiftSceneBuilder.RebuildShipPrefabForAutomation</c>
        ///
        /// <b>선체 프리팹만 굽는다 — 씬은 안 건드린다.</b> 이 진입점이 따로 있는 이유가 그것이다.
        /// <see cref="LastShiftNetworkSceneBuilder.RebuildSandboxForAutomation"/> 는 씬을 통째로
        /// 다시 짓는데, <c>-nographics</c> 배치에서 그렇게 저장된 씬은 선체가 <b>프리팹 인스턴스가
        /// 아니라 평범한 GameObject 로 풀려서</b> 들어간다. 그 상태를 커밋하면 다음에 프리팹을
        /// 구워도 씬이 안 따라오고, 씬과 프리팹이 조용히 갈라진다.
        ///
        /// 선체 지오메트리만 바뀐 카드는 이쪽을 쓴다. 씬 구조(런타임 오브젝트·NetworkManager·
        /// 아이템 배치)가 바뀌었을 때만 열린 에디터에서 씬 빌더를 돌린다.
        /// </summary>
        public static void RebuildShipPrefabForAutomation()
        {
            RebuildShipPrefab();
        }

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
            Debug.Log($"[LAST_SHIFT_SHIP_PREFAB] path={ShipPrefabPath} compartments={LastShiftCompartments.Count} " +
                      $"gallery_legs={LastShiftUpperGallery.LegCount} gallery_branches={LastShiftUpperGallery.BranchCount} " +
                      $"observation_bands={LastShiftObservationGallery.BandCount} " +
                      $"observation_drift={LastShiftObservationGallery.ArcCenterlineDrift:0.##}/{LastShiftObservationGallery.ArcLength:0.##} " +
                      $"disc_hull={LastShiftHullShell.OverallLength:0.#}x{LastShiftHullShell.OverallWidth:0.#} " +
                      $"frame_ribs={LastShiftHullFrames.BuildableRibCount}/{LastShiftHullFrames.RibCount} " +
                      $"frame_girths={LastShiftHullFrames.BuildableRingSegmentCount}/{LastShiftHullShell.SegmentCount} " +
                      $"port_bays={LastShiftHullFrames.WindowBaySegmentCount} " +
                      $"bow_bays={LastShiftObservatoryWindow.BowBaySegmentCount} result=PASS");
            return prefab;
        }

        private static GameObject BuildShipGrayboxHierarchy()
        {
            var ship = new GameObject("ShipGraybox");
            // 구역 색 정본은 Runtime 의 LastShiftDressing 이다. 여기 리터럴을 남겨 두면 구획색
            // 열한 개가 구역색 넷과 충분히 떨어졌는지를 EditMode 에서 확인할 수 없다.
            CreateZone(CockpitZoneName, ship.transform, LastShiftZone.Cockpit, cockpitMaterial ??= CreateMaterial("LS_Cockpit", LastShiftDressing.TintOf(LastShiftZone.Cockpit)));
            CreateZone(PowerZoneName, ship.transform, LastShiftZone.Power, powerMaterial ??= CreateMaterial("LS_Power", LastShiftDressing.TintOf(LastShiftZone.Power)));
            CreateZone(CoolingZoneName, ship.transform, LastShiftZone.Cooling, coolingMaterial ??= CreateMaterial("LS_Cooling", LastShiftDressing.TintOf(LastShiftZone.Cooling)));
            CreateZone(LifeSupportZoneName, ship.transform, LastShiftZone.LifeSupport, lifeSupportMaterial ??= CreateMaterial("LS_LifeSupport", LastShiftDressing.TintOf(LastShiftZone.LifeSupport)));
            // 벽 높이는 천장 내면(CeilingInnerHeight)까지 올린다. 예전 3.0 을 유지하면
            // 벽과 천장 사이에 0.2m 띠 구멍이 남아 저중력에서 뜬 물건이 그 틈으로 빠진다.
            //
            // Left/Right 는 전장 축(x)의 두 끝벽이고 Back/Front 는 전폭 축(z)의 긴 벽이다.
            // 이름은 예전 배치에서 굳은 것이라 그대로 두되, 좌표는 전부 치수 정본에서 파생한다.
            hullMaterial ??= CreateMaterial("LS_Hull", new Color(0.18f, 0.20f, 0.23f));
            CreateEndWall(ship.transform, "OuterHull_Left", -EndWallX, -HalfLength);
            CreateEndWall(ship.transform, "OuterHull_Right", EndWallX, HalfLength);
            CreateCube("OuterHull_Back", ship.transform, new Vector3(0f, CeilingInnerHeight * 0.5f, HullBackZ), new Vector3(LastShiftShipDimensions.SideWallSpan, CeilingInnerHeight, LastShiftShipDimensions.HullThickness), hullMaterial);
            CreatePortSill(ship.transform);
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
            CreateUpperGallery(ship.transform);
            CreateObservationGallery(ship.transform);
            CreateBypassDuct(ship.transform);
            CreateDiscHull(ship.transform);
            CreateCube("CockpitConsole", ship.transform, new Vector3(LastShiftShipDimensions.CockpitCenterX - 1.3f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 2.5f), cockpitMaterial);
            CreateCube("TetherRack", ship.transform, TetherRackPosition, TetherRackScale, cockpitMaterial);
            CreateCube("BusCabinet", ship.transform, new Vector3(LastShiftShipDimensions.PowerCenterX, 0.65f, BackWallInnerZ - 0.55f), new Vector3(1.6f, 1.3f, 0.5f), powerMaterial);
            CreateCube("LifeSupportRack", ship.transform, new Vector3(LastShiftShipDimensions.LifeSupportCenterX + 1.1f, 0.75f, BackWallInnerZ - 0.75f), new Vector3(0.8f, 1.5f, 0.8f), lifeSupportMaterial);
            CreateCoolingStack(ship.transform);
            CreateStateCues(ship.transform);
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
            CreatePassageDressing(ship, passage);
        }

        /// <summary>
        /// 통로 드레싱. <b>이 통로가 답해야 하는 질문은 "어느 쪽으로 지나가는가" 하나다.</b>
        /// 통로 폭을 배플이 가로막고 있고, 남는 통행 차선
        /// (<see cref="LastShiftShipDimensions.BaffleFreeStrip"/>)은 배플 한쪽에만 있으며 반대쪽
        /// (<see cref="LastShiftShipDimensions.BaffleDeadStrip"/>)은 사람이 못 지나는 죽은 틈이다.
        /// 회색 판만 서 있으면 초행에는 그 둘이 구분되지 않아 죽은 틈으로 걸어가 막힌다 —
        /// 저중력에서 물건을 들고 산소 시계를 보며 걷는 중에 일어나면 그냥 손해다.
        ///
        /// 그래서 세 가지만 한다. 바닥 유도띠가 차선을 발밑에서 알리고, 배플 모서리의 경고
        /// 띠가 부딪히는 자리를 세우고, 손잡이가 통로를 "머무는 방" 이 아니라 "지나는 구간"
        /// 으로 읽히게 한다. 상태 정보는 하나도 싣지 않는다 — 통로가 정보 우위 지점이라는
        /// §5.3 은 개구부 게이지가 만드는 것이고, 여기에 색을 더 얹으면 그것과 경쟁한다.
        ///
        /// 전부 콜라이더 없는 장식이다. 통로 통행 폭은 A3·CARRY_SPEED 가 걸린 수치라
        /// 드레싱이 1cm 도 줄이면 안 된다.
        /// </summary>
        private static void CreatePassageDressing(Transform ship, int passage)
        {
            var side = passage <= 0 ? "A" : "B";
            var centerX = LastShiftShipDimensions.PassageCenterX(passage);
            var length = LastShiftShipDimensions.PassageLength;
            var minZ = LastShiftShipDimensions.PassageMinZ(passage);
            var maxZ = LastShiftShipDimensions.PassageMaxZ(passage);

            // 통행 차선의 z 중심은 배플 반대쪽 개구부와 같다 — 띠를 그 위에 깔면 "이 선을
            // 따라가면 문이 나온다" 가 그대로 참이다. 리터럴을 쓰면 배플이 움직일 때 띠만 남는다.
            var laneZ = LastShiftShipDimensions.BaffleFreeStripCenterZ(passage);
            laneMaterial ??= EnsureMaterial("LS_Lane", new Color(0.55f, 0.70f, 0.86f), 0.8f);
            CreateDecorCube($"PassageLane_{side}", ship,
                new Vector3(centerX, 0.016f, laneZ), new Vector3(length - 0.2f, 0.03f, 0.45f), laneMaterial);

            // 배플 모서리 경고 띠. 배플은 바닥부터 천장까지 불투명한 판이라 어두운 통로에서
            // 정면으로 걸어 들어가면 벽인지 통로인지 구분이 안 된다.
            EnsureHazardMaterial();
            var baffleX = LastShiftShipDimensions.BaffleCenterX(passage);
            var faceOffset = LastShiftShipDimensions.BaffleThickness * 0.5f + 0.03f;
            var index = 0;
            foreach (var faceSign in new[] { -1f, 1f })
            foreach (var edgeZ in new[] { LastShiftShipDimensions.BaffleMinZ(passage), LastShiftShipDimensions.BaffleMaxZ(passage) })
                CreateDecorCube($"BaffleEdge_{side}_{index++}", ship,
                    new Vector3(baffleX + faceSign * faceOffset, CeilingInnerHeight * 0.5f, edgeZ),
                    new Vector3(0.06f, CeilingInnerHeight - 0.3f, 0.14f), hazardMaterial);

            // 양 벽 손잡이. 저중력이라 손으로 잡고 몸을 던지는 이동이 서사적으로 맞고,
            // 수평선 둘이 통로에 원근을 줘서 통로가 실제보다 길게 읽힌다.
            foreach (var (railName, railZ) in new[] { ("Port", minZ + 0.09f), ("Starboard", maxZ - 0.09f) })
                CreateDecorCube($"PassageRail_{side}_{railName}", ship,
                    new Vector3(centerX, 1.10f, railZ), new Vector3(length - 0.6f, 0.08f, 0.08f), EnsureFixtureMaterial());

            // 통로 소품도 데이터로 받는다. 지금 에셋에는 통로 항목이 없어 아무것도 안 서지만,
            // 훅이 없으면 art 가 통로에 뭘 놓고 싶을 때 다시 코드를 고쳐야 한다.
            CreateDressingProps(ship, LastShiftDressingSpace.OfPassage(passage));
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
            const float mullionWidth = 0.35f;
            var mullionCount = Mathf.FloorToInt((Length - mullionSpacing) / mullionSpacing);
            var mullionStart = -(mullionCount - 1) * mullionSpacing * 0.5f;
            for (var index = 0; index < mullionCount; index++)
            {
                var x = mullionStart + index * mullionSpacing;
                // 관측 회랑 문 자리는 비운다(§29.4-(2)). 번호가 아니라 겹침으로 거른다.
                if (OverlapsPortDoorway(x, mullionWidth)) continue;
                CreateCube($"WindowMullion_{index}", ship, new Vector3(x, (WindowSillHeight + windowTop) * 0.5f, HullFrontZ), new Vector3(mullionWidth, windowTop - WindowSillHeight, 0.22f), panelMaterial);
            }

            // 창 밖 우주. 좌표 정본은 LastShiftHullFrames 다 — 예전에는 여기 리터럴이
            // 따로 있어서 배경막이 옮겨질 때 프레임만 옛 값을 믿는 구조였다.
            //
            // 배경막과 별 판은 <b>원반 외피 바깥</b>(z=-22)에 선다. 예전 -9.1 은 단축 반지름
            // 20 안쪽이라 외피가 생긴 뒤로는 껍질 속에 갇혀 있었다.
            const float backdropZ = LastShiftHullFrames.WindowBackdropZ;
            var voidWidth = LastShiftHullFrames.WindowBackdropHalfX * 2f;
            CreateDecorCube("SpaceVoid", ship, new Vector3(0f, 1.6f, backdropZ), new Vector3(voidWidth, 18f, 0.2f), voidMaterial);
            var starRandom = new System.Random(20260804);
            var stars = new GameObject("StarField");
            stars.transform.SetParent(ship, false);
            var starSpreadX = voidWidth * 0.44f;

            // 별 개수는 <b>각밀도</b>를 유지하도록 잡는다. 판이 멀어진 만큼 같은 화각 안에
            // 더 많은 별이 들어오므로 개수를 그대로 두면 조종석에서 촘촘해 보인다.
            // 각밀도 = 표면밀도 x 거리^2 이므로, 폭이 k배 거리가 d배 커지면 개수는 (k/d)^2 배다.
            const float previousBackdropZ = -LastShiftShipDimensions.SideWallZ - 6f;   // -9.1
            const float previousVoidWidth = LastShiftShipDimensions.InteriorLength + 12f;  // 50
            var widthRatio = voidWidth / previousVoidWidth;
            var distanceRatio = Mathf.Abs(backdropZ) / Mathf.Abs(previousBackdropZ);
            var densityScale = (widthRatio / distanceRatio) * (widthRatio / distanceRatio);
            var starCount = Mathf.RoundToInt(90f * Length / 12.5f * densityScale);

            // 판 크기는 거리 비만큼 키운다. 안 키우면 각크기가 그만큼 작아져 화면에서 사라진다.
            const float starScale = 4.4f;

            // 별 판은 배경막 앞 0.4m 에서 시작한다. 예전 구성의 상대 간격 그대로이고, 이
            // 간격이 두 판을 같은 무한거리로 읽히게 하는 값이라 절대 z 가 아니라 배경막
            // 기준으로 잡는다.
            //
            // <b>두께는 이제 원반이 정한다.</b> 예전 2.4m 를 그대로 쓰면 앞쪽 별이 z=-19.2 에
            // 서는데, §29.4-(1) 로 좌현 테두리(단축 -20)에 유리가 생긴 지금 그건 창 <b>앞</b>,
            // 즉 승무원과 유리 사이다. 큰 별이 유리를 뚫지 않도록 중심이 아니라 판 앞면으로
            // 상한을 잡는다(아트 정본 §5.2 가 tech 로 넘긴 §6-2).
            const float starNearZ = backdropZ + 0.4f;
            const float starMaxHalfSize = 0.24f * starScale * 0.5f;
            var starDepth = Mathf.Clamp(
                LastShiftHullFrames.WindowStarNearestZ - starMaxHalfSize - starNearZ, 0f, 2.4f);

            for (var index = 0; index < starCount; index++)
            {
                var x = (float)(starRandom.NextDouble() * (starSpreadX * 2.0) - starSpreadX);
                var y = (float)(starRandom.NextDouble() * 14.0 - 4.0);
                var z = starNearZ + (float)(starRandom.NextDouble() * starDepth);
                var size = (0.10f + (float)starRandom.NextDouble() * 0.14f) * starScale;
                CreateDecorCube($"Star_{index}", stars.transform, new Vector3(x, y, z), Vector3.one * size, starMaterial);
            }

            CreateBowBackdrop(ship);
        }

        /// <summary>
        /// 관측실 선수 창 밖의 우주(§7-6). 좌현 배경막과 <b>같은 구성이고 다른 면</b>이다 —
        /// 좌현은 <c>z</c> 평면, 여기는 <c>x</c> 평면이다.
        ///
        /// 관측실이 좌현 창보다 배경막에 훨씬 가깝다(약 <c>10.5m</c> 대 <c>25m</c>). 좌현
        /// 값을 그대로 옮기면 별이 성기고 크게 보이므로, 개수는 <b>각밀도</b>로 판 크기는
        /// <b>각크기</b>로 옮긴다 — 좌현 필드가 원래 그렇게 잡혀 있다.
        /// </summary>
        private static void CreateBowBackdrop(Transform ship)
        {
            var backdropX = LastShiftObservatoryWindow.BackdropX;
            var halfZ = LastShiftObservatoryWindow.BackdropHalfZ;
            CreateDecorCube("SpaceVoid_Bow", ship, new Vector3(backdropX, 1.6f, 0f),
                new Vector3(0.2f, 18f, halfZ * 2f), voidMaterial);

            // 관측실 안에서 창까지의 거리. 방 중심에서 재고, 이 값이 각밀도·각크기의 기준이다.
            var viewDistance = LastShiftCompartments.Of(LastShiftObservatoryWindow.Compartment).CenterX
                               - backdropX;

            // 별이 흩어지는 상자. z 는 배경막 안쪽으로 물리고(가장자리가 안 보이게), y 는
            // 창 높이(0.9~2.4)에서 실제로 훑는 만큼만 잡는다 — 좌현처럼 위아래로 넓게 뿌리면
            // 문턱 판·인방에 가려 안 보이는 별을 그만큼 더 세우게 된다.
            var spreadZ = halfZ * 0.5f;
            const float spreadYMin = -1f;
            const float spreadYMax = 8f;

            // 좌현 필드의 각밀도. 위 계산이 내는 값(약 150개가 0.9sr 를 덮는다)에서 뽑았고,
            // 개수가 아니라 이 밀도가 두 창을 같은 하늘로 읽히게 한다.
            const float starsPerSteradian = 170f;
            var solidAngle = spreadZ * 2f * (spreadYMax - spreadYMin) / (viewDistance * viewDistance);
            var starCount = Mathf.RoundToInt(solidAngle * starsPerSteradian);

            // 각크기를 좌현과 맞춘다. 좌현은 배경막이 약 25m 밖이라 4.4 였다.
            const float portViewDistance = 25f;
            const float portStarScale = 4.4f;
            var starScale = portStarScale * viewDistance / portViewDistance;

            var stars = new GameObject("StarField_Bow");
            stars.transform.SetParent(ship, false);

            // 별 판 앞면이 테두리 유리보다 앞으로 나오면 승무원과 유리 사이에 별이 뜬다.
            // 좌현 WindowStarNearestZ 와 같은 규칙이고 축만 다르다.
            var starNearX = backdropX + 0.4f;
            var starMaxHalfSize = 0.24f * starScale * 0.5f;
            var starDepth = Mathf.Clamp(
                LastShiftObservatoryWindow.StarNearestX - starMaxHalfSize - starNearX, 0f, 2.4f);

            var starRandom = new System.Random(20260807);
            for (var index = 0; index < starCount; index++)
            {
                var x = starNearX + (float)starRandom.NextDouble() * starDepth;
                var y = spreadYMin + (float)starRandom.NextDouble() * (spreadYMax - spreadYMin);
                var z = (float)(starRandom.NextDouble() * (spreadZ * 2.0) - spreadZ);
                var size = (0.10f + (float)starRandom.NextDouble() * 0.14f) * starScale;
                CreateDecorCube($"StarBow_{index}", stars.transform, new Vector3(x, y, z),
                    Vector3.one * size, starMaterial);
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
        /// 냉각실 열교환기. <b>4구역 분할(§2)이 남긴 빈자리를 메운다</b> — 조종석에는 콘솔,
        /// 전력실에는 배전반, 산소실에는 랙이 있는데 냉각실만 아무것도 없었다. 분할로 새로
        /// 생긴 방이라 소품이 딸려 오지 않았던 것이고, 그 상태에서는 냉각실이 "지나가는 빈
        /// 구간" 으로 읽혀 네 방 중 하나만 격이 낮아진다.
        ///
        /// 뒷벽에 붙이는 것은 배전반·랙과 같은 줄을 만들기 위해서다. 이 자리는 개구부`2`
        /// 노출 원뿔 안이지만 <see cref="LastShiftDressing.StateCueSafeMaxZ"/> 제한에 걸리지
        /// 않는다 — §19.4 가 막는 것은 <b>상태에 반응하는</b> 단서이고, 열교환기는 냉각 상태와
        /// 무관하게 늘 같은 모습이라 원뿔로 새는 정보가 없다. 냉각 상태를 말하는 서리는
        /// <see cref="CreateStateCues"/> 가 안전대 안에 따로 놓는다.
        /// </summary>
        private static void CreateCoolingStack(Transform ship)
        {
            var centerX = LastShiftShipDimensions.RoomCenterX(LastShiftZone.Cooling);
            CreateCube("CoolingStack", ship, new Vector3(centerX, 0.90f, BackWallInnerZ - 0.60f),
                new Vector3(2.2f, 1.8f, 0.6f), coolingMaterial);
            // 방열 핀. 판 하나짜리 상자는 어느 방에 놔도 같아 보이므로, 실루엣에 결을 준다.
            for (var index = 0; index < 5; index++)
                CreateDecorCube($"CoolingStack_Fin_{index}", ship,
                    new Vector3(centerX - 0.8f + index * 0.4f, 1.85f, BackWallInnerZ - 0.60f),
                    new Vector3(0.14f, 0.5f, 0.7f), EnsureFixtureMaterial());

            CreateCoolingValve(ship);
        }

        /// <summary>
        /// 냉각실 수동 순환 밸브(<c>C-3</c> 유지 동사, <c>interaction-verb-diversification-v1.md</c> §4.3).
        ///
        /// <b>손잡이가 눈에 띄어야 한다.</b> 이 배에서 "누르고 있는 동안" 이라는 시간 형태를 갖는
        /// 조작물은 이것 하나뿐이라, 다른 벽 설비와 같은 회색 상자로 두면 승무원은 그 앞에
        /// 서 봐야만 존재를 안다. 경고 황색은 안 쓴다 — 그 색이 붙은 자리 셋(배플 모서리,
        /// 승강구, 격납고 발진 구역)은 전부 "부딪히거나 빠지는 곳" 이고, 밸브는 반대로
        /// 가야 하는 곳이다.
        /// </summary>
        private static void CreateCoolingValve(Transform ship)
        {
            var anchor = LastShiftCoolingValve.Position;

            var root = new GameObject("CoolingValve");
            root.transform.SetParent(ship, false);
            root.transform.localPosition = anchor;

            // 벽에 박히는 대좌. 로컬 z 로 벽 쪽에 물려 손잡이 회전축이 벽면에 붙어 보인다.
            CreateDecorCube("CoolingValve_Body", root.transform,
                new Vector3(0f, 0f, LastShiftCoolingValve.WallStandoffZ * 0.5f),
                new Vector3(0.5f, 0.5f, LastShiftCoolingValve.WallStandoffZ), coolingMaterial);

            // 손잡이. LastShiftCoolingValve 가 매 프레임 이 Transform 의 localRotation 만 쓰므로
            // 자식으로 살(spoke)을 달아 두면 돌아가는 것이 그대로 읽힌다.
            var lever = new GameObject("CoolingValve_Lever");
            lever.transform.SetParent(root.transform, false);
            CreateDecorCube("CoolingValve_Spoke", lever.transform, Vector3.zero,
                new Vector3(0.62f, 0.09f, 0.09f), EnsureFixtureMaterial());
            CreateDecorCube("CoolingValve_Hub", lever.transform, Vector3.zero,
                new Vector3(0.18f, 0.18f, 0.22f), EnsureFixtureMaterial());

            root.AddComponent<LastShiftCoolingValve>().Configure(lever.transform);
        }

        /// <summary>
        /// 냉각실·전력실 상태 단서(서리·그을음). 자리 정본은 Runtime 의
        /// <see cref="LastShiftDressing.StateCues"/> 다 — Editor 어셈블리에 좌표를 적으면
        /// 씬을 다시 굽기 전에는 §19.7 안전대 위반을 아무도 못 본다.
        ///
        /// <b>지금 세우는 것은 정적 판이다.</b> 상태 연동(서리가 자라고 아크가 튀는 것)은
        /// `game-ta` 소관이고, 이 카드가 확정하는 것은 그 이펙트가 나중에 붙을 <b>자리</b>다.
        /// 자리를 먼저 못 박는 이유는 §19.4 의 제약이 이펙트가 아니라 좌표에 걸려 있기
        /// 때문이다 — 이펙트를 만들 때 원뿔 데이터를 다시 읽게 두면 그때 한 번 더 틀린다.
        /// </summary>
        private static void CreateStateCues(Transform ship)
        {
            // 서리·그을음 재질은 여기서 보장한다. 에셋이 이미 있으면 색을 안 덮는다 —
            // 실값은 art 소관이라(브리프 §8.1) 빌드가 매번 코드 색으로 되돌리면 안 된다.
            frostMaterial ??= EnsureMaterial("LS_Frost", new Color(0.74f, 0.87f, 0.95f), 0.35f);
            scorchMaterial ??= EnsureMaterial("LS_Scorch", new Color(0.09f, 0.08f, 0.08f));

            var root = new GameObject("ZoneDressing");
            root.transform.SetParent(ship, false);

            foreach (LastShiftZone zone in System.Enum.GetValues(typeof(LastShiftZone)))
            {
                var space = LastShiftDressingSpace.Of(zone);
                if (!DressingSet.Props.Any(p => p != null && SameSpace(p.space, space))) continue;

                var zoneRoot = new GameObject(zone.ToString());
                zoneRoot.transform.SetParent(root.transform, false);
                CreateDressingProps(zoneRoot.transform, space);
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
        /// 끝벽 한 장. 통짜가 아닌 이유는 선체에 직접 붙는 구획(<c>ParentIndex &lt; 0</c>)의
        /// 문이 이 면에 놓이기 때문이다 — 선미는 생활공간(§9), 선수는 화물칸이다.
        ///
        /// <b>구멍을 뚫을지는 <see cref="LastShiftCompartmentSpec.IsPassable"/> 이 정한다.</b>
        /// 잠긴 구획은 그레이박스에서 구멍이 아니라 메운 판이고(§15.2), 화물칸은 확장 검토
        /// §2 로 P0 상시 개방이 되어 선수 끝벽에도 구멍이 하나 난다. 선수/선미를 한 함수로
        /// 합쳐 둔 것은 그 규칙이 양쪽에서 갈리지 않게 하기 위해서다 — 한쪽만 리터럴로
        /// 통짜를 세우면 초기 <c>Access</c> 를 되돌릴 때 그 자리가 같이 안 돌아온다.
        /// </summary>
        private static void CreateEndWall(Transform ship, string name, float plane, float doorPlaneX)
        {
            var attached = LastShiftCompartments.Specs
                .Where(spec => LastShiftCompartments.ConnectsToHull(spec) &&
                               spec.DoorPlane == LastShiftDoorPlane.AlongX &&
                               Mathf.Abs(spec.DoorPlaneCoordinate - doorPlaneX) < 0.001f)
                .ToArray();

            // 끝벽은 <b>선체 폭보다 넓어질 수 있다.</b> 이 면에 붙는 구획은 자기 문이 놓인
            // 면을 안 세우므로(IsOwnDoorFace) 그 면 전체를 여기서 닫아야 하는데, 화물칸은
            // 폭 `8m` 로 선체 폭 `6m` 보다 넓다 — 선체 폭만 세우면 양 끝에 `0.8m` 짜리
            // 세로 틈이 남고, P0 개방 뒤에는 그 틈으로 걸어 나가 원반 껍질 안쪽 빈 공간에
            // 선다. 생활공간(선미)은 선체 폭과 같아 이 항이 아무것도 안 바꾼다.
            var half = LastShiftShipDimensions.EndWallSpan * 0.5f;
            foreach (var spec in attached)
                half = Mathf.Max(half, Mathf.Max(Mathf.Abs(spec.MinZ), Mathf.Abs(spec.MaxZ)));

            var doorways = attached
                .Where(spec => spec.IsPassable)
                .Select(spec => spec.DoorCenter)
                .ToArray();

            CreateWallWithOpenings(name, ship, true, plane,
                -half, half, CeilingInnerHeight,
                LastShiftShipDimensions.HullThickness, hullMaterial, doorways);
        }

        /// <summary>
        /// 좌현 긴 벽의 문턱 판(창 아래 <see cref="WindowSillHeight"/> 구간). 통짜 한 장이
        /// 아닌 이유는 관측 회랑(§29.4-(2))이 여기 문 하나로 붙기 때문이다 — 선미 끝벽이
        /// 생활공간 때문에 갈라진 것과 같은 자리다.
        ///
        /// <b>자르는 것은 문턱뿐이다.</b> 창 위 인방(<c>OuterHull_FrontUpper</c>)은 그대로
        /// 둔다. 문 구멍 높이(<c>2.2</c>)가 창 윗단(<c>2.1</c>)보다 <c>0.1</c> 높아서 인방까지
        /// 자르면 좌현 창 띠가 이 한 자리에서만 천장까지 뚫린다 — 그레이박스에서 통과 높이가
        /// <c>0.1</c> 낮은 것보다 전장 <c>38m</c> 짜리 창 띠가 끊기는 쪽이 나쁘다. 실제 통과
        /// 높이는 창 윗단이 된다.
        /// </summary>
        private static void CreatePortSill(Transform ship)
        {
            var doorways = LastShiftObservationGallery.CockpitDoorwayIsOpen
                ? new[] { LastShiftObservationGallery.CockpitLandingCenterX }
                : System.Array.Empty<float>();

            const float span = LastShiftShipDimensions.SideWallSpan;
            CreateWallWithOpenings("OuterHull_FrontLower", ship, false, HullFrontZ,
                -span * 0.5f, span * 0.5f, WindowSillHeight,
                LastShiftShipDimensions.HullThickness, hullMaterial, doorways);
        }

        /// <summary>
        /// 이 x 가 관측 회랑 문 구멍과 겹치는가. 겹치는 창 기둥은 안 세운다 — 세우면 문
        /// 한가운데 <c>0.35m</c> 기둥이 서서 통행 폭이 갈린다.
        ///
        /// 기둥 번호를 박지 않는 이유는 문 x 가 조종석 <b>방 중심</b>에서 나오고 기둥 간격은
        /// <b>전장</b>에서 나오기 때문이다 — 둘 중 하나만 움직여도 겹치는 번호가 바뀐다.
        /// </summary>
        private static bool OverlapsPortDoorway(float x, float width)
        {
            if (!LastShiftObservationGallery.CockpitDoorwayIsOpen) return false;
            var half = (LastShiftZoneDoor.OpeningWidth + width) * 0.5f;
            return Mathf.Abs(x - LastShiftObservationGallery.CockpitLandingCenterX) < half;
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
        /// 상부 회랑(§25.4(B), §27.4). 좌표 정본은 Runtime 의
        /// <see cref="LastShiftUpperGallery"/> 이고 여기서는 판으로 세우기만 한다.
        ///
        /// <b>속이 빈 관이다.</b> 우회 덕트(<see cref="CreateBypassDuct"/>)와 같은 이유로
        /// 솔리드 큐브를 쓸 수 없다 — 콜라이더를 붙이면 회랑이 통째로 막힌 블록이 되고,
        /// 빼면 승무원이 바닥을 뚫고 떨어진다.
        ///
        /// <b>문은 여기서 안 뚫는다.</b> 회랑이 구획에 붙는 다섯 자리(§27.4)의 문은 전부
        /// 구획 쪽 면에 있고, 면 소유 규칙상 그 면은 구획이 세운다 —
        /// <see cref="ChildDoorwaysOn"/> 이 <see cref="LastShiftUpperGallery.DoorwaysOn"/> 을
        /// 같이 보는 것이 그 연결이다. 여기서 또 뚫으면 같은 평면에 판이 두 장 겹친다.
        /// </summary>
        private static void CreateUpperGallery(Transform ship)
        {
            // 구획 회색보다 살짝 푸르고 밝다. 회랑은 방이 아니라 <b>방들을 잇는 길</b>이라
            // 통로(선체 내부)와 구획 중 어느 쪽으로도 안 읽혀야 한다.
            // 중성 백색-그레이(4000K 대). 방 색(청록·주황·시안·초록)을 재사용하지 않는 것이
            // art 결정이다 — 회랑은 방이 아니라 이동 전용 이면 동선이고, 그 정체성을 색으로도
            // 분리한다. 우회 통로의 저조도 규칙도 여기엔 안 건다(실사용 동선이라 일반 통행 조도).
            galleryMaterial ??= CreateMaterial("LS_Gallery", new Color(0.62f, 0.62f, 0.60f));

            var root = new GameObject(LastShiftUpperGallery.RootName);
            root.transform.SetParent(ship, false);

            const float thickness = LastShiftUpperGallery.PanelThickness;
            const float height = LastShiftUpperGallery.InteriorHeight;
            var nearZ = LastShiftUpperGallery.NearZ;
            var farZ = LastShiftUpperGallery.FarZ;
            var runMinX = LastShiftUpperGallery.RunMinX;
            var runMaxX = LastShiftUpperGallery.RunMaxX;

            // ── 격납고 끝벽에서 구명정 위까지 달리는 긴 구간 ───────────────────────────
            CreateGalleryPlate(root.transform, "Run_Floor",
                runMinX, runMaxX + thickness, nearZ - thickness, farZ + thickness, -thickness, 0f);
            CreateGalleryPlate(root.transform, "Run_Ceiling",
                runMinX, runMaxX + thickness, nearZ - thickness, farZ + thickness, height, height + thickness);
            CreateGalleryPlate(root.transform, "Run_WallFar",
                runMinX, runMaxX + thickness, farZ, farZ + thickness, 0f, height);
            CreateGalleryPlate(root.transform, "Run_EndAft",
                runMaxX, runMaxX + thickness, nearZ, farZ, 0f, height);

            // 안쪽 벽은 다리가 붙는 x 구간을 비운다. 안 비우면 분기 셋과 강하 하나가 벽으로
            // 막혀 고리가 형상으로만 남고 실제로는 막다른 알코브 넷이 된다.
            var mouths = LastShiftUpperGallery.Legs
                .Where((_, index) => index != LastShiftUpperGallery.RunLeg)
                .OrderBy(leg => leg.MinX)
                .ToArray();
            var edge = runMinX;
            foreach (var mouth in mouths)
            {
                CreateGalleryPlate(root.transform, $"Run_WallNear_{mouth.Name}",
                    edge, mouth.MinX, nearZ - thickness, nearZ, 0f, height);
                edge = mouth.MaxX;
            }
            CreateGalleryPlate(root.transform, "Run_WallNear_End",
                edge, runMaxX, nearZ - thickness, nearZ, 0f, height);

            // ── z 로 달리는 다리 넷(분기 셋 + 강하 하나) ─────────────────────────────
            foreach (var leg in mouths)
                CreateGalleryLegAlongZ(root.transform, leg, nearZ);

            CreateGalleryDressing(root.transform);
        }

        /// <summary>
        /// 회랑 소품. <b>다리마다 부모를 따로 준다</b> — 판은 이름에 다리를 달고 있어
        /// 한 루트 밑에 있어도 안 헷갈리지만, 소품 이름은 art 가 짓고 유일성도 공간
        /// 안에서만 요구된다(<c>R0_Id</c>). 다리 다섯을 한 부모에 쏟으면 서로 다른
        /// 다리의 같은 이름이 하이어라키에서 한 자리로 겹쳐 보인다.
        ///
        /// 소품이 없는 다리에는 빈 부모를 안 만든다. 분기 셋에는 지금 아무것도 안 붙어서
        /// (개구부 프레임은 긴 구간 쪽 벽면이다) 빈 노드 셋이 그대로 남는다.
        /// </summary>
        private static void CreateGalleryDressing(Transform root)
        {
            for (var index = 0; index < LastShiftUpperGallery.LegCount; index++)
            {
                var space = LastShiftDressingSpace.OfGallery(index);
                if (!HasDressing(space)) continue;

                var legRoot = new GameObject($"Dressing_{LastShiftUpperGallery.LegAt(index).Name}");
                legRoot.transform.SetParent(root, false);
                CreateDressingProps(legRoot.transform, space);
            }
        }

        /// <summary>
        /// z 축으로 달리는 회랑 다리 하나. 구획 쪽 끝은 구획이 세운 벽이 막고(거기에 문이
        /// 뚫린다), 회랑 쪽 끝은 열려 있어야 하므로 마구리를 안 세운다.
        ///
        /// 바닥·천장이 <paramref name="nearZ"/> 가 아니라 그 <b>한 판 앞</b>에서 끝나는 것은
        /// 긴 구간의 바닥·천장이 이미 거기까지 나와 있기 때문이다 — 겹치면 같은 높이에
        /// 판 두 장이 포개져 z-fighting 이 난다.
        /// </summary>
        private static void CreateGalleryLegAlongZ(Transform root, LastShiftGalleryLeg leg, float nearZ)
        {
            const float thickness = LastShiftUpperGallery.PanelThickness;
            const float height = LastShiftUpperGallery.InteriorHeight;
            var slabMinZ = leg.MinZ + thickness;
            var slabMaxZ = nearZ - thickness;

            CreateGalleryPlate(root, $"{leg.Name}_Floor",
                leg.MinX - thickness, leg.MaxX + thickness, slabMinZ, slabMaxZ, -thickness, 0f);
            CreateGalleryPlate(root, $"{leg.Name}_Ceiling",
                leg.MinX - thickness, leg.MaxX + thickness, slabMinZ, slabMaxZ, height, height + thickness);
            CreateGalleryPlate(root, $"{leg.Name}_WallFore",
                leg.MinX - thickness, leg.MinX, leg.MinZ, nearZ, 0f, height);
            CreateGalleryPlate(root, $"{leg.Name}_WallAft",
                leg.MaxX, leg.MaxX + thickness, leg.MinZ, nearZ, 0f, height);
        }

        /// <summary>회랑 판 한 장. 덕트와 같은 이유로 세 축 전부 구간으로 받는다.</summary>
        private static void CreateGalleryPlate(Transform parent, string name,
            float minX, float maxX, float minZ, float maxZ, float minY, float maxY) =>
            CreateGalleryPlate(parent, name, minX, maxX, minZ, maxZ, minY, maxY, galleryMaterial);

        private static void CreateGalleryPlate(Transform parent, string name,
            float minX, float maxX, float minZ, float maxZ, float minY, float maxY, Material material)
        {
            if (maxX - minX <= 0.0001f || maxZ - minZ <= 0.0001f) return;
            CreateCube(name, parent,
                new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, maxY - minY, maxZ - minZ), material);
        }

        /// <summary>
        /// 좌현 관측 회랑(§29.4-(2)). 좌표 정본은 Runtime 의
        /// <see cref="LastShiftObservationGallery"/> 이고 여기서는 판으로 세우기만 한다.
        ///
        /// <b>바깥 벽을 안 세운다.</b> 회랑 바깥면이 곧 테두리 창면이라는 것이 이 회랑의
        /// 존재 이유다(§29.4-(2) 둘째 항목) — 여기에 판을 한 장 세우면 걸으면서 별을 보는
        /// 그 한 가지가 사라지고, 남는 것은 원반 자투리에 낀 관 하나다. 대신 바닥·천장
        /// 슬래브가 칸 안에서 가장 깊은 테두리 위치까지 나가 틈을 없앤다.
        ///
        /// <b>문은 여기서 안 뚫는다.</b> 양 끝 문은 선체 좌현 벽(<see cref="CreatePortSill"/>)과
        /// 화물칸 좌현 벽(<see cref="ChildDoorwaysOn"/>)에 있고, 면 소유 규칙상 그 면들은
        /// 선체와 구획이 세운다. 상부 회랑과 같은 규칙이다.
        /// </summary>
        private static void CreateObservationGallery(Transform ship)
        {
            // 상부 회랑보다 살짝 차고 어둡다. 둘 다 "방이 아닌 길" 이지만 상부 회랑은 배 안쪽
            // 이면 동선이고 이쪽은 껍질에 붙은 관측 동선이라, 같은 색을 주면 하이어라키에서
            // 어느 회랑의 판인지가 이름으로만 갈린다.
            observationMaterial ??= CreateMaterial("LS_ObservationGallery", new Color(0.56f, 0.59f, 0.63f));

            var root = new GameObject(LastShiftObservationGallery.RootName);
            root.transform.SetParent(ship, false);

            const float thickness = LastShiftObservationGallery.PanelThickness;
            const float height = LastShiftObservationGallery.InteriorHeight;
            var bands = LastShiftObservationGallery.Bands;

            for (var index = 0; index < bands.Length; index++)
            {
                var band = bands[index];
                var isArc = band.Run == LastShiftObservationGallery.ArcRun;

                // 슬래브는 회랑 양 끝에서 판 한 장만큼 더 나간다 — 마구리 판이 그 위에 선다.
                var slabMinX = index == 0 ? band.MinX - thickness : band.MinX;
                var slabMaxX = index == bands.Length - 1 ? band.MaxX + thickness : band.MaxX;

                // 안쪽 끝: 호 구간은 자기 안쪽 벽 밑까지, 착륙 구간은 선체·구획 바닥이
                // 이어받는 자리까지다. 착륙 구간에서 한 판 더 나가면 선체 갑판과 겹쳐
                // 같은 높이에 판 두 장이 포개진다.
                var slabInnerZ = isArc ? band.InnerZ + thickness : band.InnerZ;

                CreateGalleryPlate(root.transform, $"{band.Name}_Floor",
                    slabMinX, slabMaxX, band.SlabOuterZ, slabInnerZ, -thickness, 0f, observationMaterial);
                CreateGalleryPlate(root.transform, $"{band.Name}_Ceiling",
                    slabMinX, slabMaxX, band.SlabOuterZ, slabInnerZ, height, height + thickness, observationMaterial);

                if (isArc)
                    CreateGalleryPlate(root.transform, $"{band.Name}_WallInner",
                        band.MinX, band.MaxX, band.InnerZ, band.InnerZ + thickness, 0f, height, observationMaterial);
            }

            CreateObservationJunction(root.transform, "Junction_Cargo",
                LastShiftObservationGallery.ArcMinX,
                LastShiftObservationGallery.LastBandOf(LastShiftObservationGallery.CargoLandingRun),
                LastShiftObservationGallery.FirstBandOf(LastShiftObservationGallery.ArcRun));
            CreateObservationJunction(root.transform, "Junction_Cockpit",
                LastShiftObservationGallery.ArcMaxX,
                LastShiftObservationGallery.LastBandOf(LastShiftObservationGallery.ArcRun),
                LastShiftObservationGallery.FirstBandOf(LastShiftObservationGallery.CockpitLandingRun));

            var first = LastShiftObservationGallery.FirstBandOf(LastShiftObservationGallery.CargoLandingRun);
            var last = LastShiftObservationGallery.LastBandOf(LastShiftObservationGallery.CockpitLandingRun);
            CreateGalleryPlate(root.transform, "EndCap_Cargo",
                first.MinX - thickness, first.MinX, first.SlabOuterZ, first.InnerZ, 0f, height, observationMaterial);
            CreateGalleryPlate(root.transform, "EndCap_Cockpit",
                last.MaxX, last.MaxX + thickness, last.SlabOuterZ, last.InnerZ, 0f, height, observationMaterial);
        }

        /// <summary>
        /// 두 구간이 만나는 면. <b>구멍을 뚫는 것이 아니라 구멍만 남기고 메운다</b> — 착륙
        /// 구간은 선체까지 깊고 호 구간은 회랑 폭만큼 얕으므로, 그 차이만큼이 안 메우면
        /// 통째로 열린 옆구리가 된다.
        ///
        /// 아래쪽 판이 필요한 이유는 계단 때문이다. 두 칸의 바깥 끝이 한 칸 단차만큼
        /// 어긋나 있어서, 깊은 쪽 바닥이 얕은 쪽 벽 없이 그대로 노출된다.
        /// </summary>
        private static void CreateObservationJunction(Transform root, string name, float plane,
            LastShiftObservationBand west, LastShiftObservationBand east)
        {
            const float thickness = LastShiftObservationGallery.PanelThickness;
            const float height = LastShiftObservationGallery.InteriorHeight;
            var minX = plane - thickness * 0.5f;
            var maxX = plane + thickness * 0.5f;

            CreateGalleryPlate(root, $"{name}_Outer", minX, maxX,
                Mathf.Min(west.SlabOuterZ, east.SlabOuterZ),
                Mathf.Max(west.OuterZ, east.OuterZ), 0f, height, observationMaterial);
            CreateGalleryPlate(root, $"{name}_Inner", minX, maxX,
                Mathf.Min(west.InnerZ, east.InnerZ),
                Mathf.Max(west.InnerZ, east.InnerZ), 0f, height, observationMaterial);
        }

        /// <summary>
        /// 원반 외피 테두리(§26, §27.2). <see cref="LastShiftHullShell"/> 이 정본이고 여기서는
        /// 타원을 직선 판 <see cref="LastShiftHullShell.SegmentCount"/> 장으로 두른다.
        ///
        /// <b>곡면 메시를 만들지 않는다.</b> §27.7-1 이 메시·자투리 공간 구조체를 <c>art</c>
        /// 로 남겼다 — 여기서 프로시저럴 메시를 구우면 아트가 그걸 정본으로 오인하고, 그
        /// 순간 "좌표는 코드, 형상은 아트" 라는 이 프로젝트의 경계가 한 군데서만 깨진다.
        /// 그레이박스가 답해야 하는 것은 <b>평면 실루엣</b>뿐이고, 세로 프로파일(렌즈 단면)은
        /// 여기 없다.
        ///
        /// 판이 안쪽으로 들어오는 내접 근사라 구획·회랑이 그 다각형 안에 들어가는지는
        /// 이상적인 타원이 아니라 <see cref="LastShiftHullShell.InscribedContains"/> 로
        /// 검사해야 한다 — EditMode 검사가 그 자리를 잡는다.
        ///
        /// <b>판은 전부 선다.</b> 예전에는 좌현 창 구간 <c>10</c>장을 통째로 건너뛰었는데,
        /// 그 근거(배경막이 원반 안에 있어 판을 세우면 창에 회색 판만 보인다)는 §28.6-4 가
        /// 배경막을 <c>z=-22</c> 로 밀면서 사라졌다. 지금은 그 구간에 art 가 만든 개구부
        /// 프리팹이 서고(§29.4-(1)), 실루엣이 닫힌 채로 창 너머가 별이다.
        /// </summary>
        private static void CreateDiscHull(Transform ship)
        {
            discHullMaterial ??= CreateMaterial("LS_DiscHull", new Color(0.22f, 0.24f, 0.27f));

            var root = new GameObject("DiscHull");
            root.transform.SetParent(ship, false);

            var windowBay = LoadHullPrefab("LSHull_WindowBay");
            if (windowBay == null)
                Debug.LogWarning("[LastShift] LSHull_WindowBay 프리팹이 없다 — 좌현 창 구간을 예전처럼 비운다.");

            // 선수 창은 아트가 전용 부재를 만들기 전까지 좌현 것을 그대로 쓴다. 형태는 art
            // 몫이고(§7-6) tech 가 정하는 것은 개구부 좌표뿐이라, 없는 프리팹을 기다리며
            // 구멍을 안 내면 관측실은 계속 창 없는 방이다.
            var bowBay = LoadHullPrefab("LSHull_BowWindowBay");
            if (bowBay == null) bowBay = windowBay;

            for (var segment = 0; segment < LastShiftHullShell.SegmentCount; segment++)
            {
                var start = LastShiftHullShell.SegmentStart(segment);
                var end = LastShiftHullShell.SegmentStart((segment + 1) % LastShiftHullShell.SegmentCount);
                var chord = end - start;
                var middle = (start + end) * 0.5f;

                // 로컬 +x 를 현 방향에 맞춘다. y 축 회전은 +x 를 (cos, 0, -sin) 으로 보내므로
                // 부호가 뒤집힌 atan2 다 — 그냥 Atan2(dz, dx) 를 쓰면 판이 거울처럼 반대로 눕는다.
                var yaw = -Mathf.Atan2(chord.y, chord.x) * Mathf.Rad2Deg;

                if (LastShiftHullFrames.SegmentIsWindowBay(segment))
                {
                    // 프리팹이 없으면 불투명 판으로 메우지 않고 비운다. 회색 판이 창을 막는
                    // 것은 뚫린 실루엣보다 나쁘다 — 그게 애초에 이 구간을 비워 뒀던 이유다.
                    if (windowBay == null) continue;

                    // 루트가 테두리 <b>밑면</b>이다(아트 정본 §5.1 "루트 = 밑면"). 중심 높이를
                    // 주면 창이 제 높이의 절반만큼 뜬다. y·z 스케일은 1 로 둔다 — 세로로 늘이면
                    // 스텝 프로파일 비율이 세그먼트마다 달라진다.
                    var bay = (GameObject)PrefabUtility.InstantiatePrefab(windowBay, root.transform);
                    bay.name = $"WindowBay_{segment:00}";
                    bay.transform.localPosition =
                        new Vector3(middle.x, LastShiftHullShell.RimBaseY, middle.y);
                    bay.transform.localScale = new Vector3(chord.magnitude, 1f, 1f);
                    bay.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    continue;
                }

                if (LastShiftObservatoryWindow.SegmentIsBowBay(segment) && bowBay != null)
                {
                    var bay = (GameObject)PrefabUtility.InstantiatePrefab(bowBay, root.transform);
                    bay.name = $"BowWindowBay_{segment:00}";
                    bay.transform.localPosition =
                        new Vector3(middle.x, LastShiftHullShell.RimBaseY, middle.y);
                    bay.transform.localScale = new Vector3(chord.magnitude, 1f, 1f);
                    bay.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    continue;
                }

                var panel = CreateCube($"Rim_{segment:00}", root.transform,
                    new Vector3(middle.x,
                        LastShiftHullShell.RimBaseY + LastShiftHullShell.RimHeight * 0.5f,
                        middle.y),
                    new Vector3(chord.magnitude, LastShiftHullShell.RimHeight, LastShiftHullShell.PanelThickness),
                    discHullMaterial);

                panel.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }

            CreateWindowMullions(root.transform, "WindowMullions", LastShiftHullFrames.WindowMullionSeams());
            CreateWindowMullions(root.transform, "BowWindowMullions", LastShiftObservatoryWindow.BowMullionSeams());
            CreateHullFrames(root.transform);
        }

        /// <summary>원반 외피 프리팹 폴더. art 가 §28.1 에서 만든 개구부 부재가 여기 있다.</summary>
        private const string HullPrefabFolder = "Assets/DoodleUp/Prefabs/Hull";

        private static GameObject LoadHullPrefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{HullPrefabFolder}/{name}.prefab");

        /// <summary>
        /// 창 구간 멀리언. <b>세그먼트가 아니라 이음매에 선다</b> — 어디가 이음매인지는
        /// Runtime 의 <see cref="LastShiftHullFrames.WindowMullionSeams"/> 가 정한다.
        ///
        /// 회전은 그 점의 접선이다. 한쪽 세그먼트의 현 방향만 쓰면 기둥이 이웃 판과 반 칸
        /// 틀어져 이음매가 벌어져 보이므로, 인접 두 현 방향의 평균을 쓴다.
        ///
        /// <b>스케일은 건드리지 않는다.</b> 창 판은 현 길이만큼 <c>x</c> 로 늘이지만 멀리언은
        /// 기둥이라, 같이 늘이면 <c>0.16m</c> 기둥이 <c>0.8m</c> 짜리 벽이 된다(아트 정본 §3.3).
        /// </summary>
        private static void CreateWindowMullions(Transform parent, string groupName, int[] seams)
        {
            var prefab = LoadHullPrefab("LSHull_WindowMullion");
            if (prefab == null) return;
            if (seams.Length == 0) return;

            var root = new GameObject(groupName);
            root.transform.SetParent(parent, false);

            foreach (var seam in seams)
            {
                var point = LastShiftHullShell.SegmentStart(seam);
                var post = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                post.name = $"Mullion_{seam:00}";
                post.transform.localPosition =
                    new Vector3(point.x, LastShiftHullShell.RimBaseY, point.y);
                post.transform.localRotation = Quaternion.Euler(0f, SeamYaw(seam), 0f);
            }
        }

        /// <summary>이음매의 접선 방향 yaw. 인접 두 현 방향의 평균이다.</summary>
        private static float SeamYaw(int seam)
        {
            var count = LastShiftHullShell.SegmentCount;
            var here = LastShiftHullShell.SegmentStart(seam);
            var incoming = (here - LastShiftHullShell.SegmentStart((seam + count - 1) % count)).normalized;
            var outgoing = (LastShiftHullShell.SegmentStart((seam + 1) % count) - here).normalized;
            var tangent = incoming + outgoing;
            return -Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 방 벽과 외피 사이 자투리를 채우는 격벽 프레임(§27.3). 좌표 정본은 Runtime 의
        /// <see cref="LastShiftHullFrames"/> 이고, 어느 각에서 프레임이 얼마나 뻗는지는
        /// 식이 아니라 그쪽 실측이 정한다.
        ///
        /// <b>전부 콜라이더 없는 장식이다.</b> 이 바깥에는 갑판이 없어 승무원이 애초에 못
        /// 가고, 콜라이더를 남기면 저중력에서 뜬 물건이 골조에 끼어 회수가 어려워진다 —
        /// 배관 장식(<see cref="CreatePipe"/>)에 콜라이더를 뺀 것과 같은 이유다.
        /// </summary>
        private static void CreateHullFrames(Transform parent)
        {
            var frames = new GameObject("HullFrames");
            frames.transform.SetParent(parent, false);

            for (var rib = 0; rib < LastShiftHullFrames.RibCount; rib++)
            {
                if (!LastShiftHullFrames.RibIsBuildable(rib)) continue;
                var outer = LastShiftHullFrames.RibOuter(rib);
                var inner = LastShiftHullFrames.RibInner(rib);
                CreateFrameMember($"Rib_{rib:00}", frames.transform, inner, outer,
                    LastShiftHullFrames.BaseY + LastShiftHullFrames.Height * 0.5f,
                    LastShiftHullFrames.Height);
            }

            for (var segment = 0; segment < LastShiftHullShell.SegmentCount; segment++)
            {
                if (!LastShiftHullFrames.RingSegmentIsBuildable(segment)) continue;
                var start = LastShiftHullFrames.RingSegmentStart(segment);
                var end = LastShiftHullFrames.RingSegmentStart((segment + 1) % LastShiftHullShell.SegmentCount);
                CreateFrameMember($"Girth_{segment:00}", frames.transform, start, end,
                    LastShiftHullFrames.RingBeamY, LastShiftHullFrames.RibSection);
            }
        }

        /// <summary>골조 부재 하나. 두 평면 점을 잇는 판·보이고 로컬 +x 를 그 방향에 맞춘다.</summary>
        private static void CreateFrameMember(string name, Transform parent,
            Vector2 from, Vector2 to, float centreY, float height)
        {
            var span = to - from;
            var middle = (from + to) * 0.5f;
            var member = CreateDecorCube(name, parent,
                new Vector3(middle.x, centreY, middle.y),
                new Vector3(span.magnitude, height, LastShiftHullFrames.RibSection),
                discHullMaterial);
            member.transform.localRotation =
                Quaternion.Euler(0f, -Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg, 0f);
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
            // 갑판 위 배관(<c>LS_Duct</c>)보다 한 단계 어둡고 채도가 낮다. 같은 재질을 쓰면
            // 우회 통로가 본선 설비의 연장으로 읽히는데, 이 길은 <b>비용을 치르고 쓰는 임시
            // 경로</b>다. 배 안에서 이 재질이 붙는 곳은 갑판 아래뿐이다.
            bypassMaterial ??= CreateMaterial("LS_DuctBypass", new Color(0.25f, 0.24f, 0.22f));
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
            CreateBypassDressing(root.transform);
        }

        /// <summary>
        /// 우회 통로 드레싱. <b>이 길이 비싸다는 것을 색과 형태로 먼저 말한다.</b>
        ///
        /// 우회 통로의 비용은 웅크림 이동 속도와 <c>SuitOxygen</c> 소모(§5)인데, 둘 다 들어가
        /// 봐야 알 수 있는 값이다. 승강구가 갑판에 뚫린 회색 사각형이면 주 통로와 시각적으로
        /// 대등해 보이고, 그러면 승무원은 그 비용을 겪은 다음에야 배운다 — 산소 시계가 도는
        /// 중에 한 번 잘못 고른 것이 그대로 손해다.
        ///
        /// 그래서 <b>배 안에서 이 색 조합이 여기에만 있게</b> 한다. 어두운 덕트 재질
        /// (<c>LS_DuctBypass</c>)은 갑판 아래 전용이고, 경고 황색은 통로 배플 모서리·격납고
        /// 발진 구역 말고는 안 쓴다. 배플과 색을 공유하는 것은 의도다 — 둘 다 "부딪히거나
        /// 걸리는 자리" 다.
        ///
        /// <b>지오메트리와 콜라이더는 손대지 않는다.</b> 덕트 치수·경로·에어록 좌표는
        /// <see cref="LastShiftBypassDuct"/> 가 정본이고 EditMode 검사가 그 값을 직접 본다.
        /// 여기서 세우는 것은 전부 <see cref="CreateDecorCube"/> 라 콜라이더가 없다 — 승강구
        /// 문턱도 넘어가는 판이 아니라 그려진 띠이고, 관 안쪽에는 바닥 유도띠 말고 아무것도
        /// 안 둔다. 단면이 웅크림 높이 그대로라 벽에 뭘 붙이면 통행 폭이 눈으로 좁아진다.
        /// </summary>
        private static void CreateBypassDressing(Transform root)
        {
            EnsureHazardMaterial();
            laneMaterial ??= EnsureMaterial("LS_Lane", new Color(0.55f, 0.70f, 0.86f), 0.8f);

            // 갑판 위에서 보이는 것은 승강구 자리뿐이고, 그 테두리·문턱은 CreateDeckHatch 가
            // 해치와 한 덩어리로 세운다 — 여기서 또 두르면 같은 자리에 판이 겹쳐 z-fighting
            // 이 난다. 이 메서드는 갑판 아래만 맡는다.
            CreateDressingProps(root, LastShiftDressingSpace.OfBypassRun());
            CreateDressingProps(root, LastShiftDressingSpace.OfAirlock());
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
                new Vector3(maxX - minX, maxY - minY, maxZ - minZ), bypassMaterial);
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
                new Vector3(LastShiftBypassDuct.Section, LastShiftBypassDuct.StepHeight, half), bypassMaterial);

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
            CreateDecorCube("Airlock", parent, centre, new Vector3(size, size, size), bypassMaterial);

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
                    centre.z), hatch, bypassMaterial);
            CreateDecorCube("Hatch_Outer", parent,
                new Vector3(centre.x, LastShiftBypassDuct.AirlockFloorY, centre.z), hatch, bypassMaterial);
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

            // 테두리는 경고 황색이다. 배에서 이 색이 붙는 자리는 셋뿐이고(통로 배플 모서리,
            // 승강구, 격납고 발진 구역) 셋 다 "부딪히거나 걸리거나 비용을 치르는 자리" 다.
            // 승강구가 갑판에 뚫린 회색 사각형이면 주 통로와 시각적으로 대등해 보이는데,
            // 이 길의 비용(웅크림 속도·SuitOxygen 소모, §5)은 들어가 봐야 아는 값이라
            // 색이 먼저 말하지 않으면 승무원은 산소가 도는 중에 그 비용을 배운다.
            EnsureHazardMaterial();
            foreach (var sign in new[] { -1f, 1f })
            {
                // z 쪽 테두리만 문턱(coaming) 높이로 세운다. 판이 +x 로 미끄러져 열리므로
                // x 쪽에 턱을 세우면 열린 판이 그것을 뚫고 지나간다.
                var rimZ = CreateCube($"{name}_Rim_{(sign < 0f ? "Port" : "Starboard")}", hatch.transform,
                    new Vector3(0f, 0.07f, sign * (span * 0.5f + 0.06f)),
                    new Vector3(span + 0.24f, 0.14f, 0.12f), hazardMaterial);
                Object.DestroyImmediate(rimZ.GetComponent<Collider>());
                var rimX = CreateCube($"{name}_Rim_{(sign < 0f ? "Fore" : "Aft")}", hatch.transform,
                    new Vector3(sign * (span * 0.5f + 0.06f), 0.015f, 0f),
                    new Vector3(0.12f, 0.03f, span), hazardMaterial);
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
                    thickness, compartmentMaterial, openings, WindowsOn(spec, alongX, atMax));
            }

            CreateCompartmentLabel(spec, root.transform);
            CreateCompartmentDressing(spec, root.transform);

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

        /// <summary>
        /// 이 면에 뚫어야 하는 구멍의 자유축 로컬 좌표. 잠긴 자식은 구멍을 안 낸다.
        ///
        /// 자식 구획만으로는 부족하다 — 상부 회랑(§27.4)이 붙는 다섯 자리도 구획 쪽 면에
        /// 문을 요구하고, 그 면 역시 구획이 소유한다. 회랑을 <see cref="ChildrenOn"/> 의
        /// 부모-자식 사슬에 넣지 않은 것은 의도다: 회랑은 고리라서 사슬에 넣는 순간
        /// <c>ParentIndex</c> 가 트리라는 전제(<see cref="LastShiftCompartments.DoorDepth"/>)가
        /// 깨지고, 깨진 것을 고치려다 §9.4 의 "막다른 방" 이 조용히 사라진다.
        /// </summary>
        private static float[] ChildDoorwaysOn(LastShiftCompartmentSpec spec, bool alongX, bool atMax)
        {
            var origin = alongX ? spec.CenterZ : spec.CenterX;
            var face = alongX
                ? (atMax ? spec.MaxX : spec.MinX)
                : (atMax ? spec.MaxZ : spec.MinZ);
            var plane = alongX ? LastShiftDoorPlane.AlongX : LastShiftDoorPlane.AlongZ;
            var gallery = LastShiftUpperGallery.DoorwaysOn(spec.Compartment, plane, face);
            var observation = LastShiftObservationGallery.DoorwaysOn(spec.Compartment, plane, face);

            return ChildrenOn(spec, alongX, atMax)
                .Where(child => child.IsPassable)
                .Select(child => child.DoorCenter)
                .Concat(gallery)
                .Concat(observation)
                .Select(doorCenter => doorCenter - origin)
                .ToArray();
        }

        /// <summary>
        /// 벽에 뚫리는 구멍 하나. 문은 바닥부터라 <see cref="BottomY"/> 가 <c>0</c> 이고,
        /// 창은 문턱이 있어 그렇지 않다 — 둘을 같은 자료형으로 두는 것은 판을 자르는 규칙이
        /// 같기 때문이다. 다른 것은 구멍 <b>위아래에 무엇이 남는가</b>뿐이다.
        /// </summary>
        private readonly struct WallAperture
        {
            public WallAperture(float center, float halfWidth, float bottomY, float topY)
            {
                Center = center;
                HalfWidth = halfWidth;
                BottomY = bottomY;
                TopY = topY;
            }

            public float Center { get; }
            public float HalfWidth { get; }
            public float BottomY { get; }
            public float TopY { get; }
        }

        /// <summary>
        /// 이 구획 면에 뚫리는 창. 지금은 관측실 선수 끝벽 하나뿐이고, 좌표 정본은 Runtime 의
        /// <see cref="LastShiftObservatoryWindow"/> 다 — 같은 상수가 테두리 유리·골조 금지
        /// 구간도 정하므로 여기 리터럴을 두면 셋이 갈린다.
        /// </summary>
        private static WallAperture[] WindowsOn(LastShiftCompartmentSpec spec, bool alongX, bool atMax)
        {
            if (spec.Compartment != LastShiftObservatoryWindow.Compartment) return NoApertures;
            if (!alongX || atMax) return NoApertures;
            if (Mathf.Abs(spec.MinX - LastShiftObservatoryWindow.WallX) > 0.001f) return NoApertures;

            // 자유축은 z 이고 좌표는 구획 로컬이다. 창은 방 중심선에 온다.
            return new[]
            {
                new WallAperture(0f - spec.CenterZ,
                    LastShiftObservatoryWindow.OpeningWidth * 0.5f,
                    LastShiftObservatoryWindow.SillHeight,
                    LastShiftObservatoryWindow.HeadHeight)
            };
        }

        private static readonly WallAperture[] NoApertures = System.Array.Empty<WallAperture>();

        /// <summary>
        /// 판 한 장. <paramref name="openings"/> 는 이 면의 자유축 위 문 중심이고, 비어 있으면
        /// 통짜다. 구멍이 있으면 구간을 잘라 세우고 그 위에 인방을 얹는다 — 인방이 없으면
        /// 문 높이(2.2)에서 천장까지가 그대로 뚫려 그림과 통행 가능 범위가 어긋난다.
        ///
        /// <paramref name="windows"/> 는 바닥에 안 닿는 구멍이다. 판을 자르는 규칙은 문과
        /// 같고 아래에 문턱 판, 위에 인방이 한 장씩 더 붙는다.
        /// </summary>
        private static void CreateWallWithOpenings(string name, Transform parent, bool alongX,
            float plane, float freeMin, float freeMax, float height, float thickness,
            Material material, float[] openings, WallAperture[] windows = null)
        {
            const float doorWidth = LastShiftZoneDoor.OpeningWidth;
            const float doorHeight = LastShiftZoneDoor.OpeningHeight;
            windows ??= NoApertures;

            // 문과 창을 자유축 위에서 한 줄로 세워 자른다. 둘을 따로 자르면 같은 판을 두 번
            // 세우게 되고, 그건 씬에서 z-파이팅으로만 드러난다.
            var spans = openings
                .Select(opening => (Min: opening - doorWidth * 0.5f, Max: opening + doorWidth * 0.5f))
                .Concat(windows.Select(window =>
                    (Min: window.Center - window.HalfWidth, Max: window.Center + window.HalfWidth)))
                .OrderBy(span => span.Min);

            var edges = new System.Collections.Generic.List<float> { freeMin };
            foreach (var span in spans)
            {
                edges.Add(span.Min);
                edges.Add(span.Max);
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

            // 창은 위아래가 다 남는다. 문턱 판이 없으면 방에서 바닥이 그대로 우주로 이어져
            // 창이 아니라 발코니가 되고, 인방이 없으면 천장까지 뚫려 방이 반쯤 사라진다.
            for (var index = 0; index < windows.Length; index++)
            {
                var window = windows[index];
                if (window.BottomY > 0.0001f)
                    CreateSlab($"{name}_Sill_{index}", parent, alongX, plane,
                        window.Center, window.HalfWidth * 2f, window.BottomY, 0f, thickness, material);
                if (height - window.TopY > 0.0001f)
                    CreateSlab($"{name}_Head_{index}", parent, alongX, plane,
                        window.Center, window.HalfWidth * 2f, height - window.TopY, window.TopY,
                        thickness, material);
            }

            // 인방은 벽이 문보다 높을 때만 있다. 좌현 문턱 판(높이 0.6)처럼 벽 자체가 문보다
            // 낮은 자리에서는 인방 대신 그 위의 창 띠가 이어받는다 — 여기서 안 걸러 내면
            // 높이가 음수인 큐브가 서고, 그건 씬에서 안쪽이 뒤집힌 판으로 보인다.
            if (height - doorHeight <= 0.0001f) return;

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
            // 라벨은 +z 를 보는 면에 글자를 그린다. 구획 선수 쪽 벽 안쪽에 붙여 문으로 들어오는
            // 방향에서 읽히게 둔다. 색은 구획색이다 — 벽이 공통 중성색이라 라벨과 바닥 띠가
            // 이 방의 색을 말하는 유일한 자리다.
            //
            // x·y 는 여기서 안 정한다. 이 벽은 문이 뚫리는 벽이기도 해서 "방 중심" 이 곧
            // "문 한가운데" 인 구획이 다섯이고(아트 정본 §7-5), 그걸 피하는 규칙은 좌표
            // 문제라 Runtime 이 갖는다 — 여기 두면 EditMode 에서 확인이 안 된다.
            CreateZoneLabel(root.parent, LastShiftCompartmentLabels.TextOf(spec.Compartment),
                new Vector3(LastShiftCompartmentLabels.ResolveX(spec),
                    LastShiftCompartmentLabels.ResolveY(spec), spec.MinZ + 0.12f),
                LastShiftDressing.TintOf(spec.Compartment));
        }

        /// <summary>
        /// 구획 하나의 드레싱. 바닥 띠(구획색) + 소품 서넛(공통 설비색)이다.
        ///
        /// <b>벽은 물들이지 않는다.</b> 배에는 이미 압력 구역 색 넷이 있고 그것이 1차 인지
        /// 앵커다 — 구획 열한 개까지 벽 색을 가지면 색이 스물다섯 가지가 되어 구역 색이
        /// 그 안에 묻힌다. 위계를 <b>면적</b>으로 만든다: 구역은 벽·바닥, 구획은 띠·라벨.
        ///
        /// <b>잠긴 구획도 안쪽은 채운다.</b> 지금은 들어갈 수 없지만 §15.2 언락이 붙는 순간
        /// 여덟 방이 한꺼번에 열리고, 그때 비어 있으면 언락 보상이 회색 상자가 된다.
        /// 반대로 <b>바깥 면에는 아무것도 안 붙인다</b> — §17.7-3/§17.8-4 가 잠긴 문의 차폐
        /// 수준을 미결로 남겨 뒀고, 표식은 그 자체가 "정보를 흘린다" 쪽 결정이다(§21.4).
        /// 여기 있는 것은 전부 구획 볼륨 안쪽이고, 잠긴 방은 등도 없어 문틈으로 샐 빛도 없다.
        ///
        /// <b>게이지·사이렌은 없다.</b> 열한 개 전부 압력존 밖이라(§24) 상태를 말하는 계기를
        /// 달면 §17.6 이 미결로 남긴 편입 여부가 그림 쪽에서 먼저 닫힌다. 발광은 수경재배
        /// 그로우 라이트와 서버 랙 인디케이터 둘뿐이고, 둘 다 방의 <b>정체</b>를 말할 뿐
        /// 선체 상태를 말하지 않는다.
        ///
        /// 소품은 전부 콜라이더 없는 장식이다. 구획 통행 폭·문 폭은 §9.4 막다른 방 전제가
        /// 걸린 수치라 드레싱이 줄이면 안 된다.
        /// </summary>
        private static void CreateCompartmentDressing(LastShiftCompartmentSpec spec, Transform root)
        {
            // 구획 틴트는 있으면 안 덮는다. 색은 art 실값이고(브리프 §8.1) 코드 값은
            // 에셋이 아직 없을 때의 씨앗일 뿐이다 — 매 빌드 덮어쓰면 art 가 Inspector 에서
            // 고친 색이 다음 빌드에 사라지고, "데이터만 넘기면 반영된다" 가 색에서 깨진다.
            EnsureMaterial($"LS_Tint_{spec.Compartment}", LastShiftDressing.TintOf(spec.Compartment));
            EnsureFixtureMaterial();
            EnsureHazardMaterial();
            indicatorMaterial ??= EnsureMaterial("LS_ServerIndicator", new Color(0.36f, 0.94f, 0.60f), 1.1f);
            growMaterial ??= EnsureMaterial("LS_GrowLight", new Color(0.86f, 0.42f, 0.76f), 1.6f);

            CreateDressingProps(root, LastShiftDressingSpace.Of(spec.Compartment));
        }

        private static GameObject CreateDecorCube(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            var cube = CreateCube(name, parent, localPosition, scale, material);
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            return cube;
        }

        // ── 데이터 드레싱 ────────────────────────────────────────────────────────────

        private static LastShiftDressingSet dressingSet;

        /// <summary>
        /// 드레싱 데이터. <b>빌드 한 번에 한 번만 읽고, 읽는 자리에서 바로 검증한다.</b>
        /// 세운 다음 재면 위반 상태의 프리팹이 한 번은 디스크에 저장되고, 그다음 검사가
        /// 실패해도 씬은 이미 위반본이다.
        ///
        /// 에셋이 없으면 빈 리스트로 넘어가지 않고 실패한다 — 소품이 통째로 빠진 씬은
        /// 눈으로 보면 알지만 자동화 로그에서는 정상 빌드와 구분이 안 된다.
        /// </summary>
        internal static LastShiftDressingSet DressingSet
        {
            get
            {
                if (dressingSet != null) return dressingSet;

                dressingSet = AssetDatabase.LoadAssetAtPath<LastShiftDressingSet>(LastShiftDressingSet.AssetPath);
                if (dressingSet == null)
                    throw new System.InvalidOperationException(
                        $"드레싱 데이터가 없다: {LastShiftDressingSet.AssetPath}. " +
                        "메뉴 [Last Shift/SP-02A/드레싱 에셋 부트스트랩] 으로 초기값을 만들 수 있다.");

                var violations = LastShiftDressingRules.Validate(dressingSet.Props);
                if (violations.Count == 0) return dressingSet;

                foreach (var violation in violations)
                    Debug.LogError($"[LAST_SHIFT_DRESSING] {violation}");
                dressingSet = null;
                throw new System.InvalidOperationException(
                    $"드레싱 데이터가 브리프 제약을 위반한다 — 위반 {violations.Count}건. " +
                    "위 로그의 규칙 id 를 보고 에셋을 고친 뒤 다시 굽는다.");
            }
        }

        /// <summary>테스트·재빌드가 에셋 편집을 다시 읽게 한다.</summary>
        internal static void ForgetDressingSet() => dressingSet = null;

        private static bool SameSpace(LastShiftDressingSpace a, LastShiftDressingSpace b)
        {
            if (a.kind != b.kind) return false;
            return a.kind switch
            {
                LastShiftDressingSpaceKind.Zone => a.zone == b.zone,
                LastShiftDressingSpaceKind.Compartment => a.compartment == b.compartment,
                LastShiftDressingSpaceKind.Passage => a.passage == b.passage,
                LastShiftDressingSpaceKind.UpperGallery => a.galleryLeg == b.galleryLeg,
                _ => true
            };
        }

        private static bool HasDressing(LastShiftDressingSpace space) =>
            DressingSet.Props.Any(prop => prop != null && SameSpace(prop.space, space));

        /// <summary>
        /// 한 공간의 소품을 세운다. <b>자리 계산은 여기서 안 한다</b> —
        /// <see cref="LastShiftDressingSpaces.WorldCenter"/> 가 검증기와 공유하는 유일한
        /// 계산이라, 여기서 한 번 더 풀면 검사를 통과한 데이터가 위반 자리에 설 수 있다.
        ///
        /// 프리팹을 준 소품은 프리팹의 스케일을 그대로 둔다. <c>size</c> 는 경계 검사용
        /// 치수이지 스케일이 아니다 — 프리팹에 곱하면 art 가 넣은 에셋이 찌그러진다.
        /// </summary>
        private static void CreateDressingProps(Transform parent, LastShiftDressingSpace space)
        {
            foreach (var prop in DressingSet.Props)
            {
                if (prop == null || !SameSpace(prop.space, space)) continue;

                var center = LastShiftDressingSpaces.WorldCenter(prop);
                var local = parent.InverseTransformPoint(center);
                GameObject instance;
                if (prop.prefab != null)
                {
                    // 프리팹 원점은 밑면이다 — art 가 bottomY 훅에 맞춰 그렇게 만들었다
                    // (last-shift-dressing-assets-v1.md §3 "루트 = 밑면"). WorldCenter 는 중심이라
                    // 그대로 넣으면 소품이 제 높이의 절반만큼 뜬다. 박스 폴백만 중심 기준이다.
                    instance = (GameObject)PrefabUtility.InstantiatePrefab(prop.prefab, parent);
                    instance.transform.localPosition = parent.InverseTransformPoint(
                        new Vector3(center.x, LastShiftDressingSpaces.BottomY(prop), center.z));
                }
                else
                {
                    instance = CreateDecorCube(prop.id, parent, local, prop.Size,
                        prop.material != null ? prop.material : EnsureFixtureMaterial());
                }

                instance.name = prop.id;
                instance.transform.localEulerAngles = prop.eulerAngles;
            }
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

            // 천장 등은 여기서 만들지 않는다. 등기구 프리팹(LSDress_Lamp_*)이 Light 를 들고
            // 오므로 씬에서 맨 점광원을 또 만들면 같은 자리에 둘이 겹쳐 배 전체가 두 배로
            // 밝아진다. 자리·개수·색·밝기의 정본은 드레싱 세트의 Lamp 슬롯이고
            // (LastShiftDressingSeed.AddCeilingLamps), 밝기 실값은 프리팹에 박혀 있다
            // (art last-shift-dressing-assets-v1.md §3.3). 여기 남는 것은 형태 보조용
            // ambient/directional 뿐이다.
        }

        /// <summary>
        /// 경고 황색. 배에서 이 색이 붙는 자리는 셋뿐이다 — 통로 배플 모서리, 갑판 승강구,
        /// 격납고 발진 구역. 셋 다 "부딪히거나 걸리거나 비용을 치르는 자리" 이고, 다른 데
        /// 쓰기 시작하면 그 뜻이 없어진다.
        /// </summary>
        private static Material EnsureHazardMaterial() =>
            hazardMaterial ??= EnsureMaterial("LS_Hazard", new Color(0.86f, 0.62f, 0.10f));

        /// <summary>
        /// 설비 중성색. 소품 대부분이 이것을 쓴다 — 구획 벽(<c>LS_Compartment</c>)보다 밝아
        /// 실루엣이 벽에서 떨어지고, 구획색보다 채도가 낮아 색 위계를 안 건드린다.
        /// </summary>
        private static Material EnsureFixtureMaterial() =>
            fixtureMaterial ??= EnsureMaterial("LS_Fixture", new Color(0.39f, 0.40f, 0.43f));

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
            fixtureMaterial = null;
            // 에셋 편집을 다음 빌드가 다시 읽게 한다. 안 비우면 Inspector 에서 소품을 고친 뒤
            // 다시 구웠는데 도메인 리로드 전까지 옛 데이터로 서는 일이 난다.
            dressingSet = null;
            hazardMaterial = null;
            laneMaterial = null;
            bypassMaterial = null;
            frostMaterial = null;
            scorchMaterial = null;
            growMaterial = null;
            indicatorMaterial = null;
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
        /// <summary>
        /// 드레싱 재질을 <b>없을 때만</b> 만든다. 이미 있으면 색도 발광도 손대지 않는다.
        ///
        /// 구조물 재질(<see cref="CreateMaterial"/>)과 갈라 놓은 것이 이 카드의 요점 중
        /// 하나다. 선체·바닥 색은 좌표처럼 코드가 정본이라 매 빌드 덮어써야 맞지만,
        /// 드레싱 재질은 art 가 Inspector 에서 고치는 실값이다(브리프 §8.1 · §9.1) —
        /// 덮어쓰면 art 가 색을 고칠 때마다 씬을 굽는 사람이 되돌리게 되고, "데이터만
        /// 넘기면 반영된다" 는 이 시스템의 전제가 색에서만 조용히 깨진다.
        ///
        /// 코드에 남은 색은 그래서 정본이 아니라 <b>씨앗</b>이다 — 에셋이 없는 상태에서
        /// 빌드가 회색 덩어리를 뱉지 않게 하는 초기값일 뿐이다.
        /// </summary>
        private static Material EnsureMaterial(string name, Color seed, float emissionIntensity = 0f)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{name}.mat");
            if (existing != null) return existing;
            return emissionIntensity > 0f
                ? CreateEmissiveMaterial(name, seed, emissionIntensity)
                : CreateMaterial(name, seed);
        }

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
