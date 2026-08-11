using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 콘닝타워 상단 해치. <b>여닫는 주체가 아니라 따라가는 쪽이다</b> — 열림 여부는
    /// <see cref="LastShiftAirlock"/> 의 위상이 이미 정하고 있고, 여기서는 그것을 뚜껑
    /// 애니메이션과 차단 콜라이더로 옮기기만 한다.
    ///
    /// 주체를 따로 두지 않는 이유는 압력문에서 배운 것과 같다. 상태를 두 곳이 들면 언젠가
    /// 갈리고, 그때 "화면에는 열려 있는데 못 지나간다" 가 된다.
    ///
    /// <b>차단은 판이 아니라 뚫린 자리를 메운다.</b> 뚜껑은 미끄러지는 연출이라 얇고, 그걸
    /// 그대로 막으면 저중력에서 뜬 물건이 터널링으로 빠진다 — 갑판 해치가 있던 시절에
    /// 같은 이유로 같은 선택을 했다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class LastShiftEvaTopHatch : MonoBehaviour
    {
        private static readonly int HatchClipState = Animator.StringToHash("LP_EVA_Hatch_OpenClose");

        [SerializeField] private BoxCollider blocker;

        private Animator hatchAnimator;
        private float openAmount;

        public void Configure(BoxCollider hatchBlocker) => blocker = hatchBlocker;

        /// <summary>지금 통행이 막혀 있는가. 검사가 형상 쪽에서 확인하는 값이다.</summary>
        public bool IsBlocking => blocker != null && blocker.enabled;

        private void Awake()
        {
            hatchAnimator = GetComponent<Animator>();
            openAmount = LastShiftAirlock.IsOuterHatchOpen ? 1f : 0f;
            Apply();
        }

        private void Update()
        {
            var target = LastShiftAirlock.IsOuterHatchOpen ? 1f : 0f;
            if (Mathf.Approximately(openAmount, target)) return;

            // 압력문과 같은 시간에 여닫는다. 배 안에서 "문이 열리는 데 걸리는 시간" 이
            // 자리마다 다르면 플레이어가 매번 다시 배운다.
            openAmount = Mathf.MoveTowards(openAmount, target,
                Time.deltaTime / LastShiftRecoveryTuning.ZoneDoorTransitionSeconds);
            Apply();
        }

        private void Apply()
        {
            if (hatchAnimator != null && hatchAnimator.runtimeAnimatorController != null)
            {
                // speed = 0 이라 Play 만으로는 다음 프레임에야 반영된다. Update(0) 으로 그
                // 자리에서 평가시킨다 — LastShiftZoneDoor 가 같은 이유로 같은 일을 한다.
                hatchAnimator.speed = 0f;
                hatchAnimator.Play(HatchClipState, 0, Mathf.Clamp01(openAmount));
                hatchAnimator.Update(0f);
            }

            // 완전히 닫혔을 때만 막는다. 움직이는 콜라이더로 막으면 CharacterController 가
            // 뚜껑에 끼거나 밀려나고, 닫히는 순간에 빠져나가는 여지가 사라진다.
            if (blocker != null) blocker.enabled = openAmount <= 0.001f;
        }
    }
}
