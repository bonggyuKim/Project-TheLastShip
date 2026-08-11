using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 승강 플랫폼의 시각·물리 판. <see cref="LastShiftEvaLift"/> 가 정한 높이로 아트 프리팹의
    /// <c>LiftPlatformPivot</c> 을 옮긴다.
    ///
    /// <b>클립을 스크럽하지 않는다.</b> 문·해치는 아트 클립을 정규화 시간으로 긁어 쓰는데,
    /// 여기서는 그 방식이 안 된다 — <c>LP_CentralLift_UpDown</c> 이 판을 <c>0.8m</c> 만 올리고
    /// 이 샤프트는 <c>6.2m</c> 를 올라가야 한다. 클립은 옛 <c>4m</c> 승강기 시절에 만들어진
    /// 것이고, 그걸 늘려 쓰면 판이 <c>0.8m</c> 에서 멈춘 채 승무원만 남는다.
    ///
    /// 그래서 애니메이터를 끄고 트랜스폼을 직접 준다. 높이의 정본은 언제나
    /// <see cref="LastShiftEvaLift.Y"/> 하나이고, 여기서는 그것을 옮기기만 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftEvaLiftVisual : MonoBehaviour
    {
        [SerializeField] private Transform platform;

        public void Configure(Transform platformPivot) => platform = platformPivot;

        /// <summary>지금 판이 서 있는 높이. 검사가 형상 쪽에서 확인하는 값이다.</summary>
        public float PlatformY => platform != null ? platform.position.y : float.NaN;

        private void Awake()
        {
            // 애니메이터가 같은 값을 매 프레임 되돌린다. 끄지 않으면 판이 두 주인을 섬긴다.
            var animator = GetComponentInChildren<Animator>(true);
            if (animator != null) animator.enabled = false;
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (platform == null) return;
            var local = platform.localPosition;
            // 프리팹 원점이 갑판이므로 로컬 y 가 곧 승강 높이다.
            platform.localPosition = new Vector3(local.x, LastShiftEvaLift.Y, local.z);
        }
    }
}
