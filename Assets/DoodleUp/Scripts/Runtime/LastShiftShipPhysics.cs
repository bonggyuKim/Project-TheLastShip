using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 선내 저중력 정본. 전역 <see cref="UnityEngine.Physics.gravity"/> 를 바꾸지 않고 LAST SHIFT
    /// 범위에서만 저중력을 적용한다. DU02/DU03BC 의 접지·낙하 검증이 지구 중력 Rigidbody 를
    /// 전제하므로 ProjectSettings 의 m_Gravity 를 건드리면 그 테스트들이 함께 깨진다.
    ///
    /// 값 근거: 화성 표면 중력(-3.72 m/s^2). 지구의 약 3/8 이라 "떠 있다"가 즉시 읽히면서도,
    /// 무중력(0)처럼 착지가 불가능해져 걷기·조준이 무너지지는 않는다. 이동·조준·잡기
    /// 검증(SP-02~04)을 유지하려면 접지가 성립해야 한다.
    ///
    /// <b>달(-1.62) → 화성(-3.72) → -5.5 → 화성 복귀.</b> 사용자 플레이(2026-08-10)에서
    /// 낙하 속도를 두 번 올린 뒤, 답답함의 원인이 중력이 아니라 <b>점프 정점</b>이었던 것으로
    /// 정리됐다 — 중력은 화성으로 되돌리고 <see cref="JumpSpeed"/> 를 바닥까지 내리는 쪽을
    /// 택했다. 정점은 취향이 아니라 승강구 회수 높이(1.2m)가 밑에서 받치는 값이다.
    /// </summary>
    public static class LastShiftShipPhysics
    {
        /// <summary>선내 중력 가속도(y). 지구 -9.81 의 약 3/8(화성 수준).</summary>
        public const float GravityY = -3.72f;

        /// <summary>
        /// 점프 초기 상승 속도. 사용자 요청(2026-08-10)으로 4.05 → 3.85 → 3.11 로 두 번 낮췄다.
        ///
        /// 값 근거: 정점 = JumpSpeed^2 / (2 * |GravityY|). 화성 중력에서 3.11 은 정점 약 1.30m 다.
        /// <b>여기가 바닥이다</b> — 승강구 회수 높이
        /// (<see cref="LastShiftBypassDuct.RecoveryRise"/> = 1.2m)를 정점이 밑돌면 갑판 구멍에
        /// 떨어진 물건을 회수할 길이 사라진다. 남은 여유가 <b>0.10m</b> 뿐이므로 이 값을 더
        /// 내리려면 승강구에 단을 넣어 한 걸음을 갈라야 한다(설계 변경).
        ///
        /// <b>중력을 바꾸면 이 값도 반드시 같이 바꾼다.</b> 중력만 화성으로 되돌리면 정점이
        /// 1.99m 로 튀어 천장(<see cref="CeilingInnerHeight"/> = 3.2) 쪽 여유를 먹는다.
        /// </summary>
        public const float JumpSpeed = 3.11f;

        /// <summary>
        /// 천장 내면 높이. 점프 정점 계산과 씬 빌더의 천장 배치가 같은 값을 써야 한다.
        /// 이 값이 <see cref="JumpSpeed"/> 상한을 결정한다.
        /// </summary>
        public const float CeilingInnerHeight = 3.2f;

        /// <summary>점프 정점 고도(발 기준). 천장 검증용.</summary>
        public static float JumpApexHeight => JumpSpeed * JumpSpeed / (2f * Mathf.Abs(GravityY));

        /// <summary>
        /// 정점에서 원래 높이로 돌아오는 데 걸리는 시간. <b>사용자가 "늦게 떨어진다"로 느끼는
        /// 것이 이 값</b>이고, 정점이 고정된 뒤에는 <see cref="GravityY"/> 하나로만 정해진다.
        /// </summary>
        public static float JumpFallDuration => Mathf.Sqrt(2f * JumpApexHeight / Mathf.Abs(GravityY));

        /// <summary>
        /// 뛰어서 다시 착지할 때까지의 체공 시간. 상승과 낙하가 대칭이라 낙하의 두 배다.
        /// 저중력 체감은 이 값과 지구 중력에서의 같은 정점 체공(<see cref="EarthHangTime"/>)의
        /// 비율로 읽는다 — 절대 시간은 정점을 바꾸면 같이 움직여서 기준이 못 된다.
        /// </summary>
        public static float JumpHangTime => JumpFallDuration * 2f;

        /// <summary>지구 중력에서 같은 정점을 뛰었을 때의 체공. 저중력 배율의 분모다.</summary>
        public static float EarthHangTime => 2f * Mathf.Sqrt(2f * JumpApexHeight / 9.81f);

        /// <summary>
        /// 카메라 눈높이(발 기준 local y). <see cref="JumpSpeed"/> 주석이 이미 이 값을 천장
        /// 여유 계산의 근거로 쓰고 있었는데 상수가 없어 씬 빌더와 네트워크 플레이어가 각자
        /// 리터럴로 들고 있었다. 셋이 갈리면 점프 상한 근거가 조용히 틀린다.
        ///
        /// 3D 오디오의 <see cref="LastShiftZoneAudio.MinDistance"/> 도 이 값을 쓴다 — 음원이
        /// 승무원 루트에 붙고 리스너는 눈높이에 있어서, 자기 몸에서 나는 소리의 거리가
        /// 0 이 아니라 이 값이다.
        /// </summary>
        public const float EyeHeight = 1.55f;

        /// <summary>
        /// 승무원 캡슐 반지름. 판독 검사(<c>T5</c>)가 "설 수 있는 자리" 를 이 값으로 정한다 —
        /// 벽이나 배플에서 이만큼 떨어져야 실제로 서 있을 수 있고, 그 제약이 판독 가능한
        /// z 띠의 폭을 정한다. 씬 빌더의 CharacterController 와 같은 값이어야 한다.
        /// </summary>
        public const float CrewRadius = 0.28f;

        /// <summary>
        /// 승무원 CharacterController 의 skinWidth. 프리팹(<c>LastShiftNetworkPlayer</c>)의
        /// <c>m_SkinWidth</c> 와 같은 값이어야 한다.
        ///
        /// <b>여기 있는 이유는 캡슐이 자기 치수보다 크기 때문이다.</b> PhysX 컨트롤러는 이 값만큼
        /// 떨어진 자리에서 접촉을 만들므로, 통로 단면을 <see cref="CrewRadius"/>·
        /// <see cref="CrouchHeight"/> 만으로 재면 실제로 필요한 공간을 그만큼 적게 잡는다.
        /// </summary>
        public const float CrewSkinWidth = 0.08f;

        /// <summary>
        /// 승무원 CharacterController 의 stepOffset. 프리팹의 <c>m_StepOffset</c> 과 같은 값이고,
        /// <b>점프가 아니라 걸어서</b> 넘을 수 있는 턱의 상한이다. 문턱·데칼·해치 판이 통행
        /// 방해가 되는지를 재는 쪽이 전부 이 값을 본다.
        /// </summary>
        public const float CrewStepOffset = 0.3f;

        /// <summary>
        /// 서 있을 때의 승무원 높이. 씬 빌더와 플레이어 프리팹이 CharacterController 에 쓰던
        /// 리터럴 <c>1.7</c> 을 여기로 올린다 — 웅크림이 생기면서 이 값이 "기본 높이" 라는
        /// 뜻을 갖게 됐고, 두 자리에 흩어져 있으면 웅크림 복귀가 한쪽에서만 맞는다.
        /// </summary>
        public const float StandingHeight = 1.7f;

        /// <summary>
        /// 웅크린 승무원이 지나야 하는 <b>통로</b> 최소 단면(폭·높이 공통). docs §5 확정값
        /// <c>0.9m</c> 이고 <see cref="LastShiftBypassDuct.Section"/> 이 이 값을 그대로 쓴다.
        ///
        /// 웅크림 높이와 <b>같은 상수가 아니다.</b> 예전에는 하나였는데, 그러면 캡슐 높이가
        /// 통로 높이와 정확히 같아져 여유가 <c>0</c> 이 된다 — 사용자 플레이에서
        /// "Ctrl 로 웅크려도 덕트에 못 들어가는" 것으로 나왔다. 통로가 정본이고 승무원이
        /// 그 안에 들어가야 하므로, 방향은 <c>단면 → 높이</c> 한쪽뿐이다.
        /// </summary>
        public const float CrouchSection = 0.9f;

        /// <summary>
        /// 웅크린 캡슐과 통로 단면 사이 여유. <b>skinWidth 세 겹이다.</b>
        ///
        /// 예전에는 두 겹이었고 "위아래 한 겹씩" 이라고 적혀 있었는데, 아래 한 겹은 여유가
        /// 아니다 — CharacterController 는 바닥면에 붙지 않고 <see cref="CrewSkinWidth"/> 만큼
        /// <b>뜬 채로</b> 선다. 그래서 그 한 겹은 캡슐을 얇게 만드는 대신 통째로 위로 밀어
        /// 올리고, 남는 여유는 위쪽 한 겹뿐인데 그 한 겹을 컨트롤러의 접촉 거리가 그대로
        /// 먹는다. 결과가 정확히 <c>0.08 + 0.74 + 0.08 = 0.90</c> — 단면과 같은 값이라
        /// 여유가 <c>0</c> 이고, 관 입구에서 천장에 닿은 채 한 발도 못 나갔다.
        ///
        /// 세 겹의 뜻은 <b>거치 간격 + 접촉 거리 + 여유</b> 한 겹씩이다. 앞의 둘은 컨트롤러가
        /// 반드시 가져가는 몫이라 협상 대상이 아니고, 실제로 남는 여유는 마지막 한 겹이다.
        /// </summary>
        public const float CrouchClearance = CrewSkinWidth * 3f;

        /// <summary>
        /// 웅크렸을 때의 승무원 높이. <see cref="CrouchSection"/> 에서 여유를 뺀 값이다 —
        /// 리터럴로 적으면 단면이 바뀔 때 여유가 조용히 사라진다.
        /// </summary>
        public const float CrouchHeight = CrouchSection - CrouchClearance;

        /// <summary>
        /// 웅크림 눈높이. 서 있을 때(<see cref="EyeHeight"/> <c>1.55</c>)와 같은 비율을 유지한다 —
        /// 리터럴을 적으면 높이를 조정할 때 카메라만 천장을 뚫거나 바닥에 묻힌다.
        /// </summary>
        public const float CrouchEyeHeight = EyeHeight * CrouchHeight / StandingHeight;

        /// <summary>
        /// 웅크림 이동 속도. 물건을 든 속도(<c>2.8</c>)보다 느려야 "기어서 건너가는 우회로" 가
        /// 주 통로보다 빠른 지름길이 되지 않는다 — docs §5 가 우회로에 비용을 요구하는 이유다.
        /// 산소 비용(<c>SuitOxygen</c>)과 함께 두 번째 비용축이다.
        /// </summary>
        public const float CrouchSpeed = 1.6f;

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
