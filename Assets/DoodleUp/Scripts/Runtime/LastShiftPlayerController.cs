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
        private bool grabPressed;
        private bool securePressed;
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
        public string InputLabel => "WASD 이동 / Mouse 조준 / E 잡기·놓기 / F 고정 / 1·2·3 프리셋 / R 리셋";
        public string InteractionPrompt => BuildInteractionPrompt();
        public Vector3 AimOrigin => targetCamera != null ? targetCamera.transform.position : transform.position;
        public Vector3 AimDirection => targetCamera != null ? targetCamera.transform.forward : transform.forward;

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
            var presetOne = ConsumePress(keyboard.digit1Key.isPressed, ref presetOnePressed);
            var presetTwo = ConsumePress(keyboard.digit2Key.isPressed, ref presetTwoPressed);
            var presetThree = ConsumePress(keyboard.digit3Key.isPressed, ref presetThreePressed);
            var reset = ConsumePress(keyboard.rKey.isPressed, ref resetPressed);

            ApplyLook(look, deltaTime);
            ApplyMovement(move, jump, deltaTime);
            if (grab) ToggleGrab();
            if (secure && networkPlayer != null) networkPlayer.RequestSecureHeldItem();
            if (networkPlayer == null || !networkPlayer.IsSpawned) return;
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
            return key is Key.W or Key.A or Key.S or Key.D or Key.Space or Key.E or Key.F;
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
            targetCamera.transform.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
        }

        private void ApplyMovement(Vector2 move, bool jump, float deltaTime)
        {
            if (characterController == null) characterController = GetComponent<CharacterController>();
            var worldMove = transform.right * move.x + transform.forward * move.y;
            if (characterController.isGrounded)
            {
                verticalSpeed = -1f;
                if (jump) verticalSpeed = 4.8f;
            }
            else
            {
                verticalSpeed += UnityEngine.Physics.gravity.y * deltaTime;
            }

            characterController.Move((worldMove * MoveSpeed + Vector3.up * verticalSpeed) * deltaTime);
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
                var target = collider != null ? collider.bounds.center : candidate.transform.position;
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
            if (networkPlayer == null || !networkPlayer.IsSpawned)
                return heldItem != null ? "[E] 놓기" : "+";
            if (serverRejectionReason != null)
            {
                if (Time.unscaledTime <= serverRejectionExpiry)
                    return $"서버 거부: {serverRejectionReason}";
                serverRejectionReason = null;
            }
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
                if (awarenessItem == null)
                    return $"+   E 잡기: 대상을 조준하세요 (사거리 {GrabDistance:F1}m)";
                if (awarenessItem.IsSecured)
                    return $"{awarenessItem.Grabbable.Role}: {DescribeSecured(awarenessItem)}";
                var approachNeeded = Mathf.Max(0f, awarenessDistance - GrabDistance);
                return approachNeeded > 0.05f
                    ? $"{awarenessItem.Grabbable.Role}: {awarenessDistance:F1}m / 접근 필요 {approachNeeded:F1}m"
                    : $"{awarenessItem.Grabbable.Role}: {awarenessDistance:F1}m / 조준을 물체 중앙으로";
            }
            if (item.IsSecured)
                return $"{item.Grabbable.Role}: {DescribeSecured(item)}";
            if (item.IsClaimed)
                return $"{item.Grabbable.Role}: 다른 플레이어가 잡는 중";
            return $"[E] {item.Grabbable.Role} 잡기  {distance:F1}m";
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
