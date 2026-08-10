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

        private void Update()
        {
            if (animator == null) return;
            var deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            var delta = transform.position - previousPosition;
            previousPosition = transform.position;

            var horizontalSpeed = new Vector2(delta.x, delta.z).magnitude / deltaTime;
            var grounded = ResolveGrounded();
            var carrying = ResolveCarrying();

            animator.SetFloat(SpeedId, horizontalSpeed, 0.08f, deltaTime);
            animator.SetBool(GroundedId, grounded);

            if (initialized)
            {
                if (wasGrounded && !grounded && delta.y > 0f) animator.SetTrigger(JumpId);
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
