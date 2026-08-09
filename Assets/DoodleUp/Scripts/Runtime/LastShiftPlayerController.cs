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

        public const float GrabDistance = 2.2f;
        public const float AwarenessDistance = 8f;
        public const float GrabAimRadius = 0.22f;

        /// <summary>
        /// 이미 고정된 부품에 "왜 안 잡히는지" 를 알려줄 거리. <see cref="AwarenessDistance"/>
        /// 보다 훨씬 짧은 이유는 그 안내가 <b>잡으려 다가온 사람</b>에게만 쓸모 있기 때문이다.
        /// 잡을 수 있는 부품의 접근 안내(8m)와 달리, 고정 부품은 다가가도 결과가 안 바뀌므로
        /// 멀리서부터 띄우면 화면 중앙이 계속 차 있는 상태로 돌아간다.
        /// </summary>
        public const float SecuredNoticeDistance = GrabDistance + 1f;

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
        private GUIStyle identityStyle;
        private GUIStyle promptStyle;
        private LastShiftNetworkGrabbable[] awarenessItems;
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

        public string InputLabel => IsGhost
            ? "WASD 이동 / Space 상승 / Ctrl 하강 / Mouse 시선 — 유령: 잡기·수리·문 조작 불가"
            : "WASD 이동 / Mouse 조준 / E 잡기·놓기 / F 고정 / C·V·G 수리 / Q 문 / T 밸브 유지 / 1·2·3 프리셋 / R 리셋";
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

        /// <summary>지금 화면 중앙에 그릴 것이 있는가. 조준점도 이 값에만 따른다.</summary>
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
            var meteor = ConsumePress(keyboard.mKey.isPressed, ref meteorPressed);
            // 냉각실 밸브 유지(T). <b>ConsumePress 를 안 쓴다</b> — 나머지 전부가 순간 동사라
            // "눌린 프레임" 을 세지만, 이 동사는 "눌려 있는 동안" 자체가 효과다(§4.3 시간 형태).
            // §4.3 표는 R 을 적었으나 R 은 이미 프리셋 리셋이라 T 로 옮겼다.
            UpdateValveSustain(keyboard.tKey.isPressed);

            ApplyLook(look, deltaTime);
            // 붙잡고 있는 동안은 이동이 없다(§4.3 제약). 이 한 줄이 이 동사가 채우려던 문법 축
            // "소비 대상 = 사람" 그 자체다 — 효과만 있고 자리에 안 묶이면 걸어 두는 동사가 되고,
            // 그건 조종석 hold 가 이미 하고 있다.
            ApplyMovement(sustainingValve ? Vector2.zero : move, jump && !sustainingValve, deltaTime);
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
            // 운석(M). 프리셋·리셋과 같은 검증 도구 계열이라 유령 차단 밖에 둔다.
            // 이 줄이 없어서 host 로 뜬 씬에서 M 이 아무 데서도 안 먹었다 — 서버 RPC 는
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
            return LastShiftAirlock.TryOperate(transform.position, AnyDeckHatchOpen);
        }

        /// <summary>
        /// 갑판 승강구 해치가 하나라도 열려 있는가 — 에어록 인터록의 셋째 조건이 읽는 값이다.
        /// sandbox 가 정본이고(<see cref="LastShiftSandboxController.IsHatchOpen"/>), 없으면
        /// 닫힘으로 본다: 최소 조립에서 안전한 쪽은 "구멍이 없다" 이고, 그 기본값이
        /// <see cref="LastShiftDeckHatch.IsOpen"/> 과 같아야 두 판정이 안 갈린다.
        /// </summary>
        private bool AnyDeckHatchOpen =>
            Sandbox != null &&
            (Sandbox.IsHatchOpen(LastShiftBypassDuct.ForeShaft) ||
             Sandbox.IsHatchOpen(LastShiftBypassDuct.AftShaft));

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

        private LastShiftNetworkGrabbable FindAwarenessItem(out float distance)
        {
            distance = float.PositiveInfinity;
            if (awarenessItems == null || awarenessItems.Length == 0 || awarenessItems[0] == null)
                awarenessItems = FindObjectsByType<LastShiftNetworkGrabbable>(FindObjectsSortMode.None);

            LastShiftNetworkGrabbable bestItem = null;
            var aim = AimDirection.normalized;
            foreach (var candidate in awarenessItems)
            {
                if (candidate == null || candidate.Grabbable == null) continue;
                var collider = candidate.GetComponentInChildren<Collider>();
                // collider 가 없거나 꺼진 아이템은 raycast 로도 절대 잡히지 않는다.
                // 그런 대상을 안내하면 "저기 있다"고 표시하면서 잡을 수 없는 상태가 된다.
                if (collider == null || !collider.enabled) continue;
                var target = collider.bounds.center;
                var offset = target - AimOrigin;
                var candidateDistance = offset.magnitude;
                if (candidateDistance > AwarenessDistance || candidateDistance >= distance) continue;
                var direction = candidateDistance > 0.001f ? offset / candidateDistance : aim;
                if (Vector3.Dot(aim, direction) < 0.7f) continue;
                bestItem = candidate;
                distance = candidateDistance;
            }
            return bestItem;
        }

        private string BuildInteractionPrompt()
        {
            // 유령은 어느 프롬프트도 받지 않는다. 잡을 수 있다고 표시해 놓고 눌러도 안 되는
            // 것보다, 왜 안 되는지를 한 줄로 못박는 편이 낫다(문 프롬프트가 사망 승무원에게
            // "조작 불가" 를 보여 주던 것과 같은 이유다).
            if (IsGhost) return "유령 — 이동만 가능 (잡기·수리·문 조작 불가)";

            // 밸브가 가장 먼저다. 붙잡고 있는 동안은 다른 동사가 아예 막혀 있으므로(§4.3 제약),
            // 그 상태에서 잡기·문 안내를 띄우면 눌러도 안 되는 것을 알려주는 꼴이다.
            var valvePrompt = BuildValvePrompt();
            if (valvePrompt != null) return valvePrompt;

            // 잔해가 문보다 먼저다. 사거리가 겹칠 일은 없지만(잔해는 원반 밖이다) 선외에서
            // 뜰 수 있는 안내가 이것 하나뿐이라 어느 갈래에도 안 가려져야 한다.
            var salvagePrompt = BuildSalvagePrompt();
            if (salvagePrompt != null) return salvagePrompt;

            // 문 프롬프트가 아이템 프롬프트보다 먼저다. 문 앞에서만 뜨는 안내이고, 그 자리에서
            // 아이템을 조준하고 있을 확률보다 문을 조작하려 할 확률이 높다.
            var doorPrompt = BuildDoorPrompt();
            if (doorPrompt != null) return doorPrompt;

            // 수리 프롬프트는 문 다음이다. 손상 지점은 방 안이고 문은 경계에 있어 사거리가
            // 겹치지 않지만, 겹치는 배치가 생기면 문 쪽을 남긴다 — 문은 그 자리를 떠나는
            // 동사라 잘못 가려지면 승무원이 갇힌다.
            var repairPrompt = BuildRepairPrompt();
            if (repairPrompt != null) return repairPrompt;
            // 네트워크가 없는 단독 씬(SP-01)에서도 빈손이면 중앙에 아무것도 그리지 않는다.
            if (networkPlayer == null || !networkPlayer.IsSpawned)
                return heldItem != null ? "[E] 놓기" : null;
            if (serverRejectionReason != null)
            {
                if (Time.unscaledTime <= serverRejectionExpiry)
                    return $"서버 거부: {serverRejectionReason}";
                serverRejectionReason = null;
            }
            if (networkPlayer.HeldItem != null && networkPlayer.HeldItem.Grabbable == null)
                return "[E] 놓기";
            if (networkPlayer.HeldItem != null)
            {
                var distanceToNominal = Vector3.Distance(
                    networkPlayer.HeldItem.transform.position,
                    networkPlayer.HeldItem.Grabbable.NominalPosition);
                return distanceToNominal <= LastShiftSandboxController.SecureDistance
                    ? "[E] 놓기   [F] 제자리에 고정"
                    : $"[E] 놓기   고정 위치까지 {distanceToNominal:F1}m";
            }
            if (!TryGetNetworkTarget(out var item, out var distance))
            {
                var awarenessItem = FindAwarenessItem(out var awarenessDistance);
                // 조준선 앞 8m 안에 부품이 하나도 없으면 잡기에 대해 할 말이 없다. 예전에는
                // 여기서 "대상을 조준하세요" 를 돌려줘 그 문장이 사실상 상시 프롬프트였다.
                if (awarenessItem == null || awarenessItem.Grabbable == null)
                    return null;
                // 고정된 부품은 어느 거리에서도 잡히지 않는다. 그래도 문장을 남기는 것은
                // "왜 안 되는지" 를 그 자리에서 알려주기 위해서이므로, <b>실제로 잡으려 드는
                // 거리에서만</b> 남긴다. 8m 까지 띄우면 고정 소품이 널린 방을 지나는 동안
                // 중앙 상자가 사실상 계속 켜져 있어 이 카드가 지운 상시 UI 가 되돌아온다.
                if (awarenessItem.IsSecured)
                    return awarenessDistance <= SecuredNoticeDistance
                        ? $"{awarenessItem.Grabbable.Role}: {DescribeSecured(awarenessItem)}"
                        : null;
                var approachNeeded = Mathf.Max(0f, awarenessDistance - GrabDistance);
                return approachNeeded > 0.05f
                    ? $"{awarenessItem.Grabbable.Role}: {awarenessDistance:F1}m / 접근 필요 {approachNeeded:F1}m"
                    : $"{awarenessItem.Grabbable.Role}: {awarenessDistance:F1}m / 조준을 물체 중앙으로";
            }
            // 아직 spawn 되지 않은 아이템은 역할을 신뢰할 수 없다. OnGUI 는 매 프레임 돌기 때문에
            // 여기서 예외가 나면 화면이 아니라 로그가 먼저 무너진다.
            if (item.Grabbable == null)
                return $"E 잡기: 대상 확인 중  {distance:F1}m";
            if (item.IsSecured)
                return $"{item.Grabbable.Role}: {DescribeSecured(item)}";
            if (item.IsClaimed)
                return $"{item.Grabbable.Role}: 다른 플레이어가 잡는 중";
            return $"[E] {item.Grabbable.Role} 잡기  {distance:F1}m";
        }

        /// <summary>
        /// 냉각실 밸브 안내(<c>C-3</c>, §4.3). 사거리 밖이면 null 이다.
        ///
        /// 붙잡고 있는 동안 <b>무엇을 내주고 있는지</b>를 문장에 넣는다. 이 동사의 비용은 시간이
        /// 아니라 사람이고(§3 문법 축 "소비 대상 = 사람"), 화면이 그걸 말하지 않으면 잡은 사람은
        /// 자기가 조종석을 비우고 있다는 사실을 열 막대에서 역산해야 한다.
        /// </summary>
        private string BuildValvePrompt()
        {
            if (sustainingValve) return "[T] 유지 중 — 냉각 순환 밸브 (이동·다른 조작 불가)";
            if (!LastShiftCoolingValve.IsWithinReach(transform.position)) return null;
            var crew = GetComponent<LastShiftCrewOxygen>();
            if (crew != null && crew.IsDead) return "냉각 순환 밸브: 조작 불가";
            return "[T] 냉각 순환 밸브 유지 (누르고 있는 동안 · 그 자리에 묶인다)";
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
        private string BuildRepairPrompt()
        {
            var sandbox = Sandbox;
            if (sandbox == null) return null;
            if (!sandbox.TryResolveRepairPrompt(transform.position, out _, out var subjectInPlace)) return null;
            var crew = GetComponent<LastShiftCrewOxygen>();
            if (crew != null && crew.IsDead) return null;

            return subjectInPlace
                ? "[C] 안전 복구 4.0s   [V] 임시 결속 0.8s   [G] 성능 포기"
                : "[G] 이 구역 포기 — 악화는 멈추고 회복은 없다";
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
        private string BuildDoorPrompt()
        {
            var door = LastShiftZoneDoor.FindOperable(transform.position);
            var crew = GetComponent<LastShiftCrewOxygen>();
            if (door == null)
            {
                var hatch = LastShiftDeckHatch.FindOperable(transform.position);
                if (hatch == null) return BuildAirlockPrompt(crew);
                if (crew != null && crew.IsDead) return $"{hatch.ShaftLabel} 승강구: 조작 불가";
                // 여는 쪽에 경고를 붙인다. 여기서 열리는 것은 압력이 아니라 갑판의 구멍이고,
                // 저중력에서 뜬 물건이 그리로 빠지는 것이 이 동사의 유일한 되돌리기 비용이다.
                return hatch.IsOpen
                    ? $"[Q] {hatch.ShaftLabel} 승강구 해치 닫기"
                    : $"[Q] {hatch.ShaftLabel} 승강구 해치 열기 (갑판에 구멍)";
            }
            if (crew != null && crew.IsDead) return $"{door.BoundaryLabel} 문: 조작 불가";
            return door.IsOpen
                ? $"[Q] {door.BoundaryLabel} 문 닫기 (압력 차단)"
                : $"[Q] {door.BoundaryLabel} 문 열기";
        }

        /// <summary>
        /// 에어록 앞 안내. 사거리 밖이면 <c>null</c> 이라 아이템 프롬프트가 그대로 나온다.
        ///
        /// <b>막힌 사유를 문장으로 적는 것이 여기 있는 이유의 절반이다.</b> 조항 <c>O-4</c>
        /// (구간 중 봉인)와 인터록(갑판 구멍과 동시 개방 금지)은 둘 다 눌러도 아무 일이
        /// 안 일어나는 형태로 나타나는데, 배 안 어디에도 그 규칙을 적어 둔 자리가 없다.
        /// </summary>
        private string BuildAirlockPrompt(LastShiftCrewOxygen crew)
        {
            if (LastShiftAirlock.IsCycling && LastShiftAirlock.IsWithinReach(transform.position))
                return $"에어록 사이클 {LastShiftAirlock.CycleProgress:P0}";

            var action = LastShiftAirlock.NextAction(transform.position, AnyDeckHatchOpen);
            if (action == LastShiftAirlockAction.None) return null;
            if (crew != null && crew.IsDead) return "에어록: 조작 불가";

            return action switch
            {
                LastShiftAirlockAction.OpenInner => "[Q] 에어록 안쪽 해치 열기",
                LastShiftAirlockAction.CloseInner => "[Q] 에어록 안쪽 해치 닫기",
                LastShiftAirlockAction.Depressurize => "[Q] 감압 — 바깥 해치를 연다 (선외는 진공)",
                LastShiftAirlockAction.Repressurize => "[Q] 재가압 — 배로 돌아간다",
                LastShiftAirlockAction.BlockedBySegment => "에어록: 구간 중에는 봉인 (기항에서만 열린다)",
                _ => "에어록: 갑판 승강구 해치를 먼저 닫으세요"
            };
        }

        /// <summary>
        /// 잔해 앞 안내. 선외에서만 뜨고, <b>남은 산소를 같이 적는다</b> — 밖에서 읽을 수 있는
        /// 숫자가 이것 하나이고, 조항 <c>O-7</c> 의 대가(수확 상실)가 그 숫자에 걸려 있다.
        /// </summary>
        private string BuildSalvagePrompt()
        {
            if (!LastShiftSalvage.IsWithinReach(transform.position)) return null;

            var carried = $"들고 있음 {LastShiftSalvage.Carried}/{LastShiftSalvage.CarryCapacity}";
            if (LastShiftSalvage.Remaining <= 0)
                return $"{LastShiftSalvage.FieldLabel}: 다 뜯었다   {carried}";
            if (LastShiftSalvage.Carried >= LastShiftSalvage.CarryCapacity)
                return $"{LastShiftSalvage.FieldLabel}: 손이 찼다 — 에어록으로   {carried}";
            if (LastShiftSalvage.HarvestCooldown > 0f)
                return $"{LastShiftSalvage.FieldLabel} 뜯는 중 {LastShiftSalvage.HarvestCooldown:F1}s   {carried}";

            return $"[E] {LastShiftSalvage.FieldLabel} 뜯기 (남은 {LastShiftSalvage.Remaining})   {carried}";
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

        /// <summary>
        /// 화면 중앙은 <b>상호작용이 성립할 때만</b> 그린다 — 조준점도, 상자도, 문장도 같이 뜨고
        /// 같이 사라진다. 조준점만 남기지 않는 이유는, 조준점이 상시면 "떠 있음" 이 다시
        /// 무의미해져 이 카드가 지우려는 상시 UI 가 크기만 줄여 그대로 남기 때문이다.
        /// 조준이 필요한 정밀 상황(<c>조준을 물체 중앙으로</c>)에서는 이미 프롬프트가 떠 있어
        /// 조준점도 함께 나와 있다 — 조준점이 필요한 순간과 뜨는 순간이 정확히 같다.
        ///
        /// 상자 폭도 <c>460</c> 고정에서 문장 폭으로 바꾼다. <c>[E] 놓기</c> 같은 짧은 줄에
        /// 화면 절반짜리 검은 띠가 깔리면, 프롬프트가 아니라 띠가 먼저 보인다.
        ///
        /// 아래 입력 안내 줄은 그대로 상시다 — 화면 가장자리이고 시야를 덮지 않으며,
        /// 조작 목록은 "지금 여기" 가 아니라 배우는 정보라 조건부로 만들 대상이 아니다.
        /// </summary>
        private void OnGUI()
        {
            identityStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            promptStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            identityStyle.normal.textColor = identityColor;
            promptStyle.normal.textColor = Color.white;

            // 한 이벤트 안에서 한 번만 만든다. 이 속성은 씬 조회를 타므로 조준점·상자·문장이
            // 각자 부르면 같은 프레임에 같은 탐색이 세 번 돈다.
            var prompt = BuildInteractionPrompt();
            if (!string.IsNullOrEmpty(prompt))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 12f, Screen.height * 0.5f - 18f, 24f, 36f), "+", promptStyle);

                var textWidth = promptStyle.CalcSize(new GUIContent(prompt)).x;
                var boxWidth = Mathf.Min(Screen.width - 48f, textWidth + 32f);
                var boxX = (Screen.width - boxWidth) * 0.5f;
                GUI.Box(new Rect(boxX, Screen.height * 0.5f + 24f, boxWidth, 34f), GUIContent.none);
                GUI.Label(new Rect(boxX + 6f, Screen.height * 0.5f + 26f, boxWidth - 12f, 30f), prompt, promptStyle);
            }

            GUI.Box(new Rect(8f, Screen.height - 36f, Screen.width - 16f, 28f), GUIContent.none);
            GUI.Label(new Rect(12f, Screen.height - 34f, Screen.width - 24f, 24f), InputLabel, identityStyle);
        }
    }
}
