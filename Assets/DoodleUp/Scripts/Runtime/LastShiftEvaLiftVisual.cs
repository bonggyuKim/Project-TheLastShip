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
    ///
    /// <b>로컬 <c>y</c> 는 위가 아니다.</b> 아트 리그의 <c>Model</c> 노드가 Blender 축 변환
    /// 쿼터니언(<c>x=-0.7071, w=0.7071</c>, <c>x</c> 축 <c>-90°</c>)을 갖고 있어서, 그 아래
    /// 로컬 <c>+y</c> 는 월드 <c>z</c> 로 간다 — 실제로 판의 로컬 <c>z=0.2</c> 가 월드 높이
    /// <c>0.2</c> 였다. 그래서 로컬 <c>y</c> 에 <c>6.20</c> 을 써 넣던 옛 코드는 판을 위로
    /// 올린 게 아니라 <b>선체 밖으로 밀어내고</b> 있었고, 승강 상태·감압 시계는 전부 초록인데
    /// 화면에서는 판이 안 올라갔다(PlayMode 실측: <c>liftY=6.20</c> 인데 판 월드 <c>y=0.20</c>).
    /// 지금은 정지 자세를 한 번 기억해 두고 <b>월드 좌표</b>로 그 높이만 얹는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftEvaLiftVisual : MonoBehaviour
    {
        [SerializeField] private Transform platform;

        public void Configure(Transform platformPivot)
        {
            platform = platformPivot;
            CaptureRestPose();
            Apply();
        }

        /// <summary>지금 판이 서 있는 높이. 검사가 형상 쪽에서 확인하는 값이다.</summary>
        public float PlatformY => platform != null ? platform.position.y : float.NaN;

        /// <summary>
        /// 판의 <b>디딤면</b> 높이. 승무원을 태우는 쪽이 읽는 값이다.
        ///
        /// 두께를 상수로 적지 않고 콜라이더 경계에서 받는 이유는, 판이 아트 프리팹 조각이라
        /// 두께가 코드에 없기 때문이다 — 손으로 베낀 값을 두면 아트가 판을 갈 때 조용히 어긋난다.
        /// </summary>
        public float PlatformTopY
        {
            get
            {
                if (platformCollider == null && platform != null)
                    platformCollider = platform.GetComponent<Collider>();
                return platformCollider != null ? platformCollider.bounds.max.y : float.NaN;
            }
        }

        private Collider platformCollider;

        private void Awake()
        {
            // 애니메이터가 같은 값을 매 프레임 되돌린다. 끄지 않으면 판이 두 주인을 섬긴다.
            var animator = GetComponentInChildren<Animator>(true);
            if (animator != null) animator.enabled = false;
            CaptureRestPose();
            Apply();
        }

        private void LateUpdate() => Apply();

        /// <summary>
        /// 판의 정지 자세(갑판에 앉은 자리). 지금 높이에서 <see cref="LastShiftEvaLift.Y"/> 를 뺀
        /// 값이라 두 번 불려도 결과가 같다 — <see cref="Configure"/> 와 <see cref="Awake"/> 가
        /// 둘 다 부를 수 있고, 그때 판이 이미 올라가 있을 수 있다.
        /// </summary>
        private void CaptureRestPose()
        {
            if (platform == null) return;
            restPose = platform.position - Vector3.up * LastShiftEvaLift.Y;
        }

        private void Apply()
        {
            if (platform == null) return;
            // x·z 도 정지 자세로 눌러 둔다. 옛 코드가 로컬 y 에 써서 판을 수평으로 밀어내고
            // 있었으므로, 높이만 얹으면 그 밀림이 남은 씬에서 그대로 굳는다.
            platform.position = restPose + Vector3.up * LastShiftEvaLift.Y;
        }

        private Vector3 restPose;
    }
}
