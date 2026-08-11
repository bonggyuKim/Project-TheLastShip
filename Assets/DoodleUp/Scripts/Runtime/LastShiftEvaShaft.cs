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
