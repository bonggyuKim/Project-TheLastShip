using System;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 드레싱 소품이 놓이는 공간의 종류. <b>공간은 좌표가 아니라 이름으로 고른다</b> —
    /// art 가 미터 좌표를 직접 적으면 §17.4 치수 개정이 들어올 때마다 소품이 벽을 뚫고
    /// 남고, 그 위반은 씬을 다시 구워 눈으로 보기 전에는 아무도 못 본다.
    /// </summary>
    public enum LastShiftDressingSpaceKind
    {
        /// <summary>압력 구역 넷(조종석·전력·냉각·산소).</summary>
        Zone = 0,

        /// <summary>고정 부속 구획. 중앙 광장 허브 이후로는 에어록 홀·숙소 둘이다.</summary>
        Compartment = 1,

        /// <summary>
        /// 중앙 광장. <b>번호를 통로에서 물려받았다</b> — 통로 둘이 폐지되고 그 동선 역할을
        /// 광장이 통째로 승계했으므로(조항 P-1) 씬에 구워진 옛 통로 소품이 광장 소품으로
        /// 읽히는 것이 맞다. 자리는 <see cref="LastShiftDressingSpaces.BoundsOf"/> 가 다시 잡고,
        /// 광장 한가운데는 코어가 먹고 있어 소품이 그리로 가면 검증에서 걸린다.
        /// </summary>
        Plaza = 2,

        /// <summary>갑판 하부 우회 통로(본선 + 선수 다리).</summary>
        BypassRun = 3,

        /// <summary>우회 통로 끝의 에어록.</summary>
        AirlockBranch = 4

        // `UpperGallery = 5` 가 여기 있었다. 상부 회랑이 폐지되면서 빠졌다
        // (docs/bow-cockpit-central-plaza-layout-v1.md §165). 번호는 다시 안 쓴다 —
        // 씬에 구워진 드레싱 데이터가 옛 값 `5` 를 들고 있을 수 있고, 그 번호를 다른
        // 종류에 물려주면 회랑 소품이 조용히 그 종류로 되살아난다.
    }

    /// <summary>
    /// 소품이 <b>무엇으로 읽히는지</b>. 크기·색이 아니라 이 플래그가 브리프 4대 제약의
    /// 판정 기준이다 — "이 상자가 게이지인가" 는 지오메트리로는 절대 못 푸는 질문이라,
    /// 데이터를 넣는 사람이 선언하게 하고 검증기는 선언과 자리만 대조한다.
    ///
    /// 플래그를 안 붙이면 <see cref="None"/> 이고, 그때는 아무 제약도 안 걸린다.
    /// 즉 <b>제약은 opt-in 이 아니라 선언에 따라붙는다</b> — art 가 "상태 단서" 라고
    /// 적는 순간 노출 원뿔 검사가 자동으로 켜진다.
    /// </summary>
    [Flags]
    public enum LastShiftDressingSemantics
    {
        None = 0,

        /// <summary>선체·구역 상태에 반응한다(서리가 자라고 아크가 튄다). §19.4 노출 원뿔 대상.</summary>
        StateResponsive = 1 << 0,

        /// <summary>압력 게이지. §24 미편입 구획에 금지.</summary>
        PressureGauge = 1 << 1,

        /// <summary>전선 사이렌·경보 이펙트. §24 미편입 구획에 금지.</summary>
        SirenEffect = 1 << 2,

        /// <summary>해치 표식·언락 신호. §21.4 언락 전 구획에 금지.</summary>
        HatchMarker = 1 << 3,

        /// <summary>
        /// 그 방 고유 시스템의 표현(수경재배 식물 열화, 서버 LED, 구명정 발진 상태등).
        /// 브리프 §1.3 이 게이지 금지와 갈라 둔 예외라, 사유를 적어야만 통과한다.
        /// </summary>
        RoomSystemReadout = 1 << 4,

        /// <summary>쾌적 설비(핸드레일·넓은 발판·밝은 유도띠). 우회 통로에 금지 — §5.</summary>
        Comfort = 1 << 5,

        /// <summary>발광체. 우회 통로 밝기 예산의 계산 대상이다.</summary>
        LightSource = 1 << 6
    }

    /// <summary>소품 위치를 적는 방식.</summary>
    public enum LastShiftDressingAnchorMode
    {
        /// <summary>
        /// 공간 반치수 대비 단위좌표(-1 = 벽에 붙음, 0 = 가운데, +1 = 반대 벽).
        /// 벽에 붙는 소품은 전부 이쪽이다 — 방 치수가 바뀌어도 벽에 붙은 채로 따라간다.
        /// </summary>
        UnitOfSpace = 0,

        /// <summary>
        /// 공간 중심에서의 미터 오프셋. 자리가 정확히 정해진 것(상태 단서처럼 안전대
        /// 경계와의 여유가 곧 설계인 것)에만 쓴다.
        /// </summary>
        MetersFromSpaceCenter = 1
    }

    /// <summary>
    /// 공간 하나의 실제 치수. 공간별 좌표 정본(<see cref="LastShiftShipDimensions"/>,
    /// <see cref="LastShiftCompartments"/>, <see cref="LastShiftBypassDuct"/>)에서 그때그때
    /// 뽑아 만든다 — 여기에 값을 복사해 두면 정본이 두 벌이 된다.
    /// </summary>
    public readonly struct LastShiftDressingBounds
    {
        public LastShiftDressingBounds(float minX, float maxX, float minZ, float maxZ, float floorY, float ceilingY)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
            FloorY = floorY;
            CeilingY = ceilingY;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }
        public float FloorY { get; }
        public float CeilingY { get; }

        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterZ => (MinZ + MaxZ) * 0.5f;
        public float HalfLengthX => (MaxX - MinX) * 0.5f;
        public float HalfWidthZ => (MaxZ - MinZ) * 0.5f;
    }

    /// <summary>
    /// 소품이 놓일 공간의 지정. 종류에 따라 <see cref="zone"/>·<see cref="compartment"/>·
    /// <see cref="passage"/> 중 하나만 읽힌다 — Inspector 에서 셋이 다 보이는 것은
    /// 감수한다. 종류별로 클래스를 쪼개면 art 가 리스트에 항목을 추가할 때마다
    /// 타입을 먼저 골라야 하고, 그게 코드를 안 쓰는 사람에게 가장 큰 벽이다.
    /// </summary>
    [Serializable]
    public struct LastShiftDressingSpace
    {
        public LastShiftDressingSpaceKind kind;
        public LastShiftZone zone;
        public LastShiftCompartment compartment;

        /// <summary>
        /// 옛 통로 번호. <b>지금은 아무도 안 읽는다</b> — 통로 둘이 폐지되고
        /// <see cref="LastShiftDressingSpaceKind.Plaza"/> 가 그 번호를 물려받았다. 필드를 안
        /// 지우는 것은 씬에 구워진 드레싱 데이터가 이 값을 들고 있기 때문이고, 지우면
        /// Unity 가 그 항목을 통째로 다시 직렬화하면서 옆 필드까지 초기값으로 되돌린다.
        /// </summary>
        [Range(0, 1)] public int passage;

        public static LastShiftDressingSpace Of(LastShiftZone zone) =>
            new() { kind = LastShiftDressingSpaceKind.Zone, zone = zone };

        public static LastShiftDressingSpace Of(LastShiftCompartment compartment) =>
            new() { kind = LastShiftDressingSpaceKind.Compartment, compartment = compartment };

        public static LastShiftDressingSpace OfPlaza() =>
            new() { kind = LastShiftDressingSpaceKind.Plaza };

        public static LastShiftDressingSpace OfBypassRun() =>
            new() { kind = LastShiftDressingSpaceKind.BypassRun };

        public static LastShiftDressingSpace OfAirlock() =>
            new() { kind = LastShiftDressingSpaceKind.AirlockBranch };

        public override string ToString() => kind switch
        {
            LastShiftDressingSpaceKind.Zone => $"Zone.{zone}",
            LastShiftDressingSpaceKind.Compartment => $"Compartment.{compartment}",
            LastShiftDressingSpaceKind.Plaza => "Plaza",
            LastShiftDressingSpaceKind.BypassRun => "BypassRun",
            _ => "AirlockBranch"
        };
    }

    /// <summary>
    /// 드레싱 소품 하나. <b>art 가 코드를 안 쓰고 채우는 유일한 단위다.</b>
    /// 프리팹을 주면 프리팹을 세우고, 안 주면 <see cref="material"/> 을 입힌 박스를 세운다 —
    /// 그레이박스 단계에서 에셋이 아직 없는 자리를 비워 두지 않으려는 것이다.
    /// </summary>
    [Serializable]
    public sealed class LastShiftDressingProp
    {
        /// <summary>씬 하이어라키에 그대로 붙는 이름. 공간 안에서 유일해야 한다.</summary>
        public string id = "Prop";

        public LastShiftDressingSpace space;

        /// <summary>미터 단위 크기. 프리팹을 줬을 때도 경계 검사는 이 값으로 한다.</summary>
        public Vector3 size = Vector3.one;

        public LastShiftDressingAnchorMode anchorMode = LastShiftDressingAnchorMode.UnitOfSpace;

        /// <summary>(x, z) 앵커. 해석은 <see cref="anchorMode"/> 를 따른다.</summary>
        public Vector2 anchor;

        /// <summary>공간 바닥에서 소품 밑면까지의 높이.</summary>
        public float bottomY;

        /// <summary>단위좌표를 미터로 풀 때 벽에서 띄울 여유.</summary>
        public float clearance = 0.06f;

        public Vector3 eulerAngles;

        /// <summary>없으면 박스를 세운다.</summary>
        public GameObject prefab;

        /// <summary>박스에 입힐 재질. 없으면 빌더의 기본 설비색을 쓴다.</summary>
        public Material material;

        public LastShiftDressingSemantics semantics = LastShiftDressingSemantics.None;

        /// <summary><see cref="LastShiftDressingSemantics.LightSource"/> 일 때의 밝기.</summary>
        public float lightIntensity;

        /// <summary>
        /// 예외를 쓸 때의 사유. <see cref="LastShiftDressingSemantics.RoomSystemReadout"/> 은
        /// 이 칸이 비면 검증에서 떨어진다 — 브리프 §8.2 가 "게이지처럼 읽히는지" 를 미결로
        /// 남겼기 때문에, 누가 무슨 근거로 예외를 썼는지가 씬이 아니라 데이터에 남아야 한다.
        /// </summary>
        [TextArea(1, 3)] public string justification = string.Empty;

        public Vector3 Size => new(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
    }

    /// <summary>
    /// 공간 이름을 실제 좌표로 푸는 곳. <b>빌더도 검증기도 여기만 본다</b> —
    /// 씬에 세우는 자리와 검사하는 자리가 다른 식으로 계산되면 검사가 통과한 씬이
    /// 위반 상태로 나올 수 있다.
    /// </summary>
    public static class LastShiftDressingSpaces
    {
        public static LastShiftDressingBounds BoundsOf(LastShiftDressingSpace space)
        {
            switch (space.kind)
            {
                // 방 z 범위를 발자국에서 받는다. 예전에는 전폭 전체(±3)로 고정이었는데,
                // 방사형에서는 전력실·냉각실이 같은 x 범위를 z 좌우로 나눠 가지므로 z 를
                // 고정으로 두면 두 방의 소품이 서로의 방 안에 선다.
                case LastShiftDressingSpaceKind.Zone:
                    return new LastShiftDressingBounds(
                        LastShiftShipDimensions.RoomMinX(space.zone),
                        LastShiftShipDimensions.RoomMaxX(space.zone),
                        LastShiftShipDimensions.RoomMinZ(space.zone),
                        LastShiftShipDimensions.RoomMaxZ(space.zone),
                        0f,
                        LastShiftShipDimensions.CeilingInnerHeight);

                case LastShiftDressingSpaceKind.Compartment:
                {
                    var spec = LastShiftCompartments.Of(space.compartment);
                    return new LastShiftDressingBounds(
                        spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ,
                        0f, LastShiftCompartments.InteriorHeight);
                }

                case LastShiftDressingSpaceKind.Plaza:
                    return new LastShiftDressingBounds(
                        LastShiftPlazaLayout.PlazaMinX, LastShiftPlazaLayout.PlazaMaxX,
                        LastShiftPlazaLayout.PlazaMinZ, LastShiftPlazaLayout.PlazaMaxZ,
                        0f,
                        LastShiftShipDimensions.CeilingInnerHeight);

                case LastShiftDressingSpaceKind.BypassRun:
                {
                    // L 자 관의 외접 상자다. 꺾임 안쪽의 빈 모서리까지 포함하므로 실제 관보다
                    // 넉넉하고, 그래서 경계 검사는 "관 밖으로 한참 나갔다" 만 잡는다. 관 벽에
                    // 정확히 붙었는지는 눈으로 볼 문제라 여기서 재지 않는다.
                    const float half = LastShiftBypassDuct.Section * 0.5f;
                    var minZ = Mathf.Min(LastShiftBypassDuct.ForeShaftZ, LastShiftBypassDuct.RunZ) - half;
                    var maxZ = Mathf.Max(LastShiftBypassDuct.ForeShaftZ, LastShiftBypassDuct.RunZ) + half;
                    return new LastShiftDressingBounds(
                        LastShiftBypassDuct.ForeShaftX - half,
                        LastShiftBypassDuct.AftShaftX + half,
                        minZ, maxZ,
                        LastShiftBypassDuct.FloorY,
                        LastShiftBypassDuct.CeilingY);
                }

                default:
                {
                    const float half = LastShiftBypassDuct.AirlockSize * 0.5f;
                    return new LastShiftDressingBounds(
                        LastShiftBypassDuct.AirlockCenterX - half,
                        LastShiftBypassDuct.AirlockCenterX + half,
                        LastShiftBypassDuct.AirlockCenterZ - half,
                        LastShiftBypassDuct.AirlockCenterZ + half,
                        LastShiftBypassDuct.AirlockFloorY,
                        LastShiftBypassDuct.AirlockCeilingY);
                }
            }
        }

        /// <summary>소품 중심의 선체 좌표. 씬 배치와 제약 판정이 공유하는 유일한 계산이다.</summary>
        public static Vector3 WorldCenter(LastShiftDressingProp prop)
        {
            var bounds = BoundsOf(prop.space);
            var size = prop.Size;

            float x, z;
            if (prop.anchorMode == LastShiftDressingAnchorMode.MetersFromSpaceCenter)
            {
                x = bounds.CenterX + prop.anchor.x;
                z = bounds.CenterZ + prop.anchor.y;
            }
            else
            {
                var slackX = Mathf.Max(0f, bounds.HalfLengthX - size.x * 0.5f - prop.clearance);
                var slackZ = Mathf.Max(0f, bounds.HalfWidthZ - size.z * 0.5f - prop.clearance);
                x = bounds.CenterX + prop.anchor.x * slackX;
                z = bounds.CenterZ + prop.anchor.y * slackZ;
            }

            return new Vector3(x, bounds.FloorY + prop.bottomY + size.y * 0.5f, z);
        }

        // 소품 상자의 평면 구간. <b>노출 원뿔·문 앞 판정은 중심이 아니라 이 값으로 한다</b> —
        // 중심만 보면 문 정면에 걸친 넓은 상자가 중심만 비껴서 통과한다. 문 평면 축이
        // 문마다 다르므로 x 쪽도 같이 있어야 한다.

        public static float MinX(LastShiftDressingProp prop) =>
            WorldCenter(prop).x - prop.Size.x * 0.5f;

        public static float MaxX(LastShiftDressingProp prop) =>
            WorldCenter(prop).x + prop.Size.x * 0.5f;

        public static float MinZ(LastShiftDressingProp prop) =>
            WorldCenter(prop).z - prop.Size.z * 0.5f;

        public static float MaxZ(LastShiftDressingProp prop) =>
            WorldCenter(prop).z + prop.Size.z * 0.5f;

        public static float BottomY(LastShiftDressingProp prop) =>
            WorldCenter(prop).y - prop.Size.y * 0.5f;

        public static float TopY(LastShiftDressingProp prop) =>
            WorldCenter(prop).y + prop.Size.y * 0.5f;
    }
}
