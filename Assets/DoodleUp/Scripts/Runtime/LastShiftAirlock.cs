using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 에어록이 지금 어느 단계인가. <b>네 단계뿐이고 그 사이에 다른 상태를 안 만든다</b> —
    /// 해치가 둘이므로 "둘 다 닫힘 / 안쪽만 / 사이클 / 바깥쪽만" 이면 전부 덮인다.
    /// </summary>
    public enum LastShiftAirlockPhase
    {
        /// <summary>둘 다 닫힘. 배 안에서 본 기본 상태이고 구간 중에는 여기서 안 움직인다.</summary>
        Sealed,

        /// <summary>안쪽 해치가 열려 덕트와 통한다. 승무원이 들어오고 나가는 자리다.</summary>
        InnerOpen,

        /// <summary>둘 다 닫힌 채로 감압/재가압이 도는 중. 이 단계에서만 시계가 필요하다.</summary>
        Cycling,

        /// <summary>바깥 해치가 열려 선외와 통한다. <c>EVA</c> 가 성립하는 유일한 단계다.</summary>
        OuterOpen
    }

    /// <summary>
    /// 이 자리에서 에어록을 조작하면 무엇이 일어나는가. 막힌 갈래도 값으로 두는 이유는
    /// 프롬프트가 <b>왜 안 되는지</b>를 적어야 하기 때문이다 — 사망한 승무원에게 문이
    /// "조작 불가" 를 보여 주던 것과 같은 규약이다.
    /// </summary>
    public enum LastShiftAirlockAction
    {
        /// <summary>사거리 밖이거나 사이클이 도는 중. 프롬프트를 안 띄운다.</summary>
        None,

        OpenInner,
        CloseInner,
        Depressurize,
        Repressurize,

        /// <summary>구간 중이라 안 열린다 — 조항 <c>O-4</c>.</summary>
        BlockedBySegment,

        /// <summary>갑판 승강구 해치가 열려 있어 안 열린다 — 인터록 셋째 조건.</summary>
        BlockedByDeckHatch
    }

    /// <summary>
    /// 에어록 개방 시퀀스 — <c>docs/outboard-outpost-and-map-final-v1.md</c> §4.1 이 "별도 카드"
    /// 로 남겨 둔 항목이고, <see cref="LastShiftBypassDuct.AirlockInnerHatchSealed"/> 주석이
    /// 가리키던 그 카드가 이것이다.
    ///
    /// <b>좌표를 새로 안 만든다.</b> 에어록 <c>3×3×3</c> 과 안팎 해치의 y 는
    /// <see cref="LastShiftBypassDuct"/> 가 이미 확정해 뒀다(§23.5). 여기서 서는 것은
    /// <b>상태와 그 상태로 갈 수 있는 조건</b>뿐이다.
    ///
    /// <b>인터록이 이 파일의 핵심이다.</b> 조건 셋이 동시에 걸린다.
    /// <list type="number">
    /// <item><b>기항에서만 열린다</b>(조항 <c>O-4</c>). 구간 중에 열면 이탈 시간 계산의 종점이
    /// 배 밖으로 나가고 <c>RG-1(1)</c>·<c>(4-b)</c> 를 통째로 다시 짜야 한다. 판정은
    /// <see cref="LastShiftVoyage.LastTransition"/> 이 기항을 가리키는가 하나로 본다 —
    /// <see cref="LastShiftMaintenance.IsAtPort"/> 는 "한 번이라도 기항했는가" 라서 구간
    /// 중에도 참이고, 그걸 쓰면 게이트가 사실상 없다.</item>
    /// <item><b>두 해치가 동시에 안 열린다.</b> 실제 에어록의 정의이고, 여기서는 그것이
    /// 감압 사이클이 존재할 이유가 된다.</item>
    /// <item><b>안쪽 해치와 갑판 승강구 해치가 동시에 안 열린다.</b> 이게 없으면
    /// <see cref="LastShiftBypassDuct.DeepestFallY"/> 가 지키던 성질이 깨진다 —
    /// 갑판 구멍으로 떨어진 물건이 덕트 바닥(<c>-1.2</c>)이 아니라 에어록 바닥(<c>-4.2</c>)까지
    /// 내려가고, 거기서 갑판까지 <c>4.2m</c> 는 점프 정점 <c>1.49m</c> 로 못 올라온다.
    /// <c>LastShiftDeckHatchTests</c> 가 "안쪽 해치를 여는 날 이 성질이 깨진다" 고 미리
    /// 적어 둔 자리이고, <b>답이 형상이 아니라 인터록이다.</b></item>
    /// </list>
    ///
    /// <b><see cref="MonoBehaviour"/> 가 아니고 정적이다.</b> 에어록은 배에 하나뿐이고
    /// (좌표가 <see cref="LastShiftBypassDuct.AirlockCenterX"/> 하나다), 상태를 씬 오브젝트가
    /// 들면 클라이언트에서 서버와 다른 해치가 열린다 — <see cref="LastShiftDeckHatch"/> 가
    /// 자기 상태를 안 들고 sandbox 를 되묻는 것과 같은 이유다. 여기서는 그 정본이 sandbox 가
    /// 아니라 이 정적 상태이고, sandbox 는 <see cref="Tick"/> 만 돌린다.
    /// </summary>
    public static class LastShiftAirlock
    {
        /// <summary>
        /// 감압/재가압 한 번에 드는 시간. <b>수치는 <c>game-balance</c> 소관이고</b> 여기서
        /// 정하는 것은 축 하나다 — 문(<c>0.8초</c>)보다 확실히 길어야 "해치 하나 여는 것"과
        /// 다른 절차로 읽힌다. 왕복에 두 번 도므로 <c>80</c>초 예산에서 <c>8</c>초를 먹는다.
        /// </summary>
        public const float CycleSeconds = 4f;

        /// <summary>
        /// 선외 보행면의 y. <b>배 밑면과 같은 평면이다</b> — 바깥 해치가 거기 있으므로
        /// (<see cref="LastShiftBypassDuct.AirlockFloorY"/>), 나가는 순간 발이 닿는 면도 같아야
        /// 새 이동 동사(추진·유영)가 필요 없다. §4.1-3 이 별도 씬을 물리친 것과 같은 절약이고,
        /// 연출은 우주복 자력 부츠가 잡는다.
        /// </summary>
        public const float OutsideWalkY = LastShiftBypassDuct.AirlockFloorY;

        /// <summary>
        /// 선외 판정에 쓰는 여유. 배 밑면과 정확히 같은 높이를 <c>0</c> 여유로 가르면 발 높이
        /// 반올림 하나로 진공 판정이 깜빡인다.
        /// </summary>
        public const float OutsideMargin = 0.05f;

        /// <summary>
        /// 선외에서 산소가 이만큼 남으면 강제로 복귀시킨다(조항 <c>O-7</c>). <c>0</c> 이 아닌
        /// 이유는 복귀 자체가 순간이 아니어야 하기 때문이 아니라, <b><c>0</c> 에서 가로채면
        /// <see cref="LastShiftCrewOxygen.Tick"/> 의 사망 판정과 같은 프레임에 걸려 어느 쪽이
        /// 먼저인지가 프레임률에 달리기 때문이다.</b> 여유 한 칸을 두면 순서가 고정된다.
        /// </summary>
        public const float EvaReturnReserve = 0.02f;

        private static float cycleRemaining;

        /// <summary>지금 단계.</summary>
        public static LastShiftAirlockPhase Phase { get; private set; } = LastShiftAirlockPhase.Sealed;

        /// <summary>사이클이 끝나면 갈 단계. <see cref="LastShiftAirlockPhase.Cycling"/> 일 때만 의미가 있다.</summary>
        public static LastShiftAirlockPhase CycleTarget { get; private set; } = LastShiftAirlockPhase.Sealed;

        public static bool IsInnerHatchOpen => Phase == LastShiftAirlockPhase.InnerOpen;

        public static bool IsOuterHatchOpen => Phase == LastShiftAirlockPhase.OuterOpen;

        public static bool IsSealed => Phase == LastShiftAirlockPhase.Sealed;

        public static bool IsCycling => Phase == LastShiftAirlockPhase.Cycling;

        /// <summary>사이클 진행률 <c>0~1</c>. 게이지가 읽는 값이다.</summary>
        public static float CycleProgress =>
            IsCycling ? Mathf.Clamp01(1f - cycleRemaining / CycleSeconds) : 0f;

        /// <summary>
        /// 기항에 정박해 있는가 — 조항 <c>O-4</c> 게이트. 구간 판정이 기항을 가리킬 때만 참이고,
        /// 다음 구간에 들어가면(<see cref="LastShiftVoyage.EnterSegment"/> 가 판정을
        /// <see cref="LastShiftSegmentTransition.Pending"/> 으로 되돌린다) 곧바로 거짓이 된다.
        /// </summary>
        public static bool IsAtPort =>
            LastShiftVoyage.LastTransition is LastShiftSegmentTransition.ToPort
                or LastShiftSegmentTransition.TowedToPort;

        /// <summary>
        /// 이 좌표가 선외인가. <b>덕트·에어록을 먼저 빼는 것이 핵심이다</b> — 그 둘은 배 밑면
        /// 아래에 있지만 선외가 아니고, 그 판정은 이미 <see cref="LastShiftBypassDuct"/> 가
        /// 갖고 있다. 빼지 않으면 에어록 바닥에 서 있는 것과 바깥 해치로 나간 것이 같은
        /// 좌표대라 갈리지 않는다.
        ///
        /// 남은 둘 중 하나면 선외다 — <b>배 밑면 아래</b>(바깥 해치로 나간 자리)이거나
        /// <b>원반 테두리 밖</b>(잔해까지 가는 길)이다. 배 안 좌표는 어느 쪽에도 안 걸린다:
        /// 갑판은 <c>y = 0</c> 으로 밑면(<see cref="LastShiftHullShell.RimBaseY"/>) 위이고,
        /// 방은 전부 원반(<c>42 × 20</c>) 한참 안쪽이다.
        /// </summary>
        public static bool IsOutside(Vector3 position)
        {
            if (LastShiftBypassDuct.Contains(position) ||
                LastShiftBypassDuct.ShaftContains(position)) return false;

            return position.y < LastShiftHullShell.RimBaseY - OutsideMargin ||
                   !LastShiftHullShell.Contains(position.x, position.z);
        }

        /// <summary>
        /// 선외로 나갈 수 있는가 — 바깥 해치가 열려 있는 동안만이다. 사람이 통과하는 문이
        /// <b>열린 동안에만</b> 통행이 되는 것은 갑판 해치와 같은 규칙이고, 여기서는 그것이
        /// 조항 <c>O-4</c> 를 형상으로 만든다.
        /// </summary>
        public static bool IsOpenForEva => IsOuterHatchOpen;

        /// <summary>
        /// 선외에서 돌아오는 자리 — 에어록 바닥 한가운데다. 조항 <c>O-7</c> 자동 복귀와
        /// 사이클이 끝났을 때의 기준점이 <b>같은 좌표여야 한다</b>. 두 벌로 두면 산소가 마른
        /// 승무원이 돌아온 자리와 걸어 들어온 자리가 달라진다.
        /// </summary>
        public static Vector3 ReturnPoint => new(
            LastShiftBypassDuct.AirlockCenterX,
            LastShiftBypassDuct.AirlockFloorY,
            LastShiftBypassDuct.AirlockCenterZ);

        // ── 전이 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 안쪽 해치를 열 수 있는가. 인터록 셋 중 둘이 여기 있다 — 기항이어야 하고
        /// (조항 <c>O-4</c>), <paramref name="anyDeckHatchOpen"/> 이면 안 된다.
        ///
        /// 갑판 해치 상태를 인자로 받는 이유는 그것이 sandbox 소관이기 때문이다. 여기서
        /// 씬을 조회하면 EditMode 검사가 씬을 세워야 하고, 무엇보다 <b>정적 상태가 씬을
        /// 되묻는 방향</b>이 생겨 클라이언트에서 값이 갈린다.
        /// </summary>
        public static bool CanOpenInner(bool anyDeckHatchOpen) =>
            Phase == LastShiftAirlockPhase.Sealed && IsAtPort && !anyDeckHatchOpen;

        /// <summary>안쪽 해치를 연다. 조건은 <see cref="CanOpenInner"/> 그대로다.</summary>
        public static bool TryOpenInner(bool anyDeckHatchOpen)
        {
            if (!CanOpenInner(anyDeckHatchOpen)) return false;

            Phase = LastShiftAirlockPhase.InnerOpen;
            Debug.Log("[LAST_SHIFT_AIRLOCK] hatch=inner state=OPEN");
            return true;
        }

        /// <summary>안쪽 해치를 닫는다. 되돌리기이므로 조건이 없다 — 봉인은 언제나 안전한 쪽이다.</summary>
        public static bool TryCloseInner()
        {
            if (Phase != LastShiftAirlockPhase.InnerOpen) return false;

            Phase = LastShiftAirlockPhase.Sealed;
            Debug.Log("[LAST_SHIFT_AIRLOCK] hatch=inner state=CLOSED");
            return true;
        }

        /// <summary>
        /// 감압을 건다 — 안쪽이 열린 상태에서만이다. <b>안쪽 해치는 여기서 자동으로 닫힌다</b>:
        /// 감압은 닫힌 챔버에서만 성립하고, 별도로 닫게 시키면 "닫고 나서 감압" 두 동작이
        /// 되는데 그 사이 상태에 이름을 붙일 게 없다.
        /// </summary>
        public static bool TryBeginDepressurize()
        {
            if (Phase != LastShiftAirlockPhase.InnerOpen || !IsAtPort) return false;

            BeginCycle(LastShiftAirlockPhase.OuterOpen);
            return true;
        }

        /// <summary>
        /// 재가압을 건다 — 바깥이 열린 상태에서만이다. <b>기항 게이트를 안 본다</b>: 밖에 있는
        /// 승무원이 돌아올 길은 언제나 열려 있어야 하고, 그게 <c>RG-3</c>(영구 잠금 금지)다.
        /// </summary>
        public static bool TryBeginRepressurize()
        {
            if (Phase != LastShiftAirlockPhase.OuterOpen) return false;

            BeginCycle(LastShiftAirlockPhase.InnerOpen);
            return true;
        }

        // ── 조작 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 조작 사거리(<c>xz</c>). 에어록 한 변이 <c>3m</c> 라 중심에서 벽까지가 <c>1.5m</c> 이고,
        /// <b>그 값을 그대로 쓴다</b> — 안에 서 있으면 어디서든 닿고 밖에서는 해치 바로 앞에서만
        /// 닿는다. 승강구(<c>1.2</c>)보다 넉넉한 것은 여기가 방 안이 아니라 챔버라서 스폰 지점·
        /// 부품 자리와 겹칠 일이 없기 때문이다.
        /// </summary>
        public const float ReachDistance = LastShiftBypassDuct.AirlockSize * 0.5f;

        /// <summary>
        /// 안쪽과 바깥쪽을 가르는 높이. 챔버 한가운데다 — 두 해치가 같은 <c>xz</c> 에 위아래로
        /// 있으므로(§23.5) 어느 쪽을 조작하려는지는 <b>서 있는 높이</b>로만 갈린다.
        /// </summary>
        public static float MidY =>
            (LastShiftBypassDuct.AirlockFloorY + LastShiftBypassDuct.AirlockCeilingY) * 0.5f;

        /// <summary>
        /// 에어록을 조작할 수 있는 자리인가. <c>xz</c> 는 챔버 반경, <c>y</c> 는 덕트 바닥 위
        /// 웅크림 높이부터 선외 보행면까지다 — 위로는 덕트에서 안쪽 해치를 열 수 있어야 하고,
        /// 아래로는 밖에서 바깥 해치를 열 수 있어야 한다.
        /// </summary>
        public static bool IsWithinReach(Vector3 position)
        {
            if (Mathf.Abs(position.x - LastShiftBypassDuct.AirlockCenterX) > ReachDistance) return false;
            if (Mathf.Abs(position.z - LastShiftBypassDuct.AirlockCenterZ) > ReachDistance) return false;

            return position.y >= LastShiftBypassDuct.AirlockFloorY - ReachDistance &&
                   position.y <= LastShiftBypassDuct.CeilingY;
        }

        /// <summary>이 자리에서 조작하려는 것이 안쪽 해치인가. 챔버 한가운데를 경계로 가른다.</summary>
        public static bool IsAtInnerSide(Vector3 position) => position.y >= MidY;

        /// <summary>
        /// 이 자리에서 다음에 일어날 일. 프롬프트와 <see cref="TryOperate"/> 가 <b>같은 함수를
        /// 읽는다</b> — 두 벌로 두면 "열기" 라고 적힌 자리에서 눌러도 안 열리는 상태가 생긴다.
        /// </summary>
        public static LastShiftAirlockAction NextAction(Vector3 position, bool anyDeckHatchOpen)
        {
            if (!IsWithinReach(position)) return LastShiftAirlockAction.None;
            if (IsCycling) return LastShiftAirlockAction.None;

            if (IsAtInnerSide(position))
            {
                if (Phase == LastShiftAirlockPhase.InnerOpen) return LastShiftAirlockAction.CloseInner;
                if (Phase != LastShiftAirlockPhase.Sealed) return LastShiftAirlockAction.None;
                if (!IsAtPort) return LastShiftAirlockAction.BlockedBySegment;
                return anyDeckHatchOpen
                    ? LastShiftAirlockAction.BlockedByDeckHatch
                    : LastShiftAirlockAction.OpenInner;
            }

            return Phase switch
            {
                LastShiftAirlockPhase.InnerOpen => LastShiftAirlockAction.Depressurize,
                LastShiftAirlockPhase.OuterOpen => LastShiftAirlockAction.Repressurize,
                _ => LastShiftAirlockAction.None
            };
        }

        /// <summary>
        /// 에어록을 조작한다. <see cref="NextAction"/> 이 고른 것을 그대로 실행하고, 막힌
        /// 갈래(<see cref="LastShiftAirlockAction.BlockedBySegment"/> 등)는 거짓을 돌려준다 —
        /// 왜 막혔는지는 프롬프트가 이미 적고 있다.
        /// </summary>
        public static bool TryOperate(Vector3 position, bool anyDeckHatchOpen) =>
            NextAction(position, anyDeckHatchOpen) switch
            {
                LastShiftAirlockAction.OpenInner => TryOpenInner(anyDeckHatchOpen),
                LastShiftAirlockAction.CloseInner => TryCloseInner(),
                LastShiftAirlockAction.Depressurize => TryBeginDepressurize(),
                LastShiftAirlockAction.Repressurize => TryBeginRepressurize(),
                _ => false
            };

        /// <summary>
        /// 조항 <c>O-7</c> 자동 복귀가 부르는 구조 경로 — <b>인터록을 건너뛴다.</b>
        /// 산소가 마른 승무원을 에어록 바닥에 내려놓는 것만으로는 죽는 것을 못 막는다
        /// (챔버도 비가압이다). 안쪽이 열려 있어야 배 안으로 올라올 수 있으므로 여기서
        /// 사이클을 건너뛰고 <see cref="LastShiftAirlockPhase.InnerOpen"/> 으로 앉힌다.
        ///
        /// <b>부르는 쪽이 갑판 해치를 닫아야 한다.</b> 인터록의 셋째 조건(갑판 구멍과 동시
        /// 개방 금지)이 이 경로에서만 깨지는데, 그 조건이 지키는 것은 낙하 회수이고
        /// 갑판 해치를 닫으면 그대로 성립한다. sandbox 가 그 한 줄을 같이 돈다.
        /// </summary>
        public static void ForceRescueEntry()
        {
            Phase = LastShiftAirlockPhase.InnerOpen;
            CycleTarget = LastShiftAirlockPhase.InnerOpen;
            cycleRemaining = 0f;
            Debug.Log("[LAST_SHIFT_AIRLOCK] rescue=INNER_OPEN reason=eva-oxygen-reserve");
        }

        private static void BeginCycle(LastShiftAirlockPhase target)
        {
            Phase = LastShiftAirlockPhase.Cycling;
            CycleTarget = target;
            cycleRemaining = CycleSeconds;
            Debug.Log($"[LAST_SHIFT_AIRLOCK] cycle=BEGIN target={target} seconds={CycleSeconds:F1}");
        }

        /// <summary>
        /// 사이클 시계를 한 스텝 돌린다. sandbox 가 매 tick 부르고, 사이클이 안 도는 동안에는
        /// 아무 일도 안 한다 — 시계가 필요한 단계가 하나뿐이라 상태기 전체를 매 프레임
        /// 훑을 이유가 없다.
        /// </summary>
        public static void Tick(float deltaTime)
        {
            if (!IsCycling || deltaTime <= 0f) return;

            cycleRemaining -= deltaTime;
            if (cycleRemaining > 0f) return;

            cycleRemaining = 0f;
            Phase = CycleTarget;
            Debug.Log($"[LAST_SHIFT_AIRLOCK] cycle=END state={Phase}");
        }

        /// <summary>
        /// 사이클을 즉시 끝낸다. 테스트와 씬 복원이 쓰는 경계이고, 게임 입력에는 없다 —
        /// <see cref="LastShiftDeckHatch.SnapToState"/> 와 같은 자리다.
        /// </summary>
        public static void SnapCycle()
        {
            if (!IsCycling) return;

            cycleRemaining = 0f;
            Phase = CycleTarget;
        }

        // ── 구간 전환 ───────────────────────────────────────────────────────

        /// <summary>
        /// 구간에 들어간다 — <b>무조건 봉인이다</b>(조항 <c>O-4</c>). 기항 게이트를 조회로만
        /// 두면 밖에 나간 채로 출항하는 상태가 남고, 그러면 <c>RG-1</c> 이탈 시간 계산의
        /// 종점이 배 밖이 된다. 여기서 상태를 되돌리는 것이 그 창을 닫는 유일한 자리다.
        ///
        /// <b>선외에 남은 자재는 여기서 잃는다</b> — 조항 <c>O-7</c> 의 같은 규칙이고,
        /// 출항이 곧 그 기항의 미회수분을 확정한다.
        /// </summary>
        public static void SealForSegment()
        {
            Phase = LastShiftAirlockPhase.Sealed;
            CycleTarget = LastShiftAirlockPhase.Sealed;
            cycleRemaining = 0f;
        }

        /// <summary>
        /// 서버가 보낸 에어록 상태를 그대로 앉힌다. 클라이언트 전용이고 여기서 조건을 다시
        /// 보지 않는다 — 인터록을 클라이언트에서 한 번 더 돌리면 갑판 해치 상태가 한 tick
        /// 어긋난 순간 두 화면의 해치가 서로 다르게 열린다.
        /// </summary>
        public static void ApplyNetworkState(
            LastShiftAirlockPhase phase, LastShiftAirlockPhase cycleTarget, float remaining)
        {
            Phase = phase;
            CycleTarget = cycleTarget;
            cycleRemaining = Mathf.Clamp(remaining, 0f, CycleSeconds);
        }

        public static void Clear()
        {
            Phase = LastShiftAirlockPhase.Sealed;
            CycleTarget = LastShiftAirlockPhase.Sealed;
            cycleRemaining = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Clear();
    }
}
