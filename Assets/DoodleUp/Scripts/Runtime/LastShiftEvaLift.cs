using UnityEngine;

namespace DoodleUp.Runtime
{
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
        /// 올라간다. <b>출발과 동시에 감압을 건다</b> — 이 한 줄이 겹침의 전부다.
        ///
        /// 안쪽(하단) 게이트가 열려 있어야 한다. <see cref="LastShiftAirlock.TryBeginDepressurize"/>
        /// 가 그 게이트를 자동으로 닫으므로 여기서 따로 닫지 않는다 — 감압은 닫힌 챔버에서만
        /// 성립하고, 두 동작으로 나누면 그 사이 상태에 이름을 붙일 게 없다.
        /// </summary>
        public static bool TryAscend()
        {
            if (!IsAtDeck) return false;
            if (!LastShiftAirlock.TryBeginDepressurize()) return false;

            TargetY = LastShiftEvaShaft.TopHatchY;
            Debug.Log($"[LAST_SHIFT_LIFT] depart=up from={Y:F2} to={TargetY:F2} cycle=BEGIN");
            return true;
        }

        /// <summary>
        /// 내려온다. 올라갈 때와 대칭으로 출발과 동시에 재가압을 건다.
        /// 재가압은 기항 게이트를 안 보므로(<c>RG-3</c> 영구 잠금 금지) 여기서도 안 본다.
        /// </summary>
        public static bool TryDescend()
        {
            if (!IsAtHullTop) return false;
            if (!LastShiftAirlock.TryBeginRepressurize()) return false;

            TargetY = LastShiftEvaShaft.DeckY;
            Debug.Log($"[LAST_SHIFT_LIFT] depart=down from={Y:F2} to={TargetY:F2} cycle=BEGIN");
            return true;
        }

        /// <summary>
        /// 시계를 한 스텝 돌린다. sandbox 가 <see cref="LastShiftAirlock.Tick"/> 과 같은 자리에서
        /// 부른다 — 둘이 같은 프레임에 돌아야 겹침이 실제로 겹친다.
        /// </summary>
        public static void Tick(float deltaTime)
        {
            if (!IsMoving || deltaTime <= 0f) return;

            Y = Mathf.MoveTowards(Y, TargetY, LastShiftEvaShaft.LiftSpeed * deltaTime);
            if (Mathf.Abs(TargetY - Y) > Epsilon) return;

            Y = TargetY;
            Debug.Log($"[LAST_SHIFT_LIFT] arrive y={Y:F2} airlock={LastShiftAirlock.Phase}");
        }

        /// <summary>씬 전환·검사 격리용. 정적 상태라 안 지우면 다음 판으로 새어 나간다.</summary>
        public static void Clear()
        {
            Y = LastShiftEvaShaft.DeckY;
            TargetY = LastShiftEvaShaft.DeckY;
        }
    }
}
