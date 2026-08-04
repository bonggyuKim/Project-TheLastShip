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

        /// <summary>
        /// 성능 포기는 물건을 들고 오지 않아도 손상 지점에서 결정할 수 있다. 대신 그 지점에
        /// 실제로 가 있어야 하므로 <see cref="SecureDistance"/> 보다 조금 넉넉한 도달 거리를 쓴다.
        /// </summary>
        public const float SacrificeReachDistance = 1.8f;

        /// <summary>도킹 확정 트리거. 조종석 콘솔(CockpitConsole, x≈-5.1) 앞이다.</summary>
        public static readonly Vector3 DockingTriggerPosition = new(-4.6f, 0.9f, 0f);
        public const float DockingTriggerRadius = 1.6f;

        [SerializeField] private LastShiftPlayerController[] players;
        [SerializeField] private LastShiftGrabbable[] items;
        [SerializeField] private LastShiftPreset currentPreset;
        [SerializeField] private LastShiftShipState currentState;

        private readonly LastShiftControlHold controlHold = new();
        private readonly LastShiftRepairLedger repairLedger = new();
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle sirenStyle;
        private float dockingSecondsRemaining;
        private LastShiftMeteorStimulus appliedMeteor;
        private LastShiftImpactFeedback impactFeedback;
        private LastShiftTickReport lastTick = LastShiftTickReport.Idle;
        private LastShiftVerdict verdict;
        private float steeringInputDelayRemaining;
        private float pendingThrust;
        private float pendingAttitude;
        private bool hasPendingControl;
        private bool wasCrewAtDockingTrigger;
        private int damagedSystemMask;
        private bool sirenActive;
        private AudioSource sirenAudio;

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
        public LastShiftRepairLedger Repairs => repairLedger;
        public LastShiftVerdict Verdict => verdict;
        public bool IsResolved => LastShiftVerdictResolver.IsResolved(verdict);
        public float ThrustCeiling => lastTick.ThrustCeiling;
        public bool HeatProtectionEngaged => lastTick.HeatProtectionEngaged;
        public bool SteeringDelayed => lastTick.SteeringDelayed;
        public bool OxygenPumpRunning => lastTick.OxygenPumpRunning;
        public int SacrificeCount => repairLedger.SacrificeCount;
        public int BypassLapseCount => repairLedger.BypassLapseCount;

        /// <summary>S-O3 전선 사이렌(N9). 모든 구역에서 들리는 P0 유일의 국소 정보 예외다.</summary>
        public bool SirenActive => sirenActive;

        /// <summary>
        /// 살아 있는 승무원이 한 명이라도 있는가. 실패 판정(N2)과 도킹 성립 조건이 모두 이 값을 읽는다.
        /// 승무원 목록 자체가 없는 구성(EditMode 최소 조립 등)에서는 시뮬레이션을 승무원 부재로
        /// 끝내면 안 되므로 살아 있는 것으로 본다.
        /// </summary>
        public bool AnyCrewAlive
        {
            get
            {
                var living = 0;
                var counted = 0;
                if (players != null)
                {
                    foreach (var targetPlayer in players)
                    {
                        if (targetPlayer == null) continue;
                        counted++;
                        var crew = targetPlayer.GetComponent<LastShiftCrewOxygen>();
                        if (crew == null || !crew.IsDead) living++;
                    }
                }
                return counted == 0 || living > 0;
            }
        }

        public int LivingCrewCount
        {
            get
            {
                var living = 0;
                if (players == null) return 0;
                foreach (var targetPlayer in players)
                {
                    if (targetPlayer == null) continue;
                    var crew = targetPlayer.GetComponent<LastShiftCrewOxygen>();
                    if (crew == null || !crew.IsDead) living++;
                }
                return living;
            }
        }

        /// <summary>승무원 개인 예비 산소 조회. 컴포넌트가 아직 없으면 만들어 붙인다.</summary>
        public LastShiftCrewOxygen CrewOxygenOf(LastShiftPlayerController targetPlayer)
        {
            return LastShiftCrewOxygen.Ensure(targetPlayer);
        }

        /// <summary>
        /// 테스트가 특정 상태를 직접 조립하기 위한 경계. 실제 진행 경로로 그 상태에 도달하려면
        /// 수백 초를 밀어야 하는 조합(회복된 압력, 도킹 성공선 등)을 몇 tick 으로 만든다.
        /// 게임 코드에서는 호출하지 않는다 — 상태 변화의 정본은 언제나 Tick 이다.
        /// </summary>
        public void OverrideStateForProbe(in LastShiftShipState state)
        {
            currentState = state;
        }

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
            verdict = value.Verdict;
            repairLedger.ApplyReplicatedSacrificeMask(value.SacrificedSystemMask);
            lastTick = new LastShiftTickReport
            {
                ThrustCeiling = value.ThrustCeiling,
                HeatProtectionEngaged = value.HeatProtectionEngaged,
                SteeringDelayed = value.SteeringDelayed,
                OxygenPumpRunning = value.OxygenPumpRunning
            };
            // 클라이언트는 AdvanceMission 을 돌리지 않으므로 사이렌도 스냅샷으로만 켜진다.
            if (sirenActive != value.SirenActive)
            {
                sirenActive = value.SirenActive;
                if (sirenActive) EnsureSirenAudioPlaying();
                else StopSirenAudio();
            }
            else if (sirenActive) EnsureSirenAudioPlaying();
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
                // 부품을 제자리에 놓는 것(F)과 계통에 연결하는 것(C·V·G)은 다른 행동이다.
                // 놓기만 해서는 악화가 멈추지 않는다. E 는 이미 잡기/놓기라 쓰지 않는다.
                else if (keyboard.cKey.wasPressedThisFrame) TryBeginRepair(LastShiftRepairMode.SafeRestore);
                else if (keyboard.vKey.wasPressedThisFrame) TryBeginRepair(LastShiftRepairMode.QuickBypass);
                else if (keyboard.gKey.wasPressedThisFrame) TryBeginRepair(LastShiftRepairMode.PerformanceSacrifice);

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
            AdvanceMission(Time.deltaTime);
        }

        /// <summary>
        /// R2 악화 tick + R1 작업 채널 진행 + R3 판정을 한 스텝 돌린다. 운석 적용 전에는
        /// 아직 손상이 없으므로 아무것도 돌지 않고, 판정이 확정된 뒤에는 시계가 멈춘다.
        /// 테스트가 시간을 직접 밀 수 있도록 deltaTime 을 받는 public 경계로 둔다.
        /// </summary>
        public void AdvanceMission(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            if (steeringInputDelayRemaining > 0f)
            {
                steeringInputDelayRemaining -= deltaTime;
                if (steeringInputDelayRemaining <= 0f && hasPendingControl)
                {
                    steeringInputDelayRemaining = 0f;
                    hasPendingControl = false;
                    CommitControl(pendingThrust, pendingAttitude);
                }
            }

            if (!HasAppliedImpact || IsResolved) return;

            // 기존 우회의 수명을 먼저 줄여야 이 tick 끝에 막 완성된 우회가 작업 시간까지
            // 소급해서 잃지 않는다. 0.8초 작업 완료 순간부터 온전한 60초가 시작된다.
            LapseExpiredBypasses(deltaTime);
            AdvanceRepairChannels(deltaTime);

            lastTick = LastShiftDeterioration.Tick(ref currentState, BuildContainment(), deltaTime);
            RefreshResultAfterImpact();

            dockingSecondsRemaining = Mathf.Max(0f, dockingSecondsRemaining - deltaTime);

            UpdateSiren();
            AdvanceCrewOxygen(deltaTime);

            var continuous = LastShiftVerdictResolver.EvaluateContinuous(currentState, AnyCrewAlive);
            if (continuous != LastShiftVerdict.Pending)
            {
                SettleVerdict(continuous, "all-crew-suit-oxygen-depleted");
                return;
            }

            // 상주가 아니라 "진입" 으로 판정한다. 승무원 스폰 지점이 이미 트리거 반경 안이라
            // 상주로 보면 운석 직후 조건이 남아 있는 프리셋에서 가만히 서 있는 것만으로 성공한다.
            var atTrigger = IsCrewAtDockingTrigger();
            var entered = atTrigger && !wasCrewAtDockingTrigger;
            wasCrewAtDockingTrigger = atTrigger;
            if (entered)
            {
                var docking = LastShiftVerdictResolver.EvaluateDocking(currentState, repairLedger.SacrificeUsed, AnyCrewAlive);
                if (docking != LastShiftVerdict.Pending)
                {
                    SettleVerdict(docking, "docking-trigger");
                    return;
                }
            }

            if (dockingSecondsRemaining <= 0f)
                SettleVerdict(LastShiftVerdictResolver.EvaluateTimeout(currentState), "timer-expired");
        }

        private void SettleVerdict(LastShiftVerdict value, string trigger)
        {
            if (value == LastShiftVerdict.Pending || IsResolved) return;
            verdict = value;
            Debug.Log($"[LAST_SHIFT_VERDICT] generation={ResetGeneration} verdict={value} trigger={trigger} " +
                      $"thrust={currentState.ThrustDemand:F2} O2={currentState.OxygenPressure:F2} heat={currentState.EngineHeat:F2} " +
                      $"bus={currentState.BusPower:F2} T-{dockingSecondsRemaining:F0}s sacrifices={repairLedger.SacrificeCount} bypassLapses={repairLedger.BypassLapseCount}");
        }

        /// <summary>
        /// 도킹 트리거는 승무원 위치로 판정한다. 판정 시점 상태를 그대로 읽으므로
        /// "조건을 갖춘 채로 조종석에 들어간다" 가 곧 성공이다. 조건 미달이면 실패가 아니라
        /// 아직 도킹이 성립하지 않은 것이고, 남은 시간 안에 갖춰서 다시 오면 된다.
        /// </summary>
        public bool IsCrewAtDockingTrigger()
        {
            var activePlayers = players?.Where(targetPlayer => targetPlayer != null).ToArray();
            if (activePlayers == null || activePlayers.Length == 0) return false;
            foreach (var targetPlayer in activePlayers)
            {
                // 사망한 승무원의 시신은 도킹을 성립시키지 못한다. 이걸 빼면 죽은 자리가
                // 조종석 안이었다는 이유만으로 성공이 나서 "남은 1명으로 도킹" 이 검증되지 않는다.
                var crew = targetPlayer.GetComponent<LastShiftCrewOxygen>();
                if (crew != null && crew.IsDead) continue;
                if (Vector3.Distance(targetPlayer.transform.position, DockingTriggerPosition) <= DockingTriggerRadius)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// N1 개인 예비 산소 tick. 진공 구역에 있는 승무원만 소모한다. 승무원이 어느 구역에
        /// 있는지는 <see cref="LastShiftImpactFeedback.ResolveDamagedZone"/> 과 같은 x 경계로 판정해서
        /// 손상 표시와 진공 판정이 어긋나지 않게 한다.
        /// </summary>
        private void AdvanceCrewOxygen(float deltaTime)
        {
            if (players == null) return;
            foreach (var targetPlayer in players)
            {
                if (targetPlayer == null) continue;
                var crew = LastShiftCrewOxygen.Ensure(targetPlayer);
                if (crew == null) continue;
                var wasAlive = !crew.IsDead;
                crew.Tick(IsZoneVacuum(targetPlayer.transform.position), deltaTime);
                if (wasAlive && crew.IsDead)
                    Debug.Log($"[LAST_SHIFT_CREW_DEATH] generation={ResetGeneration} crew={targetPlayer.PlayerSlot} " +
                              $"livingCrew={LivingCrewCount} O2={currentState.OxygenPressure:F2} T-{dockingSecondsRemaining:F0}s");
            }
        }

        /// <summary>
        /// 이 위치가 진공인가. 선체 압력이 0.00 이면 전 구역이 진공이고, 산소 계통을 성능 포기로
        /// 밀폐했다면 밀폐한 구역(생명유지 구역)만 압력과 무관하게 진공이다.
        /// </summary>
        public bool IsZoneVacuum(Vector3 position)
        {
            var sealedOff = repairLedger.IsSacrificed(LastShiftShipSystem.Oxygen) &&
                            LastShiftImpactFeedback.ResolveDamagedZone(position) == SealedZoneName;
            return LastShiftVerdictResolver.IsZoneVacuum(currentState, sealedOff);
        }

        /// <summary>
        /// 산소 계통을 포기했을 때 밀폐되는 구역. 파공 부품(PatchPlate, x≈4.5)이 있는 생명유지
        /// 구역이며, 이 값은 씬 빌더의 아이템 배치와 함께 움직인다.
        /// </summary>
        private static string SealedZoneName => LastShiftSceneZones.LifeSupportZoneName;

        /// <summary>
        /// N9 S-O3 전선 사이렌. 발동 0.15 / 해제 0.20 이라 경계에서 떨리지 않는다.
        /// 예비 산소 소모(압력 0.00)와는 절대 겹치지 않는다 — 겹치면 사이렌이 곧 사망 예고가 되어
        /// 24초 대응 창이 사라진다.
        /// </summary>
        private void UpdateSiren()
        {
            var next = LastShiftVerdictResolver.EvaluateSiren(currentState, sirenActive);
            if (next == sirenActive)
            {
                if (next) EnsureSirenAudioPlaying();
                return;
            }

            sirenActive = next;
            if (next)
            {
                EnsureSirenAudioPlaying();
                Debug.Log($"[LAST_SHIFT_SIREN] generation={ResetGeneration} state=ON O2={currentState.OxygenPressure:F2} scope=all-zones");
            }
            else
            {
                StopSirenAudio();
                Debug.Log($"[LAST_SHIFT_SIREN] generation={ResetGeneration} state=OFF O2={currentState.OxygenPressure:F2}");
            }
        }

        private void EnsureSirenAudioPlaying()
        {
            if (!Application.isPlaying) return;
            if (sirenAudio == null)
            {
                sirenAudio = gameObject.AddComponent<AudioSource>();
                sirenAudio.playOnAwake = false;
                sirenAudio.loop = true;
                // spatialBlend 0 이 곧 "모든 구역에서 들린다" 이다. 3D 로 두면 조종석에서
                // 감쇠되어 N9 가 존재하지 않는 것과 같아진다.
                sirenAudio.spatialBlend = 0f;
                sirenAudio.volume = 0.45f;
                sirenAudio.clip = LastShiftProceduralAudio.CreateSirenLoop();
            }
            if (!sirenAudio.isPlaying) sirenAudio.Play();
        }

        private void StopSirenAudio()
        {
            if (sirenAudio != null && sirenAudio.isPlaying) sirenAudio.Stop();
        }

        private LastShiftContainment BuildContainment()
        {
            return new LastShiftContainment
            {
                CoolingRestored = IsSystemRestored(LastShiftShipSystem.Cooling),
                CoolingSacrificed = repairLedger.IsSacrificed(LastShiftShipSystem.Cooling),
                PowerRestored = IsSystemRestored(LastShiftShipSystem.Power),
                PowerSacrificed = repairLedger.IsSacrificed(LastShiftShipSystem.Power),
                OxygenRestored = IsSystemRestored(LastShiftShipSystem.Oxygen),
                OxygenSacrificed = repairLedger.IsSacrificed(LastShiftShipSystem.Oxygen)
            };
        }

        /// <summary>
        /// 계통이 실제로 되돌아왔는지. 아이템 <see cref="LastShiftGrabbable.Secured"/> 가 정본이고,
        /// 장부는 그 위에 "연결까지 마쳤는가" 를 얹는다. 물건만 제자리에 놓아도 연결하지
        /// 않았으면 악화는 계속된다 — 그게 R1 이 F 고정과 별개의 동사인 이유다.
        /// </summary>
        public bool IsSystemRestored(LastShiftShipSystem system)
        {
            // 운석에 안 맞은 계통은 애초에 되돌릴 것이 없다. 이걸 빼면 프리셋마다 손상은 하나인데
            // 나머지 두 계통도 "미복구" 로 읽혀 세 시계가 동시에 돌고, 무엇이 터졌는지 구분이 사라진다.
            if (!IsSystemDamaged(system)) return true;
            if (!repairLedger[system].HasCompletedRepair) return false;
            var item = FindItem(LastShiftSystemMap.RoleFor(system));
            return item != null && item.Secured;
        }

        /// <summary>
        /// 충격 시점에 이 계통이 손상됐는가. 부품이 제자리를 벗어난 것이 곧 손상이다.
        /// 프리셋이 미리 풀어 둔 부품과 운석이 날려버린 부품 모두 여기에 걸린다.
        /// </summary>
        public bool IsSystemDamaged(LastShiftShipSystem system)
        {
            return (damagedSystemMask & (1 << (int)system)) != 0;
        }

        /// <summary>
        /// 충격 직후 어느 계통이 터졌는지 한 번만 확정한다. 매 tick 다시 계산하면 부품을
        /// 집어 든 순간 "손상됨" 이 되고, 놓으면 사라져 악화가 들쭉날쭉해진다.
        /// </summary>
        private void CaptureDamagedSystems()
        {
            damagedSystemMask = 0;
            for (var index = 0; index < LastShiftSystemMap.SystemCount; index++)
            {
                var item = FindItem(LastShiftSystemMap.RoleFor((LastShiftShipSystem)index));
                if (item != null && !item.Secured) damagedSystemMask |= 1 << index;
            }
            Debug.Log($"[LAST_SHIFT_DAMAGE] generation={ResetGeneration} cooling={IsSystemDamaged(LastShiftShipSystem.Cooling)} " +
                      $"power={IsSystemDamaged(LastShiftShipSystem.Power)} oxygen={IsSystemDamaged(LastShiftShipSystem.Oxygen)}");
        }

        private void AdvanceRepairChannels(float deltaTime)
        {
            for (var index = 0; index < LastShiftSystemMap.SystemCount; index++)
            {
                var system = (LastShiftShipSystem)index;
                if (!repairLedger.IsChanneling(system)) continue;

                // 작업 중 부품이 제자리를 떠났다면(다른 승무원이 집어갔거나 튕겨 나갔다면) 채널을 취소한다.
                if (!IsRepairSubjectInPlace(system))
                {
                    repairLedger.CancelChannel(system);
                    Debug.Log($"[LAST_SHIFT_REPAIR] system={system} result=CANCELLED reason=subject-left-nominal");
                    continue;
                }

                if (!repairLedger.TryAdvanceChannel(system, deltaTime, out var completedMode)) continue;
                OnRepairCompleted(system, completedMode);
            }
        }

        private void LapseExpiredBypasses(float deltaTime)
        {
            for (var index = 0; index < LastShiftSystemMap.SystemCount; index++)
            {
                var system = (LastShiftShipSystem)index;
                if (!repairLedger.TryLapseBypass(system, deltaTime)) continue;

                // 재이탈은 물리적으로도 풀려야 한다. 장부만 되돌리면 부품은 계속 제자리에 붙어 있어
                // 플레이어가 "왜 다시 나빠지는지" 를 볼 방법이 없다.
                var item = FindItem(LastShiftSystemMap.RoleFor(system));
                if (item != null) item.SetSecured(false);
                Debug.Log($"[LAST_SHIFT_REPAIR] system={system} result=LAPSED reason=quick-bypass-expired lapses={repairLedger.BypassLapseCount}");
                RefreshResultAfterImpact();
            }
        }

        /// <summary>
        /// R1 수리 동사 진입점. 성능 포기는 손상 지점에 도달한 것만으로 성립하고, 나머지 두 계열은
        /// 해당 부품이 제자리에 있어야 한다. 어느 계통을 고칠지는 플레이어가 가져온 물건과
        /// 서 있는 위치가 결정한다 — 정답을 아이콘으로 알려주지 않는다.
        /// </summary>
        public bool TryBeginRepair(LastShiftRepairMode mode)
        {
            var holder = players?.FirstOrDefault(targetPlayer =>
            {
                if (targetPlayer == null || targetPlayer.HeldItem == null) return false;
                var crew = targetPlayer.GetComponent<LastShiftCrewOxygen>();
                return crew == null || !crew.IsDead;
            });
            return TryBeginRepair(mode, holder != null ? holder.HeldItem : null, CrewPosition);
        }

        /// <summary>
        /// 네트워크 경로 진입점. 어느 승무원이 무엇을 들고 어디에 서 있는지를 서버가 지정한다.
        /// 로컬 경로처럼 players 평균을 쓰면 2인 플레이에서 아무도 도달하지 않은 계통을 포기할 수 있다.
        /// </summary>
        public bool TryBeginRepair(LastShiftRepairMode mode, LastShiftGrabbable heldItem, Vector3 crewPosition)
        {
            if (!HasAppliedImpact || IsResolved) return false;
            if (!TryResolveRepairTarget(mode, heldItem, crewPosition, out var system)) return false;
            return TryBeginRepair(system, mode);
        }

        public bool TryBeginRepair(LastShiftShipSystem system, LastShiftRepairMode mode)
        {
            if (!HasAppliedImpact || IsResolved) return false;
            if (mode != LastShiftRepairMode.PerformanceSacrifice && !IsRepairSubjectInPlace(system)) return false;
            if (!repairLedger.BeginChannel(system, mode)) return false;

            if (LastShiftRecoveryTuning.DurationFor(mode) <= 0f)
            {
                OnRepairCompleted(system, mode);
                return true;
            }

            Debug.Log($"[LAST_SHIFT_REPAIR] system={system} mode={mode} result=STARTED duration={LastShiftRecoveryTuning.DurationFor(mode):F1}s");
            return true;
        }

        private void OnRepairCompleted(LastShiftShipSystem system, LastShiftRepairMode mode)
        {
            if (mode == LastShiftRepairMode.PerformanceSacrifice)
            {
                // 구역 차단은 악화를 멈추지만 회복도 없다. 부품은 쓰지 않았으므로 상태를 건드리지 않는다.
                lastTick.ThrustCeiling = LastShiftDeterioration.ResolveThrustCeiling(currentState, BuildContainment());
            }
            else
            {
                var item = FindItem(LastShiftSystemMap.RoleFor(system));
                if (item != null && !item.Secured) SecureCompletedRepair(item);
            }

            RefreshResultAfterImpact();
            Debug.Log($"[LAST_SHIFT_REPAIR] system={system} mode={mode} result=COMPLETED restored={IsSystemRestored(system)} " +
                      $"sacrificed={repairLedger.IsSacrificed(system)} bypassRemaining={repairLedger.BypassRemaining(system):F0}s " +
                      $"heat={currentState.EngineHeat:F2} bus={currentState.BusPower:F2} O2={currentState.OxygenPressure:F2}");
        }

        /// <summary>
        /// 채널 완료는 F 고정과 달리 부품을 들고 있는 동안 일어날 수 있다. 먼저 holder 쪽 참조를
        /// 정상 drop 경로로 비운 뒤 부품을 nominal 에 고정해야 stale heldItem 이 남지 않는다.
        /// 네트워크 holder/ownership 은 NetworkGrabbable 의 서버 경로로 원자적으로 정리한다.
        /// </summary>
        private void SecureCompletedRepair(LastShiftGrabbable item)
        {
            var networkItem = item.GetComponent<LastShiftNetworkGrabbable>();
            if (networkItem != null && networkItem.IsSpawned)
            {
                var holder = players?
                    .Where(targetPlayer => targetPlayer != null)
                    .Select(targetPlayer => targetPlayer.GetComponent<LastShiftNetworkPlayer>())
                    .FirstOrDefault(networkPlayer => networkPlayer != null && networkPlayer.HeldItem == networkItem);
                if (holder != null && networkItem.SecureFromServer(holder)) return;
            }

            var localHolder = players?.FirstOrDefault(targetPlayer => targetPlayer != null && targetPlayer.HeldItem == item);
            localHolder?.DropForProbe();
            item.SetSecured(true, true);
        }

        /// <summary>
        /// 어느 계통에 작업할지 해석한다. 부품 계열은 들고 있는 물건이 결정하고, 성능 포기는
        /// 가장 가까운 손상 계통을 대상으로 한다. Tether 는 어느 계통도 되돌리지 못한다.
        /// </summary>
        private bool TryResolveRepairTarget(
            LastShiftRepairMode mode,
            LastShiftGrabbable heldItem,
            Vector3 crewPosition,
            out LastShiftShipSystem system)
        {
            system = LastShiftShipSystem.Cooling;
            if (mode != LastShiftRepairMode.PerformanceSacrifice)
            {
                if (heldItem == null) return false;
                if (!LastShiftSystemMap.TryResolve(heldItem.Role, out system)) return false;
                return Vector3.Distance(heldItem.transform.position, heldItem.NominalPosition) <= SecureDistance;
            }

            var best = float.PositiveInfinity;
            var found = false;
            for (var index = 0; index < LastShiftSystemMap.SystemCount; index++)
            {
                var candidate = (LastShiftShipSystem)index;
                if (repairLedger.IsSacrificed(candidate) || IsSystemRestored(candidate)) continue;
                var item = FindItem(LastShiftSystemMap.RoleFor(candidate));
                if (item == null) continue;
                var distance = Vector3.Distance(crewPosition, item.NominalPosition);
                if (distance > SacrificeReachDistance || distance >= best) continue;
                best = distance;
                system = candidate;
                found = true;
            }
            return found;
        }

        /// <summary>부품이 제자리(nominal) 반경 안에 있는가. 들고 있는 상태도 포함한다.</summary>
        private bool IsRepairSubjectInPlace(LastShiftShipSystem system)
        {
            var item = FindItem(LastShiftSystemMap.RoleFor(system));
            return item != null && Vector3.Distance(item.transform.position, item.NominalPosition) <= SecureDistance;
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
            dockingSecondsRemaining = LastShiftRecoveryTuning.DockingTimerSeconds;
            ResetGeneration++;
            HasAppliedImpact = false;
            appliedMeteor = default;
            FirstResult = default;
            LastResult = default;
            repairLedger.Reset();
            damagedSystemMask = 0;
            verdict = LastShiftVerdict.Pending;
            lastTick = LastShiftTickReport.Idle;
            steeringInputDelayRemaining = 0f;
            hasPendingControl = false;
            sirenActive = false;
            StopSirenAudio();
            // 예비 산소는 항해 1회 예산이라 미션 중에는 절대 회복되지 않는다. 리셋만이
            // 새 항해이므로, 여기서 되돌리지 않으면 두 번째 프리셋이 이미 죽은 승무원으로 시작한다.
            if (players != null)
            {
                foreach (var targetPlayer in players)
                {
                    var crew = LastShiftCrewOxygen.Ensure(targetPlayer);
                    if (crew != null) crew.ResetCrewOxygen();
                }
            }
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
            // 승무원 재배치가 끝난 뒤의 실제 위치로 진입 엣지 기준을 잡는다.
            wasCrewAtDockingTrigger = IsCrewAtDockingTrigger();
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
            CaptureDamagedSystems();
            // 운석 직후 상태로 상한을 즉시 반영한다. 여기서 계산하지 않으면 첫 tick 까지
            // ThrustCeiling 이 1.0 으로 보이고, 열이 이미 한계인 프리셋에서 잠금이 한 프레임 늦는다.
            lastTick = LastShiftDeterioration.Tick(ref currentState, BuildContainment(), 0f);
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

        /// <summary>
        /// 조종 입력. 전력이 미복구면 <see cref="LastShiftRecoveryTuning.UnpoweredSteeringDelaySeconds"/>
        /// 만큼 반영이 늦는다 — 전력 시계가 "다른 두 복구의 속도를 뺏는" 두 번째 방식이다.
        /// 지연 중 새 입력이 오면 마지막 입력만 남기고, 지연은 다시 처음부터 센다.
        /// </summary>
        public void ApplyControl(float thrustDemand, float attitudeDegrees)
        {
            if (HasAppliedImpact && !IsResolved && lastTick.SteeringDelayed)
            {
                pendingThrust = thrustDemand;
                pendingAttitude = attitudeDegrees;
                hasPendingControl = true;
                steeringInputDelayRemaining = LastShiftRecoveryTuning.UnpoweredSteeringDelaySeconds;
                return;
            }

            CommitControl(thrustDemand, attitudeDegrees);
        }

        private void CommitControl(float thrustDemand, float attitudeDegrees)
        {
            // 엔진 보호 잠금과 구역 포기 디레이트는 플레이어 입력보다 우선한다. 여기서 걸지 않으면
            // 화살표로 0.9 를 넣어 잠금을 우회할 수 있고, 다음 tick 까지 그 값이 유효하다.
            var ceiling = HasAppliedImpact
                ? LastShiftDeterioration.ResolveThrustCeiling(currentState, BuildContainment())
                : 1f;
            currentState.ThrustDemand = Mathf.Min(Mathf.Clamp01(thrustDemand), ceiling);
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
                // 사망한 승무원은 평균에서 뺀다. 시신 위치가 남으면 살아 있는 승무원이 실제로
                // 도달하지 않은 계통까지 성능 포기 사거리 안으로 들어온다.
                var activePlayers = players?
                    .Where(targetPlayer => targetPlayer != null)
                    .Where(targetPlayer =>
                    {
                        var crew = targetPlayer.GetComponent<LastShiftCrewOxygen>();
                        return crew == null || !crew.IsDead;
                    })
                    .ToArray();
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
            DrawShipStateLine();
            GUI.Label(new Rect(28f, 157f, 650f, 52f),
                HasAppliedImpact
                    ? $"FIRST DOMINANT: {FirstResult.Problem}\nCURRENT DOMINANT: {LastResult.Problem}"
                    : "FIRST DOMINANT PROBLEM: pending meteor", headingStyle);
            GUI.Label(new Rect(28f, 215f, 650f, 84f), HasAppliedImpact ? LastResult.CauseChain : "Preset only configures pre-impact state. Press M to apply the canonical meteor once.", bodyStyle);
            DrawSuitOxygenGauges();
        }

        /// <summary>
        /// 산소 칸만 따로 그린다. N9 사이렌 동안 이 칸이 적색 점멸해야 하는데, 한 줄에 묶어
        /// 그리면 다른 수치까지 함께 붉어져 "무엇이 위험한가" 가 사라진다.
        /// </summary>
        private void DrawShipStateLine()
        {
            sirenStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(28f, 106f, 650f, 24f),
                $"INPUT state: thrust={currentState.ThrustDemand:F2} bus={currentState.BusPower:F2} " +
                $"hull={currentState.HullIntegrity:F2} heat={currentState.EngineHeat:F2} attitude={currentState.ShipAttitudeDegrees:F0} damage={currentState.ExistingDamage:F2}", bodyStyle);

            sirenStyle.normal.textColor = sirenActive
                ? Color.Lerp(new Color(0.88f, 0.94f, 1f), new Color(1f, 0.25f, 0.18f), BlinkPhase)
                : new Color(0.88f, 0.94f, 1f);
            var sirenSuffix = sirenActive ? "  ⚠ 전선 경보: 산소 위험 (전 구역)" : string.Empty;
            GUI.Label(new Rect(28f, 130f, 650f, 24f), $"O2 {currentState.OxygenPressure:F2}{sirenSuffix}", sirenStyle);
        }

        /// <summary>
        /// N8 조건부 개인 예비 산소 막대. 소모가 시작된 승무원에게만 나타난다. 사이렌 시점에
        /// 이 막대를 띄우지 않는 것이 의도다 — 겹치면 사이렌이 곧 사망 예고가 된다.
        /// </summary>
        private void DrawSuitOxygenGauges()
        {
            if (players == null) return;
            var row = 0;
            foreach (var targetPlayer in players)
            {
                if (targetPlayer == null) continue;
                var crew = targetPlayer.GetComponent<LastShiftCrewOxygen>();
                if (crew == null || !crew.ShowsSuitGauge) continue;
                LastShiftCrewOxygen.DrawGauge(crew, targetPlayer.PlayerSlot.ToString(), row, ref sirenStyle);
                row++;
            }
        }

        /// <summary>적색 점멸 위상. 사이렌 칸과 예비 막대가 같은 박자로 뛰어야 같은 사건으로 읽힌다.</summary>
        private static float BlinkPhase => LastShiftCrewOxygen.BlinkPhase;
    }
}
