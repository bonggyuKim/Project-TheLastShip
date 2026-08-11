using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 코어 하단 게이트의 차단면 — 조종석 쪽 한 면이다.
    ///
    /// <b>여닫는 주체가 아니라 따라가는 쪽이다.</b> 열림 여부는 <see cref="LastShiftAirlock"/>
    /// 의 위상이 이미 정하고 있고(안쪽 해치가 열렸는가), 여기서는 그것을 통행 차단으로 옮기기만
    /// 한다. 상단 해치(<see cref="LastShiftEvaTopHatch"/>)와 같은 구조다 — 상태를 두 곳이 들면
    /// 언젠가 갈리고, 그때 "화면에는 열려 있는데 못 지나간다" 가 된다.
    ///
    /// 나머지 세 면은 이 컴포넌트가 없다. 전력실·냉각실·산소실 방향은 <b>언제나</b> 막혀
    /// 있어야 SIMUL_ZONES 가 성립하고(기획 §4.2), 그래서 애초에 열 수단을 두지 않는다 —
    /// 끌 수 있는 스위치를 만들어 두면 언젠가 꺼진다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftEvaGate : MonoBehaviour
    {
        [SerializeField] private BoxCollider blocker;

        public void Configure(BoxCollider gateBlocker) => blocker = gateBlocker;

        /// <summary>지금 통행이 막혀 있는가. 검사가 형상 쪽에서 확인하는 값이다.</summary>
        public bool IsBlocking => blocker != null && blocker.enabled;

        private void Awake() => Apply();
        private void Update() => Apply();

        private void Apply()
        {
            if (blocker == null) return;
            // 안쪽 해치가 열려 있는 동안만 지나갈 수 있다. 감압이 시작되면 그 순간 닫힌다 —
            // TryBeginDepressurize 가 해치를 자동으로 닫으므로 여기서 따로 볼 것이 없다.
            blocker.enabled = !LastShiftAirlock.IsInnerHatchOpen;
        }
    }
}
