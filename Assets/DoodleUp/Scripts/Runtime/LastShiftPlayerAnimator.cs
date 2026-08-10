using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// Player 표현 전용 어댑터. NGO 메시지를 추가하지 않고 복제된 transform과 소지 상태에서
    /// 애니메이션 파라미터를 유도한다. 게임플레이 이동은 계속 CharacterController가 소유한다.
    /// </summary>
    public sealed class LastShiftPlayerAnimator : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int JumpId = Animator.StringToHash("Jump");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int GrabId = Animator.StringToHash("Grab");
        private static readonly int DropId = Animator.StringToHash("Drop");

        [SerializeField] private Animator animator;
        [SerializeField] private LastShiftPlayerController playerController;
        [SerializeField] private LastShiftNetworkPlayer networkPlayer;

        private Vector3 previousPosition;
        private bool initialized;
        private bool wasGrounded = true;
        private bool wasCarrying;

        public Animator Animator => animator;

        public void Configure(Animator targetAnimator, LastShiftPlayerController controller,
            LastShiftNetworkPlayer network)
        {
            animator = targetAnimator;
            playerController = controller;
            networkPlayer = network;
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
            initialized = false;
        }

        /// <summary>
        /// 이 속도를 넘는 위치차는 걸어서 난 것이 아니다 — 순간이동으로 본다.
        ///
        /// <b><see cref="LastShiftPlayerController.CurrentMoveSpeed"/> 는
        /// <see cref="LastShiftPlayerController.MoveSpeed"/> 를 절대 안 넘는다</b> — 웅크림도
        /// 운반도 더 느린 값이라, 정상 보행의 상한이 곧 <c>MoveSpeed</c> 다. <c>3</c>배를 두면
        /// 프레임 하나가 튀거나 경사에서 밀려도 안 걸리고, 순간이동(리셋 지점까지 수십 m)과는
        /// 자릿수가 다르다.
        /// </summary>
        public const float TeleportSpeedMetersPerSecond = LastShiftPlayerController.MoveSpeed * 3f;

        /// <summary>
        /// 이 프레임의 위치차가 순간이동인가. <b>수평만 본다</b> — 낙하는 <c>y</c> 로만 빠르게
        /// 움직이는 정상 상태이고, 그것을 순간이동으로 읽으면 떨어지는 동안 애니메이션이 멎는다.
        ///
        /// <b>순수 함수인 것이 의도다</b> — <see cref="Update"/> 안에 조건으로 두면 프레임을
        /// 돌리지 않고는 못 재고, 그러면 이 판정에 회귀 시험을 붙일 자리가 없다.
        /// </summary>
        public static bool IsTeleport(Vector3 delta, float deltaTime) =>
            deltaTime > 0f &&
            new Vector2(delta.x, delta.z).magnitude / deltaTime > TeleportSpeedMetersPerSecond;

        private void Update()
        {
            if (animator == null) return;
            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            var delta = transform.position - previousPosition;
            previousPosition = transform.position;

            var grounded = ResolveGrounded();
            var carrying = ResolveCarrying();

            // <b>순간이동한 프레임은 위치에서 나온 값을 통째로 건너뛴다.</b>
            // <see cref="LastShiftNetworkPlayer.ResetToSlotRpc"/>(프리셋 리셋 · 슬롯 배치 ·
            // 산소 고갈 자동 복귀)가 승무원을 수십 m 옮기는데, 그 한 프레임의 위치차를 그대로
            // 나누면 Speed 가 수백~수천이 되어 블렌드 트리가 최고 속도에 박힌다. 더 나쁜 것은
            // 아래 점프 조건이다 — 위로 옮겨 앉으면 걷지도 뛰지도 않은 승무원이 점프 모션을 한다.
            //
            // 한 프레임만 건너뛰면 된다: 다음 프레임의 previousPosition 은 이미 옮겨 간 자리라
            // 그때부터는 실제로 걸은 거리가 나온다.
            var teleported = IsTeleport(delta, deltaTime);
            if (!teleported)
            {
                animator.SetFloat(SpeedId, new Vector2(delta.x, delta.z).magnitude / deltaTime, 0.08f, deltaTime);
                if (initialized && wasGrounded && !grounded && delta.y > 0f) animator.SetTrigger(JumpId);
            }

            // 접지와 소지는 위치차에서 안 나온다(CharacterController · 소지 슬롯) — 순간이동해도
            // 그대로 유효하다. 여기서 같이 건너뛰면 리셋 프레임에 든 물건이 다음 변화까지 안 잡힌다.
            animator.SetBool(GroundedId, grounded);

            if (initialized)
            {
                if (!wasCarrying && carrying) animator.SetTrigger(GrabId);
                else if (wasCarrying && !carrying) animator.SetTrigger(DropId);
            }

            wasGrounded = grounded;
            wasCarrying = carrying;
            initialized = true;
        }

        private bool ResolveGrounded()
        {
            var character = GetComponent<CharacterController>();
            if (character != null && character.enabled) return character.isGrounded;
            return UnityEngine.Physics.Raycast(transform.position + Vector3.up * 0.15f, Vector3.down, 0.25f,
                UnityEngine.Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private bool ResolveCarrying()
        {
            if (networkPlayer != null && networkPlayer.IsSpawned) return networkPlayer.HeldItem != null;
            return playerController != null && playerController.HeldItem != null;
        }

        public void SampleForProbe(float speed, bool grounded, bool carrying)
        {
            if (animator == null) return;
            animator.SetFloat(SpeedId, Mathf.Max(0f, speed));
            animator.SetBool(GroundedId, grounded);
            if (!wasCarrying && carrying) animator.SetTrigger(GrabId);
            else if (wasCarrying && !carrying) animator.SetTrigger(DropId);
            wasGrounded = grounded;
            wasCarrying = carrying;
            initialized = true;
            animator.Update(1f / 30f);
        }
    }
}
