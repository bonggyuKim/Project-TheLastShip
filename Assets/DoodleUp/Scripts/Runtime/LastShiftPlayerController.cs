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
        private bool presetOnePressed;
        private bool presetTwoPressed;
        private bool presetThreePressed;
        private bool resetPressed;
        private bool managesCursor = true;
        private string serverRejectionReason;
        private float serverRejectionExpiry;

        public LastShiftGrabbable HeldItem => heldItem;
        public LastShiftPlayerSlot PlayerSlot => playerSlot;
        public Camera TargetCamera => targetCamera;
        public Transform HoldSocket => holdSocket;
        public bool UsesMouseLook => true;
        public string InputLabel => "WASD 이동 / Mouse 조준 / E 잡기·놓기 / F 고정 / Q 문 / 1·2·3 프리셋 / R 리셋";
        public string InteractionPrompt => BuildInteractionPrompt();
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

            ApplyLook(look, deltaTime);
            ApplyMovement(move, jump, deltaTime);
            if (grab) ToggleGrab();
            if (secure && networkPlayer != null) networkPlayer.RequestSecureHeldItem();
            if (door && (networkPlayer == null || !networkPlayer.IsSpawned)) TryOperateNearestDoor();
            if (networkPlayer == null || !networkPlayer.IsSpawned) return;
            if (door) networkPlayer.RequestDoorToggle();
            if (safeRestore) networkPlayer.RequestRepair(LastShiftRepairMode.SafeRestore);
            else if (quickBypass) networkPlayer.RequestRepair(LastShiftRepairMode.QuickBypass);
            else if (sacrifice) networkPlayer.RequestRepair(LastShiftRepairMode.PerformanceSacrifice);
            if (presetOne) networkPlayer.RequestPresetReset(LastShiftPreset.HighHeatHighThrust);
            else if (presetTwo) networkPlayer.RequestPresetReset(LastShiftPreset.PowerOverloadLooseBattery);
            else if (presetThree) networkPlayer.RequestPresetReset(LastShiftPreset.BadAttitudeHighOxygen);
            else if (reset) networkPlayer.RequestCurrentPresetReset();
        }

        public bool TryGrabForProbe(LastShiftGrabbable item)
        {
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
            var door = LastShiftZoneDoor.FindOperable(transform.position);
            return door != null && door.TryOperate(this);
        }

        public void ResetPlayer(Vector3 position)
        {
            ResetPlayer(position, Quaternion.identity);
        }

        public void ResetPlayer(Vector3 position, Quaternion rotation)
        {
            DropHeldItem();
            characterController.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            characterController.enabled = true;
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
        /// 지금 적용되는 이동 속도. 부피가 큰 부품을 든 동안에만 <see cref="CarrySpeed"/> 다.
        /// 솔로와 네트워크가 각자 소지품을 다른 곳에 들고 있으므로 둘 다 본다 — 한쪽만 보면
        /// 호스트에서만 느려지거나 클라이언트에서만 느려져 같은 배에서 두 속도가 생긴다.
        /// </summary>
        public float CurrentMoveSpeed => IsCarryingBulkyItem ? CarrySpeed : MoveSpeed;

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
            // 문 프롬프트가 아이템 프롬프트보다 먼저다. 문 앞에서만 뜨는 안내이고, 그 자리에서
            // 아이템을 조준하고 있을 확률보다 문을 조작하려 할 확률이 높다.
            var doorPrompt = BuildDoorPrompt();
            if (doorPrompt != null) return doorPrompt;
            if (networkPlayer == null || !networkPlayer.IsSpawned)
                return heldItem != null ? "[E] 놓기" : "+";
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
                if (awarenessItem == null || awarenessItem.Grabbable == null)
                    return $"+   E 잡기: 대상을 조준하세요 (사거리 {GrabDistance:F1}m)";
                if (awarenessItem.IsSecured)
                    return $"{awarenessItem.Grabbable.Role}: {DescribeSecured(awarenessItem)}";
                var approachNeeded = Mathf.Max(0f, awarenessDistance - GrabDistance);
                return approachNeeded > 0.05f
                    ? $"{awarenessItem.Grabbable.Role}: {awarenessDistance:F1}m / 접근 필요 {approachNeeded:F1}m"
                    : $"{awarenessItem.Grabbable.Role}: {awarenessDistance:F1}m / 조준을 물체 중앙으로";
            }
            // 아직 spawn 되지 않은 아이템은 역할을 신뢰할 수 없다. OnGUI 는 매 프레임 돌기 때문에
            // 여기서 예외가 나면 화면이 아니라 로그가 먼저 무너진다.
            if (item.Grabbable == null)
                return $"+   E 잡기: 대상 확인 중  {distance:F1}m";
            if (item.IsSecured)
                return $"{item.Grabbable.Role}: {DescribeSecured(item)}";
            if (item.IsClaimed)
                return $"{item.Grabbable.Role}: 다른 플레이어가 잡는 중";
            return $"[E] {item.Grabbable.Role} 잡기  {distance:F1}m";
        }

        /// <summary>
        /// 문 앞 안내. 사거리 밖이면 null 이라 아이템 프롬프트가 그대로 나온다.
        /// 사망한 승무원에게는 "조작 불가" 를 보여 준다 — 눌러도 아무 일이 없는 것보다
        /// 왜 안 되는지가 보여야 한다.
        /// </summary>
        private string BuildDoorPrompt()
        {
            var door = LastShiftZoneDoor.FindOperable(transform.position);
            if (door == null) return null;
            var crew = GetComponent<LastShiftCrewOxygen>();
            if (crew != null && crew.IsDead) return $"{door.BoundaryLabel} 문: 조작 불가";
            return door.IsOpen
                ? $"[Q] {door.BoundaryLabel} 문 닫기 (압력 차단)"
                : $"[Q] {door.BoundaryLabel} 문 열기";
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
            GUI.Label(new Rect(Screen.width * 0.5f - 12f, Screen.height * 0.5f - 18f, 24f, 36f), "+", promptStyle);
            GUI.Box(new Rect(Screen.width * 0.5f - 230f, Screen.height * 0.5f + 24f, 460f, 34f), GUIContent.none);
            GUI.Label(new Rect(Screen.width * 0.5f - 224f, Screen.height * 0.5f + 26f, 448f, 30f), InteractionPrompt, promptStyle);
            GUI.Box(new Rect(8f, Screen.height - 36f, Screen.width - 16f, 28f), GUIContent.none);
            GUI.Label(new Rect(12f, Screen.height - 34f, Screen.width - 24f, 24f), InputLabel, identityStyle);
        }
    }
}
