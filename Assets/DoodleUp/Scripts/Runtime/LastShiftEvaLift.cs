using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 코어 앞에서 조작 키를 눌렀을 때 다음에 일어날 일. <b>프롬프트와
    /// <see cref="LastShiftEvaLift.TryOperate"/> 가 같은 함수를 읽는다</b> — 두 벌로 두면
    /// "열기" 라고 적힌 자리에서 눌러도 안 열리는 상태가 생긴다.
    /// </summary>
    public enum LastShiftLiftAction
    {
        None,

        /// <summary>사이클이 도는 중. 누를 것이 없고 진행률만 보여 준다.</summary>
        Cycling,

        /// <summary>조종석 방향 게이트를 연다.</summary>
        OpenGate,

        /// <summary>게이트를 닫는다 — 광장에 선 사람의 동사다.</summary>
        CloseGate,

        /// <summary>발판에 올라선 사람의 동사. 올라가며 감압이 같이 돈다.</summary>
        Ascend,

        /// <summary>탑 정상 발판에 선 사람의 동사. 내려오며 재가압이 같이 돈다.</summary>
        Descend,

        /// <summary>조항 <c>O-4</c> — 구간 중에는 봉인이다.</summary>
        BlockedBySegment,

        /// <summary>발판이 위에 있어 게이트를 열 수 없다.</summary>
        BlockedByLiftAway
    }

    /// <summary>
    /// 승강 플랫폼. 광장 갑판과 탑 정상 사이를 오간다.
    ///
    /// <b>감압을 승강 중에 겹쳐 돈다.</b> game-balance 최종 검증에서 순차 실행은 첫 EVA 왕복이
    /// <c>39.70/40</c>초로 여유가 <c>0.3</c>초뿐이고, 겹치면 <c>28.70</c>초로 <c>11.30</c>초가
    /// 남는다 — 그래서 이것은 최적화가 아니라 <b>통과 조건</b>이다.
    ///
    /// 겹침이 성립하는 자리는 딱 하나, <see cref="TryAscend"/> 가 출발과 동시에 사이클을 거는
    /// 것이다. 도착해서 걸면 그 순간 순차가 되고 <c>11</c>초가 사라진다. 그래서 그 등식을
    /// EditMode 검사가 지킨다 — 코드를 읽고 "겹치는 것 같다" 로는 이 카드가 통과할 수 없다.
    ///
    /// 물리적으로도 이쪽이 맞다. 코어가 곧 챔버라(기획 확정 2026-08-11) 올라가는 동안 그
    /// 공간이 감압되는 것이고, 따로 서서 기다릴 자리가 애초에 없다.
    /// </summary>
    public static class LastShiftEvaLift
    {
        private const float Epsilon = 0.001f;

        /// <summary>플랫폼 바닥의 현재 높이.</summary>
        public static float Y { get; private set; } = LastShiftEvaShaft.DeckY;

        /// <summary>지금 향하는 높이. 안 움직이면 <see cref="Y"/> 와 같다.</summary>
        public static float TargetY { get; private set; } = LastShiftEvaShaft.DeckY;

        public static bool IsMoving => Mathf.Abs(TargetY - Y) > Epsilon;

        /// <summary>갑판에 서 있는가. <b>하단 게이트 인터록이 보는 값</b>이다.</summary>
        public static bool IsAtDeck => !IsMoving && Y <= LastShiftEvaShaft.DeckY + Epsilon;

        /// <summary>탑 정상에 서 있는가.</summary>
        public static bool IsAtHullTop => !IsMoving && Y >= LastShiftEvaShaft.TopHatchY - Epsilon;

        /// <summary>
        /// 올라간다 — <b>1단까지만</b>. 출발과 동시에 감압을 건다: 이 한 줄이 겹침의 전부다.
        ///
        /// 2단(문턱까지)은 <see cref="Tick"/> 이 감압 완료를 보고 이어서 올린다. 사람이 버튼을
        /// 두 번 누르게 하지 않는 이유는, 감압이 끝난 챔버에 그대로 서 있을 이유가 없어서다 —
        /// 나가려고 탄 것이고 그 사이에 할 수 있는 선택이 없다.
        /// </summary>
        public static bool TryAscend()
        {
            if (!IsAtDeck) return false;
            if (!LastShiftAirlock.TryBeginDepressurize()) return false;

            TargetY = LastShiftEvaShaft.DepressurizeStopY;
            Debug.Log($"[LAST_SHIFT_LIFT] depart=up stage=1 to={TargetY:F2} cycle=BEGIN");
            return true;
        }

        /// <summary>
        /// 내려온다 — 문턱에서 <b>1단 자리까지</b>. 올라갈 때와 대칭으로 출발과 동시에
        /// 재가압을 걸고, 재가압이 끝나면 <see cref="Tick"/> 이 갑판까지 마저 내린다.
        /// 재가압은 기항 게이트를 안 보므로(<c>RG-3</c> 영구 잠금 금지) 여기서도 안 본다.
        /// </summary>
        public static bool TryDescend()
        {
            if (!IsAtHullTop) return false;
            if (!LastShiftAirlock.TryBeginRepressurize()) return false;

            TargetY = LastShiftEvaShaft.DepressurizeStopY;
            Debug.Log($"[LAST_SHIFT_LIFT] depart=down stage=1 to={TargetY:F2} cycle=BEGIN");
            return true;
        }

        /// <summary>
        /// 시계를 한 스텝 돌린다. sandbox 가 <see cref="LastShiftAirlock.Tick"/> 과 같은 자리에서
        /// 부른다 — 둘이 같은 프레임에 돌아야 겹침이 실제로 겹친다.
        ///
        /// 1단에 도착해 있고 사이클이 끝났으면 여기서 2단을 잇는다.
        /// </summary>
        public static void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            if (IsMoving)
            {
                Y = Mathf.MoveTowards(Y, TargetY, LastShiftEvaShaft.LiftSpeed * deltaTime);
                if (Mathf.Abs(TargetY - Y) > Epsilon) return;
                Y = TargetY;
                Debug.Log($"[LAST_SHIFT_LIFT] arrive y={Y:F2} airlock={LastShiftAirlock.Phase}");
            }

            if (Mathf.Abs(Y - LastShiftEvaShaft.DepressurizeStopY) > Epsilon) return;

            // 감압이 끝났으면 문턱까지, 재가압이 끝났으면 갑판까지 — 1단 자리는 지나가는
            // 자리이지 서 있는 자리가 아니다.
            if (LastShiftAirlock.IsOuterHatchOpen) TargetY = LastShiftEvaShaft.TopHatchY;
            else if (LastShiftAirlock.IsInnerHatchOpen) TargetY = LastShiftEvaShaft.DeckY;
        }

        /// <summary>
        /// 이 자리에서 조작 키를 누르면 일어날 일.
        ///
        /// <b>게이트 개폐와 승강이 같은 키에 붙는다</b>(§23.6 — 수직 진입에 새 조작 동사를 안
        /// 만든다). 갈리는 자리는 딱 하나, <b>발자국 안인가</b> 다: 발판에 올라선 사람은
        /// 올라가려는 것이고 광장에 선 사람은 게이트를 닫으려는 것이다.
        ///
        /// 해석은 <see cref="LastShiftAirlock.NextAction"/> 이 이미 다 하고 있고 여기서는 그
        /// 결과를 승강 동사로 옮기기만 한다 — 위상 판단을 두 곳에 두지 않는다. 인터록 인자도
        /// 여기서 채운다(<see cref="IsAtDeck"/>): 발판이 위에 있는데 게이트가 열리면 코어 바닥이
        /// 뚫린 채로 열리는 것이다.
        /// </summary>
        public static LastShiftLiftAction NextAction(Vector3 position)
        {
            if (!LastShiftAirlock.IsWithinReach(position)) return LastShiftLiftAction.None;
            if (LastShiftAirlock.IsCycling) return LastShiftLiftAction.Cycling;

            var inShaft = LastShiftEvaShaft.Contains(position.x, position.z);
            return LastShiftAirlock.NextAction(position, !IsAtDeck) switch
            {
                LastShiftAirlockAction.OpenInner => LastShiftLiftAction.OpenGate,
                LastShiftAirlockAction.CloseInner => inShaft && IsAtDeck
                    ? LastShiftLiftAction.Ascend
                    : LastShiftLiftAction.CloseGate,
                // <b>발자국 안일 때만 내려간다.</b> 선체 위를 걷다가 눌러서 발판만 내려가면
                // 불러올 수단이 없어 밖에 갇힌다 — 조항 RG-3 이 금지하는 영구 잠금이다.
                LastShiftAirlockAction.Repressurize => inShaft && IsAtHullTop
                    ? LastShiftLiftAction.Descend
                    : LastShiftLiftAction.None,
                LastShiftAirlockAction.BlockedBySegment => LastShiftLiftAction.BlockedBySegment,
                LastShiftAirlockAction.BlockedByDeckHatch => LastShiftLiftAction.BlockedByLiftAway,
                _ => LastShiftLiftAction.None
            };
        }

        /// <summary>
        /// 조작 한 번. 갈래는 <see cref="NextAction"/> 이 정하므로 여기서 조건을 다시 안 본다.
        /// </summary>
        public static bool TryOperate(Vector3 position)
        {
            return NextAction(position) switch
            {
                LastShiftLiftAction.OpenGate => LastShiftAirlock.TryOpenInner(!IsAtDeck),
                LastShiftLiftAction.CloseGate => LastShiftAirlock.TryCloseInner(),
                LastShiftLiftAction.Ascend => TryAscend(),
                LastShiftLiftAction.Descend => TryDescend(),
                _ => false
            };
        }

        /// <summary>씬 전환·검사 격리용. 정적 상태라 안 지우면 다음 판으로 새어 나간다.</summary>
        public static void Clear()
        {
            Y = LastShiftEvaShaft.DeckY;
            TargetY = LastShiftEvaShaft.DeckY;
        }
    }
}
