using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Runtime
{
    public sealed class LastShiftSandboxController : MonoBehaviour
    {
        public const string ProfileId = "LAST_SHIFT_SP01";
        public const float SecureDistance = 0.9f;
        public static readonly Vector3 PlayerSpawn = new(-3.5f, 0.1f, 0f);

        [SerializeField] private LastShiftPlayerController[] players;
        [SerializeField] private LastShiftGrabbable[] items;
        [SerializeField] private LastShiftPreset currentPreset;
        [SerializeField] private LastShiftShipState currentState;

        private readonly LastShiftControlHold controlHold = new();
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private float dockingSecondsRemaining;
        private LastShiftMeteorStimulus appliedMeteor;
        private LastShiftImpactFeedback impactFeedback;

        public LastShiftPreset CurrentPreset => currentPreset;
        public LastShiftShipState CurrentState => currentState;
        public LastShiftPlayerController[] Players => players;
        public LastShiftGrabbable[] Items => items;
        public LastShiftMeteorStimulus Meteor => LastShiftMeteorStimulus.Canonical;
        public LastShiftResolverResult FirstResult { get; private set; }
        public LastShiftResolverResult LastResult { get; private set; }
        public bool HasAppliedImpact { get; private set; }
        public int ImpactApplicationCount { get; private set; }
        public int ResetGeneration { get; private set; }
        public float DockingSecondsRemaining => dockingSecondsRemaining;
        public float ControlHoldRemaining => controlHold.RemainingSeconds;

        public void ApplyNetworkSnapshot(in LastShiftNetworkSnapshot value)
        {
            // 클라이언트는 ApplyMeteorImpact 를 돌리지 않으므로 충격 연출 트리거가 없다.
            // 스냅샷의 ImpactApplicationCount 증가가 곧 "서버에서 충격이 터졌다" 이므로
            // 그 변화를 연출 트리거로 쓴다. 리셋으로 카운트가 유지되는 동안은 재생하지 않는다.
            var impactAdvanced = value.HasAppliedImpact && value.ImpactApplicationCount > ImpactApplicationCount;

            currentPreset = value.Preset;
            currentState = value.ShipState;
            dockingSecondsRemaining = value.DockingSecondsRemaining;
            ResetGeneration = value.ResetGeneration;
            ImpactApplicationCount = value.ImpactApplicationCount;
            HasAppliedImpact = value.HasAppliedImpact;
            FirstResult = new LastShiftResolverResult(value.FirstProblem, 0f, 0f, 0f, "server snapshot");
            LastResult = new LastShiftResolverResult(value.CurrentProblem, value.CoolingScore, value.BatteryScore, value.LeakScore, "server snapshot");
            if (impactAdvanced) PlayImpactFeedback(Meteor);
        }

        public void RegisterPlayer(LastShiftPlayerController player)
        {
            if (player == null || players != null && players.Contains(player)) return;
            players = players == null ? new[] { player } : players.Append(player).ToArray();
        }

        public void UnregisterPlayer(LastShiftPlayerController player)
        {
            if (player == null || players == null) return;
            players = players.Where(targetPlayer => targetPlayer != null && targetPlayer != player).ToArray();
        }

        public void Configure(LastShiftPlayerController targetPlayer, LastShiftGrabbable[] sceneItems)
        {
            Configure(new[] { targetPlayer }, sceneItems);
        }

        public void Configure(LastShiftPlayerController[] targetPlayers, LastShiftGrabbable[] sceneItems)
        {
            players = targetPlayers;
            items = sceneItems;
        }

        private void Awake()
        {
            if (players == null || players.Length == 0) players = FindObjectsByType<LastShiftPlayerController>(FindObjectsSortMode.None);
            if (items == null || items.Length == 0) items = FindObjectsByType<LastShiftGrabbable>(FindObjectsSortMode.None);
        }

        private void Start()
        {
            if (GetComponent<Unity.Netcode.NetworkObject>() != null) return;
            ResetPreset(LastShiftPreset.HighHeatHighThrust);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var networkSandbox = GetComponent<LastShiftNetworkSandbox>();
            if (keyboard != null && (networkSandbox == null || !networkSandbox.IsSpawned))
            {
                if (keyboard.digit1Key.wasPressedThisFrame) RequestPresetReset(LastShiftPreset.HighHeatHighThrust);
                else if (keyboard.digit2Key.wasPressedThisFrame) RequestPresetReset(LastShiftPreset.PowerOverloadLooseBattery);
                else if (keyboard.digit3Key.wasPressedThisFrame) RequestPresetReset(LastShiftPreset.BadAttitudeHighOxygen);
                else if (keyboard.rKey.wasPressedThisFrame) RequestPresetReset(currentPreset);
                else if (keyboard.mKey.wasPressedThisFrame) ApplyMeteorImpact();
                else if (keyboard.fKey.wasPressedThisFrame) TrySecureHeldItem();

                var thrust = currentState.ThrustDemand;
                var attitude = currentState.ShipAttitudeDegrees;
                var controlChanged = false;
                if (keyboard.upArrowKey.wasPressedThisFrame)
                {
                    thrust = Mathf.Clamp01(thrust + 0.1f);
                    controlChanged = true;
                }
                if (keyboard.downArrowKey.wasPressedThisFrame)
                {
                    thrust = Mathf.Clamp01(thrust - 0.1f);
                    controlChanged = true;
                }
                if (keyboard.leftArrowKey.wasPressedThisFrame)
                {
                    attitude = Mathf.Clamp(attitude - 10f, -90f, 90f);
                    controlChanged = true;
                }
                if (keyboard.rightArrowKey.wasPressedThisFrame)
                {
                    attitude = Mathf.Clamp(attitude + 10f, -90f, 90f);
                    controlChanged = true;
                }
                if (controlChanged) ApplyControl(thrust, attitude);
            }

            AdvanceControlHold(Time.deltaTime);
            dockingSecondsRemaining = Mathf.Max(0f, dockingSecondsRemaining - Time.deltaTime);
        }

        public void RequestPresetReset(LastShiftPreset preset)
        {
            var networkSandbox = GetComponent<LastShiftNetworkSandbox>();
            if (networkSandbox != null && networkSandbox.IsSpawned)
            {
                if (networkSandbox.IsServer) networkSandbox.ResetPresetFromServer(preset);
                else networkSandbox.RequestPresetResetRpc(preset);
                return;
            }
            ResetPreset(preset);
        }

        public void ResetPreset(LastShiftPreset preset)
        {
            var networkSandbox = GetComponent<LastShiftNetworkSandbox>();
            if (networkSandbox != null && networkSandbox.IsSpawned && networkSandbox.IsServer)
                networkSandbox.PrepareForPresetReset();

            currentPreset = preset;
            currentState = LastShiftPresetFactory.Create(preset);
            dockingSecondsRemaining = 6f * 60f;
            ResetGeneration++;
            HasAppliedImpact = false;
            appliedMeteor = default;
            FirstResult = default;
            LastResult = default;
            if (players != null && (networkSandbox == null || !networkSandbox.IsSpawned))
            {
                foreach (var targetPlayer in players)
                    if (targetPlayer != null) targetPlayer.ResetPlayer(PlayerSpawn);
            }
            if (items != null)
            {
                foreach (var item in items)
                    if (item != null) item.ResetItem();
            }
            ApplyPresetItemState(preset);
            // 리셋은 pre-impact 로 되돌리는 것이므로 손상 구역 표시도 함께 걷어낸다.
            // 남겨두면 아무 일도 없는 상태에서 구역이 계속 점멸한다.
            if (impactFeedback == null) impactFeedback = GetComponent<LastShiftImpactFeedback>();
            if (impactFeedback == null) impactFeedback = FindFirstObjectByType<LastShiftImpactFeedback>();
            if (impactFeedback != null) impactFeedback.ClearDamageMarkers();
            controlHold.Reset(currentState.ThrustDemand, currentState.ShipAttitudeDegrees);
            Debug.Log($"[LAST_SHIFT_RESET] generation={ResetGeneration} preset={preset} phase=pre-impact");
        }

        public bool ApplyMeteorImpact()
        {
            return ApplyMeteorImpact(Meteor);
        }

        public bool ApplyMeteorImpact(LastShiftMeteorStimulus meteor)
        {
            if (HasAppliedImpact) return false;

            currentState = LastShiftMeteorApplication.Apply(meteor, currentState, items);
            appliedMeteor = meteor;
            HasAppliedImpact = true;
            ImpactApplicationCount++;
            FirstResult = ResolveCurrentState(appliedMeteor);
            LastResult = FirstResult;
            Debug.Log($"[LAST_SHIFT_IMPACT] application={ImpactApplicationCount} point={meteor.ImpactPoint} vector={meteor.ImpactVector} E={meteor.Energy:F1} firstResult={FirstResult.Problem}");
            PlayImpactFeedback(meteor);
            return true;
        }

        /// <summary>
        /// 관측 채널(흔들림·소리·손상 구역 표시)을 재생한다. 시드는 ImpactApplicationCount 라서
        /// 서버와 클라이언트가 같은 흔들림 궤적을 만든다. 채널이 없으면(연출 컴포넌트 미부착)
        /// 시뮬레이션은 그대로 진행되어야 하므로 조용히 통과시킨다.
        /// </summary>
        private void PlayImpactFeedback(in LastShiftMeteorStimulus meteor)
        {
            if (impactFeedback == null) impactFeedback = GetComponent<LastShiftImpactFeedback>();
            if (impactFeedback == null) impactFeedback = FindFirstObjectByType<LastShiftImpactFeedback>();
            if (impactFeedback == null) return;
            impactFeedback.PlayImpact(meteor.ImpactPoint, LastShiftMeteorApplication.CalculateSeverity(meteor), ImpactApplicationCount);
        }

        public bool TrySecureHeldItem()
        {
            var holder = players?.FirstOrDefault(targetPlayer =>
                targetPlayer != null &&
                targetPlayer.HeldItem != null &&
                Vector3.Distance(targetPlayer.HeldItem.transform.position, targetPlayer.HeldItem.NominalPosition) <= SecureDistance);
            return TrySecureHeldItem(holder);
        }

        public bool TrySecureHeldItem(LastShiftPlayerController holder)
        {
            var held = holder != null ? holder.HeldItem : null;
            if (held == null) return false;
            if (Vector3.Distance(held.transform.position, held.NominalPosition) > SecureDistance) return false;

            holder.DropForProbe();
            if (!held.TrySecureAtNominal(SecureDistance)) return false;

            RefreshResultAfterImpact();
            Debug.Log($"[LAST_SHIFT_SECURE] player={holder.PlayerSlot} role={held.Role} nominal={held.NominalPosition} problem={LastResult.Problem}");
            return true;
        }

        public void ApplyControl(float thrustDemand, float attitudeDegrees)
        {
            currentState.ThrustDemand = Mathf.Clamp01(thrustDemand);
            currentState.ShipAttitudeDegrees = Mathf.Clamp(attitudeDegrees, -90f, 90f);
            controlHold.Set(currentState.ThrustDemand, currentState.ShipAttitudeDegrees);
            RefreshResultAfterImpact();
        }

        public void AdvanceControlHold(float deltaTime)
        {
            var holdWasActive = controlHold.IsActive;
            controlHold.Tick(deltaTime);
            if (!holdWasActive || controlHold.IsActive) return;

            var presetState = LastShiftPresetFactory.Create(currentPreset);
            currentState.ThrustDemand = presetState.ThrustDemand;
            currentState.ShipAttitudeDegrees = presetState.ShipAttitudeDegrees;
            RefreshResultAfterImpact();
        }

        public void RefreshResultAfterImpact()
        {
            if (HasAppliedImpact) LastResult = ResolveCurrentState(appliedMeteor);
        }

        private LastShiftResolverResult ResolveCurrentState(LastShiftMeteorStimulus meteor)
        {
            var battery = FindItem(LastShiftItemRole.Battery);
            var cooling = FindItem(LastShiftItemRole.CoolingCanister);
            var patch = FindItem(LastShiftItemRole.PatchPlate);
            var tether = FindItem(LastShiftItemRole.Tether);
            return LastShiftDamageResolver.Resolve(new LastShiftResolverInput(
                meteor,
                currentState,
                CrewPosition,
                PositionOf(battery),
                NominalPositionOf(battery),
                battery != null && battery.Secured,
                PositionOf(cooling),
                NominalPositionOf(cooling),
                cooling != null && cooling.Secured,
                PositionOf(patch),
                NominalPositionOf(patch),
                patch != null && patch.Secured,
                PositionOf(tether),
                NominalPositionOf(tether),
                tether != null && tether.Secured));
        }

        private Vector3 CrewPosition
        {
            get
            {
                var activePlayers = players?.Where(targetPlayer => targetPlayer != null).ToArray();
                if (activePlayers == null || activePlayers.Length == 0) return Vector3.zero;
                var total = Vector3.zero;
                foreach (var targetPlayer in activePlayers) total += targetPlayer.transform.position;
                return total / activePlayers.Length;
            }
        }

        /// <summary>
        /// 네 역할 전부를 명시적으로 설정한다. 여기서 빠진 역할은 <see cref="LastShiftGrabbable.ResetItem"/> 가
        /// 씬 저작값(secured=true)으로 되돌리기 때문에 모든 프리셋에서 영구 고정되어 grab 자체를 검증할 수 없게 된다.
        /// 프리셋별 "느슨한 주범"은 그대로 유지하고, 공용 도구인 Tether 는 어떤 프리셋에서도 상시 잡을 수 있게 둔다.
        /// </summary>
        private void ApplyPresetItemState(LastShiftPreset preset)
        {
            SetItemSecured(LastShiftItemRole.Battery, preset != LastShiftPreset.PowerOverloadLooseBattery);
            SetItemSecured(LastShiftItemRole.CoolingCanister, preset != LastShiftPreset.HighHeatHighThrust);
            SetItemSecured(LastShiftItemRole.PatchPlate, preset != LastShiftPreset.BadAttitudeHighOxygen);
            SetItemSecured(LastShiftItemRole.Tether, false);
            Debug.Log($"[LAST_SHIFT_PRESET_ITEMS] preset={preset} loose={DescribeLooseItems()}");
        }

        private void SetItemSecured(LastShiftItemRole role, bool secured)
        {
            var item = FindItem(role);
            if (item != null) item.SetSecured(secured);
        }

        private string DescribeLooseItems()
        {
            var loose = items?
                .Where(item => item != null && !item.Secured)
                .Select(item => item.Role.ToString())
                .ToArray();
            return loose == null || loose.Length == 0 ? "none" : string.Join("+", loose);
        }

        private LastShiftGrabbable FindItem(LastShiftItemRole role)
        {
            return items?.FirstOrDefault(item => item != null && item.Role == role);
        }

        private static Vector3 PositionOf(LastShiftGrabbable item)
        {
            return item != null ? item.transform.position : Vector3.zero;
        }

        private static Vector3 NominalPositionOf(LastShiftGrabbable item)
        {
            return item != null ? item.NominalPosition : Vector3.zero;
        }

        private void OnGUI()
        {
            headingStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            bodyStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = new Color(0.88f, 0.94f, 1f) } };
            GUI.Box(new Rect(16f, 16f, 680f, 300f), GUIContent.none);
            GUI.Label(new Rect(28f, 24f, 650f, 28f), $"LAST SHIFT SP-01 SOLO | Preset {(char)('A' + (int)currentPreset)}: {currentPreset}", headingStyle);
            GUI.Label(new Rect(28f, 56f, 650f, 48f),
                $"WASD/Space/E/F/Mouse | 1·2·3 프리셋 | R 리셋 | M one-shot meteor | 화살표 조종 (8초)\n" +
                $"Docking T-{dockingSecondsRemaining:F0}s | Hold {controlHold.RemainingSeconds:F1}s | " +
                $"phase={(HasAppliedImpact ? "POST-IMPACT" : "PRE-IMPACT")}", bodyStyle);
            GUI.Label(new Rect(28f, 106f, 650f, 48f),
                $"INPUT state: thrust={currentState.ThrustDemand:F2} bus={currentState.BusPower:F2} O2={currentState.OxygenPressure:F2} " +
                $"hull={currentState.HullIntegrity:F2} heat={currentState.EngineHeat:F2} attitude={currentState.ShipAttitudeDegrees:F0} damage={currentState.ExistingDamage:F2}", bodyStyle);
            GUI.Label(new Rect(28f, 157f, 650f, 52f),
                HasAppliedImpact
                    ? $"FIRST DOMINANT: {FirstResult.Problem}\nCURRENT DOMINANT: {LastResult.Problem}"
                    : "FIRST DOMINANT PROBLEM: pending meteor", headingStyle);
            GUI.Label(new Rect(28f, 215f, 650f, 84f), HasAppliedImpact ? LastResult.CauseChain : "Preset only configures pre-impact state. Press M to apply the canonical meteor once.", bodyStyle);
        }
    }
}
