using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 선내 저중력 정본. 전역 <see cref="UnityEngine.Physics.gravity"/> 를 바꾸지 않고 LAST SHIFT
    /// 범위에서만 저중력을 적용한다. DU02/DU03BC 의 접지·낙하 검증이 지구 중력 Rigidbody 를
    /// 전제하므로 ProjectSettings 의 m_Gravity 를 건드리면 그 테스트들이 함께 깨진다.
    ///
    /// 값 근거: 달 표면 중력(-1.62 m/s^2)을 채택했다. 지구의 약 1/6 이라 점프 체공과 낙하 시간이
    /// 대략 2.5배 길어져 "떠 있다"가 즉시 읽히면서도, 무중력(0)처럼 착지가 불가능해져 걷기·조준이
    /// 무너지지는 않는다. 이동·조준·잡기 검증(SP-02~04)을 유지하려면 접지가 성립해야 한다.
    /// </summary>
    public static class LastShiftShipPhysics
    {
        /// <summary>선내 중력 가속도(y). 지구 -9.81 의 약 1/6.</summary>
        public const float GravityY = -1.62f;

        /// <summary>
        /// 점프 초기 상승 속도. 지구 중력에서 4.8 이던 값을 2.2 로 낮췄다. 저중력에서 4.8 을
        /// 유지하면 정점 고도가 7m 를 넘어 천장을 뚫고 카메라가 선체 밖으로 나간다.
        ///
        /// 값 근거: 정점 = JumpSpeed^2 / (2 * |GravityY|). 2.2 는 정점 약 1.49m 로,
        /// 카메라 눈높이(1.55) + 정점이 천장 내면(<see cref="CeilingInnerHeight"/> = 3.2) 아래에
        /// 머문다. 3.4 는 정점 3.57m 라 천장을 뚫으므로 쓸 수 없다. 체공은 약 2.7초로
        /// 지구 중력 점프(약 1초)보다 확실히 길어 저중력이 즉시 읽힌다.
        /// </summary>
        public const float JumpSpeed = 2.2f;

        /// <summary>
        /// 천장 내면 높이. 점프 정점 계산과 씬 빌더의 천장 배치가 같은 값을 써야 한다.
        /// 이 값이 <see cref="JumpSpeed"/> 상한을 결정한다.
        /// </summary>
        public const float CeilingInnerHeight = 3.2f;

        /// <summary>점프 정점 고도(발 기준). 천장 검증용.</summary>
        public static float JumpApexHeight => JumpSpeed * JumpSpeed / (2f * Mathf.Abs(GravityY));

        /// <summary>접지 시 유지하는 하향 속도. 경사·틈에서 미끄러지지 않을 최소값.</summary>
        public const float GroundedSettleSpeed = -1f;

        /// <summary>
        /// 놓친 물건이 "천천히 떠서 흐르는" 감각을 만드는 선형 감쇠. 0 이면 저중력에서
        /// 물건이 관성만으로 계속 가속해 선체 밖으로 나가고, 너무 크면 즉시 멈춰 떠다니지 않는다.
        /// </summary>
        public const float ItemLinearDamping = 0.35f;

        /// <summary>회전 감쇠. 회전이 영원히 남으면 잡기 조준이 불필요하게 어려워진다.</summary>
        public const float ItemAngularDamping = 0.25f;

        /// <summary>
        /// Rigidbody 를 선내 저중력 규칙으로 전환한다. 전역 중력을 끄고 저중력을 직접 적용하는
        /// 방식이라 <see cref="ApplyShipGravity"/> 를 매 물리 스텝 호출해야 한다.
        /// </summary>
        public static void ConfigureItemBody(Rigidbody body)
        {
            if (body == null) return;
            body.useGravity = false;
            body.linearDamping = ItemLinearDamping;
            body.angularDamping = ItemAngularDamping;
        }

        /// <summary>선내 저중력 가속을 한 물리 스텝만큼 적용한다.</summary>
        public static void ApplyShipGravity(Rigidbody body, float fixedDeltaTime)
        {
            if (body == null || body.isKinematic) return;
            body.linearVelocity += new Vector3(0f, GravityY * fixedDeltaTime, 0f);
        }
    }
}
