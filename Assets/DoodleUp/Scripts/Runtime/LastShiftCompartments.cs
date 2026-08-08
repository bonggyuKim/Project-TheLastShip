using System;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 그레이박스 구획. 기획 정본은 <c>docs/corridor-4p-redesign-v1.md</c> §17.4 이고
    /// 에어록은 빠져 있다 — 우회 통로 z 경로가 아직 미결이라 좌표를 못 박을 수 없다(§17.5).
    /// 그래서 12 항목 중 11 이다.
    ///
    /// 여기서 "구획"은 <see cref="LastShiftZone"/>(압력 구역)과 다른 것이다. §17.6 이 명시한 대로
    /// 그레이박스가 답하는 것은 "방이 어디 있는가" 뿐이고, 그 방이 <c>ZonePressure</c>·게이지·
    /// <c>SIMUL_ZONES</c>·<c>RG-1</c> 중 무엇을 따르는지는 아직 정해지지 않았다. 이 enum 이
    /// <see cref="LastShiftZone"/> 과 섞이면 그 미결이 조용히 "편입됨"으로 굳는다.
    /// </summary>
    public enum LastShiftCompartment
    {
        Observatory = 0,
        Workshop = 1,
        CargoBay = 2,
        Hangar = 3,
        ServerRoom = 4,
        Lavatory = 5,
        Quarters = 6,
        Lounge = 7,
        Hydroponics = 8,
        MedBay = 9,
        EscapePod = 10
    }

    /// <summary>
    /// 초기 접근 상태. §15.2 의 언락 트리거 자체는 메타 진행 백본(구현은 P0 이후, §15.5)이
    /// 들고 있을 것이므로 여기서는 <b>그레이박스가 무엇을 세워야 하는지</b>만 구분한다 —
    /// 문 구멍을 뚫을지, 잠긴 판으로 메울지가 갈린다.
    /// </summary>
    public enum LastShiftCompartmentAccess
    {
        /// <summary>
        /// 처음부터 드나든다. 생활공간 셋(§9)과 선수 사슬 넷(화물칸·격납고·정비창·관측실)이
        /// 여기다 — 뒤 넷은 §15.2 언락 대상이지만 트리거가 P0 이후라 확장 검토 §2 가
        /// P0 초기값을 열어 두기로 했다.
        /// </summary>
        Open = 0,

        /// <summary>
        /// 공간은 있되 문이 안 열린다. §15.2 언락 대상 중 P0 에서 안 여는 셋
        /// (서버/통신실·수경재배·의무실)이 여기다.
        /// </summary>
        Locked = 1,

        /// <summary>
        /// 공간은 처음부터 열려 있고 "발진 가능 상태" 만 잠긴다(§15.4). 구명정 전용이다 —
        /// 나머지 여덟과 언락의 <b>종류</b>가 다르므로 <see cref="Locked"/> 로 뭉뚱그리지 않는다.
        /// </summary>
        SpaceOpenFunctionLocked = 2
    }

    /// <summary>문이 놓인 평면의 법선 축. 그레이박스에서는 둘뿐이다.</summary>
    public enum LastShiftDoorPlane
    {
        /// <summary>x 평면에 놓인 문. 평면 위 자유 좌표는 z 다.</summary>
        AlongX = 0,

        /// <summary>z 평면에 놓인 문. 평면 위 자유 좌표는 x 다.</summary>
        AlongZ = 1
    }

    /// <summary>구획 하나의 그레이박스 제원. 높이는 전 항목 공통이라 여기 없다.</summary>
    public readonly struct LastShiftCompartmentSpec
    {
        public LastShiftCompartmentSpec(
            LastShiftCompartment compartment,
            float minX, float maxX, float minZ, float maxZ,
            LastShiftDoorPlane doorPlane, float doorPlaneCoordinate, float doorCenter,
            int parentIndex, LastShiftCompartmentAccess access)
        {
            Compartment = compartment;
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
            DoorPlane = doorPlane;
            DoorPlaneCoordinate = doorPlaneCoordinate;
            DoorCenter = doorCenter;
            ParentIndex = parentIndex;
            Access = access;
        }

        public LastShiftCompartment Compartment { get; }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        public LastShiftDoorPlane DoorPlane { get; }

        /// <summary>문이 놓인 평면. <see cref="LastShiftDoorPlane.AlongX"/> 면 x, 아니면 z 다.</summary>
        public float DoorPlaneCoordinate { get; }

        /// <summary>그 평면 위 문 중심. <see cref="LastShiftDoorPlane.AlongX"/> 면 z, 아니면 x 다.</summary>
        public float DoorCenter { get; }

        /// <summary>
        /// 이 구획을 안쪽으로 잇는 상대. <c>-1</c> 이면 선체(주 통로)에 직접 붙는다.
        /// <b>구획마다 안쪽 문이 정확히 하나</b>라는 것이 §9.4·§17.6 의 "막다른 방" 전제를
        /// 자료 구조로 강제하는 자리다 — 둘을 허용하면 우회로가 생겨 §9.5 가 명시적으로
        /// 아니라고 답한 "4인 게이트 대안 경로"가 실수로 만들어진다.
        /// </summary>
        public int ParentIndex { get; }

        public LastShiftCompartmentAccess Access { get; }

        public float LengthX => MaxX - MinX;
        public float WidthZ => MaxZ - MinZ;
        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterZ => (MinZ + MaxZ) * 0.5f;

        /// <summary>문 중심 좌표. y 는 문 구멍 높이의 절반이다.</summary>
        public Vector3 DoorPosition => DoorPlane == LastShiftDoorPlane.AlongX
            ? new Vector3(DoorPlaneCoordinate, LastShiftZoneDoor.OpeningHeight * 0.5f, DoorCenter)
            : new Vector3(DoorCenter, LastShiftZoneDoor.OpeningHeight * 0.5f, DoorPlaneCoordinate);

        /// <summary>바닥부터 천장 내면까지를 채우는 내부 볼륨.</summary>
        public Bounds Volume => new(
            new Vector3(CenterX, LastShiftCompartments.InteriorHeight * 0.5f, CenterZ),
            new Vector3(LengthX, LastShiftCompartments.InteriorHeight, WidthZ));

        /// <summary>드나들 수 있는가. 잠긴 문은 그레이박스에서 구멍이 아니라 메운 판이다.</summary>
        public bool IsPassable => Access != LastShiftCompartmentAccess.Locked;
    }

    /// <summary>
    /// 그레이박스 좌표의 정본. 씬 빌더·검증기·테스트가 전부 여기서 파생한다.
    ///
    /// <b>x 리터럴을 적지 않는다.</b> §17.4 표의 값들(`-19`, `+19`, `-15`, `+13` 등)은 전부
    /// 선체 반전장 `19m`(전장 `38m`) 기준이고, 지금 선체는 아직 `36m` 다 — §2.2 의
    /// `36 → 38` 개정이 아직 코드에 안 들어왔기 때문이다. 표 값을 그대로 박아 두면 그
    /// 개정이 들어오는 순간 구획 열한 개가 통째로 선체 안쪽 `1m` 로 파고들거나 `1m` 떠서
    /// 벌어진다. 그래서 표의 숫자가 아니라 <b>표가 무엇에 붙어 있는지</b>를 적는다 —
    /// 화물칸은 조종석 선수 끝벽에, 생활공간은 산소실 선미 끝벽에, 서버실은 조종석 방
    /// 중심의 우현 벽에 붙는다. 그러면 전장이 바뀔 때 구획이 따라 움직인다.
    ///
    /// 축 규약은 선체와 같다 — x = 전장, z = 전폭, y = 높이.
    /// </summary>
    public static class LastShiftCompartments
    {
        /// <summary>
        /// 전 항목 공통 내부 높이. §17.4 가 "전 항목 높이 3m 균일" 로 확정했다.
        /// 선체 내부(<see cref="LastShiftShipPhysics.CeilingInnerHeight"/> `3.2m`)보다 낮은 것이
        /// 의도다 — 부속 구획이 본선보다 낮아야 통과할 때 "다른 공간으로 넘어왔다"가 읽힌다.
        /// 문 구멍 높이 `2.2m` 는 그대로 들어간다.
        /// </summary>
        public const float InteriorHeight = 3f;

        /// <summary>구획 벽·바닥·천장 판 두께. 선체와 같은 두께를 쓴다.</summary>
        public const float PanelThickness = LastShiftShipDimensions.HullThickness;

        /// <summary>구획 수. 에어록을 뺀 `11` 이다(§17.5).</summary>
        public const int Count = 11;

        private static readonly LastShiftCompartmentSpec[] specs = BuildSpecs();

        /// <summary>선수→선미 순이 아니라 <see cref="LastShiftCompartment"/> 값 순이다.</summary>
        public static LastShiftCompartmentSpec[] Specs => specs;

        public static LastShiftCompartmentSpec Of(LastShiftCompartment compartment) => specs[(int)compartment];

        /// <summary>구획 이름. 씬 오브젝트 이름과 로그가 같은 문자열을 봐야 검증이 성립한다.</summary>
        public static string NameOf(LastShiftCompartment compartment) => compartment switch
        {
            LastShiftCompartment.Observatory => "Compartment_Observatory",
            LastShiftCompartment.Workshop => "Compartment_Workshop",
            LastShiftCompartment.CargoBay => "Compartment_CargoBay",
            LastShiftCompartment.Hangar => "Compartment_Hangar",
            LastShiftCompartment.ServerRoom => "Compartment_ServerRoom",
            LastShiftCompartment.Lavatory => "Compartment_Lavatory",
            LastShiftCompartment.Quarters => "Compartment_Quarters",
            LastShiftCompartment.Lounge => "Compartment_Lounge",
            LastShiftCompartment.Hydroponics => "Compartment_Hydroponics",
            LastShiftCompartment.MedBay => "Compartment_MedBay",
            _ => "Compartment_EscapePod"
        };

        /// <summary>
        /// 안쪽 문이 선체 벽을 뚫는 구획인가. <c>ParentIndex &lt; 0</c> 과 같은 뜻이지만
        /// 씬 빌더가 "선체 판에 구멍을 내야 하는가" 를 묻는 자리라 이름을 따로 둔다.
        /// </summary>
        public static bool ConnectsToHull(LastShiftCompartmentSpec spec) => spec.ParentIndex < 0;

        private static LastShiftCompartmentSpec[] BuildSpecs()
        {
            // 붙는 자리. 전부 선체 치수 정본에서 뽑고, 여기서만 이름을 짧게 준다.
            var bow = -LastShiftShipDimensions.HalfLength;   // 조종석 선수 끝벽 안쪽 면 (§17.4 의 -19)
            var stern = LastShiftShipDimensions.HalfLength;  // 산소실 선미 끝벽 안쪽 면 (§17.4 의 +19)
            var starboard = LastShiftShipDimensions.HalfWidth;   // 우현 긴 벽 (§17.4 의 +3)
            var port = -LastShiftShipDimensions.HalfWidth;       // 좌현 긴 벽 — 창이 있는 쪽이다
            var cockpitCenter = LastShiftShipDimensions.CockpitCenterX;                     // §17.4 의 -15
            var lifeSupportMin = LastShiftShipDimensions.RoomMinX(LastShiftZone.LifeSupport); // §17.4 의 +11

            var result = new LastShiftCompartmentSpec[Count];

            // ── 선수 쪽 사슬 — 조종석 끝벽에서 화물칸 → 정비창 → 관측실 로 뻗고,
            //    격납고만 화물칸 우현으로 갈라진다(§17.3 도해).
            //
            //    <b>넷 다 P0 에서 상시 개방이다</b>(확장 검토 §2). §15.2 는 이 넷을 언락
            //    순서 1·2·3·4 로 두었지만 그 트리거가 메타 진행 백본에 걸려 있고 구현이
            //    P0 이후(§15.5)라, 잠가 두면 P0 기간 내내 안 열린다 — 지어 놓은 `181m²`
            //    가 통째로 메운 판이 되고, 폭 `8m` 이상인 방 둘(화물칸·격납고)이 거기
            //    들어 있어 "열린 공간 중 가장 넓은 방이 폭 `6m`" 가 된다. 회랑 둘도
            //    같이 죽는다: 관측 회랑은 화물칸 쪽 끝이, 상부 회랑은 격납고 쪽 끝이
            //    메운 판이라 지어만 놓고 못 쓰는 `195.5m²` 였다.
            //
            //    <b>언락 설계를 폐기하는 것이 아니라 "P0 씬 = 언락이 끝난 뒤의 배" 로
            //    정의하는 것이다</b>(확장 검토 §2.3). §15.2 의 순서 근거는 그대로 살아
            //    있고, 메타 진행 백본이 붙을 때 여기 초기 <c>Access</c> 값만 되돌리면
            //    설계가 복원된다 — 그래서 사슬 넷만 바꾸고 §15.2 표는 안 건드린다.
            //    안 여는 셋(서버/통신실·수경재배·의무실)은 §2.2 대로 <c>Locked</c> 다.
            result[(int)LastShiftCompartment.CargoBay] = new LastShiftCompartmentSpec(
                LastShiftCompartment.CargoBay,
                bow - 8f, bow, -4f, 4f,
                LastShiftDoorPlane.AlongX, bow, 0f,
                -1, LastShiftCompartmentAccess.Open);

            result[(int)LastShiftCompartment.Hangar] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Hangar,
                bow - 8f, bow, 4f, 14f,
                LastShiftDoorPlane.AlongZ, 4f, bow - 4f,
                (int)LastShiftCompartment.CargoBay, LastShiftCompartmentAccess.Open);

            result[(int)LastShiftCompartment.Workshop] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Workshop,
                bow - 13f, bow - 8f, -2.5f, 2.5f,
                LastShiftDoorPlane.AlongX, bow - 8f, 0f,
                (int)LastShiftCompartment.CargoBay, LastShiftCompartmentAccess.Open);

            result[(int)LastShiftCompartment.Observatory] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Observatory,
                bow - 16f, bow - 13f, -2f, 2f,
                LastShiftDoorPlane.AlongX, bow - 13f, 0f,
                (int)LastShiftCompartment.Workshop, LastShiftCompartmentAccess.Open);

            // ── 조종석 분기. §17.4 는 좌현(`z -9~-3`)이라고 적었지만 <b>이 선체의 좌현은 벽이
            //    아니라 창이다</b> — `OuterHull_Front*` 는 전장 전체에 걸쳐 눈높이 구간이 비어
            //    있고 그 너머에 `SpaceVoid` 와 `StarField` 가 놓여 있다. 좌현에 구획을 붙이면
            //    조종석에서 보이는 별이 통째로 회색 상자로 막힌다. 치수(`4×6×3`)와 문 x(조종석
            //    방 중심)는 표 그대로 두고 <b>부호만 우현으로 뒤집는다</b>. §9.2 가 부속 블록
            //    위치를 "구조적으로 동일, art/tech 판단" 으로 넘긴 것과 같은 종류의 판단이다.
            result[(int)LastShiftCompartment.ServerRoom] = new LastShiftCompartmentSpec(
                LastShiftCompartment.ServerRoom,
                cockpitCenter - 2f, cockpitCenter + 2f, starboard, starboard + 6f,
                LastShiftDoorPlane.AlongZ, starboard, cockpitCenter,
                -1, LastShiftCompartmentAccess.Locked);

            // ── 산소실 우현 분기. 문은 산소실 방 선수 끝에서 2m 들어간 자리다.
            result[(int)LastShiftCompartment.Hydroponics] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Hydroponics,
                lifeSupportMin - 1f, lifeSupportMin + 5f, starboard, starboard + 6f,
                LastShiftDoorPlane.AlongZ, starboard, lifeSupportMin + 2f,
                -1, LastShiftCompartmentAccess.Locked);

            // ── 선미 쪽 사슬 — 생활공간 셋(§9)이 일렬로 붙고 그 끝에 구명정이 온다.
            //    생활공간은 §15.2 언락 목록에 없다. 처음부터 드나드는 공간이다.
            result[(int)LastShiftCompartment.Lavatory] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Lavatory,
                stern, stern + 2f, port, starboard,
                LastShiftDoorPlane.AlongX, stern, 0f,
                -1, LastShiftCompartmentAccess.Open);

            result[(int)LastShiftCompartment.Quarters] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Quarters,
                stern + 2f, stern + 6f, port, starboard,
                LastShiftDoorPlane.AlongX, stern + 2f, 0f,
                (int)LastShiftCompartment.Lavatory, LastShiftCompartmentAccess.Open);

            result[(int)LastShiftCompartment.Lounge] = new LastShiftCompartmentSpec(
                LastShiftCompartment.Lounge,
                stern + 6f, stern + 10f, port, starboard,
                LastShiftDoorPlane.AlongX, stern + 6f, 0f,
                (int)LastShiftCompartment.Quarters, LastShiftCompartmentAccess.Open);

            result[(int)LastShiftCompartment.EscapePod] = new LastShiftCompartmentSpec(
                LastShiftCompartment.EscapePod,
                stern + 10f, stern + 14f, -2f, 2f,
                LastShiftDoorPlane.AlongX, stern + 10f, 0f,
                (int)LastShiftCompartment.Lounge, LastShiftCompartmentAccess.SpaceOpenFunctionLocked);

            // ── 숙소 우현 분기.
            result[(int)LastShiftCompartment.MedBay] = new LastShiftCompartmentSpec(
                LastShiftCompartment.MedBay,
                stern + 2f, stern + 7f, starboard, starboard + 5f,
                LastShiftDoorPlane.AlongZ, starboard, stern + 4f,
                (int)LastShiftCompartment.Quarters, LastShiftCompartmentAccess.Locked);

            return result;
        }

        /// <summary>
        /// 두 구획 볼륨이 실제로 겹치는가. 맞닿는 면(공유 벽)은 겹침이 아니다 —
        /// 사슬로 이어 붙인 구획은 언제나 한 면을 공유하므로 열린 구간 비교를 써야 한다.
        /// </summary>
        public static bool VolumesOverlap(in LastShiftCompartmentSpec a, in LastShiftCompartmentSpec b) =>
            VolumesOverlap(a.MinX, a.MaxX, a.MinZ, a.MaxZ, b.MinX, b.MaxX, b.MinZ, b.MaxZ);

        /// <summary>
        /// 발자국 좌표만 받는 같은 판정. <see cref="LastShiftPlacementRules"/> 가 구획표 밖의
        /// 배치 후보를 잴 때 쓴다 — 자유 배치 후보는 아직 <see cref="LastShiftCompartmentSpec"/>
        /// 가 아니지만 겹침 규약은 같아야 한다.
        /// </summary>
        public static bool VolumesOverlap(
            float aMinX, float aMaxX, float aMinZ, float aMaxZ,
            float bMinX, float bMaxX, float bMinZ, float bMaxZ) =>
            aMinX < bMaxX - Epsilon && bMinX < aMaxX - Epsilon &&
            aMinZ < bMaxZ - Epsilon && bMinZ < aMaxZ - Epsilon;

        /// <summary>구획 볼륨이 선체 내부(방·통로가 타일링한 영역)를 침범하는가.</summary>
        public static bool OverlapsHullInterior(in LastShiftCompartmentSpec spec) =>
            OverlapsHullInterior(spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ);

        /// <summary>발자국 좌표만 받는 같은 판정. <see cref="VolumesOverlap(float,float,float,float,float,float,float,float)"/> 와 같은 이유로 있다.</summary>
        public static bool OverlapsHullInterior(float minX, float maxX, float minZ, float maxZ) =>
            minX < LastShiftShipDimensions.HalfLength - Epsilon &&
            -LastShiftShipDimensions.HalfLength < maxX - Epsilon &&
            minZ < LastShiftShipDimensions.HalfWidth - Epsilon &&
            -LastShiftShipDimensions.HalfWidth < maxZ - Epsilon;

        /// <summary>
        /// 문이 <b>자기 구획의 경계면 위</b>에 있고, 구멍 폭이 그 면 안에 다 들어가는가.
        /// 문이 면에서 벗어나 있으면 씬에서는 벽 옆 허공에 문틀이 서고, 폭이 넘치면
        /// 모서리에 틈이 남아 그레이박스가 안 닫힌다.
        /// </summary>
        public static bool DoorSitsOnOwnBoundary(in LastShiftCompartmentSpec spec)
        {
            var half = LastShiftZoneDoor.OpeningWidth * 0.5f;
            if (spec.DoorPlane == LastShiftDoorPlane.AlongX)
                return (Mathf.Abs(spec.DoorPlaneCoordinate - spec.MinX) < Epsilon ||
                        Mathf.Abs(spec.DoorPlaneCoordinate - spec.MaxX) < Epsilon) &&
                       spec.DoorCenter - half >= spec.MinZ - Epsilon &&
                       spec.DoorCenter + half <= spec.MaxZ + Epsilon;

            return (Mathf.Abs(spec.DoorPlaneCoordinate - spec.MinZ) < Epsilon ||
                    Mathf.Abs(spec.DoorPlaneCoordinate - spec.MaxZ) < Epsilon) &&
                   spec.DoorCenter - half >= spec.MinX - Epsilon &&
                   spec.DoorCenter + half <= spec.MaxX + Epsilon;
        }

        /// <summary>
        /// 선체까지 몇 개의 문을 지나야 하는가. 부모 사슬을 거슬러 올라가고,
        /// 사슬이 <see cref="Count"/> 보다 길어지면 순환이라 <c>-1</c> 을 돌려준다 —
        /// 순환이 있으면 §9.5 가 아니라고 답한 대안 경로가 생긴다.
        /// </summary>
        public static int DoorDepth(LastShiftCompartment compartment)
        {
            var index = (int)compartment;
            for (var depth = 1; depth <= Count; depth++)
            {
                var parent = specs[index].ParentIndex;
                if (parent < 0) return depth;
                index = parent;
            }
            return -1;
        }

        private const float Epsilon = 0.0001f;

        static LastShiftCompartments()
        {
            if (specs.Length != Enum.GetValues(typeof(LastShiftCompartment)).Length)
                throw new InvalidOperationException("compartment spec table must cover every LastShiftCompartment value");
        }
    }
}
