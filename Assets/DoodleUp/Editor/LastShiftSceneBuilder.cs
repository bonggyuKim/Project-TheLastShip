using System.Collections.Generic;
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
        /// <summary>
        /// 구역 문 비주얼. 그레이박스 판 두 짝을 대체하는 아트 킷 프리팹이다(game-art 인계).
        /// 정면이 로컬 <c>+X</c>, up 이 <c>+Y</c> 라 <see cref="CreateZoneDoor"/> 의 yaw 보정을
        /// 그대로 탄다 — 이 규약이 깨지면 z 평면 문 둘만 90° 돌아간 채 서고, 정지 화면에서는
        /// 안 보인다.
        /// </summary>
        private const string DoorKitPrefabPath =
            "Assets/DoodleUp/Prefabs/LastShiftModularKit/LPK_Door_Airlock_2m.prefab";

        private static Material coolingMaterial;
        private static Material lifeSupportMaterial;
        private static Material ceilingMaterial;
        private static Material ductMaterial;
        private static Material panelMaterial;
        private static Material starMaterial;
        private static Material voidMaterial;
        private static Material compartmentMaterial;

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
        // 배 전체를 덮는 직사각형(`Length`/`Width`/`HalfLength`/`HalfWidth`)과 그 네 면
        // (`EndWallX`/`SideWallZ`/`HullFrontZ`/`HullBackZ`) 별칭이 여기 있었다. 방사형
        // 발자국은 플러스 모양이라 그런 사각형이 없다 — 좌표는 전부 방·광장 발자국에서
        // 뽑고, 그 자리는 LastShiftShipDimensions.Room*/LastShiftPlazaLayout 이 답한다.

        private const float CeilingThickness = LastShiftShipDimensions.HullThickness;

        private const float WindowSillHeight = 0.6f;
        internal const string CockpitGlassRootName = "CockpitWindowGlass";
        internal const string CockpitGlassMaterialPath = "Assets/DoodleUp/Materials/Dressing/LSD_Glass.mat";

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
        public const string SpaceSkyMaterialPath = "Assets/DoodleUp/Materials/LS_SpaceSky.mat";
        public const string SpaceSkyShaderName = "DoodleUp/Last Shift Space Sky";

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
            LastShiftModularKitImporter.AppendAssemblyIfAvailable(ship);
            // 상태 단서는 선체 프리팹과 함께 저장한다. 씬 재생성 없이도 네트워크 씬 인스턴스가
            // 같은 VFX 구성을 공유하며, 실제 상태 시스템은 LastShiftHazardVfx의 Set*만 호출한다.
            ship.AddComponent<LastShiftHazardVfx>();
            PrefabUtility.SaveAsPrefabAsset(ship, ShipPrefabPath);
            Object.DestroyImmediate(ship);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ShipPrefabPath, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShipPrefabPath);
            if (prefab == null)
                throw new System.InvalidOperationException($"{ShipPrefabPath} failed to save or import.");
            Debug.Log($"[LAST_SHIFT_SHIP_PREFAB] path={ShipPrefabPath} compartments={LastShiftCompartments.Count} " +
                      $"disc_hull={LastShiftHullShell.OverallLength:0.#}x{LastShiftHullShell.OverallWidth:0.#} " +
                      $"frame_ribs={LastShiftHullFrames.BuildableRibCount}/{LastShiftHullFrames.RibCount} " +
                      $"frame_girths={LastShiftHullFrames.BuildableRingSegmentCount}/{LastShiftHullShell.SegmentCount} " +
                      $"port_bays={LastShiftHullFrames.WindowBaySegmentCount} result=PASS");
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
            CreatePlaza(ship.transform);
            foreach (var zoneId in MainZones)
                CreateMainRoom(ship.transform, zoneId);
            // 압력 경계마다 문 하나. 벌크헤드는 광장 벽이 이미 세웠다 — 문은 그 구멍에 든다.
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                CreateBoundaryDoor($"B{boundary}", ship.transform, boundary);
            CreateCockpitWindows(ship.transform);
            CreateInstrumentPanels(ship.transform);
            CreateDucts(ship.transform);
            CreateCompartments(ship.transform);
            CreateBypassDuct(ship.transform);
            CreateCube("CockpitConsole", ship.transform, new Vector3(LastShiftShipDimensions.CockpitCenterX - 1.3f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 2.5f), cockpitMaterial);
            CreateCube("BusCabinet", ship.transform, new Vector3(LastShiftShipDimensions.PowerCenterX, 0.65f, RoomBackWallZ(LastShiftZone.Power) + 0.55f), new Vector3(1.6f, 1.3f, 0.5f), powerMaterial);
            CreateCube("LifeSupportRack", ship.transform, new Vector3(LastShiftShipDimensions.LifeSupportCenterX + 1.1f, 0.75f, RoomBackWallZ(LastShiftZone.LifeSupport) - 0.75f), new Vector3(0.8f, 1.5f, 0.8f), lifeSupportMaterial);
            CreateCoolingStack(ship.transform);
            CreateStateCues(ship.transform);
            // 구역 이름표는 <b>HUD·프롬프트와 같은 문자열</b>을 쓴다. 여기서 따로 적으면
            // 벽에는 `LIFE SUPPORT`, 화면에는 `산소실` 이 떠서 같은 방이 두 이름을 갖는다 —
            // `LastShiftZoneAtlas.ShortLabelOf` 가 이미 그 자리의 정본이다.
            //
            // <b>이름표가 붙는 벽이 방마다 다르다.</b> 일자 스파인에서는 넷 다 우현 긴 벽
            // 하나에 걸렸는데, 방사형에서는 전력실·냉각실이 z 로 갈라져 그 벽 자체가 없다.
            // 그래서 각 방의 <b>광장 반대편 끝벽</b>에 건다 — 문으로 들어오면 정면이다.
            foreach (var zoneId in MainZones)
                CreateZoneLabel(ship.transform, LastShiftZoneAtlas.ShortLabelOf(zoneId),
                    RoomLabelPosition(zoneId), MaterialOf(zoneId).color);
            return ship;
        }

        /// <summary>본선 방 넷. 광장은 여기 없다 — 방이 아니라 허브다.</summary>
        private static readonly LastShiftZone[] MainZones =
        {
            LastShiftZone.Cockpit, LastShiftZone.Power,
            LastShiftZone.Cooling, LastShiftZone.LifeSupport
        };

        private static Material MaterialOf(LastShiftZone zone) => zone switch
        {
            LastShiftZone.Cockpit => cockpitMaterial,
            LastShiftZone.Power => powerMaterial,
            LastShiftZone.Cooling => coolingMaterial,
            _ => lifeSupportMaterial
        };

        /// <summary>이 방에서 벽걸이가 붙는 면의 안쪽 z. 전폭이 방마다 달라 발자국에서 뽑는다.</summary>
        private static float RoomBackWallZ(LastShiftZone zone) =>
            zone == LastShiftZone.Power
                ? LastShiftShipDimensions.RoomMinZ(zone)
                : LastShiftShipDimensions.RoomMaxZ(zone);

        /// <summary>
        /// 구역 이름표 자리. 광장 문 반대편 끝벽 안쪽이고, 글자는 그 벽을 등지고 광장 쪽을 본다.
        /// </summary>
        private static Vector3 RoomLabelPosition(LastShiftZone zone)
        {
            const float inset = 0.13f;
            var door = LastShiftPlazaLayout.DoorOf(LastShiftPlazaLayout.RoomOf(zone));
            var room = LastShiftShipDimensions.RoomOf(zone);
            // 문 평면에서 먼 쪽 끝벽. 문이 x 평면이면 끝벽도 x 평면이다.
            if (door.PlaneIsX)
            {
                var far = Mathf.Abs(room.MinX - door.Plane) > Mathf.Abs(room.MaxX - door.Plane)
                    ? room.MinX + inset : room.MaxX - inset;
                return new Vector3(far, 2.25f, room.Corner(0).y + room.WidthZ * 0.5f);
            }

            var farZ = Mathf.Abs(room.MinZ - door.Plane) > Mathf.Abs(room.MaxZ - door.Plane)
                ? room.MinZ + inset : room.MaxZ - inset;
            return new Vector3(LastShiftShipDimensions.RoomCenterX(zone), 2.25f, farZ);
        }

        /// <summary>
        /// 중앙 광장. <b>배 전체의 허브이고 통로가 아니다</b>(조항 P-1) — 고정 방 여섯이 전부
        /// 이 정사각형의 네 변에 직결하며 경유 방이 없다.
        ///
        /// 벽 넷은 <b>자기 변에 난 문 구멍을 뺀 나머지</b>다. 방 쪽 면을 광장이 세우는 것이
        /// 면 소유 규칙(문이 향하는 쪽이 세운다)이고, 그래서 방 빌더는 광장에 면한 벽을
        /// 안 세운다 — 양쪽이 다 세우면 같은 평면에 판이 두 장 겹친다.
        ///
        /// 코어는 장식이 아니라 <c>SIMUL_ZONES ≤ 2</c> 의 성립 조건이다(§6.4). 형상·표면은
        /// 아트 소관이지만 <b>점유 자체</b>는 게임플레이 가드레일이라 그레이박스가 세운다 —
        /// 없으면 게이지 셋이 동시에 읽히는 자리가 광장 한가운데에 <c>3,688</c>점 남는다.
        /// </summary>
        private static void CreatePlaza(Transform ship)
        {
            var plaza = new GameObject("Plaza");
            plaza.transform.SetParent(ship, false);

            var footprint = LastShiftPlazaLayout.Of(LastShiftPlazaSpace.Plaza);
            CreateSpaceFloor(plaza.transform, "Floor", footprint, LastShiftZone.Cockpit);
            CreateSpaceCeiling(plaza.transform, "Ceiling", footprint);

            // 벽 넷. 변마다 그 변에 얹힌 문 중심을 모아 한 번에 자른다 — 좌현·우현 변은
            // 문이 둘씩(압력문 + 부속 생활문)이라 한 짝씩 따로 자르면 판이 겹친다.
            CreatePlazaWall(plaza.transform, "PlazaWall_Bow", true, LastShiftPlazaLayout.PlazaMinX,
                LastShiftPlazaLayout.PlazaMinZ, LastShiftPlazaLayout.PlazaMaxZ);
            CreatePlazaWall(plaza.transform, "PlazaWall_Stern", true, LastShiftPlazaLayout.PlazaMaxX,
                LastShiftPlazaLayout.PlazaMinZ, LastShiftPlazaLayout.PlazaMaxZ);
            CreatePlazaWall(plaza.transform, "PlazaWall_Port", false, LastShiftPlazaLayout.PlazaMinZ,
                LastShiftPlazaLayout.PlazaMinX, LastShiftPlazaLayout.PlazaMaxX);
            CreatePlazaWall(plaza.transform, "PlazaWall_Starboard", false, LastShiftPlazaLayout.PlazaMaxZ,
                LastShiftPlazaLayout.PlazaMinX, LastShiftPlazaLayout.PlazaMaxX);

            CreateCube("PlazaCore", plaza.transform,
                new Vector3(0f, CeilingInnerHeight * 0.5f, 0f),
                new Vector3(LastShiftPlazaLayout.CoreHalfExtent * 2f, CeilingInnerHeight,
                    LastShiftPlazaLayout.CoreHalfExtent * 2f), hullMaterial);
        }

        /// <summary>광장 한 변. 그 변 위에 얹힌 문 전부를 구멍으로 남긴다.</summary>
        private static void CreatePlazaWall(Transform plaza, string name, bool alongX,
            float plane, float freeMin, float freeMax)
        {
            var openings = LastShiftPlazaLayout.Doors
                .Where(door => door.PlaneIsX == alongX && Mathf.Abs(door.Plane - plane) < 0.0001f)
                .Select(door => door.Center)
                .OrderBy(center => center)
                .ToArray();

            CreateWallWithOpenings(name, plaza, alongX, plane, freeMin, freeMax,
                CeilingInnerHeight, LastShiftShipDimensions.HullThickness, hullMaterial, openings);
        }

        /// <summary>
        /// 본선 방 하나. 광장에 면한 벽은 <b>안 세운다</b> — 그 면과 문 구멍은 광장이 소유한다.
        /// 나머지 세 면과 바닥·천장을 세운다.
        /// </summary>
        private static void CreateMainRoom(Transform ship, LastShiftZone zoneId)
        {
            var space = LastShiftPlazaLayout.RoomOf(zoneId);
            var footprint = LastShiftPlazaLayout.Of(space);
            var door = LastShiftPlazaLayout.DoorOf(space);

            var room = new GameObject("Room_" + space);
            room.transform.SetParent(ship, false);

            CreateSpaceFloor(room.transform, "Floor", footprint, zoneId);
            CreateSpaceCeiling(room.transform, "Ceiling", footprint);

            // 네 면 중 문이 얹힌 면만 건너뛴다. 판정을 좌표 비교로 두는 것이 요점이다 —
            // "선미 면" 같은 이름으로 두면 발자국이 광장 반대편으로 옮겨갈 때 방이 안 닫힌다.
            CreateRoomWall(room.transform, "Wall_Bow", true, footprint.MinX,
                footprint.MinZ, footprint.MaxZ, door);
            CreateRoomWall(room.transform, "Wall_Stern", true, footprint.MaxX,
                footprint.MinZ, footprint.MaxZ, door);
            // 조종석 좌현 벽만 창 띠를 문다. 창은 방 벽의 구멍이지 별도 오브젝트가 아니라
            // 벽 빌더에 같이 넘긴다 — 통짜 벽을 세운 뒤 그 위에 창을 얹으면 문턱 판과 벽이
            // 같은 평면에서 겹친다.
            CreateRoomWall(room.transform, "Wall_Port", false, footprint.MinZ,
                footprint.MinX, footprint.MaxX, door,
                zoneId == LastShiftZone.Cockpit ? CockpitWindowBand() : null);
            CreateRoomWall(room.transform, "Wall_Starboard", false, footprint.MaxZ,
                footprint.MinX, footprint.MaxX, door);
        }

        private static void CreateRoomWall(Transform room, string name, bool alongX, float plane,
            float freeMin, float freeMax, in LastShiftPlazaDoor door, WallAperture[] windows = null)
        {
            if (door.PlaneIsX == alongX && Mathf.Abs(door.Plane - plane) < 0.0001f) return;
            CreateWallWithOpenings(name, room, alongX, plane, freeMin, freeMax,
                CeilingInnerHeight, LastShiftShipDimensions.HullThickness, hullMaterial,
                System.Array.Empty<float>(), windows);
        }

        /// <summary>
        /// 공간 하나의 바닥. 승강구가 그 공간 안에 있으면 액자형 넉 장으로 두른다 —
        /// 우회 통로 진입점이 조종석·산소실 방 안이라 그 둘만 해당한다.
        /// </summary>
        private static void CreateSpaceFloor(Transform parent, string name,
            in LastShiftPlazaFootprint footprint, LastShiftZone zoneId)
        {
            const float thickness = LastShiftShipDimensions.HullThickness;
            floorMaterial ??= CreateMaterial("LS_Floor", new Color(0.30f, 0.32f, 0.35f));

            if (!LastShiftBypassDuct.TryShaftInZone(zoneId, out var mouth) ||
                !footprint.Contains(mouth.x, mouth.z))
            {
                CreateCube(name, parent,
                    new Vector3(footprint.MinX + footprint.LengthX * 0.5f, -thickness * 0.5f,
                        footprint.MinZ + footprint.WidthZ * 0.5f),
                    new Vector3(footprint.LengthX, thickness, footprint.WidthZ), floorMaterial);
                return;
            }

            var half = LastShiftDeckHatch.OpeningSpan * 0.5f;
            CreateFloorSlab(parent, name + "_Fore", footprint.MinX, mouth.x - half, footprint.MinZ, footprint.MaxZ);
            CreateFloorSlab(parent, name + "_Aft", mouth.x + half, footprint.MaxX, footprint.MinZ, footprint.MaxZ);
            CreateFloorSlab(parent, name + "_ShaftPort", mouth.x - half, mouth.x + half, footprint.MinZ, mouth.z - half);
            CreateFloorSlab(parent, name + "_ShaftStarboard", mouth.x - half, mouth.x + half, mouth.z + half, footprint.MaxZ);
        }

        /// <summary>
        /// 공간 하나의 천장. 닫아야 하는 이유는 두 가지다 — "우주선 안" 이 읽히려면 위가 막혀
        /// 있어야 하고, 저중력에서 뜬 물건이 위로 빠져나가 <c>ItemSafetyBounds</c> 복구를
        /// 계속 밟는 것을 막아야 한다.
        /// </summary>
        private static void CreateSpaceCeiling(Transform parent, string name,
            in LastShiftPlazaFootprint footprint)
        {
            ceilingMaterial ??= CreateMaterial("LS_Ceiling", new Color(0.21f, 0.23f, 0.26f));
            var centerX = footprint.MinX + footprint.LengthX * 0.5f;
            var centerZ = footprint.MinZ + footprint.WidthZ * 0.5f;
            CreateCube(name, parent,
                new Vector3(centerX, CeilingInnerHeight + CeilingThickness * 0.5f, centerZ),
                new Vector3(footprint.LengthX + CeilingThickness * 2f, CeilingThickness,
                    footprint.WidthZ + CeilingThickness * 2f), ceilingMaterial);

            // 천장 리브. 평평한 판만 있으면 실내가 아니라 뚜껑처럼 보인다. 개수가 아니라
            // 간격을 고정한다 — 개수를 고정하면 방마다 리브 간격이 달라져 같은 배로 안 읽힌다.
            const float ribSpacing = 1.8f;
            var ribCount = Mathf.FloorToInt((footprint.LengthX - ribSpacing) / ribSpacing);
            var ribStart = centerX - (ribCount - 1) * ribSpacing * 0.5f;
            for (var index = 0; index < ribCount; index++)
                CreateDecorCube(name + "Rib_" + index, parent,
                    new Vector3(ribStart + index * ribSpacing, CeilingInnerHeight - 0.06f, centerZ),
                    new Vector3(0.18f, 0.12f, footprint.WidthZ), hullMaterial);
        }

        /// <summary>
        /// 압력 경계의 문 하나. <b>벌크헤드는 여기서 안 세운다</b> — 광장 벽이 이미 구멍만
        /// 남기고 다 세웠고, 인방도 <see cref="CreateWallWithOpenings"/> 가 얹었다. 일자
        /// 스파인에서 벌크헤드와 문이 한 함수였던 것은 경계 평면이 방 사이 허공이라 그 판을
        /// 세울 주인이 따로 없었기 때문이고, 방사형에서는 광장 벽이 그 주인이다.
        /// </summary>
        private static void CreateBoundaryDoor(string side, Transform ship, int boundary)
        {
            CreateZoneDoor("ZoneDoor_" + side, ship, boundary, LastShiftZoneAtlas.BoundaryDoor(boundary));
        }

        /// <summary>
        /// 미닫이 문 하나. 판 두 짝이 가운데에서 만나 닫히고, 열리면 각각 옆벽 뒤로 물러난다.
        /// 판에는 콜라이더를 두지 않고 별도 차단 콜라이더 하나로 통행을 막는다 — 움직이는
        /// 콜라이더로 막으면 CharacterController 가 판에 끼거나 밀려나서, 확인하려는 것
        /// ("닫힌 문은 못 지나간다")이 아니라 밀림 현상이 먼저 보인다.
        /// </summary>
        private static void CreateZoneDoor(string name, Transform ship, int boundary,
            in LastShiftPlazaDoor plazaDoor)
        {
            const float thickness = LastShiftZoneDoor.PanelThickness;
            const float opening = LastShiftZoneDoor.OpeningWidth;
            const float openingHeight = LastShiftZoneDoor.OpeningHeight;

            var door = new GameObject(name);
            door.transform.SetParent(ship, false);

            // <b>문틀을 회전으로 축에 맞춘다.</b> 압력문 셋 중 둘(전력실·냉각실)이 z 평면에
            // 서므로 로컬 좌표를 축마다 다시 쓰면 판·문틀·차단 콜라이더·인방 여섯 자리가
            // 전부 갈래를 갖는다 — 그중 하나만 빠져도 그 조각이 구멍에서 어긋난 채 조용히
            // 통과한다. yaw 90° 로 세우면 <see cref="LastShiftZoneDoor"/> 의 로컬 계산
            // (판이 로컬 z 로 물러나고 법선이 로컬 x 다)이 한 벌로 끝난다.
            if (!plazaDoor.PlaneIsX)
                door.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            // 문 오브젝트 자체를 개구부 중심에 놓는다. 판·문틀·차단 콜라이더는 이 아래에서
            // 로컬 대칭으로 두면 되고, LastShiftZoneDoor 가 매 프레임 다시 쓰는 판 위치도
            // 로컬이라 그대로 따라온다. 자식마다 중심 z 를 더하는 방식으로 짜면 여섯 자리
            // 중 하나만 빠져도 그 조각이 구멍에서 어긋난 채 조용히 통과한다.
            door.transform.localPosition = new Vector3(plazaDoor.Waypoint.x, 0f, plazaDoor.Waypoint.y);

            // <b>비주얼은 여기서 안 만든다</b>(2026-08-11, 결정 A). 문 킷은 정본 지도가 소유하고
            // LastShiftModularKitImporter 가 <space.id>Door 로 세운다. 여기서 또 인스턴스화하면
            // 문 자리마다 킷이 두 벌이 되고, 좌표도 JSON 과 경계표 두 곳에서 나온다.
            // 이 함수가 만드는 것은 통행 판정(차단 콜라이더)과 상태기(LastShiftZoneDoor)뿐이고,
            // 판을 움직이는 Animator 는 컴포넌트가 런타임에 킷에서 찾아 문다 —
            // 조립 순서(배를 굽고 나서 킷을 임포트한다)상 빌드 시점에는 아직 없기 때문이다.

            var blockerObject = new GameObject($"{name}_Blocker");
            blockerObject.transform.SetParent(door.transform, false);
            blockerObject.transform.localPosition = new Vector3(0f, openingHeight * 0.5f, 0f);
            var blocker = blockerObject.AddComponent<BoxCollider>();
            blocker.size = new Vector3(thickness, openingHeight, opening);
            blocker.enabled = false;

            door.AddComponent<LastShiftZoneDoor>().Configure(boundary, (Animator)null, blocker);
        }
        /// <summary>
        /// 조종석 좌현 창과 그 너머 별. 별은 실제 스카이박스 대신 창 밖에 놓은 점 격자다.
        /// 스카이박스 자산을 요구하지 않고도 "밖은 우주"가 읽히고, 창 프레임이 시야를 잘라
        /// 주므로 격자라는 것이 드러나지 않는다.
        ///
        /// <b>창이 선체 긴 벽에서 조종석 방 벽으로 옮겨 왔다.</b> 일자 스파인에서는 좌현 벽
        /// 하나가 전장 <c>38m</c> 를 달려 네 방이 그 창을 나눠 썼는데, 방사형에는 그런 벽이
        /// 없다. 창을 광장 좌현 변에 두는 안은 기각했다 — 그 변은 전력실·에어록 홀이
        /// 이미 다 먹었고(§5.1 자유면이 <c>x [3,6]</c> 한 구간뿐이다), 남는 <c>3m</c> 에
        /// 창을 뚫으면 확장 여섯 자리 중 하나가 창으로 사라진다.
        ///
        /// 조종석 좌현 벽(<c>z = -3</c>, <c>x [-14,-6]</c>)이 답인 이유는 배경막이다 —
        /// <see cref="LastShiftHullFrames.WindowBackdropZ"/> 가 <c>z = -22</c> 라 이 벽이
        /// 정면으로 그것을 본다. 그리고 조종석은 배에서 가장 오래 머무는 방이다.
        /// </summary>
        private static void CreateCockpitWindows(Transform ship)
        {
            // Skybox는 환경 셰이더가 전담한다. 창 안쪽에 Plane/Box로 만든 별·은하수 메시를
            // 놓으면 무한 거리 배경이 아니라 실내 소품처럼 보이므로 여기서는 만들지 않는다.

            // 벽 개구부만 있고 유리가 없던 상태는 창이 깨진 것처럼 읽혔다. 드레싱 키트의
            // 반투명 유리를 같은 개구부 정본으로 다시 세워, 벽/유리가 서로 다른 좌표를
            // 믿다가 틈이 생기는 일을 막는다. 장식 메시라 콜라이더는 두지 않는다.
            var glass = AssetDatabase.LoadAssetAtPath<Material>(CockpitGlassMaterialPath);
            if (glass == null)
                throw new System.InvalidOperationException($"조종석 유리 재질이 없다: {CockpitGlassMaterialPath}");

            var glassRoot = new GameObject(CockpitGlassRootName);
            glassRoot.transform.SetParent(ship, false);
            var apertures = CockpitWindowBand();
            var wallZ = LastShiftShipDimensions.RoomMinZ(LastShiftZone.Cockpit);
            const float glassThickness = 0.025f;
            for (var index = 0; index < apertures.Length; index++)
            {
                var aperture = apertures[index];
                var height = aperture.TopY - aperture.BottomY;
                CreateDecorCube($"CockpitGlass_{index:00}", glassRoot.transform,
                    new Vector3(aperture.Center, aperture.BottomY + height * 0.5f,
                        wallZ - glassThickness * 0.5f),
                    new Vector3(aperture.HalfWidth * 2f, height, glassThickness), glass);
            }
        }

        /// <summary>
        /// 조종석 좌현 벽에 뚫리는 창 띠. 판 사이 기둥은 <see cref="CreateWallWithOpenings"/> 가
        /// 남긴 벽 조각이 그대로 맡는다 — 기둥을 따로 세우던 옛 코드는 창을 통짜로 뚫어
        /// 놓고 그 위에 기둥을 얹는 구성이었고, 그러면 기둥과 문턱 판이 같은 평면에서 겹쳤다.
        ///
        /// 유리는 <see cref="CreateCockpitWindows"/> 가 이 배열을 그대로 사용해 세운다.
        /// 개구부와 유리가 같은 정본을 써야 벽을 옮긴 뒤에도 깨진 듯한 틈이 생기지 않는다.
        /// </summary>
        private static WallAperture[] CockpitWindowBand()
        {
            const float windowTop = 2.1f;
            const float mullionWidth = 0.35f;
            const float margin = 0.2f;

            var minX = LastShiftShipDimensions.RoomMinX(LastShiftZone.Cockpit) + margin;
            var maxX = LastShiftShipDimensions.RoomMaxX(LastShiftZone.Cockpit) - margin;

            // 창 한 짝의 크기를 고정하고 개수를 방 길이에서 뽑는다. 개수를 고정하면 방이
            // 길어질 때 창 하나가 통유리가 된다.
            const float paneWidth = 2.2f;
            var span = maxX - minX;
            var paneCount = Mathf.Max(1, Mathf.FloorToInt((span + mullionWidth) / (paneWidth + mullionWidth)));
            var used = paneCount * paneWidth + (paneCount - 1) * mullionWidth;
            var cursor = minX + (span - used) * 0.5f;

            var band = new WallAperture[paneCount];
            for (var index = 0; index < paneCount; index++)
            {
                band[index] = new WallAperture(cursor + paneWidth * 0.5f, paneWidth * 0.5f,
                    WindowSillHeight, windowTop);
                cursor += paneWidth + mullionWidth;
            }
            return band;
        }


        /// <summary>
        /// 계기·콘솔 패널. 벽면이 완전히 비어 있으면 큐브 상자로 읽히므로, 각 구역 벽에
        /// 패널과 발광 계기 띠를 붙여 "장비가 있는 실내"로 만든다.
        /// </summary>
        private static void CreateInstrumentPanels(Transform ship)
        {
            panelMaterial ??= CreateMaterial("LS_Panel", new Color(0.14f, 0.16f, 0.19f));

            // 방마다 벽 패널 한 짝. <b>붙는 벽이 방마다 다르다</b> — 일자 스파인에서는 넷 다
            // 우현 긴 벽 하나를 공유했는데, 방사형에서는 그 벽 자체가 없다. 각 방의 자기
            // 벽에서 뽑으므로 발자국이 움직이면 패널이 따라온다.
            foreach (var zoneId in MainZones)
                CreateWallPanel($"Panel_{zoneId}", ship,
                    new Vector3(LastShiftShipDimensions.RoomCenterX(zoneId), 1.55f, RoomPanelZ(zoneId)),
                    new Vector3(3.2f, 1.1f, 0.12f), MaterialOf(zoneId).color);

            // 배 양 끝의 세로 패널. 조종석 선수 벽과 산소실 선미 벽이고, 둘 사이가 28m 라
            // 각 방 안에서만 보인다.
            CreateWallPanel("Panel_BowWall", ship,
                new Vector3(LastShiftShipDimensions.RoomMinX(LastShiftZone.Cockpit) + 0.06f, 1.7f, -0.9f),
                new Vector3(0.12f, 1.0f, 2.2f), cockpitMaterial.color);
            CreateWallPanel("Panel_SternWall", ship,
                new Vector3(LastShiftShipDimensions.RoomMaxX(LastShiftZone.LifeSupport) - 0.06f, 1.7f, -0.9f),
                new Vector3(0.12f, 1.0f, 2.2f), lifeSupportMaterial.color);
        }

        /// <summary>
        /// 이 방에서 벽걸이가 붙는 면의 <b>안쪽</b> z. 전력실만 좌현(<c>z-</c>)으로 열려 있어
        /// 부호가 반대다 — 넷을 한 상수로 두면 전력실 패널이 벽 바깥에 선다.
        /// </summary>
        private static float RoomPanelZ(LastShiftZone zone) =>
            zone == LastShiftZone.Power
                ? LastShiftShipDimensions.RoomMinZ(zone) + 0.06f
                : LastShiftShipDimensions.RoomMaxZ(zone) - 0.06f;

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
            CreateCube("CoolingStack", ship, new Vector3(centerX, 0.90f, RoomBackWallZ(LastShiftZone.Cooling) - 0.60f),
                new Vector3(2.2f, 1.8f, 0.6f), coolingMaterial);
            // 방열 핀. 판 하나짜리 상자는 어느 방에 놔도 같아 보이므로, 실루엣에 결을 준다.
            for (var index = 0; index < 5; index++)
                CreateDecorCube($"CoolingStack_Fin_{index}", ship,
                    new Vector3(centerX - 0.8f + index * 0.4f, 1.85f, RoomBackWallZ(LastShiftZone.Cooling) - 0.60f),
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
            // 조종석↔산소실 축을 가로지르는 주 배관 두 줄. 캡슐 y 스케일이 반길이다.
            // <b>기준이 광장이 아니라 방 끝이다</b> — 광장 반폭(6m)으로 잡으면 관이 광장에서
            // 끊겨 방 천장이 비고, 그 두 방이 배에서 가장 오래 머무는 자리다.
            var mainHalfLength = LastShiftShipDimensions.RoomMaxX(LastShiftZone.LifeSupport) - 0.3f;
            CreatePipe("Duct_Main_Fore", ship, new Vector3(0f, CeilingInnerHeight - 0.42f, -LastShiftShipDimensions.HalfWidth * 0.31f), new Vector3(0f, 0f, 90f), 0.16f, mainHalfLength);
            CreatePipe("Duct_Main_Aft", ship, new Vector3(0f, CeilingInnerHeight - 0.42f, LastShiftShipDimensions.HalfWidth * 0.33f), new Vector3(0f, 0f, 90f), 0.13f, mainHalfLength);
            // 벽으로 내려가는 수직 지관. 벽 패널(폭 3.2, 방 중심) 양옆 빈 구간에 둔다.
            // 패널 위에 겹치면 발광 계기 띠를 가려 정면에서 관이 계기판을 관통한 것처럼 보인다.
            foreach (var zone in MainZones)
            {
                var center = LastShiftShipDimensions.RoomCenterX(zone);
                var riserZ = RoomPanelZ(zone) + (zone == LastShiftZone.Power ? 0.16f : -0.16f);
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

        // 끝벽 빌더(`CreateEndWall`)·좌현 문턱(`CreatePortSill`)·창 기둥 회피
        // (`OverlapsPortDoorway`) 셋이 여기 있었다. 셋 다 <b>배가 직사각형 하나였을 때</b>의
        // 함수다 — 선체에 직결하는 구획의 문은 "선체 끝벽" 에 났고, 그 벽을 세울 주인이
        // 선체 자신이었다.
        //
        // 방사형에서는 그 자리가 <b>광장 벽</b>이다. 부속 둘(에어록 홀·숙소)의 문이 광장 변
        // 위에 있고, <see cref="CreatePlazaWall"/> 이 그 변에 얹힌 문 전부를 한 번에 잘라
        // 구멍으로 남긴다. 면 소유 규칙은 그대로다 — 바뀐 것은 소유자가 누구인가뿐이다.

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
            // 바닥은 에어록 자리를 비운다 — 안 비우면 안쪽 해치를 열어도 판이 그대로 남아
            // 에어록으로 못 내려간다. 구멍은 <c>L</c> 자 모서리 한 칸(<c>Section</c> 정사각형)이고
            // 그 자리가 곧 에어록 중심이라, 닫힌 안쪽 해치의 차단면이 그 칸을 그대로 메운다.
            CreateDuctPlate(root.transform, "Run_Floor", foreX + half, runMaxX, runZ - half, runZ + half, floor - thickness, floor);
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
            CreateOutboard(ship);
        }

        /// <summary>
        /// 선외 — 보행 그리드와 잔해(<c>outboard-outpost-and-map-final-v1.md</c> §4.1·§4.2).
        ///
        /// <b>별도 씬이 아니라 같은 씬의 에어록 바깥이다</b>(§4.1). 그래서 배 루트 아래에
        /// 그대로 붙고, 좌표 정본은 <see cref="LastShiftSalvage"/> 와
        /// <see cref="LastShiftAirlock.OutsideWalkY"/> 다.
        ///
        /// <b>보행 그리드가 새 이동 동사를 없앤다.</b> 선외에 바닥이 없으면 저중력이 그대로
        /// 걸려 나가는 즉시 떨어지고, 그러면 추진·유영이라는 동사를 새로 만들어야 한다.
        /// 바깥 해치와 같은 평면에 띠 하나를 깔면 <see cref="LastShiftPlayerController"/> 가
        /// 배 안에서 하던 그대로 걷는다 — 연출은 우주복 자력 부츠가 잡는다.
        /// </summary>
        private static void CreateOutboard(Transform ship)
        {
            var root = new GameObject("Outboard");
            root.transform.SetParent(ship, false);

            var walkMaterial = CreateMaterial("LS_EvaGrid", new Color(0.30f, 0.34f, 0.38f));
            var airlock = LastShiftAirlock.ReturnPoint;
            var field = LastShiftSalvage.FieldCenter;
            var span = new Vector2(field.x - airlock.x, field.z - airlock.z);
            const float laneWidth = LastShiftBypassDuct.AirlockSize;
            const float plateThickness = LastShiftBypassDuct.PanelThickness;

            // 에어록 바로 아래 발판. 나가는 순간 발이 닿는 자리라 띠보다 넓게 잡는다 —
            // 좁으면 감압이 끝나고 내려선 첫 걸음이 곧바로 판 밖이다.
            CreateCube("Outboard_AirlockPad", root.transform,
                new Vector3(airlock.x, airlock.y - plateThickness * 0.5f, airlock.z),
                new Vector3(laneWidth * 2f, plateThickness, laneWidth * 2f), walkMaterial);

            // 잔해까지의 띠. 한 장으로 깔고 방향만 돌린다 — 꺾이면 "어디로 가야 하는가" 가
            // 갈래가 되고, 첫 EVA 에서 길을 찾게 만들 이유가 없다(§5.2-3).
            var lane = CreateCube("Outboard_Lane", root.transform,
                new Vector3((airlock.x + field.x) * 0.5f, airlock.y - plateThickness * 0.5f,
                    (airlock.z + field.z) * 0.5f),
                new Vector3(span.magnitude, plateThickness, laneWidth), walkMaterial);
            lane.transform.localRotation = Quaternion.Euler(0f, -Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg, 0f);

            CreateSalvageField(root.transform, walkMaterial);
        }

        /// <summary>
        /// 잔해 덩어리. 조각 <see cref="LastShiftSalvage.ChunksPerField"/>개가 코어 둘레에
        /// 붙어 있고, 뜯을 때마다 뒤에서부터 꺼진다 — 남은 수가 눈으로 읽히면서도 저중력에서
        /// 떠다니는 물체가 하나도 안 생긴다(<see cref="LastShiftSalvageField"/> 주석).
        /// </summary>
        private static void CreateSalvageField(Transform parent, Material walkMaterial)
        {
            var centre = LastShiftSalvage.FieldCenter;
            var root = new GameObject("SalvageField");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = centre;

            // 잔해 앞 발판. 뜯는 동안 서 있을 자리이고 사거리(2.2m)를 덮는다.
            var padSpan = LastShiftSalvage.HarvestReach * 2f;
            CreateCube("Salvage_Pad", root.transform,
                new Vector3(0f, -LastShiftBypassDuct.PanelThickness * 0.5f, 0f),
                new Vector3(padSpan, LastShiftBypassDuct.PanelThickness, padSpan), walkMaterial);

            var coreMaterial = CreateMaterial("LS_SalvageCore", new Color(0.34f, 0.33f, 0.32f));
            var core = CreateCube("Salvage_Core", root.transform, new Vector3(0f, 1.1f, 0f),
                new Vector3(2.2f, 2.2f, 2.2f), coreMaterial);

            var chunkMaterial = CreateMaterial("LS_SalvageChunk", LastShiftSalvageField.ColorOf(LastShiftSalvageKind.Cooling));
            var chunks = new Transform[LastShiftSalvage.ChunksPerField];
            var tinted = new Renderer[LastShiftSalvage.ChunksPerField];
            for (var index = 0; index < chunks.Length; index++)
            {
                var angle = Mathf.PI * 2f * index / chunks.Length;
                var chunk = CreateCube($"Salvage_Chunk_{index}", root.transform,
                    new Vector3(Mathf.Cos(angle) * 1.5f, 1.1f + Mathf.Sin(angle) * 0.5f, Mathf.Sin(angle) * 1.5f),
                    new Vector3(0.8f, 0.8f, 0.8f), chunkMaterial);
                // 조각은 부딪히는 것이 아니라 뜯는 것이다. 콜라이더를 남기면 잔해 앞에서
                // 몸이 먼저 걸려 사거리 판정(좌표)과 손이 닿는 느낌이 어긋난다.
                Object.DestroyImmediate(chunk.GetComponent<Collider>());
                chunk.transform.localRotation = Quaternion.Euler(angle * Mathf.Rad2Deg, angle * 30f, 15f);
                chunks[index] = chunk.transform;
                tinted[index] = chunk.GetComponent<Renderer>();
            }

            Object.DestroyImmediate(core.GetComponent<Collider>());
            root.AddComponent<LastShiftSalvageField>().Configure(chunks, tinted);
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
        /// <b>바닥에 단을 두지 않는다.</b> §23.6 은 단을 권고했고 한 번 세웠는데, 승강구 발밑은
        /// 한 변이 <see cref="LastShiftBypassDuct.Section"/>(<c>0.9m</c>)인 정사각형뿐이다 —
        /// 단을 밟고 선 승무원은 머리가 덕트 천장 위로 나오고, 단에서 내려설 자리가 캡슐 지름
        /// (<c>0.56m</c>)보다 좁아 단과 천장 사이에 낀다. 실제로 "승강구까지는 내려가는데
        /// 웅크려도 통로로 안 들어가진다" 가 여기서 났다. 단이 없어도 상승 <c>1.2m</c> 는
        /// 점프 정점(<c>1.49m</c>) 안이라 §23.6 의 결론은 그대로다.
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

            // 해치 두 짝. <b>이제 열린다</b> — 상태 정본은 LastShiftAirlock 이고 판은
            // LastShiftAirlockHatch 가 그 값을 향해 따라가기만 한다(승강구 해치와 같은 구조).
            //
            // 안쪽 해치는 덕트 바닥 판 <b>바로 아래</b>에 매단다. 예전처럼 덕트 바닥과 같은 y 에
            // 두면 판과 겹쳐 z-fighting 이 난다.
            //
            // 안쪽 해치는 <b>덕트 바닥 판에 뚫어 둔 칸을 그대로 메운다</b> — 판 폭이 통로 단면
            // (0.9m)이라 그보다 넓은 문짝을 달면 관 밖으로 삐져나오고, 좁게 달면 닫아도 발밑에
            // 틈이 남는다. 그래서 안쪽만 단면 치수이고 바깥쪽은 문 개구 치수 그대로다.
            var innerSpan = LastShiftAirlockHatch.SpanOf(LastShiftAirlockSide.Inner);
            var innerRootY = LastShiftBypassDuct.AirlockCeilingY - LastShiftBypassDuct.PanelThickness
                - LastShiftAirlockHatch.PanelThickness * 0.5f;
            CreateAirlockHatch(parent, LastShiftAirlockSide.Inner,
                new Vector3(centre.x, innerRootY, centre.z),
                new Vector3(innerSpan, LastShiftAirlockHatch.PanelThickness, innerSpan),
                new Vector3(innerSpan, LastShiftBypassDuct.PanelThickness, innerSpan),
                LastShiftBypassDuct.FloorY - LastShiftBypassDuct.PanelThickness * 0.5f - innerRootY);

            var outerSpan = LastShiftAirlockHatch.SpanOf(LastShiftAirlockSide.Outer);
            CreateAirlockHatch(parent, LastShiftAirlockSide.Outer,
                new Vector3(centre.x, LastShiftBypassDuct.AirlockFloorY, centre.z),
                new Vector3(outerSpan, LastShiftAirlockHatch.PanelThickness, outerSpan),
                new Vector3(outerSpan, LastShiftShipDimensions.HullThickness, outerSpan), 0f);

            // 에어록 계단. 안쪽 해치가 열리면 최저점이 에어록 바닥으로 3m 내려가는데, 그
            // 3m 는 점프 정점(1.49m)으로 못 오른다 — 단 둘이면 한 걸음이 1m 로 갈린다.
            // 사다리(새 조작 동사)를 안 만드는 것은 §23.6 이 승강구에서 내린 것과 같은 결정이고,
            // LastShiftBypassDuct.RecoveryRise 가 그 성질을 코드로 말한다.
            var stepSpan = LastShiftZoneDoor.OpeningWidth * 0.7f;
            for (var step = 1; step <= LastShiftBypassDuct.AirlockStepCount; step++)
            {
                var top = LastShiftBypassDuct.AirlockFloorY + LastShiftBypassDuct.AirlockStepRise * step;
                CreateCube($"Airlock_Step_{step}", parent,
                    new Vector3(centre.x, (LastShiftBypassDuct.AirlockFloorY + top) * 0.5f,
                        centre.z + size * 0.5f - stepSpan * (0.5f + (LastShiftBypassDuct.AirlockStepCount - step))),
                    new Vector3(size - 0.2f, top - LastShiftBypassDuct.AirlockFloorY, stepSpan), bypassMaterial);
            }
        }

        /// <summary>
        /// 에어록 해치 한 짝. 승강구 해치(<see cref="CreateDeckHatch"/>)와 같은 구조다 —
        /// 판은 미끄러지고 통행 차단은 별도 콜라이더가 맡는다. 다른 것은 하나뿐이고, 여기서는
        /// 차단면이 <b>기본으로 켜져 있다</b>(둘 다 닫힌 것이 시작 상태다).
        /// </summary>
        private static void CreateAirlockHatch(
            Transform parent, LastShiftAirlockSide side, Vector3 centre, Vector3 size,
            Vector3 blockerSize, float blockerLocalY)
        {
            var name = $"AirlockHatch_{side}";
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = centre;

            var panel = CreateCube($"{name}_Panel", root.transform, Vector3.zero, size,
                CreateMaterial($"LS_AirlockHatch_{side}", new Color(0.46f, 0.40f, 0.30f)));
            Object.DestroyImmediate(panel.GetComponent<Collider>());

            var blockerObject = new GameObject($"{name}_Blocker");
            blockerObject.transform.SetParent(root.transform, false);
            // 차단면은 판이 아니라 <b>뚫린 자리</b>를 메운다. 판은 미끄러지는 연출이라 두께가
            // 얇고, 그걸 그대로 막으면 저중력에서 뜬 물건이 터널링으로 빠진다.
            blockerObject.transform.localPosition = new Vector3(0f, blockerLocalY, 0f);
            var blocker = blockerObject.AddComponent<BoxCollider>();
            blocker.size = blockerSize;

            root.AddComponent<LastShiftAirlockHatch>().Configure(side, panel.transform, blocker);
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

            var root = new GameObject(LastShiftCompartments.NameOf(spec));
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
                .Where(child => child.ParentIndex == spec.Index &&
                                child.DoorPlane == (alongX ? LastShiftDoorPlane.AlongX : LastShiftDoorPlane.AlongZ) &&
                                Mathf.Abs(child.DoorPlaneCoordinate - face) < 0.001f)
                .ToArray();
        }

        /// <summary>
        /// 이 면에 뚫어야 하는 구멍의 자유축 로컬 좌표. 잠긴 자식은 구멍을 안 낸다.
        ///
        /// <b>이제 자식 구획만 본다.</b> 예전에는 회랑 둘이 구획 쪽 면에 문을 따로 요구해서
        /// 그 목록을 합쳐야 했는데, 회랑이 폐지되면서 그 항목이 없어졌다
        /// (docs/bow-cockpit-central-plaza-layout-v1.md §165·§166) — 구멍을 여는 것은
        /// <c>ParentIndex</c> 사슬 하나뿐이라 <see cref="LastShiftCompartments.DoorDepth"/> 의
        /// 트리 전제가 다시 유일한 전제가 됐다.
        /// </summary>
        private static float[] ChildDoorwaysOn(LastShiftCompartmentSpec spec, bool alongX, bool atMax)
        {
            var origin = alongX ? spec.CenterZ : spec.CenterX;
            var face = alongX
                ? (atMax ? spec.MaxX : spec.MinX)
                : (atMax ? spec.MaxZ : spec.MinZ);
            return ChildrenOn(spec, alongX, atMax)
                .Where(child => child.IsPassable)
                .Select(child => child.DoorCenter)
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
        /// 이 구획 면에 뚫리는 창. <b>지금은 없다</b> — 유일한 구획 창이던 관측실 선수 끝벽이
        /// 그 방과 함께 카탈로그로 이관됐다(맵 개편 §3.2).
        ///
        /// 함수를 남겨 두는 것은 벽 빌더가 문과 창을 <see cref="WallAperture"/> 하나로 자르고
        /// 있기 때문이다. 창을 다시 여는 것은 카탈로그 관측실 프리팹이 올 때이고, 그때
        /// 좌표는 여기가 아니라 모듈 쪽이 든다.
        /// </summary>
        private static WallAperture[] WindowsOn(LastShiftCompartmentSpec spec, bool alongX, bool atMax) =>
            NoApertures;

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
                // 광장은 하나뿐이라 종류가 같으면 같은 공간이다 — 통로 둘을 번호로 가르던
                // 갈래가 여기 있었다.
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
        /// 구역 하나. <b>바닥은 여기서 안 깐다</b> — 방사형에서는 한 구역이 사각형 하나가
        /// 아니라서(조종석 구역은 광장·조종석 방·에어록 홀·숙소 넷의 합집합) 구역 상자로
        /// 바닥을 깔면 팔 사이 빈 사분면까지 갑판이 덮인다. 바닥은 공간마다
        /// <see cref="CreateSpaceFloor"/> 가 자기 발자국만큼만 깐다.
        ///
        /// 그래서 여기 남는 것은 <b>구역 색 띠</b> 하나다. 그 띠가 런타임 손상 표시
        /// (<see cref="LastShiftImpactFeedback"/>)가 찾는 구역 오브젝트이기도 하다.
        /// </summary>
        private static void CreateZone(string name, Transform parent, LastShiftZone zoneId, Material material)
        {
            var zone = new GameObject(name);
            zone.transform.SetParent(parent, false);
            zone.transform.position = new Vector3(LastShiftShipDimensions.RoomCenterX(zoneId), 0f, 0f);
            CreateZoneStrip(zone.transform, zoneId, material);
        }

        /// <summary>바닥 판 한 장. x·z 구간으로 받는다 — 구멍을 두르는 넉 장이 전부 구간 계산이라 중심·크기로 받으면 읽기 어렵다.</summary>
        private static void CreateFloorSlab(Transform parent, string name, float minX, float maxX, float minZ, float maxZ)
        {
            const float thickness = LastShiftShipDimensions.HullThickness;
            if (maxX - minX <= 0.0001f || maxZ - minZ <= 0.0001f) return;
            CreateCube(name, parent, new Vector3((minX + maxX) * 0.5f, -thickness * 0.5f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, thickness, maxZ - minZ), floorMaterial);
        }

        private static void CreateZoneStrip(Transform zone, LastShiftZone zoneId, Material material)
        {
            // 구역 색 띠. 벽 패널 앞 바닥에 깔아 어느 구역에 서 있는지가 발밑에서 읽힌다.
            // 구역 오브젝트가 방 중심 x 에 서 있으므로 z 만 로컬로 넘긴다.
            var stripZ = RoomPanelZ(zoneId) + (zoneId == LastShiftZone.Power ? 0.8f : -0.8f);
            var strip = CreateCube("ZoneStrip", zone, new Vector3(0f, 0.015f, stripZ),
                new Vector3(LastShiftShipDimensions.RoomLengthOf(zoneId) - 0.3f, 0.03f, 0.25f), material);
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
        /// 부품 정위치의 정본. <see cref="CreateItems"/> 가 새로 놓을 때 쓰는 값과 <b>같은 출처</b>이며,
        /// 이미 놓인 씬 인스턴스를 다시 맞추는 쪽(<c>RealignSceneItems</c>)과 그것을 지키는 테스트가
        /// 이 접근자를 통해 같은 값을 본다.
        ///
        /// 좌표를 씬에서 읽어 오지 않고 여기서 내려 주는 것이 요점이다 — 부품이 어느 구역에
        /// 있는지가 게임 규칙이라(<c>PatchPlate</c> 가 산소실에 있어야 <see cref="LastShiftSandboxController.BreachZone"/>
        /// 이 산소실이다), 방 배치가 움직이면 씬이 아니라 이쪽이 정답이다.
        /// </summary>
        public static Vector3 NominalPositionOf(LastShiftItemRole role)
        {
            foreach (var spec in ItemSpecs)
                if (spec.Role == role)
                    return spec.Position;
            throw new System.ArgumentException($"{role} 의 정위치가 부품표에 없다.", nameof(role));
        }

        /// <summary>
        /// 이미 씬에 놓인 부품들을 정위치 정본으로 되돌린다. 새로 짓지 않는다.
        ///
        /// <b>왜 통짜 재빌드가 아닌가.</b> <see cref="RebuildShipPrefab"/> 은 선체·구획·드레싱까지
        /// 전부 다시 굽고, 드레싱 규칙 위반이 하나라도 있으면 그 자리에서 던진다. 방 배치가
        /// 움직였을 때 실제로 어긋나는 것은 <b>부품 좌표 넷</b>이고, 그 넷을 되맞추는 데
        /// 선체를 다시 구울 이유가 없다 — 아트 데이터 상태와 시뮬레이션 정합성이 서로를
        /// 인질로 잡지 않아야 한다.
        /// </summary>
        public static int RealignSceneItems(IEnumerable<LastShiftGrabbable> items)
        {
            var moved = 0;
            foreach (var item in items)
            {
                if (item == null) continue;
                var nominal = NominalPositionOf(item.Role);
                var wasOff = (item.transform.position - nominal).sqrMagnitude > 1e-6f ||
                             (item.NominalPosition - nominal).sqrMagnitude > 1e-6f;
                item.transform.position = nominal;
                item.transform.rotation = Quaternion.identity;
                // Configure 가 정위치를 <b>호출 시점의 transform</b> 에서 잡으므로 이동 뒤에 불러야 한다.
                // 순서를 바꾸면 좌표만 옮기고 nominalPosition 은 옛 값으로 남는다.
                item.Configure(item.Role, true);
                if (!wasOff) continue;
                moved++;
                Debug.Log($"[LAST_SHIFT_ITEM_REALIGN] role={item.Role} nominal={nominal} " +
                          $"zone={LastShiftZoneAtlas.KeyOf(LastShiftZoneAtlas.Resolve(nominal))}");
            }
            return moved;
        }

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
            // 글자가 좌우로 뒤집혀 거꾸로 읽힌다.
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
            ConfigureSpaceSky();
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

        /// <summary>창과 선외 활동에서 보이는 우주 배경을 씬의 환경 정본으로 연결한다.</summary>
        public static void ConfigureSpaceSky()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SpaceSkyMaterialPath);
            if (sky == null)
                throw new System.InvalidOperationException($"Space sky material missing: {SpaceSkyMaterialPath}");
            if (sky.shader == null || sky.shader.name != SpaceSkyShaderName)
                throw new System.InvalidOperationException($"Space sky shader mismatch: expected {SpaceSkyShaderName}");

            RenderSettings.skybox = sky;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.35f;
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
