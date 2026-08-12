using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Runtime
{
    public enum LastShiftPlayerSlot
    {
        PlayerOne,
        PlayerTwo
    }

    [RequireComponent(typeof(CharacterController))]
    public sealed class LastShiftPlayerController : MonoBehaviour
    {
        public const float MoveSpeed = 4f;

        /// <summary>
        /// 부피가 큰 부품을 든 동안의 이동 속도. 전역 <see cref="MoveSpeed"/> 를 낮추지 않는
        /// 이유는 그것이 같은 빈 공간을 더 오래 걷게 만드는 일이기 때문이다
        /// (concept-draft.md:165 가 금지한 그것). 큰 물건을 들었을 때만 느려지면
        /// 의미 있는 일을 하는 동안에만 느려진다.
        ///
        /// 설계 요구는 <c>CARRY_SPEED &lt; 3.5</c> 하나이고, 2.8 이라는 값 자체는
        /// game-balance 검증 대상이다. 이 상수를 인접 구역 왕복이 hold 8초를 넘도록
        /// 두는 것이 목적이다 — 넘는 순간 "가서 물건을 가져오기" 가 솔로로 불가능해지고,
        /// 역할 잠금 없이 2인이 필요해지는 지점이 하나 더 생긴다.
        /// </summary>
        public const float CarrySpeed = 2.8f;

        /// <summary>
        /// 부피가 크다고 볼 최소 길이(가장 긴 변)와 최소 두께(두 번째로 긴 변).
        ///
        /// 역할 이름을 나열하지 않고 치수로 판정하는 이유는, 부품이 늘어도 여기를 고치지
        /// 않고 "큰 물건은 느리다" 가 성립하게 하려는 것이다. 두 변을 함께 보는 이유는
        /// 한쪽만으로는 갈리지 않기 때문이다.
        ///
        /// <code>
        /// 부품            치수                긴 변  둘째 변  부피
        /// PatchPlate      1.15 x 1.15 x 0.18   1.15   1.15   0.238  느려짐
        /// CoolingCanister 0.55 x 1.10 x 0.55   1.10   0.55   0.333  느려짐
        /// Battery         0.65 x 0.65 x 0.90   0.90   0.65   0.380  그대로
        /// Tether          0.25 x 0.25 x 1.20   1.20   0.25   0.075  그대로
        /// </code>
        ///
        /// 부피로만 보면 Battery(0.380)가 걸리고 PatchPlate(0.238)가 빠져 기획이 지정한
        /// 둘과 정반대가 된다. 긴 변으로만 보면 Tether(1.20)가 함께 걸려, 밧줄을 들었다는
        /// 이유로 결속 동사가 무거워진다. "길고 <b>또한</b> 가늘지 않은" 것만 걸러야
        /// 판자와 통은 걸리고 밧줄과 배터리는 빠진다.
        /// </summary>
        public const float BulkyItemLongestSide = 1.0f;
        public const float BulkyItemSecondSide = 0.5f;

        /// <summary>
        /// 잡기 판정 사거리이자 <b>잡기 프롬프트가 뜨는 유일한 거리</b>.
        ///
        /// 예전에는 여기에 접근 힌트용 거리가 둘 더 있었다 — <c>AwarenessDistance = 8m</c>
        /// (아직 못 잡는 부품까지 남은 거리를 미리 알려줌)와 <c>SecuredNoticeDistance = 3.2m</c>
        /// (고정 부품에 왜 안 잡히는지 미리 알려줌). 둘 다 "지금은 누를 수 없다" 를 말하는
        /// 문장이고, 배 안은 부품이 널려 있어 <b>걷는 내내 그 문장이 켜져 있었다.</b>
        /// 누를 수 없는 것을 말하는 안내가 상시면 프롬프트는 신호가 아니라 배경이 된다.
        /// 사거리 밖에서는 아무것도 그리지 않는다 — 그래야 떠 있다는 사실이 정보가 된다.
        /// </summary>
        public const float GrabDistance = 2.2f;

        /// <summary>
        /// 조준 허용 반경. 사거리 끝(<see cref="GrabDistance"/>)에서 반각 약 <c>5.7°</c> 라
        /// "그 부근에 에임을 댔을 때" 는 되고 방향만 대충 맞은 상태는 안 된다.
        ///
        /// 표시 판정·실제 grab·서버 검증이 <see cref="TryResolveGrabTarget"/> 하나를 공유하므로
        /// 이 값은 셋을 함께 움직인다. 프롬프트만 따로 좁히면 "안 뜨는데 잡히는" 상태가 생긴다.
        /// </summary>
        public const float GrabAimRadius = 0.22f;

        /// <summary>
        /// 유령의 부유 속도(기획 §4.4 N11). <see cref="MoveSpeed"/> 와 <b>같은 값이며 일부러
        /// 같다.</b> 기획이 v0.4 에서 "유령의 우위는 속도가 아니라 접근" 으로 근거를 바꿨고,
        /// 여기서 속도를 올리면 폐기된 그 논거를 코드가 되살린다.
        /// </summary>
        public const float GhostFloatSpeed = MoveSpeed;

        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform holdSocket;
        [SerializeField] private LastShiftGrabbable heldItem;
        [SerializeField] private LastShiftPlayerSlot playerSlot;
        [SerializeField] private Color identityColor = new(0.2f, 0.65f, 1f);

        private CharacterController characterController;
        private LastShiftNetworkPlayer networkPlayer;
        private float verticalSpeed;
        private float yaw;
        private float pitch;
        private Vector3 cameraShakeOffset;
        private bool grabPressed;
        private bool securePressed;
        private bool safeRestorePressed;
        private bool quickBypassPressed;
        private bool sacrificePressed;
        private bool doorPressed;

        /// <summary>
        /// 지금 이 승무원이 냉각실 밸브를 붙잡고 있는가(<c>C-3</c>, §4.3).
        ///
        /// <b>서버 목록의 사본이 아니라 로컬 입력 상태다.</b> 판정 정본은
        /// <see cref="LastShiftSandboxController.IsCoolingValveHeld"/> 이고 클라이언트에서는 그
        /// 컴포넌트가 꺼져 있어 읽을 수 없다. 이 플래그가 담당하는 것은 <b>이 화면에서 즉시
        /// 반응해야 하는 둘</b>뿐이다 — 이동 잠금과 프롬프트. 그 둘을 서버 왕복 뒤로 미루면
        /// 붙잡는 순간 한 프레임 동안 미끄러지고, 그게 "붙잡음" 이라는 시간 형태를 흐린다.
        /// </summary>
        private bool sustainingValve;
        private bool presetOnePressed;
        private bool presetTwoPressed;
        private bool presetThreePressed;
        private bool resetPressed;
        private bool meteorPressed;
        private bool mapPressed;
        private bool managesCursor = true;
        private string serverRejectionReason;
        private float serverRejectionExpiry;
        private float ghostVerticalInput;

        public LastShiftGrabbable HeldItem => heldItem;
        public LastShiftPlayerSlot PlayerSlot => playerSlot;
        public Camera TargetCamera => targetCamera;
        public Transform HoldSocket => holdSocket;
        public bool UsesMouseLook => true;

        /// <summary>
        /// 유령인가(기획 §4.4 N11). 사망(<c>SuitOxygen == 0.00</c>)의 표현 방식이지 새 계통이
        /// 아니므로 상태의 정본은 <see cref="LastShiftCrewOxygen.IsDead"/> 이고, 이 컴포넌트는
        /// 그 상태를 <see cref="SetGhost"/> 로 받아 <b>이동 방식과 조작 차단</b>만 바꾼다.
        ///
        /// 사망 시 이 컴포넌트를 통째로 꺼 버리면 원칙 문장("이동 제약만 잃는다")의 정반대가
        /// 된다 — 이동까지 잃고 조작만 남는 것이 아니라 둘 다 잃는다.
        /// </summary>
        public bool IsGhost { get; private set; }

        /// <summary>
        /// 화면 아래 조작줄. <b><c>M</c> 이 여기 없던 것이 온보딩 사고의 절반이었다</b>
        /// (2026-08-13 플레이테스트 — "어느 방이 어딘지 모름"). 방 이름이 뜨는 유일한 화면이
        /// 지도인데 그 키가 어디에도 안 적혀 있어서, 지도를 아는 사람만 배 배치를 알 수 있었다.
        ///
        /// 괄호로 <b>무엇이 보이는지</b>를 붙인다. "M 지도" 만으로는 그것이 지금 필요한 화면인지
        /// 알 수 없고, 처음 하는 사람이 찾는 말은 "지도" 이 아니라 "방 이름" 이다.
        /// 유령 줄에도 같이 있다 — 지도는 유령도 열 수 있는 보기 전용 화면이다.
        /// </summary>
        public string InputLabel => IsGhost
            ? "WASD 이동 / Space 상승 / Ctrl 하강 / Mouse 시선 / M 지도(방 이름) — 유령: 잡기·수리·문 조작 불가"
            : "WASD 이동 / Mouse 조준 / E 잡기·놓기 / F 고정 / C·V·G 수리 / Q 문 / T 밸브 유지 / " +
              "M 지도(방 이름) / 1·2·3 프리셋 / R 리셋";
        /// <summary>
        /// 화면 중앙 프롬프트. <b>지금 이 자리에서 누를 것이 있을 때만 문자열이 있고, 없으면
        /// 빈 문자열이다.</b> 예전에는 아무것도 없는 자리에서도 `+   E 잡기: 대상을 조준하세요`
        /// 가 상시로 떠 있어, 화면 한가운데 폭 460px 상자가 배 전체를 도는 내내 시야를 덮었다.
        /// 상시로 떠 있으면 <b>떠 있다는 사실 자체가 정보를 잃는다</b> — 프롬프트가 보인다는
        /// 것이 곧 "여기서 뭔가 된다" 여야 조준이 신호가 된다(CT-01 §1.1 L3 국소 프롬프트).
        ///
        /// null 이 아니라 빈 문자열인 이유는 읽는 쪽이 로그·프로브를 포함해 여럿이고
        /// (<see cref="LastShiftNetworkLifecycleProbe"/> 는 <c>Contains</c> 로 판정한다),
        /// 그쪽에 null 검사를 강요하면 이 속성 하나 때문에 매 호출부가 늘어난다.
        /// 그릴지 말지는 <see cref="HasInteractionPrompt"/> 하나로 판단한다.
        /// </summary>
        public string InteractionPrompt => BuildInteractionPrompt() ?? string.Empty;

        /// <summary>지금 그릴 프롬프트가 있는가. 조준점도 이 값에만 따른다.</summary>
        public bool HasInteractionPrompt => BuildInteractionPrompt() != null;
        public Vector3 AimOrigin => targetCamera != null ? targetCamera.transform.position : transform.position;

        /// <summary>
        /// 조준 방향은 카메라 transform.forward 가 아니라 조준 상태(yaw/pitch)에서 직접 만든다.
        /// 카메라에는 충격 흔들림 오프셋이 합성돼 있으므로 transform 을 읽으면 흔들리는 동안
        /// 조준선이 함께 흔들리고, 그 값이 서버 grab 검증(AuthoritativeAim*)까지 전달된다.
        /// 화면은 흔들려도 판정은 흔들리지 않아야 한다.
        /// </summary>
        public Vector3 AimDirection => targetCamera != null
            ? transform.rotation * Quaternion.Euler(-pitch, 0f, 0f) * Vector3.forward
            : transform.forward;

        public void Configure(Camera camera, Transform socket)
        {
            Configure(camera, socket, LastShiftPlayerSlot.PlayerOne, new Color(0.2f, 0.65f, 1f));
        }

        public void Configure(Camera camera, Transform socket, LastShiftPlayerSlot slot, Color color)
        {
            targetCamera = camera;
            holdSocket = socket;
            playerSlot = slot;
            identityColor = color;
            characterController = GetComponent<CharacterController>();
        }

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            networkPlayer = GetComponent<LastShiftNetworkPlayer>();
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>(true);
            if (holdSocket == null && targetCamera != null) holdSocket = targetCamera.transform.Find("HoldSocket");
            // A2. 귀를 여기서 만든다. 컴포넌트는 붙여 두되 활성 여부는 소유권을 아는 쪽이 정한다 —
            // 네트워크 승무원은 ApplyLocalPresentation 이 다시 정하고, 단독 씬(SP-01)은
            // NetworkPlayer 가 없으므로 여기서 바로 켠다. 배에 활성 리스너는 하나여야 한다.
            LastShiftZoneAudio.EnsureListener(targetCamera, networkPlayer == null);
        }

        private void Start()
        {
            yaw = transform.eulerAngles.y;
            pitch = targetCamera != null ? -targetCamera.transform.localEulerAngles.x : 0f;
            if (pitch < -180f) pitch += 360f;
            if (!ManagesCursor) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            // 소유권 게이트가 이 컴포넌트를 끄면 프롬프트도 같이 사라져야 한다. 임대가
            // 아니라 자기가 만든 계층이라, 이건 스스로 치운다. 커서 해제보다 먼저 하는
            // 이유는 아래 조기 반환이 커서 관리를 안 하는 원격 승무원에게 걸리기 때문이다.
            if (promptView != null) promptView.gameObject.SetActive(false);

            if (!ManagesCursor) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private bool ManagesCursor => managesCursor && (networkPlayer == null || !networkPlayer.IsSpawned || networkPlayer.IsOwner);

        public void SetCursorManagement(bool enabled)
        {
            managesCursor = enabled;
            if (!enabled)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Update()
        {
            ProcessKeyboardInput(Keyboard.current, Time.deltaTime);

            // AI_W_07 — 숙소 출입문 사거리. <b>어느 문인지 따로 안 가린다</b>: 이 줄을 기다리는
            // 구간은 깨어난 자리에서 방을 나서기 전까지뿐이고, 숙소에는 광장으로 나가는 문
            // 하나만 붙는다. 사거리 조회는 씬을 뒤지므로 그 구간에만 돈다.
            if (LastShiftWakeSequence.IsAwaitingQuartersDoor &&
                LastShiftZoneDoor.FindOperable(transform.position) != null)
                LastShiftWakeSequence.NotifyQuartersDoorInRange();
        }

        /// <summary>
        /// HUD 는 이동·상호작용이 다 끝난 뒤에 그린다. <c>OnGUI</c> 시절에는 프레임마다
        /// 레이아웃·리페인트로 <b>두 번씩</b> 돌면서 씬 조회(<see cref="BuildPrompt"/>)를
        /// 두 번 했는데, 여기서는 한 번이다.
        /// </summary>
        private void LateUpdate()
        {
            DrawHud();
        }

        /// <summary>
        /// 유령 전환/복귀(기획 §4.4 N11 구현물 1). 호출자는 <see cref="LastShiftCrewOxygen"/>
        /// 하나이며, 서버·클라이언트·솔로 세 경로가 모두 그 컴포넌트를 거친다.
        ///
        /// 하는 일은 셋이다 — <b>콜라이더를 끄고</b>(CharacterController 를 비활성화하면
        /// 캡슐 콜라이더가 함께 꺼진다), <b>들고 있던 것을 놓고</b>, <b>자세를 세운다</b>.
        /// 중력은 <see cref="ApplyMovement"/> 가 유령 분기에서 아예 적분하지 않으므로
        /// 여기서 끌 것이 없다.
        ///
        /// <see cref="Behaviour.enabled"/> 는 건드리지 않는다. 그 플래그는 네트워크 소유권
        /// 게이트(<see cref="LastShiftNetworkPlayer"/> 의 ApplyLocalPresentation)가 쓰고
        /// 있고, 여기서 함께 쓰면 원격 승무원이 죽었다 살아날 때 남의 화면에서 조작 권한이
        /// 되살아난다.
        /// </summary>
        public void SetGhost(bool ghost)
        {
            if (IsGhost == ghost) return;
            if (characterController == null) characterController = GetComponent<CharacterController>();
            IsGhost = ghost;

            if (ghost)
            {
                // 부품을 문 채로 굳으면 그 부품이 시신과 함께 잠긴다. 유령은 물건을 만질 수
                // 없으므로 놓을 방법도 없다 — 죽는 순간이 유일한 반환 시점이다.
                DropHeldItem();
                // 덕트 안에서 죽으면 웅크린 자세가 그대로 남아 시선 높이가 바닥에 붙는다.
                // 유령은 몸이 없으니 자세도 없고, 머리 위 공간 검사도 의미가 없다.
                if (IsCrouching) ApplyStance(false);
                verticalSpeed = 0f;
                ghostVerticalInput = 0f;
            }

            if (characterController != null) characterController.enabled = !ghost;
        }

        public void ProcessKeyboardInput(Keyboard keyboard)
        {
            ProcessKeyboardInput(keyboard, Time.deltaTime);
        }

        public void ProcessKeyboardInput(Keyboard keyboard, float deltaTime)
        {
            if (keyboard == null || targetCamera == null) return;

            var move = ReadMove(keyboard);
            var look = ReadLook(Mouse.current);
            var jump = keyboard.spaceKey.wasPressedThisFrame;
            // 누르고 있는 동안 웅크린다. 토글로 두면 덕트에서 나온 뒤에도 웅크린 채 걸어
            // 다니게 되고, 그 상태가 화면에서 잘 안 읽혀 "왜 느리지" 가 된다.
            var descend = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            SetCrouching(descend);
            // 유령의 수직 입력. 같은 두 키를 재사용한다 — 웅크림은 유령에게 의미가 없고,
            // 조작 동사를 하나 더 만들지 않는 것이 §4.4 의 "새 규칙 없음" 판정 근거다.
            ghostVerticalInput = (keyboard.spaceKey.isPressed ? 1f : 0f) - (descend ? 1f : 0f);
            var grab = ConsumePress(keyboard.eKey.isPressed, ref grabPressed);
            var secure = ConsumePress(keyboard.fKey.isPressed, ref securePressed);
            // 제자리에 놓기(F)와 계통에 연결하기(C·V·G)는 다른 행동이다. E·F 는 이미 쓰고 있다.
            var safeRestore = ConsumePress(keyboard.cKey.isPressed, ref safeRestorePressed);
            var quickBypass = ConsumePress(keyboard.vKey.isPressed, ref quickBypassPressed);
            var sacrifice = ConsumePress(keyboard.gKey.isPressed, ref sacrificePressed);
            // 문 개폐(Q). 잡기(E)·고정(F)·수리(C·V·G)와 다른 키여야 한다 — 문 앞에서 부품을
            // 들고 있는 상황이 흔하고, 키를 겹치면 "문을 열려다 부품을 놓는" 사고가 난다.
            var door = ConsumePress(keyboard.qKey.isPressed, ref doorPressed);
            var presetOne = ConsumePress(keyboard.digit1Key.isPressed, ref presetOnePressed);
            var presetTwo = ConsumePress(keyboard.digit2Key.isPressed, ref presetTwoPressed);
            var presetThree = ConsumePress(keyboard.digit3Key.isPressed, ref presetThreePressed);
            var reset = ConsumePress(keyboard.rKey.isPressed, ref resetPressed);
            // 운석은 K 다(M 은 지도가 가져갔다). 키만 옮겼고 나머지 경로는 그대로다.
            var meteor = ConsumePress(keyboard.kKey.isPressed, ref meteorPressed);
            // 지도(M). <b>여기서 읽는다</b> — LastShiftSandboxController 의 키 블록은
            // 클라이언트에서 통째로 꺼져 있어서(LastShiftNetworkSandbox 가 enabled = IsServer),
            // 거기 두면 host 에서만 열린다. 지도는 화면일 뿐이라 피어마다 따로이고 RPC 가 없다.
            if (ConsumePress(keyboard.mKey.isPressed, ref mapPressed)) LastShiftMapView.Toggle();
            LastShiftMapView.Tick();
            // 냉각실 밸브 유지(T). <b>ConsumePress 를 안 쓴다</b> — 나머지 전부가 순간 동사라
            // "눌린 프레임" 을 세지만, 이 동사는 "눌려 있는 동안" 자체가 효과다(§4.3 시간 형태).
            // §4.3 표는 R 을 적었으나 R 은 이미 프리셋 리셋이라 T 로 옮겼다.
            UpdateValveSustain(keyboard.tKey.isPressed);

            // 기상 도입부는 시점과 이동을 따로 푼다(정본 §4-1). 상태기가 안 돌면 둘 다 참이라
            // 평상시에는 없는 것과 같다 — 잠금이 기본값이 되는 경로를 만들지 않는다.
            var canLook = LastShiftWakeSequence.CanLook;
            var canMove = LastShiftWakeSequence.CanMove;
            // AI_W_06 은 "첫 이동 입력" 이다. 실제로 걸었는지가 아니라 <b>키를 눌렀는지</b>이므로
            // 벽에 붙어 밀고 있어도 뜬다 — 안 그러면 문 쪽이 막힌 자리에서 영영 안 넘어간다.
            if (canMove && move.sqrMagnitude > 0f) LastShiftWakeSequence.NotifyFirstMove();

            ApplyLook(canLook ? look : Vector2.zero, deltaTime);
            // 붙잡고 있는 동안은 이동이 없다(§4.3 제약). 이 한 줄이 이 동사가 채우려던 문법 축
            // "소비 대상 = 사람" 그 자체다 — 효과만 있고 자리에 안 묶이면 걸어 두는 동사가 되고,
            // 그건 조종석 hold 가 이미 하고 있다.
            ApplyMovement(sustainingValve || !canMove ? Vector2.zero : move,
                jump && !sustainingValve && canMove, deltaTime);
            // 유령은 배를 만질 수 없다(기획 §4.4 N11 구현물 2 — 잡기·수리·문·조종 전면 차단).
            // 서버도 같은 판정을 각 진입점에서 다시 하지만, 요청 자체가 나가지 않는 것이
            // 정상 상태다. 프리셋·리셋(1·2·3·R)은 조작 동사가 아니라 검증 도구이므로 남긴다 —
            // 막으면 2인 모두 죽은 뒤 아무도 씬을 되돌릴 수 없다.
            //
            // 붙잡고 있는 동안은 다른 동사도 못 쓴다(§4.3 제약). 유령 차단과 같은 자리에 거는
            // 것은 이유가 같아서다 — 두 경우 모두 "요청 자체가 나가지 않는 것" 이 정상이고,
            // 서버는 어차피 각 진입점에서 자기 조건을 다시 본다.
            if (!IsGhost && !sustainingValve)
            {
                // 잔해 뜯기가 잡기보다 먼저다 — §5.2-3 이 "기존 아이템 운반 동사 그대로" 라고
                // 정한 그 자리이고, 선외에는 잡을 부품이 없으므로 두 갈래가 실제로 안 겹친다.
                if (grab && !LastShiftSalvage.TryHarvest(transform.position)) ToggleGrab();
                if (secure && networkPlayer != null) networkPlayer.RequestSecureHeldItem();
                if (door && (networkPlayer == null || !networkPlayer.IsSpawned)) TryOperateNearestDoor();
            }
            if (networkPlayer == null || !networkPlayer.IsSpawned) return;
            if (!IsGhost && !sustainingValve)
            {
                if (door) networkPlayer.RequestDoorToggle();
                if (safeRestore) networkPlayer.RequestRepair(LastShiftRepairMode.SafeRestore);
                else if (quickBypass) networkPlayer.RequestRepair(LastShiftRepairMode.QuickBypass);
                else if (sacrifice) networkPlayer.RequestRepair(LastShiftRepairMode.PerformanceSacrifice);
            }
            if (presetOne) networkPlayer.RequestPresetReset(LastShiftPreset.HighHeatHighThrust);
            else if (presetTwo) networkPlayer.RequestPresetReset(LastShiftPreset.PowerOverloadLooseBattery);
            else if (presetThree) networkPlayer.RequestPresetReset(LastShiftPreset.BadAttitudeHighOxygen);
            else if (reset) networkPlayer.RequestCurrentPresetReset();
            // 운석(K). 프리셋·리셋과 같은 검증 도구 계열이라 유령 차단 밖에 둔다.
            // 이 줄이 없어서 host 로 뜬 씬에서 그 키가 아무 데서도 안 먹었다 — 서버 RPC 는
            // 있었지만 부르는 곳이 없었고, LastShiftSandboxController 의 키 처리 블록은
            // 네트워크 샌드박스가 스폰되면 통째로 꺼진다.
            else if (meteor) networkPlayer.RequestMeteorImpact();
        }

        public bool TryGrabForProbe(LastShiftGrabbable item)
        {
            if (IsGhost) return false;
            if (item == null || item.IsHeld || heldItem != null || holdSocket == null) return false;
            heldItem = item;
            heldItem.Grab(holdSocket);
            return true;
        }

        public void DropForProbe()
        {
            DropHeldItem();
        }

        /// <summary>
        /// 조준각을 직접 설정한다. 조준은 카메라 transform 이 아니라 pitch 상태에서 나오므로
        /// (충격 흔들림이 판정에 섞이지 않게 하려는 의도), 카메라 localRotation 을 밖에서
        /// 써도 조준은 움직이지 않는다. 검증 코드가 조준을 세울 때는 이 경로를 쓴다.
        /// </summary>
        public void SetAimPitchForProbe(float pitchDegrees)
        {
            pitch = Mathf.Clamp(pitchDegrees, -80f, 80f);
            ApplyCameraRotation();
        }

        /// <summary>
        /// 조준을 특정 방향으로 세운다. 몸통 yaw 와 카메라 pitch 로 분해해서 넣으므로
        /// 이후 <see cref="ApplyLook"/> 이 돌아도 조준이 되돌아가지 않는다. 카메라
        /// world rotation 을 직접 쓰면 조준 상태가 갱신되지 않아 무효가 된다.
        /// </summary>
        public void SetAimDirectionForProbe(Vector3 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) return;
            direction.Normalize();
            var flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude > 1e-6f) yaw = Quaternion.LookRotation(flat, Vector3.up).eulerAngles.y;
            pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f)) * Mathf.Rad2Deg, -80f, 80f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            ApplyCameraRotation();
        }

        public void MoveForProbe(Vector2 move, float deltaTime)
        {
            ApplyMovement(Vector2.ClampMagnitude(move, 1f), false, deltaTime);
        }

        /// <summary>
        /// 수직 입력까지 지정하는 이동 프로브. 유령의 부유는 Space·Ctrl 이 만드는 값이라
        /// 평면 입력만으로는 재현되지 않고, 키보드는 EditMode 검증에서 만들 수 없다.
        /// </summary>
        public void MoveForProbe(Vector2 move, float vertical, float deltaTime)
        {
            ghostVerticalInput = Mathf.Clamp(vertical, -1f, 1f);
            ApplyMovement(Vector2.ClampMagnitude(move, 1f), false, deltaTime);
        }

        public Vector2 ReadMoveForProbe(Keyboard keyboard)
        {
            return ReadMove(keyboard);
        }

        public bool OwnsInputKey(Key key)
        {
            return key is Key.W or Key.A or Key.S or Key.D or Key.Space or Key.E or Key.F
                or Key.C or Key.V or Key.G or Key.Q;
        }

        /// <summary>
        /// 문 앞에 서 있으면 그 문을 조작한다. 솔로 경로 전용이며, 네트워크에서는 서버가
        /// 같은 판정을 다시 한다(<see cref="LastShiftNetworkSandbox.RequestDoorToggleRpc"/>).
        /// 살아 있는 승무원인지는 <see cref="LastShiftZoneDoor.TryOperate"/> 가 본다.
        /// </summary>
        public bool TryOperateNearestDoor()
        {
            // 문 개폐는 격리이고, 격리는 대가를 치르는 사람이 결정해야 한다(기획 §4.4).
            // 진입점이 셋(솔로 입력·서버 RPC·이 함수)이라 각자 막지 않고 조작 판정이 모이는
            // LastShiftZoneDoor.TryOperate 가 최종 권위지만, 여기서도 조기에 끊어 둔다.
            if (IsGhost) return false;
            var door = LastShiftZoneDoor.FindOperable(transform.position);
            if (door != null) return door.TryOperate(this);

            // 갑판 승강구 해치도 같은 키다(§23.6 — 수직 진입에 새 조작 동사를 안 만든다).
            // 사거리가 겹치지 않아 순서가 결과를 바꾸지 않는다(LastShiftDeckHatch.FindOperable).
            var hatch = LastShiftDeckHatch.FindOperable(transform.position);
            if (hatch != null) return hatch.TryOperate(this);

            // 에어록도 같은 키다. 승강구와 x·z 가 같지만(에어록이 선수 승강구 아래 모서리에서
            // 분기한다, §23.5) 승강구 사거리는 1.2m·y 무시라 덕트 안 어디서나 걸린다 —
            // 그래서 승강구를 <b>먼저</b> 본다. 덕트 바닥에 서면 승강구가, 에어록 안으로
            // 내려가면 에어록이 잡히고, 그 경계가 LastShiftAirlock.IsAtInnerSide 다.
            return LastShiftAirlock.TryOperate(transform.position, LiftAwayFromDeck);
        }

        /// <summary>
        /// 갑판 승강구 해치가 하나라도 열려 있는가 — 에어록 인터록의 셋째 조건이 읽는 값이다.
        /// sandbox 가 정본이고(<see cref="LastShiftSandboxController.IsHatchOpen"/>), 없으면
        /// 닫힘으로 본다: 최소 조립에서 안전한 쪽은 "구멍이 없다" 이고, 그 기본값이
        /// <see cref="LastShiftDeckHatch.IsOpen"/> 과 같아야 두 판정이 안 갈린다.
        /// </summary>
        private static bool LiftAwayFromDeck => !LastShiftEvaLift.IsAtDeck;

        public void ResetPlayer(Vector3 position)
        {
            ResetPlayer(position, Quaternion.identity);
        }

        public void ResetPlayer(Vector3 position, Quaternion rotation)
        {
            DropHeldItem();
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            // 유령이면 꺼진 채로 둔다. 무조건 켜면 리셋 한 번으로 유령이 벽에 다시 막힌다.
            characterController.enabled = !IsGhost;
            yaw = rotation.eulerAngles.y;
            pitch = 0f;
            verticalSpeed = 0f;
            if (targetCamera != null) targetCamera.transform.localRotation = Quaternion.identity;
        }

        private static bool ConsumePress(bool isPressed, ref bool wasPressed)
        {
            var pressedThisUpdate = isPressed && !wasPressed;
            wasPressed = isPressed;
            return pressedThisUpdate;
        }

        private Vector2 ReadMove(Keyboard keyboard)
        {
            return Vector2.ClampMagnitude(new Vector2(
                (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)), 1f);
        }

        private static Vector2 ReadLook(Mouse mouse)
        {
            return mouse != null ? mouse.delta.ReadValue() * 0.12f : Vector2.zero;
        }

        private void ApplyLook(Vector2 look, float deltaTime)
        {
            yaw += look.x;
            pitch = Mathf.Clamp(pitch + look.y, -80f, 80f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            ApplyCameraRotation();
        }

        /// <summary>
        /// 조준각과 충격 흔들림을 한 곳에서 합성한다. 흔들림을 카메라 localRotation 에 직접
        /// 쓰면 다음 조준 갱신이 그대로 덮어써서 흔들림이 사라지고, 반대로 흔들림이 나중에
        /// 쓰면 조준이 밀린다. 조준 상태(pitch)는 유지하고 표시 회전만 더한다.
        /// </summary>
        private void ApplyCameraRotation()
        {
            if (targetCamera == null) return;
            targetCamera.transform.localRotation = Quaternion.Euler(-pitch + cameraShakeOffset.x, cameraShakeOffset.y, cameraShakeOffset.z);
        }

        /// <summary>
        /// 충격 연출이 프레임마다 넘기는 흔들림 각(도). 조준 캐시는 건드리지 않으므로
        /// 서버 grab 검증(AuthoritativeAim*)이 흔들림 때문에 흔들리지 않는다.
        /// </summary>
        public void SetCameraShakeOffset(Vector3 eulerOffset)
        {
            cameraShakeOffset = eulerOffset;
            ApplyCameraRotation();
        }

        private void ApplyMovement(Vector2 move, bool jump, float deltaTime)
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (IsGhost)
            {
                ApplyGhostMovement(move, deltaTime);
                return;
            }

            var worldMove = transform.right * move.x + transform.forward * move.y;
            // 선내 저중력은 LastShiftShipPhysics 정본을 쓴다. 전역 Physics.gravity 를 읽으면
            // ProjectSettings 를 바꿔야 하고, 그러면 지구 중력을 전제한 DU02/DU03BC 검증이 깨진다.
            if (characterController.isGrounded)
            {
                verticalSpeed = LastShiftShipPhysics.GroundedSettleSpeed;
                if (jump) verticalSpeed = LastShiftShipPhysics.JumpSpeed;
            }
            else
            {
                verticalSpeed += LastShiftShipPhysics.GravityY * deltaTime;
            }

            characterController.Move((worldMove * CurrentMoveSpeed + Vector3.up * verticalSpeed) * deltaTime);
        }

        /// <summary>
        /// 유령 이동(기획 §4.4 — "이동 제약만 잃는다"). 벽·문·닫힌 격리를 통과해야 하므로
        /// <b><see cref="CharacterController.Move"/> 를 쓰지 않는다.</b> 그 함수는 콜라이더를
        /// 꺼도 씬 지오메트리를 쓸어(sweep) 막히기 때문에, 통과하려면 transform 을 직접
        /// 옮기는 수밖에 없다.
        ///
        /// 시선 기준 3차원 부유다. 저중력을 받지 않으므로 바닥이라는 기준면이 없고, 위아래는
        /// Space·Ctrl 로 직접 준다. 이것이 §4.4 가 말한 "격리된 산소실에 그냥 걸어 들어간다" 의
        /// 실제 동작이다.
        /// </summary>
        private void ApplyGhostMovement(Vector2 move, float deltaTime)
        {
            var velocity = AimDirection * move.y + transform.right * move.x + Vector3.up * ghostVerticalInput;
            // 대각 입력이 축 입력보다 빨라지지 않게 한다. 셋을 더한 뒤 한 번만 자른다.
            if (velocity.sqrMagnitude > 1f) velocity.Normalize();
            transform.position += velocity * (GhostFloatSpeed * deltaTime);
        }

        /// <summary>
        /// 지금 적용되는 이동 속도. 부피가 큰 부품을 든 동안에만 <see cref="CarrySpeed"/> 다.
        /// 솔로와 네트워크가 각자 소지품을 다른 곳에 들고 있으므로 둘 다 본다 — 한쪽만 보면
        /// 호스트에서만 느려지거나 클라이언트에서만 느려져 같은 배에서 두 속도가 생긴다.
        /// </summary>
        public float CurrentMoveSpeed => IsCrouching
            ? LastShiftShipPhysics.CrouchSpeed
            : IsCarryingBulkyItem ? CarrySpeed : MoveSpeed;

        /// <summary>
        /// 웅크리고 있는가. 우회 통로(docs §5, 단면 <c>0.9m</c>)를 지나는 자세이고, 그 통로가
        /// 유일한 용도다 — 선내 어디서든 웅크릴 수 있게 두는 것은 조작을 하나 늘리는 대신
        /// 얻는 것이 없다. 다만 상태를 통로 안에서만 켜지게 만들면 "통로에 들어가려면 이미
        /// 웅크려 있어야 하는데 통로 밖에서는 못 웅크린다" 는 순환이 생긴다.
        /// </summary>
        public bool IsCrouching { get; private set; }

        /// <summary>
        /// 웅크림 자세를 적용한다. 높이만 바꾸는 것이 아니라 <b>중심과 눈높이도 같이</b> 옮긴다 —
        /// CharacterController 는 중심이 그대로면 높이를 줄일 때 발이 바닥에서 떠서, 웅크리는
        /// 순간 승무원이 공중에 뜨고 다음 프레임에 떨어진다.
        ///
        /// <b>일어서기는 머리 위 공간이 있을 때만 된다.</b> 없으면 웅크린 채로 남는다 —
        /// 덕트 안에서 일어서면 CharacterController 가 천장을 뚫고 나가 갑판 위로 솟는다.
        /// </summary>
        public void SetCrouching(bool crouch)
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            if (crouch == IsCrouching) return;
            // 유령에게는 자세가 없다. 웅크림의 유일한 용도가 단면 0.9m 통로를 지나는 것인데,
            // 유령은 통로든 벽이든 그냥 통과한다.
            if (IsGhost) return;
            if (!crouch && !HasStandingHeadroom()) return;

            ApplyStance(crouch);
        }

        /// <summary>
        /// 자세를 실제로 적용한다. <see cref="SetCrouching"/> 의 머리 위 공간 검사와 분리한
        /// 이유는 유령 전환이 그 검사를 통과할 필요가 없기 때문이다 — 덕트 안에서 죽어도
        /// 몸이 없으므로 천장을 뚫을 것이 없다.
        /// </summary>
        private void ApplyStance(bool crouch)
        {
            IsCrouching = crouch;
            var height = crouch ? LastShiftShipPhysics.CrouchHeight : LastShiftShipPhysics.StandingHeight;
            characterController.height = height;
            characterController.center = new Vector3(0f, height * 0.5f, 0f);
            if (targetCamera != null)
                targetCamera.transform.localPosition = new Vector3(0f,
                    crouch ? LastShiftShipPhysics.CrouchEyeHeight : LastShiftShipPhysics.EyeHeight, 0f);
        }

        /// <summary>
        /// 일어설 자리가 있는가. 웅크린 캡슐 <b>머리 위</b>로 서 있을 높이만큼 비어 있는지 본다.
        /// 반지름을 조금 줄여 쏘는 것은 벽에 붙어 선 상태에서 벽 자체를 짚어 영영 못 일어서는
        /// 것을 막기 위해서다.
        ///
        /// <b>자기 콜라이더를 걸러야 한다.</b> CharacterController 는 물리 질의에 그대로 잡히므로,
        /// 걸러내지 않으면 웅크린 자기 캡슐이 검사에 걸려 <b>어디서도 못 일어선다</b> — 웅크림이
        /// 누르고 있는 동안만 유지되는 조작이라(Ctrl) 그 순간 승무원이 영영 웅크린 채로 남는다.
        /// 들고 있는 부품도 승무원 아래에 붙으므로 같이 뺀다.
        /// </summary>
        private bool HasStandingHeadroom()
        {
            var probeRadius = LastShiftShipPhysics.CrewRadius * 0.9f;
            // 아래 끝은 웅크린 캡슐의 정수리다. 그보다 낮게 잡으면 몸통 옆에 붙은 설비가
            // "머리 위 공간 없음" 으로 읽힌다.
            var bottom = transform.position + Vector3.up * (LastShiftShipPhysics.CrouchHeight + probeRadius);
            var top = transform.position + Vector3.up * (LastShiftShipPhysics.StandingHeight - probeRadius);
            var hits = UnityEngine.Physics.OverlapCapsuleNonAlloc(bottom, top, probeRadius,
                HeadroomHits, ~0, QueryTriggerInteraction.Ignore);
            for (var index = 0; index < hits; index++)
            {
                var hit = HeadroomHits[index];
                if (hit == null || hit.transform.IsChildOf(transform)) continue;
                return false;
            }

            return true;
        }

        /// <summary>머리 위 공간 검사 버퍼. 매 프레임 도는 자리라 할당을 남기지 않는다.</summary>
        private static readonly Collider[] HeadroomHits = new Collider[8];

        public bool IsCarryingBulkyItem
        {
            get
            {
                var carried = networkPlayer != null && networkPlayer.IsSpawned
                    ? (networkPlayer.HeldItem != null ? networkPlayer.HeldItem.Grabbable : null)
                    : heldItem;
                return IsBulky(carried);
            }
        }

        /// <summary>
        /// "길고 또한 가늘지 않은가". 가장 긴 변과 두 번째로 긴 변을 함께 본다 —
        /// <see cref="BulkyItemLongestSide"/> 주석의 표가 왜 두 변이어야 하는지를 담고 있다.
        /// </summary>
        public static bool IsBulky(LastShiftGrabbable item)
        {
            if (item == null) return false;
            var size = item.transform.localScale;
            var longest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            var shortest = Mathf.Min(size.x, Mathf.Min(size.y, size.z));
            var second = size.x + size.y + size.z - longest - shortest;
            return longest >= BulkyItemLongestSide && second >= BulkyItemSecondSide;
        }

        private void ToggleGrab()
        {
            if (networkPlayer != null && networkPlayer.IsSpawned)
            {
                if (networkPlayer.HeldItem != null)
                {
                    networkPlayer.RequestDrop(targetCamera != null ? targetCamera.transform.forward * 1.4f : Vector3.zero);
                    return;
                }

                if (TryGetNetworkTarget(out var networkItem, out _))
                    networkPlayer.RequestGrab(networkItem);
                else
                    Debug.Log($"[LAST_SHIFT_INTERACTION] client={networkPlayer.OwnerClientId} action=grab result=FAIL reason=client-no-aim-target prompt={InteractionPrompt}");
                return;
            }

            if (heldItem != null)
            {
                DropHeldItem();
                return;
            }

            if (UnityEngine.Physics.Raycast(
                    targetCamera.transform.position,
                    targetCamera.transform.forward,
                    out var hit,
                    GrabDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                TryGrabForProbe(hit.collider.GetComponentInParent<LastShiftGrabbable>());
            }
        }

        /// <summary>
        /// 단일 조준 판정. 프롬프트 표시와 실제 grab 요청, 서버 검증이 모두 이 함수만 사용한다.
        /// 판정 기준이 갈라지면 "잡을 수 있다"고 표시하면서 잡히지 않는 상태가 생긴다.
        /// </summary>
        public static bool TryResolveGrabTarget(
            Vector3 origin,
            Vector3 direction,
            out LastShiftNetworkGrabbable item,
            out float distance)
        {
            item = null;
            distance = float.PositiveInfinity;
            var aim = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            var hits = UnityEngine.Physics.SphereCastAll(
                origin,
                GrabAimRadius,
                aim,
                GrabDistance,
                ~0,
                QueryTriggerInteraction.Ignore);
            foreach (var candidate in hits)
            {
                var candidateItem = candidate.collider.GetComponentInParent<LastShiftNetworkGrabbable>();
                if (candidateItem == null) continue;
                var candidateDistance = candidate.distance > 0f
                    ? candidate.distance
                    : Vector3.Distance(origin, candidate.collider.bounds.center);
                if (candidateDistance >= distance) continue;
                item = candidateItem;
                distance = candidateDistance;
            }
            return item != null;
        }

        private bool TryGetNetworkTarget(out LastShiftNetworkGrabbable item, out float distance)
        {
            return TryResolveGrabTarget(AimOrigin, AimDirection, out item, out distance);
        }

        /// <summary>지금 이 승무원이 냉각실 밸브를 붙잡고 있는가. 이동 잠금·프롬프트가 읽는다.</summary>
        public bool IsSustainingValve => sustainingValve;

        /// <summary>
        /// 밸브 유지 입력 한 프레임(<c>C-3</c>, §4.3). <b>상태가 바뀔 때만</b> 아래로 내려보낸다 —
        /// 매 프레임 RPC 를 쏘면 붙잡고 있는 <c>14</c>초가 초당 수십 개의 서버 호출이 된다.
        ///
        /// 사거리를 여기서도 보는 것은 예측이지 판정이 아니다. 서버는
        /// <see cref="LastShiftSandboxController.SetCoolingValveHeld"/> 에서 같은 검사를 다시 하고,
        /// 붙잡은 뒤 위치가 밖에서 바뀌는 경우는 서버의 매 tick 정리가 잡는다.
        /// </summary>
        private void UpdateValveSustain(bool pressed)
        {
            var wanted = pressed && !IsGhost &&
                         (sustainingValve || LastShiftCoolingValve.IsWithinReach(transform.position));
            if (wanted == sustainingValve) return;
            sustainingValve = wanted;

            if (networkPlayer != null && networkPlayer.IsSpawned)
            {
                networkPlayer.RequestCoolingValveHold(wanted);
                return;
            }

            // 네트워크가 없는 경로는 샌드박스가 자기 Update 에서 같은 키를 직접 읽는다
            // (수리 3종·문과 같은 분담). 여기서 또 부르면 같은 프레임에 잡고 놓는다.
        }

        /// <summary>
        /// 서버가 grab 을 거부했을 때 그 사유를 소유자 화면 프롬프트에 그대로 노출한다.
        /// </summary>
        public void ReportServerRejection(string reason)
        {
            serverRejectionReason = reason;
            serverRejectionExpiry = Time.unscaledTime + 2.5f;
        }

        /// <summary>
        /// 프롬프트 한 건 — <b>문장과, 그 문장이 가리키는 대상의 월드 좌표</b>를 같이 나른다.
        ///
        /// 문장만 돌려주면 그릴 자리를 화면 고정 좌표로 정하는 수밖에 없다. 그러면 "이 안내가
        /// 저 물건에 대한 것" 이라는 연결을 글자로만 말해야 하고(<c>Battery: ...</c>), 대상이
        /// 둘 이상 보이는 자리에서는 그 글자도 어느 쪽인지 못 가린다. 좌표를 같이 들고 다니면
        /// 안내가 대상 위에 붙어서 연결이 그림으로 성립한다.
        ///
        /// 앵커가 없는 것도 정상이다 — 유령 상태나 서버 거부처럼 <b>대상이 아니라 승무원 자신의
        /// 상태</b>를 말하는 문장은 가리킬 물건이 없다. 그쪽은 화면 고정 자리로 떨어진다.
        /// </summary>
        private readonly struct PromptDraw
        {
            public readonly string Text;
            public readonly Vector3 Anchor;
            public readonly bool HasAnchor;

            private PromptDraw(string text, Vector3 anchor, bool hasAnchor)
            {
                Text = text;
                Anchor = anchor;
                HasAnchor = hasAnchor;
            }

            /// <summary>그릴 것이 없다.</summary>
            public static PromptDraw None => default;

            /// <summary>대상 위에 뜬다.</summary>
            public static PromptDraw At(string text, Vector3 anchor) => new(text, anchor, true);

            /// <summary>가리킬 대상이 없어 화면 고정 자리에 뜬다.</summary>
            public static PromptDraw Floating(string text) => new(text, Vector3.zero, false);

            public bool Exists => Text != null;
        }

        /// <summary>
        /// 대상 <b>윗면</b>의 월드 좌표. 안내는 물건을 덮는 것이 아니라 물건 위에 떠야 하므로
        /// 중심이 아니라 위쪽 경계를 잡는다.
        ///
        /// Renderer 를 먼저 보는 이유는 그것이 <b>플레이어가 실제로 보는 크기</b>이기 때문이다.
        /// Collider 가 렌더보다 크거나 작은 부품이 있고, 그럴 때 판정 상자를 기준으로 띄우면
        /// 눈에는 물건에서 떨어진 자리에 뜬다. 둘 다 없으면 transform 을 그대로 쓴다.
        /// </summary>
        private static Vector3 AnchorTopOf(Component target, Vector3 fallback)
        {
            if (target == null) return fallback;
            var renderer = target.GetComponentInChildren<Renderer>();
            if (renderer != null)
                return new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, renderer.bounds.center.z);
            var collider = target.GetComponentInChildren<Collider>();
            if (collider != null)
                return new Vector3(collider.bounds.center.x, collider.bounds.max.y, collider.bounds.center.z);
            return target.transform.position;
        }

        /// <summary>
        /// 좌표를 가진 정적 동사(에어록·잔해처럼 컴포넌트를 안 거치는 것)의 앵커.
        /// 바닥 좌표를 그대로 쓰면 안내가 발치에 뜨므로 사람 눈높이만큼 올린다.
        /// </summary>
        private static Vector3 AnchorAbove(Vector3 groundPoint) =>
            groundPoint + Vector3.up * LastShiftShipPhysics.EyeHeight;

        private string BuildInteractionPrompt() => BuildPrompt().Text;

        private PromptDraw BuildPrompt()
        {
            // 유령은 어느 프롬프트도 받지 않는다. 잡을 수 있다고 표시해 놓고 눌러도 안 되는
            // 것보다, 왜 안 되는지를 한 줄로 못박는 편이 낫다(문 프롬프트가 사망 승무원에게
            // "조작 불가" 를 보여 주던 것과 같은 이유다). 이건 대상이 아니라 내 상태라 앵커가 없다.
            if (IsGhost) return PromptDraw.Floating("유령 — 이동만 가능 (잡기·수리·문 조작 불가)");

            // 밸브가 가장 먼저다. 붙잡고 있는 동안은 다른 동사가 아예 막혀 있으므로(§4.3 제약),
            // 그 상태에서 잡기·문 안내를 띄우면 눌러도 안 되는 것을 알려주는 꼴이다.
            var valvePrompt = BuildValvePrompt();
            if (valvePrompt.Exists) return valvePrompt;

            // 잔해가 문보다 먼저다. 사거리가 겹칠 일은 없지만(잔해는 원반 밖이다) 선외에서
            // 뜰 수 있는 안내가 이것 하나뿐이라 어느 갈래에도 안 가려져야 한다.
            var salvagePrompt = BuildSalvagePrompt();
            if (salvagePrompt.Exists) return salvagePrompt;

            // 문 프롬프트가 아이템 프롬프트보다 먼저다. 문 앞에서만 뜨는 안내이고, 그 자리에서
            // 아이템을 조준하고 있을 확률보다 문을 조작하려 할 확률이 높다.
            var doorPrompt = BuildDoorPrompt();
            if (doorPrompt.Exists) return doorPrompt;

            // 수리 프롬프트는 문 다음이다. 손상 지점은 방 안이고 문은 경계에 있어 사거리가
            // 겹치지 않지만, 겹치는 배치가 생기면 문 쪽을 남긴다 — 문은 그 자리를 떠나는
            // 동사라 잘못 가려지면 승무원이 갇힌다.
            var repairPrompt = BuildRepairPrompt();
            if (repairPrompt.Exists) return repairPrompt;
            // 네트워크가 없는 단독 씬(SP-01)에서도 빈손이면 아무것도 그리지 않는다.
            if (networkPlayer == null || !networkPlayer.IsSpawned)
                return heldItem != null
                    ? PromptDraw.At("[E] 놓기", AnchorTopOf(heldItem, heldItem.transform.position))
                    : PromptDraw.None;
            if (serverRejectionReason != null)
            {
                if (Time.unscaledTime <= serverRejectionExpiry)
                    return PromptDraw.Floating($"서버 거부: {serverRejectionReason}");
                serverRejectionReason = null;
            }
            if (networkPlayer.HeldItem != null && networkPlayer.HeldItem.Grabbable == null)
                return PromptDraw.At("[E] 놓기", AnchorTopOf(networkPlayer.HeldItem, networkPlayer.HeldItem.transform.position));
            if (networkPlayer.HeldItem != null)
            {
                var held = networkPlayer.HeldItem;
                var distanceToNominal = Vector3.Distance(
                    held.transform.position,
                    held.Grabbable.NominalPosition);
                // 들고 있는 것도 물건이다 — 안내는 손에 든 그것 위에 붙는다.
                var heldAnchor = AnchorTopOf(held, held.transform.position);
                return PromptDraw.At(
                    distanceToNominal <= LastShiftSandboxController.SecureDistance
                        ? "[E] 놓기   [F] 제자리에 고정"
                        : $"[E] 놓기   고정 위치까지 {distanceToNominal:F1}m",
                    heldAnchor);
            }
            // 여기부터가 잡기 안내다. <b>판정과 표시가 같은 함수를 쓴다</b> —
            // 조준이 <see cref="TryResolveGrabTarget"/> 에 걸리지 않으면 할 말이 없다.
            // 예전에는 이 자리에서 조준선 앞 8m 를 따로 훑어 "접근 필요 3.1m" 같은 문장을
            // 돌려줬는데, 그건 지금 누를 수 없는 것을 말하는 안내라 상시 UI 로 되돌아왔다.
            if (!TryGetNetworkTarget(out var item, out _)) return PromptDraw.None;
            var itemAnchor = AnchorTopOf(item, item.transform.position);
            // 아직 spawn 되지 않은 아이템은 역할을 신뢰할 수 없다. OnGUI 는 매 프레임 돌기 때문에
            // 여기서 예외가 나면 화면이 아니라 로그가 먼저 무너진다.
            if (item.Grabbable == null)
                return PromptDraw.At("[E] 잡기 — 대상 확인 중", itemAnchor);
            // 고정된 부품은 눌러도 안 잡힌다. 사거리 안에서 조준했을 때만 사유를 남기므로
            // 이 문장은 "잡으려 다가와 조준한 사람" 에게만 뜬다.
            if (item.IsSecured)
                return PromptDraw.At($"{item.Grabbable.Role}: {DescribeSecured(item)}", itemAnchor);
            if (item.IsClaimed)
                return PromptDraw.At($"{item.Grabbable.Role}: 다른 플레이어가 잡는 중", itemAnchor);
            // 거리 숫자를 뺀다. 사거리 안에서만 뜨게 된 뒤로 그 숫자가 알려 줄 것이
            // "이미 잡을 수 있다" 뿐이고, 그건 프롬프트가 떠 있다는 사실이 이미 말한다.
            return PromptDraw.At($"[E] {item.Grabbable.Role} 잡기", itemAnchor);
        }

        /// <summary>
        /// 냉각실 밸브 안내(<c>C-3</c>, §4.3). 사거리 밖이면 null 이다.
        ///
        /// 붙잡고 있는 동안 <b>무엇을 내주고 있는지</b>를 문장에 넣는다. 이 동사의 비용은 시간이
        /// 아니라 사람이고(§3 문법 축 "소비 대상 = 사람"), 화면이 그걸 말하지 않으면 잡은 사람은
        /// 자기가 조종석을 비우고 있다는 사실을 열 막대에서 역산해야 한다.
        /// </summary>
        private PromptDraw BuildValvePrompt()
        {
            // 손잡이 좌표는 정적이라 씬 조회 없이 바로 앵커가 된다 — 이미 손잡이 높이다.
            var handle = LastShiftCoolingValve.Position;
            if (sustainingValve)
                return PromptDraw.At("[T] 유지 중 — 냉각 순환 밸브 (이동·다른 조작 불가)", handle);
            if (!LastShiftCoolingValve.IsWithinReach(transform.position)) return PromptDraw.None;
            var crew = GetComponent<LastShiftCrewOxygen>();
            if (crew != null && crew.IsDead) return PromptDraw.At("냉각 순환 밸브: 조작 불가", handle);
            return PromptDraw.At("[T] 냉각 순환 밸브 유지 (누르고 있는 동안 · 그 자리에 묶인다)", handle);
        }

        /// <summary>
        /// 수리 동사 안내(<c>C-2</c>, §4.2). 판정은 전부
        /// <see cref="LastShiftSandboxController.TryResolveRepairPrompt"/> 에 있고 여기는 문장만 만든다.
        ///
        /// <b>물건이 없을 때 <c>G</c> 만 남기는 것이 이 카드의 <c>C-2</c> 다.</b> 지금까지 셋이
        /// 같은 무게로 나열조차 되지 않았고(수리 프롬프트가 아예 없었다), 물건이 정위치에 없으면
        /// <c>C</c>·<c>V</c> 는 조용히 실패했다. 실패가 조용하면 플레이어는 그 자리에서
        /// <c>G</c> 라는 답이 있다는 것을 배울 방법이 없다.
        /// </summary>
        private PromptDraw BuildRepairPrompt()
        {
            var sandbox = Sandbox;
            if (sandbox == null) return PromptDraw.None;
            // 앵커는 <b>수리 대상 부품의 제자리</b>다. 손상 지점이 곧 그 부품이 들어가야 할
            // 자리이고, 부품을 들고 있든 바닥에 굴러다니든 동사가 걸린 좌표는 그쪽이다.
            if (!sandbox.TryResolveRepairPrompt(transform.position, out _, out var subjectInPlace, out var subjectNominal))
                return PromptDraw.None;
            var crew = GetComponent<LastShiftCrewOxygen>();
            if (crew != null && crew.IsDead) return PromptDraw.None;

            return PromptDraw.At(
                subjectInPlace
                    ? "[C] 안전 복구 4.0s   [V] 임시 결속 0.8s   [G] 성능 포기"
                    : "[G] 이 구역 포기 — 악화는 멈추고 회복은 없다",
                subjectNominal);
        }

        /// <summary>
        /// 씬의 샌드박스. 지연 조회하는 이유는 <see cref="LastShiftDeckHatch"/> 와 같다 —
        /// EditMode 조립·씬 빌드에서 Awake 순서가 보장되지 않고, 그때 캐시가 null 로 굳으면
        /// 프롬프트가 영영 안 뜬다. 클라이언트에서는 이 컴포넌트가 <c>enabled = false</c> 지만
        /// 오브젝트는 살아 있어 조회되고, 프롬프트가 읽는 값은 전부 스냅샷으로 들어온다.
        /// </summary>
        private LastShiftSandboxController Sandbox =>
            cachedSandbox != null ? cachedSandbox : cachedSandbox = FindFirstObjectByType<LastShiftSandboxController>();

        private LastShiftSandboxController cachedSandbox;

        /// <summary>
        /// 문 앞 안내. 사거리 밖이면 null 이라 아이템 프롬프트가 그대로 나온다.
        /// 사망한 승무원에게는 "조작 불가" 를 보여 준다 — 눌러도 아무 일이 없는 것보다
        /// 왜 안 되는지가 보여야 한다.
        /// </summary>
        private PromptDraw BuildDoorPrompt()
        {
            var door = LastShiftZoneDoor.FindOperable(transform.position);
            var crew = GetComponent<LastShiftCrewOxygen>();
            if (door == null)
            {
                var hatch = LastShiftDeckHatch.FindOperable(transform.position);
                if (hatch == null) return BuildAirlockPrompt(crew);
                // 승강구는 갑판에 뚫린 구멍이라 윗면이 곧 바닥면이다. 발치에 뜨지 않도록
                // 눈높이만큼 올려서 구멍 위 허공에 띄운다.
                var mouth = AnchorAbove(hatch.Mouth);
                if (crew != null && crew.IsDead)
                    return PromptDraw.At($"{hatch.ShaftLabel} 승강구: 조작 불가", mouth);
                // 여는 쪽에 경고를 붙인다. 여기서 열리는 것은 압력이 아니라 갑판의 구멍이고,
                // 저중력에서 뜬 물건이 그리로 빠지는 것이 이 동사의 유일한 되돌리기 비용이다.
                return PromptDraw.At(
                    hatch.IsOpen
                        ? $"[Q] {hatch.ShaftLabel} 승강구 해치 닫기"
                        : $"[Q] {hatch.ShaftLabel} 승강구 해치 열기 (갑판에 구멍)",
                    mouth);
            }
            var doorTop = AnchorTopOf(door, door.transform.position);
            if (crew != null && crew.IsDead)
                return PromptDraw.At($"{door.BoundaryLabel} 문: 조작 불가", doorTop);
            return PromptDraw.At(
                door.IsOpen
                    ? $"[Q] {door.BoundaryLabel} 문 닫기 (압력 차단)"
                    : $"[Q] {door.BoundaryLabel} 문 열기",
                doorTop);
        }

        /// <summary>
        /// 에어록 앞 안내. 사거리 밖이면 <c>null</c> 이라 아이템 프롬프트가 그대로 나온다.
        ///
        /// <b>막힌 사유를 문장으로 적는 것이 여기 있는 이유의 절반이다.</b> 조항 <c>O-4</c>
        /// (구간 중 봉인)와 인터록(갑판 구멍과 동시 개방 금지)은 둘 다 눌러도 아무 일이
        /// 안 일어나는 형태로 나타나는데, 배 안 어디에도 그 규칙을 적어 둔 자리가 없다.
        /// </summary>
        private PromptDraw BuildAirlockPrompt(LastShiftCrewOxygen crew)
        {
            // 에어록은 정적 좌표만 있고 컴포넌트를 안 거친다. 바닥 한가운데라 눈높이로 올린다.
            var airlock = AnchorAbove(LastShiftAirlock.ReturnPoint);
            if (LastShiftAirlock.IsCycling && LastShiftAirlock.IsWithinReach(transform.position))
                return PromptDraw.At($"에어록 사이클 {LastShiftAirlock.CycleProgress:P0}", airlock);

            var action = LastShiftAirlock.NextAction(transform.position, LiftAwayFromDeck);
            if (action == LastShiftAirlockAction.None) return PromptDraw.None;
            if (crew != null && crew.IsDead) return PromptDraw.At("에어록: 조작 불가", airlock);

            return PromptDraw.At(action switch
            {
                LastShiftAirlockAction.OpenInner => "[Q] 에어록 안쪽 해치 열기",
                LastShiftAirlockAction.CloseInner => "[Q] 에어록 안쪽 해치 닫기",
                LastShiftAirlockAction.Depressurize => "[Q] 감압 — 바깥 해치를 연다 (선외는 진공)",
                LastShiftAirlockAction.Repressurize => "[Q] 재가압 — 배로 돌아간다",
                LastShiftAirlockAction.BlockedBySegment => "에어록: 구간 중에는 봉인 (기항에서만 열린다)",
                _ => "에어록: 갑판 승강구 해치를 먼저 닫으세요"
            }, airlock);
        }

        /// <summary>
        /// 잔해 앞 안내. 선외에서만 뜨고, <b>남은 산소를 같이 적는다</b> — 밖에서 읽을 수 있는
        /// 숫자가 이것 하나이고, 조항 <c>O-7</c> 의 대가(수확 상실)가 그 숫자에 걸려 있다.
        /// </summary>
        private PromptDraw BuildSalvagePrompt()
        {
            if (!LastShiftSalvage.IsWithinReach(transform.position)) return PromptDraw.None;

            var field = AnchorAbove(LastShiftSalvage.FieldCenter);
            var carried = $"들고 있음 {LastShiftSalvage.Carried}/{LastShiftSalvage.CarryCapacity}";
            if (LastShiftSalvage.Remaining <= 0)
                return PromptDraw.At($"{LastShiftSalvage.FieldLabel}: 다 뜯었다   {carried}", field);
            if (LastShiftSalvage.Carried >= LastShiftSalvage.CarryCapacity)
                return PromptDraw.At($"{LastShiftSalvage.FieldLabel}: 손이 찼다 — 에어록으로   {carried}", field);
            if (LastShiftSalvage.HarvestCooldown > 0f)
                return PromptDraw.At($"{LastShiftSalvage.FieldLabel} 뜯는 중 {LastShiftSalvage.HarvestCooldown:F1}s   {carried}", field);

            return PromptDraw.At($"[E] {LastShiftSalvage.FieldLabel} 뜯기 (남은 {LastShiftSalvage.Remaining})   {carried}", field);
        }

        /// <summary>
        /// 고정 사유를 구분한다. 승무원이 F 로 고정한 것과 프리셋 초기 배치로 고정된 것은
        /// 플레이어 입장에서 대응이 다르므로 같은 문구로 뭉개면 안 된다.
        /// </summary>
        private static string DescribeSecured(LastShiftNetworkGrabbable item)
        {
            return item.IsSecuredByCrew
                ? "고정 완료 (승무원이 제자리에 고정함)"
                : "초기 고정 (프리셋 정상 상태 / 이 프리셋의 느슨한 부품을 찾으세요)";
        }

        private void DropHeldItem()
        {
            if (heldItem == null) return;
            heldItem.Drop(targetCamera != null ? targetCamera.transform.forward * 1.4f : Vector3.zero);
            heldItem = null;
        }

        /// <summary>상시 조작 안내 줄의 높이와 화면 가장자리 여백. 프롬프트는 이 줄 위에 앉는다.</summary>
        public const float InputBarHeight = 36f;
        public const float InputBarMargin = 8f;

        /// <summary>프롬프트 상자 높이, 화면 가장자리 여백, 그리고 앵커와 상자 사이 간격.</summary>
        public const float PromptBoxHeight = 40f;
        public const float PromptBoxGap = 10f;

        /// <summary>대상 윗면과 상자 아랫변 사이의 화면 간격. 물건에 닿지 않을 만큼만 띄운다.</summary>
        public const float PromptAnchorGap = 14f;

        /// <summary>조준점 십자의 크기(가로 x 세로).</summary>
        public const float CrosshairWidth = 24f;
        public const float CrosshairHeight = 36f;

        /// <summary>
        /// 프롬프트 상자의 자리 — <b>대상 바로 위</b>.
        ///
        /// <paramref name="anchor"/> 는 대상 윗면을 화면에 투영한 점(GUI 좌표, y 아래로 증가)이고
        /// 상자는 그 점 위에 <see cref="PromptAnchorGap"/> 만큼 띄워 가로 중앙을 맞춰 앉는다.
        ///
        /// 여기까지 온 과정이 두 단계였다. 처음에는 화면 정중앙 고정이라 조준한 대상을 안내가
        /// 덮었고, 다음에는 하단 고정으로 내렸더니 <b>시선이 가 있는 곳에서 너무 멀어 안 읽혔다</b>.
        /// 고정 좌표로는 둘 중 하나를 고를 수밖에 없다 — 대상을 덮거나, 대상에서 멀거나.
        /// 대상을 따라가면 둘 다 아니다: 시선이 이미 그 물건에 있고 안내는 그 바로 위에 있다.
        ///
        /// 화면 밖으로는 안 나간다. 대상이 화면 가장자리에 걸리거나 위쪽 끝에 있으면 상자가
        /// 잘려 읽을 수 없으므로, 가장자리 여백 안쪽으로 밀어 넣는다 — 살짝 어긋나게 붙는 것이
        /// 반쯤 잘린 것보다 낫다. 아래쪽 한계는 상시 조작 안내 줄이다(겹치면 둘 다 못 읽는다).
        /// </summary>
        public static Rect ResolvePromptRect(float screenWidth, float screenHeight, float textWidth, Vector2 anchor)
        {
            var maxWidth = Mathf.Max(0f, screenWidth - InputBarMargin * 2f);
            var boxWidth = Mathf.Min(maxWidth, textWidth + 28f);

            var boxX = anchor.x - boxWidth * 0.5f;
            var maxX = Mathf.Max(InputBarMargin, screenWidth - InputBarMargin - boxWidth);
            boxX = Mathf.Clamp(boxX, InputBarMargin, maxX);

            var boxY = anchor.y - PromptAnchorGap - PromptBoxHeight;
            var maxY = screenHeight - InputBarMargin - InputBarHeight - PromptBoxGap - PromptBoxHeight;
            boxY = Mathf.Clamp(boxY, InputBarMargin, Mathf.Max(InputBarMargin, maxY));

            return new Rect(boxX, boxY, boxWidth, PromptBoxHeight);
        }

        /// <summary>
        /// 가리킬 대상이 없는 문장(유령 상태·서버 거부)이나 대상이 카메라 뒤에 있을 때의 앵커.
        /// 조준점 <b>아래</b>로 잡아, 같은 규칙(앵커 위에 상자)을 태우면 십자 바로 밑에 앉는다.
        /// </summary>
        public static Vector2 ResolveFloatingAnchor(float screenWidth, float screenHeight)
        {
            return new Vector2(
                screenWidth * 0.5f,
                screenHeight * 0.5f + CrosshairHeight * 0.5f + PromptAnchorGap + PromptBoxHeight);
        }

        /// <summary>
        /// 조준점 자리. <b>상시 표시다</b> — 어디를 겨누고 있는지는 상호작용이 성립하든 아니든
        /// 알아야 하는 정보이고, 조준점이 사라지는 화면은 조준 자체를 감각으로 하게 만든다.
        /// 프롬프트와 같이 뜨고 지던 시절에는 "십자가 보인다 = 뭔가 된다" 라는 신호를 얻는 대신
        /// 겨냥이라는 기본 동작을 잃었다.
        /// </summary>
        public static Rect ResolveCrosshairRect(float screenWidth, float screenHeight)
        {
            return new Rect(
                screenWidth * 0.5f - CrosshairWidth * 0.5f,
                screenHeight * 0.5f - CrosshairHeight * 0.5f,
                CrosshairWidth,
                CrosshairHeight);
        }

        /// <summary>
        /// 월드 좌표를 GUI 좌표(y 아래로 증가)로 옮긴다. 카메라 뒤(<c>z &lt;= 0</c>)면 실패한다 —
        /// <c>WorldToScreenPoint</c> 는 뒤쪽 점도 좌표를 돌려주는데 그 값은 화면 반대편을 가리켜서,
        /// 그대로 쓰면 등 뒤의 문 안내가 눈앞에 붙는다.
        /// </summary>
        private bool TryProjectAnchor(Vector3 world, out Vector2 guiPoint)
        {
            guiPoint = default;
            if (targetCamera == null) return false;
            var screenPoint = targetCamera.WorldToScreenPoint(world);
            if (screenPoint.z <= 0f) return false;
            guiPoint = new Vector2(screenPoint.x, Screen.height - screenPoint.y);
            return true;
        }

        /// <summary>
        /// 조준점은 <b>상시</b>, 프롬프트는 <b>대상 위에</b> 그린다.
        ///
        /// 자리 계산은 <see cref="ResolvePromptRect"/>·<see cref="ResolveCrosshairRect"/>·
        /// <see cref="ResolveFloatingAnchor"/> 에 있고 여기서는 배치와 값만 꽂는다. 그 셋은
        /// 단위를 안 가리고 비율만 쓰므로, <c>OnGUI</c> 시절 화면 픽셀로 쓰던 것을 그대로
        /// <b>캔버스 단위로</b> 태울 수 있었다 — UGUI 전환에서 손대지 않은 부분이 이것이다.
        ///
        /// 아래 입력 안내 줄은 그대로 상시다 — 화면 가장자리이고 시야를 덮지 않으며,
        /// 조작 목록은 "지금 여기" 가 아니라 배우는 정보라 조건부로 만들 대상이 아니다.
        /// </summary>
        private void DrawHud()
        {
            var layer = LastShiftUiLayer.Instance;
            if (layer == null) return;

            // 접속이 끊겨 로비로 돌아간 프레임에는 승무원이 아직 살아 있을 수 있다.
            // 그때 조준점·상호작용 프롬프트가 로비 위에 남으면 판이 도는 것으로 보인다.
            // 임대를 안 갱신하면 다음 프레임에 저절로 꺼진다 — 지우는 코드가 따로 없다.
            if (LastShiftRoomLobby.IsBlockingGameplay) return;

            // 암전 중에는 조준점도 프롬프트도 없다. 시점이 잠긴 화면에 조준선만 검정 위에
            // 떠 있으면 "조작은 되는데 화면이 안 나온다" 로 읽힌다.
            if (!LastShiftWakeSequence.CanLook) return;

            // <b>자리 계산은 캔버스 단위로 한다.</b> 아트 키트가 1920×1080 을 자로 잡았고
            // 그래야 4K 에서 프롬프트가 손톱만 해지지 않는다. 계산 함수 자체는 단위를 안
            // 가리므로(비율만 쓴다) IMGUI 시절 그대로 태운다.
            var screen = LastShiftUiLayer.ScreenSize;
            var canvas = LastShiftUiTheme.CanvasSize(screen);

            // 지도가 떠 있으면 <b>조준점도 상호작용 프롬프트도 안 그린다</b>. 보기 전용
            // 화면인데 조준선이 남아 있으면 "지금 조작이 되는가" 가 흐려지고, 프롬프트는
            // 지도 뒤의 대상을 가리켜서 두 화면이 겹쳐 읽힌다.
            if (LastShiftMapView.IsOpen)
            {
                DrawMap(layer, screen, canvas);
                if (promptView != null && promptView.gameObject.activeSelf)
                    promptView.gameObject.SetActive(false);
                return;
            }

            // 조준점은 프롬프트와 무관하게 항상 그린다.
            layer.LabelCanvas("crosshair", ResolveCrosshairRect(canvas.x, canvas.y),
                "+", CrosshairFontSize, TextAnchor.MiddleCenter, Color.white);

            // 한 프레임에 한 번만 만든다. 이 함수는 씬 조회를 타므로 문장과 앵커를 각자
            // 부르면 같은 프레임에 같은 탐색이 두 번 돈다.
            var prompt = BuildPrompt();
            if (prompt.Exists && prompt.Text.Length > 0)
            {
                var view = EnsurePromptView(layer);

                // 앵커가 없거나 대상이 카메라 뒤면 조준점 아래 고정 자리로 떨어진다.
                Vector2 anchor;
                if (!prompt.HasAnchor || !TryProjectAnchor(prompt.Anchor, out anchor))
                    anchor = ResolveFloatingAnchor(canvas.x, canvas.y);
                else
                    anchor = LastShiftUiTheme.ScreenPointToCanvas(anchor, screen);

                var box = ResolvePromptRect(canvas.x, canvas.y, view.MeasureBody(prompt.Text), anchor);
                view.gameObject.SetActive(true);
                view.Apply(LastShiftUiTheme.FlipY(box), prompt.Text);
            }
            else if (promptView != null && promptView.gameObject.activeSelf)
            {
                promptView.gameObject.SetActive(false);
            }

            var barRect = new Rect(InputBarMargin, canvas.y - InputBarMargin - InputBarHeight,
                canvas.x - InputBarMargin * 2f, InputBarHeight);
            layer.PanelCanvas("inputBar", barRect, 0.72f);
            layer.LabelCanvas("inputLabel", barRect, InputLabel,
                InputLabelFontSize, TextAnchor.MiddleCenter, identityColor);
        }

        /// <summary>테두리 네 조각을 담는 자리. 프레임마다 새 배열을 안 만든다.</summary>
        private static readonly Rect[] MapOutlineScratch = new Rect[4];

        /// <summary>
        /// 지도(<c>M</c>) 한 장. <b>배 배치 + 지금 누가 어디 있는가</b>가 전부이고 조작은 없다.
        ///
        /// <b>투영은 <see cref="LastShiftHullSchematic"/> 것을 그대로 쓴다</b> — 배치 화면과 같은
        /// 자라, 청사진에서 본 좌표와 지도에서 본 좌표가 어긋나지 않는다.
        /// </summary>
        private void DrawMap(LastShiftUiLayer layer, Vector2 screen, Vector2 canvas)
        {
            var plan = LastShiftMapView.Schematic(screen);

            Tint(layer.Panel("map:backdrop", new Rect(0f, 0f, screen.x, screen.y)),
                LastShiftUiTheme.PanelNavy, LastShiftMapView.BackdropAlpha);

            // 방은 <b>테두리만</b> 그린다. 속을 칠하면 그 위의 표식이 배경에 묻힌다.
            //
            // <b>이름은 테두리와 같은 회에 붙인다</b>(2026-08-13 플레이테스트 — "어느 방이
            // 어딘지 모름"). 테두리만 있는 지도는 배 모양을 알려 주지만 어느 사각형이 무엇인지는
            // 말하지 않아서, 광장에 서서 문 다섯을 하나씩 열어 보는 것 말고는 배를 배울 길이
            // 없었다. 이름이 뜨는 자리를 한 화면으로 모은 것이 그 카드의 결론이고 여기가 그 정본이다.
            var index = 0;
            foreach (var footprint in LastShiftPlazaLayout.Footprints)
            {
                var room = plan.ToScreenRect(
                    footprint.MinX, footprint.MaxX, footprint.MinZ, footprint.MaxZ);
                DrawMapOutline(layer, "map:room" + index, room, LastShiftUiTheme.BodyText, 0.55f);
                DrawMapRoomName(layer, index, room, footprint.Space);
                index++;
            }

            // 코어는 지나갈 수 없는 자리라 다른 색이다 — 광장이 통짜 방으로 보이면
            // 지도를 보고 정한 동선이 실제로는 막힌다.
            var core = plan.ToScreenRect(
                -LastShiftPlazaLayout.CoreHalfExtent, LastShiftPlazaLayout.CoreHalfExtent,
                -LastShiftPlazaLayout.CoreHalfExtent, LastShiftPlazaLayout.CoreHalfExtent);
            DrawMapOutline(layer, "map:core", core, LastShiftUiTheme.Unstable, 0.75f);

            // 코어에는 이름이 반드시 붙는다. 튜토리얼이 사람을 보내는 곳이 여기인데(선외로 나가는
            // 유일한 길) 이름이 없으면 광장 한복판의 못 지나가는 기둥으로만 읽힌다.
            layer.Label("map:shaftName", LastShiftMapView.ShaftNameRect(core),
                LastShiftRoomLabels.ShaftName, LastShiftMapView.RoomNameFontSize,
                LastShiftUiTheme.Unstable, TextAnchor.MiddleCenter);

            // 문은 벽에 난 구멍이라 <b>선 하나</b>로 눕힌다. 방 테두리 위에 겹쳐 그려서
            // 어느 변에 붙었는지가 보인다.
            index = 0;
            foreach (var door in LastShiftPlazaLayout.Doors)
            {
                const float lip = 0.25f;
                var rect = door.PlaneIsX
                    ? plan.ToScreenRect(door.Plane - lip, door.Plane + lip, door.MinSpan, door.MaxSpan)
                    : plan.ToScreenRect(door.MinSpan, door.MaxSpan, door.Plane - lip, door.Plane + lip);
                Tint(layer.Panel("map:door" + index, rect), LastShiftUiTheme.Nominal, 0.9f);
                index++;
            }

            DrawMapCrew(layer, plan);

            var hint = new Rect(0f, canvas.y - InputBarMargin - InputBarHeight, canvas.x, InputBarHeight);
            layer.LabelCanvas("map:hint", hint, "지도 — M 으로 닫기",
                InputLabelFontSize, TextAnchor.MiddleCenter, LastShiftUiTheme.BodyText);
        }

        /// <summary>
        /// 사람 표식. <b>씬 조회가 여기 있다</b> — 지도가 떠 있는 동안에만 돌고, 최대 넷이라
        /// 프레임마다 한 번을 받아들인다. 상시 HUD 경로에는 이 조회가 없다.
        /// </summary>
        private void DrawMapCrew(LastShiftUiLayer layer, in LastShiftHullSchematic plan)
        {
            var crew = FindObjectsByType<LastShiftPlayerController>(FindObjectsSortMode.None);
            var index = 0;
            foreach (var member in crew)
            {
                if (member == null) continue;
                var mine = member == this;
                var point = plan.ToScreen(member.transform.position);
                var size = mine ? LastShiftMapView.SelfMarkerSize : LastShiftMapView.CrewMarkerSize;
                var color = mine ? LastShiftUiTheme.Nominal : LastShiftUiTheme.Ivory;

                Tint(layer.Panel("map:crew" + index, LastShiftMapView.MarkerRect(point, size)), color, 1f);

                // 보는 쪽에 코를 하나 더 찍는다. 내 것만 그린다 — 남의 시선까지 그리면
                // 표식 넷이 겹칠 때 어느 코가 누구 것인지 안 갈린다.
                if (mine)
                    Tint(layer.Panel("map:nose",
                            LastShiftMapView.MarkerRect(
                                LastShiftMapView.NosePoint(plan, member.transform.position,
                                    member.transform.forward),
                                LastShiftMapView.CrewMarkerSize * 0.5f)),
                        color, 0.9f);
                index++;
            }
        }

        /// <summary>
        /// 방 하나의 이름표 — 이름 한 줄, 자리가 되면 부제 한 줄. 문구는
        /// <see cref="LastShiftRoomLabels"/> 하나에서 나온다(HUD 구역 칸과 같은 이름을 쓴다).
        ///
        /// <b>부제는 방이 좁으면 생략한다.</b> 두 줄을 억지로 넣으면 아래 줄이 이웃 방 위로
        /// 넘어가서 그 부제가 어느 방 것인지 모르게 된다 — 판정은
        /// <see cref="LastShiftMapView.FitsPurpose"/> 가 좌표로 내린다.
        /// </summary>
        private static void DrawMapRoomName(LastShiftUiLayer layer, int index, Rect room,
            LastShiftPlazaSpace space)
        {
            layer.Label("map:name" + index, LastShiftMapView.RoomNameRect(room),
                LastShiftRoomLabels.NameOf(space), LastShiftMapView.RoomNameFontSize,
                LastShiftUiTheme.Ivory, TextAnchor.MiddleCenter);

            // 조각을 안 쓰는 프레임에도 이름은 남으므로, 좁은 방에서는 부제 자리를 빈 문자열로
            // 덮는다. 안 덮으면 화면 크기가 바뀌어 방이 줄었을 때 지난 프레임의 부제가 남는다.
            layer.Label("map:purpose" + index, LastShiftMapView.RoomPurposeRect(room),
                LastShiftMapView.FitsPurpose(room) ? LastShiftRoomLabels.PurposeOf(space) : string.Empty,
                LastShiftMapView.RoomPurposeFontSize,
                new Color(LastShiftUiTheme.BodyText.r, LastShiftUiTheme.BodyText.g,
                    LastShiftUiTheme.BodyText.b, 0.7f),
                TextAnchor.MiddleCenter);
        }

        private static void DrawMapOutline(LastShiftUiLayer layer, string id, Rect rect,
            Color color, float alpha)
        {
            LastShiftMapView.OutlineBands(rect, LastShiftMapView.RoomOutline, MapOutlineScratch);
            for (var side = 0; side < MapOutlineScratch.Length; side++)
                Tint(layer.Panel(id + ":" + side, MapOutlineScratch[side]), color, alpha);
        }

        /// <summary>임대해 온 조각에 색을 입힌다. <see cref="LastShiftUiLayer.Panel"/> 은 진하기만 받는다.</summary>
        private static void Tint(UnityEngine.UI.Image image, Color color, float alpha)
        {
            if (image != null) image.color = new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>조준점 글자 크기. 십자 사각형 안에 들어가는 최대치다.</summary>
        private const int CrosshairFontSize = 22;

        /// <summary>조작 안내 줄 글자 크기.</summary>
        private const int InputLabelFontSize = 16;

        private LastShiftPromptView promptView;

        private LastShiftPromptView EnsurePromptView(LastShiftUiLayer layer)
        {
            if (promptView == null)
                promptView = LastShiftPromptView.Create(layer.OverlayRoot, $"Prompt:{GetInstanceID()}");
            return promptView;
        }
    }
}
