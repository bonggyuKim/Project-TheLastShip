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
        /// 감압 정지 높이. <b>2단 승강의 1단이다</b>(사용자 승인 2026-08-11).
        ///
        /// 승무원이 탑 <b>안에</b> 선 채로 감압이 돌아야 "챔버 안에서 감압" 이 그림으로
        /// 성립한다. 그러려면 발밑이 해치 문턱에서 표준 서는 높이만큼 내려와 있어야 한다 —
        /// 그 높이가 압력문 개구부 높이라 그대로 쓴다. 실측 <c>6.2 - 2.2 = 4.0</c>.
        ///
        /// 여기까지가 1단이고, 감압이 끝나면 2단으로 문턱까지 마저 올라가 걸어 나간다.
        /// 1단에서 멈추고 끝내면 나가는 데 <c>2.2m</c> 를 올라가야 하는데 점프 정점이
        /// <c>1.30m</c> 라 못 나간다 — 그것이 2단이 필요한 이유다.
        /// </summary>
        public static float DepressurizeStopY => TopHatchY - LastShiftZoneDoor.OpeningHeight;

        /// <summary>
        /// 승강 속도. <b>1단 구간이 사이클과 같은 시간에 끝나도록</b> 묶는다.
        ///
        /// 감압은 1단에서만 돌므로 겹침의 기준 구간도 1단이다. 2단은 이미 감압이 끝난 뒤라
        /// 겹칠 것이 없다. 실측 <c>4.0 / 4 = 1.00 m/s</c> 이고, game-balance 가 제안한
        /// <c>0.9~1.0</c> 범위에 그대로 떨어진다 — 유추로 고른 값과 파생값이 만난 자리다.
        /// </summary>
        public static float LiftSpeed => (DepressurizeStopY - DeckY) / LastShiftAirlock.CycleSeconds;

        /// <summary>1단(갑판 → 감압 정지)에 걸리는 시간. 겹침이 성립하면 사이클 시간과 같다.</summary>
        public static float CycleStageSeconds => (DepressurizeStopY - DeckY) / LiftSpeed;

        /// <summary>2단(감압 정지 → 해치 문턱)에 걸리는 시간. 실측 <c>2.20</c>초.</summary>
        public static float ExitStageSeconds => (TopHatchY - DepressurizeStopY) / LiftSpeed;

        /// <summary>상승 전체에 걸리는 시간. 1단은 사이클에 흡수되고 2단이 그대로 더해진다.</summary>
        public static float AscentSeconds => Mathf.Max(CycleStageSeconds, LastShiftAirlock.CycleSeconds)
                                             + ExitStageSeconds;

        /// <summary>이 평면 좌표가 샤프트 안인가. 코어 판정을 그대로 쓴다.</summary>
        public static bool Contains(float x, float z) => LastShiftPlazaLayout.InsideCore(x, z);

        /// <summary>
        /// 코어에서 <paramref name="margin"/> 안쪽인가 — <b>다가섰는가</b>를 재는 쪽이다.
        /// 안에 선 것과 다가선 것을 대본이 다른 줄로 나누므로(접근 · 도착) 둘 다 필요하다.
        /// </summary>
        public static bool Contains(float x, float z, float margin) =>
            Mathf.Abs(x) <= HalfExtent + margin && Mathf.Abs(z) <= HalfExtent + margin;

        /// <summary>
        /// 이 높이가 <b>가압 구간</b>인가. 갑판에서 선체 상단까지가 배 안이고 그 위는 탑 내부다 —
        /// 탑도 해치를 닫고 있는 동안은 가압이므로, 비가압 판정은 높이가 아니라
        /// <see cref="LastShiftAirlock"/> 의 위상이 정한다. 여기서는 "배 형상 안인가" 만 답한다.
        /// </summary>
        public static bool IsInsideHull(float y) => y <= HullTopY;
    }
}
