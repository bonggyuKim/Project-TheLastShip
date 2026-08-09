using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 판정기가 보는 배치 하나. <see cref="LastShiftCompartmentSpec"/> 에서 <b>재는 데 쓰이는 것만</b>
    /// 남긴 형태다 — 발자국 넷, 문 하나, 부모 하나, 통행 여부.
    ///
    /// <b>왜 <c>LastShiftCompartmentSpec</c> 을 그대로 안 쓰는가.</b> RG-1 을 재는 표가 하나가 아니다.
    /// 정본 구획표(<c>LastShiftRg1GuardrailTests</c>)와 확정 배치표
    /// (<see cref="LastShiftPlazaLayout"/>)가 부모를 가리키는 방법조차 다르고(인덱스 대 광장 변),
    /// 판정기를 어느 한쪽 자료형에 묶으면 나머지는 사본을 계속 들고 있어야 한다. 자유 배치가
    /// 붙으면 <b>세 번째 사본</b>이 생긴다.
    ///
    /// 그래서 자를 표에서 떼어낸다. 표가 각자 이 형태로 옮겨 담고, 재는 것은 하나다.
    ///
    /// <b>폐기된 <c>LastShiftPlazaProposal</c> 이 세 번째 표였다.</b> 중앙 광장 확정안 §3.4 가 그
    /// 좌표 체계(통로 A/B·배플·개구부 다섯)를 폐지하면서 표와 그 검사 둘이 같이 나갔다.
    /// </summary>
    public readonly struct LastShiftPlacement
    {
        public LastShiftPlacement(
            float minX, float maxX, float minZ, float maxZ,
            Vector3 door, int parentIndex, bool passable = true)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
            Door = new Vector3(door.x, 0f, door.z);
            ParentIndex = parentIndex;
            Passable = passable;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        /// <summary>안쪽 문. <b>바닥면 좌표다</b> — 생성자가 <c>y</c> 를 <c>0</c> 으로 눕힌다.</summary>
        public Vector3 Door { get; }

        /// <summary>안쪽으로 잇는 상대의 표 인덱스. <c>-1</c> 이면 선체(주 통로)에 직접 붙는다.</summary>
        public int ParentIndex { get; }

        /// <summary>
        /// 지금 드나드는가. 잠긴 문은 그레이박스에서 구멍이 아니라 메운 판이라 이탈·최장 쌍
        /// 기본 계산에서 빠진다. 기항 개방(<c>docs/voyage-run-structure-v1.md</c> §4.2)이 끝난
        /// 상태를 재려면 <c>includeImpassable</c> 을 켠다.
        /// </summary>
        public bool Passable { get; }

        public float LengthX => MaxX - MinX;
        public float WidthZ => MaxZ - MinZ;

        /// <summary>정본 구획 하나를 판정기 입력으로 옮긴다. 부모 인덱스가 이미 인덱스라 그대로 산다.</summary>
        public static LastShiftPlacement From(in LastShiftCompartmentSpec spec) => new(
            spec.MinX, spec.MaxX, spec.MinZ, spec.MaxZ,
            spec.DoorPosition, spec.ParentIndex, spec.IsPassable);
    }

    /// <summary>
    /// <c>W-1</c> 최장 쌍에서 두 선체 문 사이를 무엇으로 재는가.
    ///
    /// <b>두 값이 있는 것은 사본 둘이 실제로 갈라져 있었기 때문이고, 그 갈라짐이 의도였다.</b>
    /// 현행 배는 부속 구획이 전부 <c>z</c> 얕은 자리에 붙어 있어 스파인을 <c>x</c> 차로 근사해도
    /// 되지만, 선수 조종석 + 중앙 광장 도안은 광장·선미 클러스터가 <c>z</c> 로 벌어져서 그
    /// 근사가 <b>과소평가로 뒤집힌다</b>(<c>docs/rg1-recalc-bow-cockpit-plaza-v1.md</c> §5).
    /// </summary>
    public enum LastShiftPairSpine
    {
        /// <summary>
        /// 두 선체 문의 <c>x</c> 차. 현행 정본 구획표가 고정한 값들이 이 자로 잰 것이다 —
        /// 표가 <c>z</c> 로 안 벌어져 있을 때만 쓸 수 있다.
        /// </summary>
        AlongLength = 0,

        /// <summary>
        /// 두 선체 문 사이 실거리. <b>자유 배치의 기본값이다</b> — 배치가 어디로 갈지 모르는
        /// 상황에서 <see cref="AlongLength"/> 근사는 안전한 쪽으로 틀리지 않는다.
        /// </summary>
        StraightLine = 1
    }

    /// <summary>
    /// 배치를 물린 이유. <b><c>W-1</c> 은 여기 없다</b> — 측정법 v1.1 §2.5 가 그것을 판정에서
    /// 내리고 설계 신호로 남겼다. 판정기는 값을 재서 <see cref="LastShiftPlacementVerdict.LongestPairMeters"/>
    /// 로 돌려주기만 하고, 그걸로 배치를 막지 않는다.
    /// </summary>
    [Flags]
    public enum LastShiftPlacementRejection
    {
        None = 0,

        /// <summary>기존 배치와 볼륨이 겹친다. 맞닿는 면(공유 벽)은 겹침이 아니다.</summary>
        OverlapsPlacement = 1 << 0,

        /// <summary>선체 내부(방·통로가 타일링한 영역)를 파고든다.</summary>
        OverlapsHullInterior = 1 << 1,

        /// <summary>부모 사슬이 선체에 안 닿는다 — 표 밖을 가리키거나 순환이다.</summary>
        ChainBroken = 1 << 2,

        /// <summary>사슬이 <c>maxDoorDepth</c> 보다 깊다.</summary>
        ChainTooDeep = 1 << 3,

        /// <summary>
        /// <c>RG-1(1)</c> 이탈이 한도를 넘는다. 이것이 판정기가 존재하는 이유다 —
        /// 지금까지 이 값은 EditMode 테스트 안에서만 재졌다.
        /// </summary>
        EgressOverLimit = 1 << 4
    }

    /// <summary>
    /// 배치 하나에 대한 판정. <b>거부 사유만 담지 않는다</b> — 통과했어도 이탈 거리·구역 귀속·
    /// 사슬 깊이는 그대로 필요하다(자유 배치 확장 검토 조항 F-1 의 구역 오버레이 등록).
    /// </summary>
    public readonly struct LastShiftPlacementVerdict
    {
        public LastShiftPlacementVerdict(
            LastShiftPlacementRejection rejection, int overlappingIndex,
            LastShiftZone zone, Vector3 hullDoor,
            int doorDepth, float chainMeters, float egressMeters, float longestPairMeters)
        {
            Rejection = rejection;
            OverlappingIndex = overlappingIndex;
            Zone = zone;
            HullDoor = hullDoor;
            DoorDepth = doorDepth;
            ChainMeters = chainMeters;
            EgressMeters = egressMeters;
            LongestPairMeters = longestPairMeters;
        }

        public LastShiftPlacementRejection Rejection { get; }

        public bool Accepted => Rejection == LastShiftPlacementRejection.None;

        /// <summary>처음으로 겹친 기존 배치의 인덱스. 안 겹쳤으면 <c>-1</c> 이다.</summary>
        public int OverlappingIndex { get; }

        /// <summary>
        /// 사슬 뿌리의 선체 문이 속한 압력 구역. 사슬이 끊겼으면 의미가 없다 —
        /// <see cref="LastShiftPlacementRejection.ChainBroken"/> 을 먼저 본다.
        /// </summary>
        public LastShiftZone Zone { get; }

        /// <summary>사슬 뿌리가 선체에 내는 문. 구역 귀속이 이 좌표에서 나온다.</summary>
        public Vector3 HullDoor { get; }

        /// <summary>선체까지 지나는 문의 수. 선체 직결이 <c>1</c> 이다. 사슬이 끊겼으면 <c>-1</c>.</summary>
        public int DoorDepth { get; }

        /// <summary>가장 먼 구석에서 선체 문까지의 사슬 거리.</summary>
        public float ChainMeters { get; }

        /// <summary><c>RG-1(1)</c> 이탈 거리 — 사슬 + 선체 문에서 구역 반대쪽 끝까지의 스파인.</summary>
        public float EgressMeters { get; }

        /// <summary><c>W-1</c>. 이 배치를 넣었을 때 <see cref="Zone"/> 구역 안 최장 동선이다.</summary>
        public float LongestPairMeters { get; }

        public float EgressSeconds => LastShiftPlacementRules.EgressSeconds(EgressMeters);
    }

    /// <summary>
    /// 배치 판정기. <c>RG-1(1)</c> 이탈과 <c>W-1</c> 최장 쌍을 <b>런타임에서</b> 잰다.
    ///
    /// <b>이 파일이 생긴 이유는 자가 테스트 어셈블리 안에만 있었기 때문이다.</b>
    /// <c>DoodleUp.Tests.EditMode</c> 는 <c>"includePlatforms": ["Editor"]</c> 에
    /// <c>autoReferenced: false</c> 라 런타임에서 부를 방법이 없었고, 그래서 이탈 거리 계산이
    /// 두 테스트 파일에 독립 사본으로 있었다. 자유 배치는 커서를 움직일 때마다 이 값을 다시
    /// 물어야 하므로 사본이 셋이 되거나 판정이 없거나 둘 중 하나였다 —
    /// <c>docs/tech/free-placement-runtime-chain-estimate-v1.md</c> §5.
    ///
    /// <b>비용은 문제가 아니었다.</b> <c>N=20</c> 에서 전체 재판정이 산술 수백 회다. 없었던 것은
    /// 계산이 아니라 <b>부를 자리</b>였다.
    /// </summary>
    public static class LastShiftPlacementRules
    {
        /// <summary>
        /// 가드레일 <c>(1)</c>. 한 구역에서 구역 밖으로 나가는 최악 시간의 한도.
        /// 정본은 <c>docs/ship-scale-and-density-v1.md</c> §5.4 다.
        /// </summary>
        public const float TraverseLimitSeconds = 10f;

        /// <summary>
        /// 압력문(구역 경계) 통과 시간. <b>조건부 가산이 아니라 상수다</b> — 구역을 벗어난다는
        /// 것은 정의상 구역 경계를 통과하는 것이고 그 경계에 있는 것이 압력문이다. 그리고 정확히
        /// 한 번이다. 구획 문은 압력문이 아니라 <c>0</c> 이다.
        /// 측정법 정본은 <c>docs/rg1-1-measurement-definition-v1.md</c> §1 (M-5).
        /// </summary>
        public const float PressureDoorSeconds = 0.8f;

        /// <summary>
        /// 사슬 깊이 상한. <b>이제는 기획이 정한 수다</b> —
        /// <c>docs/free-placement-chain-depth-cap-v1.md</c> §3 이 정본이다.
        ///
        /// <b>왜 <c>RG-1(1)</c> 만으로는 부족한가.</b> 이탈 한도가 실제로 물리는 것은 스파인이
        /// 긴 구역(조종석·산소실, <c>14m</c>)뿐이다. 전력실·냉각실은 구역 길이가 <c>5m</c> 라
        /// 선체 문에서 구역 끝까지가 <c>2.5m</c> 고, 보행 예산 <c>36.8m</c> 중 <c>34m</c> 가
        /// 사슬에 남는다. <c>2m</c> 짜리 방을 이으면 <b>깊이 <c>16</c> 까지 이탈 판정을
        /// 통과한다</b>. 깊이 상한은 그 구역에서만 물리며,
        /// 두 자가 서로 다른 실패를 막는다.
        ///
        /// <b>왜 <c>6</c> 인가.</b> 정본 구획표의 최대 깊이가 <c>4</c>(화장실→숙소→라운지→구명정)
        /// 이고, 상한은 "시작 배 최대 깊이 <c>+ 2</c>" 다. 어느 사슬 끝에도 두 칸이 남으므로
        /// 상한이 시작 상태만으로 확장을 봉인하지 않는다. 규약이 깨지는 것은
        /// <c>LastShiftPlacementVerdictTests.CanonicalDepthLeavesTwoLinksUnderTheCap</c> 가 잡는다.
        /// </summary>
        public const int MaxDoorDepth = 6;

        /// <summary>
        /// 깊이로 안 물리는 값. <b>기본값이 아니다</b> — 배치 UI 는 <see cref="MaxDoorDepth"/> 를
        /// 쓰고, 이 값은 깊이를 빼고 다른 사유만 보고 싶은 도구·테스트가 명시적으로 준다.
        /// </summary>
        public const int UnboundedDoorDepth = int.MaxValue;

        /// <summary>이탈 거리 → 가드레일 <c>(1)</c> 판정 시간. 압력문 한 번을 상수로 더한다.</summary>
        public static float EgressSeconds(float meters) =>
            meters / LastShiftPlayerController.MoveSpeed + PressureDoorSeconds;

        /// <summary>정본 구획표를 판정기 입력으로 옮긴다. 인덱스가 보존되므로 부모 사슬이 그대로 산다.</summary>
        public static LastShiftPlacement[] TableOf(IReadOnlyList<LastShiftCompartmentSpec> specs)
        {
            if (specs == null) throw new ArgumentNullException(nameof(specs));

            var table = new LastShiftPlacement[specs.Count];
            for (var index = 0; index < specs.Count; index++)
                table[index] = LastShiftPlacement.From(specs[index]);
            return table;
        }

        // ── 사슬 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 가장 먼 구석에서 선체에 붙는 문까지의 사슬 거리와, 그 선체 문 좌표·깊이.
        ///
        /// <paramref name="placement"/> 는 <paramref name="table"/> 안에 있어도 되고 아직 없어도
        /// 된다 — 후자가 배치 후보를 판정하는 자리다. 부모 인덱스만 표를 가리키면 된다.
        ///
        /// 사슬이 표 길이를 넘으면 순환이라 <c>false</c> 다. <c>+1</c> 을 두는 것은 후보가 표
        /// 밖에 있을 때 한 칸이 더 필요하기 때문이다.
        /// </summary>
        public static bool TryChainToHull(
            IReadOnlyList<LastShiftPlacement> table, in LastShiftPlacement placement,
            out float meters, out Vector3 hullDoor, out int doorDepth)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            meters = 0f;
            hullDoor = Vector3.zero;
            doorDepth = -1;

            // 자기 구석 넷 중 문에서 가장 먼 것. 이탈은 최악 출발점에서 재는 값이다.
            var door = placement.Door;
            meters = Mathf.Max(
                Mathf.Max(
                    Vector3.Distance(new Vector3(placement.MinX, 0f, placement.MinZ), door),
                    Vector3.Distance(new Vector3(placement.MinX, 0f, placement.MaxZ), door)),
                Mathf.Max(
                    Vector3.Distance(new Vector3(placement.MaxX, 0f, placement.MinZ), door),
                    Vector3.Distance(new Vector3(placement.MaxX, 0f, placement.MaxZ), door)));

            var current = placement;
            for (var step = 0; step <= table.Count; step++)
            {
                if (current.ParentIndex < 0)
                {
                    hullDoor = current.Door;
                    doorDepth = step + 1;
                    return true;
                }

                if (current.ParentIndex >= table.Count)
                {
                    meters = 0f;
                    return false;
                }

                var parent = table[current.ParentIndex];
                meters += Vector3.Distance(current.Door, parent.Door);
                current = parent;
            }

            meters = 0f;
            return false;
        }

        /// <summary>
        /// 선체까지 몇 개의 문을 지나는가. 선체 직결이 <c>1</c> 이고, 순환이면 <c>-1</c> 이다 —
        /// <see cref="LastShiftCompartments.DoorDepth"/> 와 같은 규약이다.
        /// </summary>
        public static int DoorDepth(IReadOnlyList<LastShiftPlacement> table, in LastShiftPlacement placement) =>
            TryChainToHull(table, placement, out _, out _, out var depth) ? depth : -1;

        /// <summary>
        /// 사슬 뿌리의 선체 문이 속한 압력 구역. 사슬이 끊겼으면 <c>false</c> 다.
        ///
        /// <b><see cref="LastShiftZoneAtlas.ResolveHull"/> 를 부른다 —
        /// <see cref="LastShiftZoneAtlas.Resolve"/> 가 아니다.</b> 후자는 이미 등록된 모듈을
        /// 먼저 보므로, 뿌리 좌표가 어느 모듈에 덮이면 새 배치의 구역이 그 모듈에서 나온다.
        /// 조항 F-1 이 말하는 뿌리는 언제나 <b>선체</b>이고, 판정기 안의 다섯 자리가 전부 같다.
        /// </summary>
        public static bool TryZoneOf(
            IReadOnlyList<LastShiftPlacement> table, in LastShiftPlacement placement, out LastShiftZone zone)
        {
            if (!TryChainToHull(table, placement, out _, out var hullDoor, out _))
            {
                zone = default;
                return false;
            }

            zone = LastShiftZoneAtlas.ResolveHull(hullDoor);
            return true;
        }

        // ── RG-1(1) 이탈 ────────────────────────────────────────────────────

        /// <summary>
        /// 가장 먼 구석에서 자기 구역을 빠져나갈 때까지의 거리. 사슬 거리에 선체 문에서 그 구역의
        /// 이탈구까지를 더한다. 가드레일 <c>(1)</c> 이 실제로 재는 것이 이 값이다.
        /// </summary>
        public static bool TryEgress(
            IReadOnlyList<LastShiftPlacement> table, in LastShiftPlacement placement,
            out float meters, out LastShiftZone zone)
        {
            meters = 0f;
            zone = default;

            if (!TryChainToHull(table, placement, out var chain, out var hullDoor, out _)) return false;

            zone = LastShiftZoneAtlas.ResolveHull(hullDoor);
            meters = chain + SpineToZoneEnd(hullDoor, zone);
            return true;
        }

        /// <summary>
        /// 선체 문에서 그 구역을 <b>벗어나는 압력문</b>까지. 이탈과 최장 쌍이 같은 자를 쓰게
        /// 하는 자리다.
        ///
        /// <b>x 스파인이 여기서 없어졌다.</b> 일자 스파인에서는 구역이 <c>x</c> 밴드라 "구역
        /// 반대쪽 끝" 이 <c>max(|x - ZoneMinX|, |x - ZoneMaxX|)</c> 였다. 방사형에서는 그 식이
        /// 두 번 틀린다 — 조종석 구역 경계 상자가 넷의 합집합이라 <c>x</c> 로 <c>23m</c> 를
        /// 걸치지만 그중 대부분이 다른 방이고, 전력실·냉각실은 같은 <c>x</c> 범위를 <c>z</c>
        /// 좌우로 나눠 써서 <c>x</c> 차가 이탈과 아무 상관이 없다.
        ///
        /// 남은 실제 요건은 <c>RG-1(1)</c> 정의 그대로다: <b>구역 밖으로 나가는 거리</b>.
        /// 방사형에서 그것은 광장 변 압력문 셋 중 이 구역의 이탈구까지이고, 어느 셋인지는
        /// <see cref="LastShiftPlazaLayout.IsExitFor"/> 가 고른다 — 고정 발자국을 재는
        /// <see cref="LastShiftPlazaLayout.WorstEgressMeters(LastShiftZone)"/> 와 같은 자다.
        /// </summary>
        public static float SpineToZoneEnd(Vector3 hullDoor, LastShiftZone zone)
        {
            var from = new Vector2(hullDoor.x, hullDoor.z);
            var best = float.MaxValue;
            foreach (var door in LastShiftPlazaLayout.Doors)
            {
                if (!LastShiftPlazaLayout.IsExitFor(door, zone)) continue;
                best = Mathf.Min(best, Vector2.Distance(from, door.Waypoint));
            }

            return best == float.MaxValue ? 0f : best;
        }

        /// <summary>
        /// 구역별 최장 이탈과 그 출발점. <c>Index</c> 가 <c>-1</c> 이면 붙은 구획이 아니라
        /// <b>고정 발자국 자체</b>가 최악이라는 뜻이다.
        ///
        /// 사슬이 끊긴 배치는 세지 않는다 — 그건 이 자가 답할 물음이 아니라
        /// <see cref="Evaluate"/> 가 먼저 물릴 것이다.
        /// </summary>
        public static (LastShiftZone Zone, float Meters, int Index)[] WorstEgressPerZone(
            IReadOnlyList<LastShiftPlacement> table, bool includeImpassable)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var worst = new (LastShiftZone Zone, float Meters, int Index)[LastShiftZoneAtlas.ZoneCount];
            for (var zone = (LastShiftZone)0; (int)zone < LastShiftZoneAtlas.ZoneCount; zone++)
                worst[(int)zone] = (zone, LastShiftPlazaLayout.WorstEgressMeters(zone), -1);

            for (var index = 0; index < table.Count; index++)
            {
                var placement = table[index];
                if (!placement.Passable && !includeImpassable) continue;
                if (!TryEgress(table, placement, out var meters, out var zone)) continue;
                if (meters > worst[(int)zone].Meters) worst[(int)zone] = (zone, meters, index);
            }

            return worst;
        }

        // ── W-1 최장 쌍 ─────────────────────────────────────────────────────

        /// <summary>
        /// 구역별 "같은 구역 안 두 점 사이 최장 거리". <b>가드레일 판정이 아니라 설계 신호다</b> —
        /// 측정법 v1.1 §2.4·§2.5. 한도는 없고 래칫만 있다.
        ///
        /// 후보 셋 중 최대다. (가) 고정 발자국만으로 나오는 최악 이탈, (나) 배치 안쪽 구석 → 구역 끝
        /// (= 이탈값), (다) 같은 구역에 붙은 배치 둘의 안쪽 구석끼리 — 각자의 사슬에 두 선체 문
        /// 사이 스파인을 더한다. (다) 의 스파인을 무엇으로 재는지가 <paramref name="spine"/> 다.
        ///
        /// 같은 배치 안의 두 점은 쌍이 아니다 — 자기 자신과 짝지으면 사슬을 두 번 세서 실제로
        /// 걸을 수 없는 거리가 나온다.
        /// </summary>
        public static float[] LongestPairPerZone(
            IReadOnlyList<LastShiftPlacement> table, bool includeImpassable,
            LastShiftPairSpine spine = LastShiftPairSpine.StraightLine)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var longest = new float[LastShiftZoneAtlas.ZoneCount];
            for (var zone = (LastShiftZone)0; (int)zone < LastShiftZoneAtlas.ZoneCount; zone++)
                longest[(int)zone] = LastShiftPlazaLayout.WorstEgressMeters(zone);

            var open = CollectOpen(table, includeImpassable);
            AccumulatePairs(longest, open, spine);
            return longest;
        }

        /// <summary>
        /// 후보 하나를 <paramref name="table"/> 에 <b>넣었다고 치고</b> 그 후보가 속할 구역의
        /// <c>W-1</c> 을 잰다. <paramref name="ignoreIndex"/> 는 이미 표에 있는 배치를 옮기는
        /// 자리다 — 안 빼면 옮기기 전 자리와 옮긴 자리가 둘 다 세어져 쌍이 부풀어 오른다.
        ///
        /// 사슬이 끊긴 후보는 구역이 없으므로 <c>0</c> 이다.
        /// </summary>
        public static float LongestPairWith(
            IReadOnlyList<LastShiftPlacement> table, in LastShiftPlacement candidate,
            bool includeImpassable, LastShiftPairSpine spine = LastShiftPairSpine.StraightLine,
            int ignoreIndex = -1)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (!TryChainToHull(table, candidate, out var chain, out var hullDoor, out _)) return 0f;

            var zone = LastShiftZoneAtlas.ResolveHull(hullDoor);
            var open = CollectOpen(table, includeImpassable, ignoreIndex);
            if (candidate.Passable || includeImpassable)
                open.Add((zone, chain, hullDoor, chain + SpineToZoneEnd(hullDoor, zone)));

            var longest = new float[LastShiftZoneAtlas.ZoneCount];
            for (var each = (LastShiftZone)0; (int)each < LastShiftZoneAtlas.ZoneCount; each++)
                longest[(int)each] = LastShiftPlazaLayout.WorstEgressMeters(each);

            AccumulatePairs(longest, open, spine);
            return longest[(int)zone];
        }

        private static List<(LastShiftZone Zone, float Chain, Vector3 HullDoor, float Egress)> CollectOpen(
            IReadOnlyList<LastShiftPlacement> table, bool includeImpassable, int ignoreIndex = -1)
        {
            var open = new List<(LastShiftZone, float, Vector3, float)>();
            for (var index = 0; index < table.Count; index++)
            {
                if (index == ignoreIndex) continue;

                var placement = table[index];
                if (!placement.Passable && !includeImpassable) continue;
                if (!TryChainToHull(table, placement, out var chain, out var hullDoor, out _)) continue;

                var zone = LastShiftZoneAtlas.ResolveHull(hullDoor);
                open.Add((zone, chain, hullDoor, chain + SpineToZoneEnd(hullDoor, zone)));
            }

            return open;
        }

        private static void AccumulatePairs(
            float[] longest,
            List<(LastShiftZone Zone, float Chain, Vector3 HullDoor, float Egress)> open,
            LastShiftPairSpine spine)
        {
            for (var i = 0; i < open.Count; i++)
            {
                var a = open[i];
                longest[(int)a.Zone] = Mathf.Max(longest[(int)a.Zone], a.Egress);

                for (var j = i + 1; j < open.Count; j++)
                {
                    var b = open[j];
                    if (b.Zone != a.Zone) continue;

                    var between = spine == LastShiftPairSpine.AlongLength
                        ? Mathf.Abs(a.HullDoor.x - b.HullDoor.x)
                        : Vector3.Distance(a.HullDoor, b.HullDoor);
                    longest[(int)a.Zone] = Mathf.Max(longest[(int)a.Zone], a.Chain + between + b.Chain);
                }
            }
        }

        // ── 판정 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 배치 후보 하나를 판정한다. <paramref name="ignoreIndex"/> 는 <b>이미 표에 있는 배치를
        /// 옮기는</b> 자리다 — 자기 자신과의 겹침은 겹침이 아니다.
        ///
        /// <b>거부는 다 모아서 돌려준다.</b> 첫 사유에서 멈추면 커서를 움직이는 사람이 하나를
        /// 고칠 때마다 다음 사유를 새로 만나고, 무엇이 몇 개 남았는지가 안 보인다.
        ///
        /// <paramref name="maxDoorDepth"/> 의 기본값은 <see cref="MaxDoorDepth"/> 다 — 깊이로
        /// 안 물리려면 <see cref="UnboundedDoorDepth"/> 를 명시적으로 준다.
        /// </summary>
        public static LastShiftPlacementVerdict Evaluate(
            IReadOnlyList<LastShiftPlacement> table, in LastShiftPlacement candidate,
            int ignoreIndex = -1, bool includeImpassable = false,
            LastShiftPairSpine spine = LastShiftPairSpine.StraightLine,
            int maxDoorDepth = MaxDoorDepth)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));

            var rejection = LastShiftPlacementRejection.None;

            // 겹침 — O(N).
            var overlapping = -1;
            for (var index = 0; index < table.Count; index++)
            {
                if (index == ignoreIndex) continue;
                if (!Overlaps(candidate, table[index])) continue;
                overlapping = index;
                rejection |= LastShiftPlacementRejection.OverlapsPlacement;
                break;
            }

            // 선체 침범 — O(1). 정본 판정을 그대로 쓴다.
            if (LastShiftCompartments.OverlapsHullInterior(
                    candidate.MinX, candidate.MaxX, candidate.MinZ, candidate.MaxZ))
                rejection |= LastShiftPlacementRejection.OverlapsHullInterior;

            // 사슬 — O(깊이).
            if (!TryChainToHull(table, candidate, out var chain, out var hullDoor, out var depth))
                return new LastShiftPlacementVerdict(
                    rejection | LastShiftPlacementRejection.ChainBroken,
                    overlapping, default, Vector3.zero, -1, 0f, 0f, 0f);

            if (depth > maxDoorDepth) rejection |= LastShiftPlacementRejection.ChainTooDeep;

            // 구역 귀속 — 사슬 뿌리의 선체 문이 정한다(자유 배치 확장 검토 조항 F-1).
            var zone = LastShiftZoneAtlas.ResolveHull(hullDoor);

            // RG-1(1) 이탈 — O(깊이).
            var egress = chain + SpineToZoneEnd(hullDoor, zone);
            if (EgressSeconds(egress) >= TraverseLimitSeconds)
                rejection |= LastShiftPlacementRejection.EgressOverLimit;

            // W-1 — O(N^2). 판정이 아니라 관측이라 거부 사유가 없다(측정법 v1.1 §2.5).
            var pair = LongestPairWith(table, candidate, includeImpassable, spine, ignoreIndex);

            return new LastShiftPlacementVerdict(
                rejection, overlapping, zone, hullDoor, depth, chain, egress, pair);
        }

        /// <summary>맞닿는 면(공유 벽)은 겹침이 아니다 — 정본과 같은 열린 구간 비교다.</summary>
        public static bool Overlaps(in LastShiftPlacement a, in LastShiftPlacement b) =>
            LastShiftCompartments.VolumesOverlap(
                a.MinX, a.MaxX, a.MinZ, a.MaxZ,
                b.MinX, b.MaxX, b.MinZ, b.MaxZ);
    }
}
