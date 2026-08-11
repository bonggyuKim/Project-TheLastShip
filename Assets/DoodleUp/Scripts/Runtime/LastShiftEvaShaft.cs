using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// EVA 승강 샤프트. <b>광장 중앙 코어가 곧 승강기이자 감압 챔버다</b>(기획 확정 2026-08-11).
    ///
    /// 새 구조물을 만들지 않는다. 코어의 <c>4x4</c> 발자국은 SIMUL_ZONES 가드레일로 실측 검증된
    /// 값이라 위치·치수를 못 바꾸고(<see cref="LastShiftPlazaLayout.CoreHalfExtent"/>), 그것을
    /// <b>그대로 위로 연장</b>하는 것이 이 설계의 전부다. 아래로 내려가던 옛 경로
    /// (<see cref="LastShiftBypassDuct"/>)는 이 설계에서 근거가 없어진다.
    ///
    /// <b>돌출 높이는 유추가 아니라 파생값이다.</b> 기획이 "3m 정도" 를 제안했고, 실측해 보니
    /// 그 값이 구획 표준 실내고와 정확히 같다 — 탑을 선체 상단에 얹고 그 안에 사람이 서면
    /// 필요한 높이가 곧 <see cref="LastShiftCompartments.InteriorHeight"/> 이기 때문이다.
    /// 그래서 리터럴 <c>3.0</c> 을 적지 않고 그 상수를 쓴다. 실내고가 바뀌면 탑도 따라 큰다.
    /// </summary>
    public static class LastShiftEvaShaft
    {
        /// <summary>샤프트 단면의 반너비. 코어와 <b>같은 물체</b>라 같은 값이어야 한다.</summary>
        public const float HalfExtent = LastShiftPlazaLayout.CoreHalfExtent;

        /// <summary>하단 게이트가 열리는 높이 — 광장 갑판이다(가압측).</summary>
        public const float DeckY = 0f;

        /// <summary>
        /// 선체 상단. 테두리 밑면에서 테두리 높이만큼 올라간 자리이고, 실측 <c>3.2</c> 다.
        /// 여기가 탑이 얹히는 바닥이다.
        /// </summary>
        public const float HullTopY = LastShiftHullShell.RimBaseY + LastShiftHullShell.RimHeight;

        /// <summary>선체 위로 돌출하는 탑의 높이. 구획 표준 실내고와 같다.</summary>
        public const float TowerHeight = LastShiftCompartments.InteriorHeight;

        /// <summary>상단 해치가 열리는 높이 — 탑 정상이다(진공측). 실측 <c>6.2</c>.</summary>
        public const float TopHatchY = HullTopY + TowerHeight;

        /// <summary>
        /// 해치 개구부 한 변. 압력문 개구부와 같은 값을 쓴다 — 배 안에서 "지나갈 수 있는 구멍"
        /// 의 크기가 자리마다 다르면 플레이어가 매번 다시 배워야 한다.
        /// </summary>
        public const float HatchOpening = LastShiftZoneDoor.OpeningWidth;

        /// <summary>샤프트 총 이동 거리. 승강 시간·산소 예산 계산의 기준이다.</summary>
        public const float TravelHeight = TopHatchY - DeckY;

        /// <summary>
        /// 승강 속도. <b>리터럴이 아니라 사이클과 묶는다</b> — 감압/재가압을 승강 중에 겹쳐
        /// 돌리기로 했으므로(game-balance 채택 2026-08-11), 리프트가 <b>사이클이 끝나는
        /// 바로 그 순간</b> 도착하는 속도가 유일하게 낭비가 없는 값이다.
        ///
        /// 이보다 느리면 사이클이 끝나고도 아직 올라가는 중이고, 빠르면 도착해 놓고 사이클을
        /// 기다린다. 어느 쪽이든 EVA 왕복 시간은 느린 쪽에 묶이므로 여기서 맞춘다.
        /// 둘 중 하나가 바뀌면 속도가 따라간다.
        ///
        /// 실측 <c>6.2 / 4 = 1.55 m/s</c>. game-balance 최소 요구(겹침 <c>0.64</c>,
        /// 순차 <c>1.10</c>)를 <b>둘 다</b> 넘으므로, 나중에 겹침을 못 쓰게 되어도 산소 예산이
        /// 성립한다.
        /// </summary>
        public static float LiftSpeed => TravelHeight / LastShiftAirlock.CycleSeconds;

        /// <summary>승강 한 번에 걸리는 시간. 겹침이 성립하면 사이클 시간과 같다.</summary>
        public static float LiftSeconds => TravelHeight / LiftSpeed;

        /// <summary>이 평면 좌표가 샤프트 안인가. 코어 판정을 그대로 쓴다.</summary>
        public static bool Contains(float x, float z) => LastShiftPlazaLayout.InsideCore(x, z);

        /// <summary>
        /// 이 높이가 <b>가압 구간</b>인가. 갑판에서 선체 상단까지가 배 안이고 그 위는 탑 내부다 —
        /// 탑도 해치를 닫고 있는 동안은 가압이므로, 비가압 판정은 높이가 아니라
        /// <see cref="LastShiftAirlock"/> 의 위상이 정한다. 여기서는 "배 형상 안인가" 만 답한다.
        /// </summary>
        public static bool IsInsideHull(float y) => y <= HullTopY;
    }
}
