using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 래그돌 테스트맵 조작기. 씬 <c>Assets/Scenes/LAST_SHIFT_RAGDOLL_LAB.unity</c> 전용이고
    /// 본 게임 씬에는 절대 안 들어간다.
    ///
    /// 프로토타입에서 손으로 확인해야 하는 건 결국 넷이다 — (1) 부딪히면 부위별로 덜렁거리는가,
    /// (2) 저중력이라 몇 초 지켜볼 만한가, (3) 지구 중력이면 정말 안 웃긴가, (4) 튜닝을 안 하면
    /// 정말 안 멈추는가. 키 넷이 각각 그 하나씩을 담당한다.
    /// </summary>
    [RequireComponent(typeof(LastShiftRagdoll))]
    public sealed class LastShiftRagdollLab : MonoBehaviour
    {
        /// <summary>
        /// 기본 충격의 <b>수평</b> 방향. 위쪽 성분은 튜닝의
        /// <see cref="LastShiftRagdollTuning.BodyCheckRise"/> 가 얹으므로 여기엔 안 넣는다.
        /// 캡처 자동화가 플레이와 같은 시나리오를 돌리려고 같이 읽는다.
        ///
        /// <b>문에서 멀어지는 쪽(-z)이다.</b> 문 쪽으로 밀었더니 따라가는 카메라가 문벽 뒤로 들어가
        /// 열 장 중 절반이 벽만 찍혔다 — 문틀은 배경에 두고 승무원이 이쪽으로 튕겨 나오게 한다.
        /// </summary>
        public static readonly Vector3 DefaultImpactHeading = Vector3.back;

        /// <summary>기본 운석 폭심(승무원 기준 상대 좌표). 충격 방향과 같은 쪽으로 밀도록 문 쪽에 둔다.</summary>
        public static readonly Vector3 DefaultBlastOrigin = new Vector3(0f, 0.2f, 1.6f);

        /// <summary>
        /// 골반 기준 카메라 오프셋. <b>고정 카메라는 이 프로토타입에 안 맞는다</b> — 저중력에서
        /// 밀린 몸이 몇 미터를 흘러가므로, 정작 봐야 할 팔다리가 화면에서 점만 해진다(실측:
        /// 첫 캡처에서 2.7초 뒤 승무원이 화면 폭의 3%였다). 캡처와 플레이가 같은 값을 쓴다.
        /// </summary>
        public static readonly Vector3 CameraOffset = new Vector3(1.9f, 0.85f, -2.3f);

        /// <summary>카메라가 바라보는 지점의 골반 기준 높이 보정. 몸통 가운데를 보게 한다.</summary>
        public const float CameraFocusRise = 0.25f;

        /// <summary>골반 위치를 기준으로 카메라를 놓는다. 캡처 자동화도 이 함수를 쓴다.</summary>
        public static void FrameSubject(Camera camera, Vector3 pelvisPosition)
        {
            if (camera == null) return;
            var focus = pelvisPosition + Vector3.up * CameraFocusRise;
            var position = focus + CameraOffset;
            camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position, Vector3.up));
        }

        [Tooltip("충격을 밀어 넣는 수평 방향. 씬의 통로 방향과 맞춰 둔다.")]
        [SerializeField] private Vector3 impactHeading = Vector3.back;

        [Tooltip("운석 폭심. 승무원 발밑 앞쪽을 기본으로 둔다.")]
        [SerializeField] private Vector3 blastOrigin = new Vector3(0f, 0.2f, 1.6f);

        private LastShiftRagdoll _ragdoll;
        private bool _earthGravity;
        private bool _wizardTuning;

        /// <summary>현재 적용 중인 중력이 지구 중력인지. HUD 와 테스트가 같이 읽는다.</summary>
        public bool EarthGravity => _earthGravity;

        /// <summary>대조군(감쇠·슬립 기본값, 정지 판정 없음) 을 쓰고 있는지.</summary>
        public bool WizardTuning => _wizardTuning;

        private void Awake()
        {
            _ragdoll = GetComponent<LastShiftRagdoll>();
            Rebuild();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.spaceKey.wasPressedThisFrame) BodyCheck();
            if (keyboard.hKey.wasPressedThisFrame) HeadFlick();
            if (keyboard.bKey.wasPressedThisFrame) Blast();
            if (keyboard.rKey.wasPressedThisFrame) _ragdoll.ResetToRestPose();

            if (keyboard.gKey.wasPressedThisFrame)
            {
                _earthGravity = !_earthGravity;
                Rebuild();
            }

            if (keyboard.tKey.wasPressedThisFrame)
            {
                _wizardTuning = !_wizardTuning;
                Rebuild();
            }
        }

        /// <summary>R-1. 좁은 통로에서 옆 사람과 부딪힌 상황 — 몸 전체가 밀리고 상체가 먼저 꺾인다.</summary>
        public void BodyCheck()
        {
            var tuning = _ragdoll.Tuning;
            var direction = tuning.ImpactDirection(impactHeading);
            _ragdoll.ApplyVelocityChange(direction * tuning.BodyCheckSpeed);
            _ragdoll.ApplyImpulse(LastShiftRagdollPart.Chest, direction * tuning.BodyCheckSnapImpulse);
        }

        /// <summary>목 관절만 따로 본다. "머리가 덜렁거리는가"의 직접 시험.</summary>
        public void HeadFlick()
        {
            var tuning = _ragdoll.Tuning;
            _ragdoll.ApplyImpulse(LastShiftRagdollPart.Head, tuning.ImpactDirection(impactHeading) * tuning.HeadFlickImpulse);
        }

        /// <summary>R-3. 운석 충격이 승무원도 날린다.</summary>
        public void Blast()
        {
            var tuning = _ragdoll.Tuning;
            _ragdoll.ApplyBlast(transform.position + blastOrigin, tuning.BlastImpulse, tuning.BlastRadius);
        }

        /// <summary>현재 토글 상태로 래그돌을 다시 만든다.</summary>
        public void Rebuild()
        {
            var tuning = _wizardTuning ? LastShiftRagdollTuning.WizardDefault() : LastShiftRagdollTuning.Comic();
            if (_earthGravity) tuning = tuning.WithEarthGravity();
            _ragdoll.Build(tuning);
        }

        private void LateUpdate()
        {
            if (_ragdoll == null || _ragdoll.Root == null) return;
            LastShiftRagdollLab.FrameSubject(Camera.main, _ragdoll.Root.position);
        }

        private void OnGUI()
        {
            if (_ragdoll == null || !_ragdoll.IsBuilt) return;

            var tuning = _ragdoll.Tuning;
            var settled = _ragdoll.SettledAtSeconds >= 0f
                ? $"{_ragdoll.SettledAtSeconds:F2}s 에 정지"
                : $"{_ragdoll.SecondsSinceImpulse:F2}s 째 안 멈춤";

            var text =
                $"[래그돌 테스트맵]\n" +
                $"Space 몸통 충돌  H 머리 튕기기  B 운석 충격  R 리셋\n" +
                $"G 중력 전환  T 튜닝 전환\n\n" +
                $"중력 y = {tuning.GravityY:F2} ({(_earthGravity ? "지구" : "선내 저중력")})\n" +
                $"튜닝 = {(_wizardTuning ? "Wizard 기본(대조군)" : "Comic(목표)")}  " +
                $"angularDamping={tuning.AngularDamping:F2} sleepThreshold={tuning.SleepThreshold:F3}\n" +
                $"정지 판정 = {(tuning.SettleEnabled ? "켬" : "끔")} → {settled}\n" +
                $"최대 선속도 {_ragdoll.MaxLinearSpeed:F2} m/s  최대 각속도 {_ragdoll.MaxAngularSpeed:F2} rad/s";

            GUI.Label(new Rect(16f, 16f, 720f, 220f), text);
        }
    }
}
