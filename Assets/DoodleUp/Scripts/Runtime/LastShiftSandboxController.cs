using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoodleUp.Runtime
{
    public sealed class LastShiftSandboxController : MonoBehaviour
    {
        public const string ProfileId = "LAST_SHIFT_SP01";
        public const float SecureDistance = 0.9f;
        public static readonly Vector3 PlayerSpawn = LastShiftShipDimensions.SpawnPoint;

        /// <summary>
        /// 성능 포기는 물건을 들고 오지 않아도 손상 지점에서 결정할 수 있다. 대신 그 지점에
        /// 실제로 가 있어야 하므로 <see cref="SecureDistance"/> 보다 조금 넉넉한 도달 거리를 쓴다.
        /// </summary>
        public const float SacrificeReachDistance = 1.8f;

        /// <summary>도킹 확정 트리거. 조종석 끝벽(선수) 앞이다.</summary>
        public static readonly Vector3 DockingTriggerPosition = LastShiftShipDimensions.DockingPoint;
        public const float DockingTriggerRadius = 1.6f;

        [SerializeField] private LastShiftPlayerController[] players;
        [SerializeField] private LastShiftGrabbable[] items;
        [SerializeField] private LastShiftPreset currentPreset;
        [SerializeField] private LastShiftShipState currentState;

        private readonly LastShiftControlHold controlHold = new();
        private readonly LastShiftRepairLedger repairLedger = new();
        private LastShiftZonePressures zonePressures = LastShiftZonePressures.Uniform(1f);
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
        private LastShiftDoorState doorState = LastShiftDoorState.AllOpen;

        /// <summary>
        /// 승강구 해치. 문과 달리 <b>닫힌 상태로 시작</b>한다 — 근거는
        /// <see cref="LastShiftHatchState.AllClosed"/> 주석에 있다.
        /// </summary>
        private LastShiftHatchState hatchState = LastShiftHatchState.AllClosed;

        // 판독(T2)이 읽는 미억제 계통 마스크. 클라이언트에서는 손상 판정과 수리 장부가 없어
        // 같은 식을 다시 계산할 수 없으므로 서버가 접은 값을 그대로 받는다.
        private byte replicatedUncontainedSystemMask;
        private bool usesReplicatedState;

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
        /// <summary>
        /// N6 상황 평가층. <b>이 카드 전까지 런타임에서 한 번도 만들어진 적이 없었다</b> —
        /// 클래스는 완성돼 있었고 EditMode 테스트만 그것을 알고 있었다. HUD 가 "무엇이
        /// 위험한가" 를 못 보여준 근본 원인이 이것이고, 그래서 원시 수치 덤프가 남아 있었다.
        /// </summary>
        private readonly LastShiftSituationTracker situationTracker = new();

        /// <summary>§5.4 디버그 층 표시 여부. <b>기본 OFF</b> 이고 <c>F3</c> 으로 토글한다.</summary>
        private bool debugHudVisible;

        /// <summary>계통별 현재 대표 상황(열·전력·추진). 산소는 구역별이라 아래를 쓴다.</summary>
        public LastShiftSituation SituationOf(LastShiftSystemChannel channel) =>
            situationTracker.StatusOf(channel).Situation;

        /// <summary>구역별 산소 대표 상황.</summary>
        public LastShiftSituation OxygenSituationOf(LastShiftZone zone) =>
            situationTracker.OxygenStatusOf(zone).Situation;

        /// <summary>
        /// CT-01 §5.2 구역 칸 하나가 표시할 등급. <b>그 구역 계통과 그 구역 산소 중 높은 쪽</b>이다.
        /// 산소실은 대응 계통이 없어 산소만 본다.
        /// </summary>
        public LastShiftSituationGrade ZoneGradeOf(LastShiftZone zone)
        {
            var grade = LastShiftSituationTable.GradeOf(OxygenSituationOf(zone));
            if (!LastShiftSituationText.TryChannelOfZone(zone, out var channel)) return grade;

            var channelGrade = LastShiftSituationTable.GradeOf(SituationOf(channel));
            return channelGrade > grade ? channelGrade : grade;
        }

        /// <summary>그 구역 칸의 등급을 만든 상황. 구역 안에서만 읽히는 원인 1행이 이걸 쓴다.</summary>
        public LastShiftSituation DominantSituationOf(LastShiftZone zone)
        {
            var oxygen = OxygenSituationOf(zone);
            if (!LastShiftSituationText.TryChannelOfZone(zone, out var channel)) return oxygen;

            var channelSituation = SituationOf(channel);
            return LastShiftSituationTable.GradeOf(channelSituation) > LastShiftSituationTable.GradeOf(oxygen)
                ? channelSituation
                : oxygen;
        }

        public float ThrustCeiling => lastTick.ThrustCeiling;
        public bool HeatProtectionEngaged => lastTick.HeatProtectionEngaged;
        public bool SteeringDelayed => lastTick.SteeringDelayed;
        public bool OxygenPumpRunning => lastTick.OxygenPumpRunning;
        public int SacrificeCount => repairLedger.SacrificeCount;
        public int BypassLapseCount => repairLedger.BypassLapseCount;

        /// <summary>S-O3 전선 사이렌(N9). 모든 구역에서 들리는 P0 유일의 국소 정보 예외다.</summary>
        public bool SirenActive => sirenActive;

        /// <summary>
        /// N0 구역별 산소 압력. <see cref="CurrentState"/>.OxygenPressure 는 이 중 조종석 값의
        /// 파생이다 — 도킹 성공 판정이 세 구역 평균이 아니라 조종석 하나이기 때문이다(기획 §2.2).
        /// </summary>
        public LastShiftZonePressures ZonePressures => zonePressures;

        public float PressureOf(LastShiftZone zone) => zonePressures[zone];

        /// <summary>
        /// 개구부 너머 상태의 3단계 등급(CT-10 T2). 개구부 프레임에 붙는 게이지가 읽는 값이다.
        ///
        /// <b>문 상태를 보지 않는다.</b> 여기 어디에도 <see cref="doorState"/> 가 들어가지
        /// 않는다 — 열어야 수치가 보이면 "확인" 과 "압력 혼합" 이 같은 동작이 되어 판단이
        /// 사라진다(기획 §3.1.2). 닫힌 문 앞에서도 같은 값이 읽힌다.
        ///
        /// <b>얼마나 떨어져서 읽히는가는 여기서 정하지 않는다.</b> 그건 게이지의 크기·밝기이고
        /// 아트 소관이다. 이 API 는 거리와 무관하게 언제나 같은 값을 돌려준다.
        /// </summary>
        public LastShiftDistressReading DistressOf(LastShiftZone zone)
        {
            return LastShiftDoorDistress.Evaluate(
                zone, zonePressures[zone], IsZoneVacuum(zone), WorstUncontainedClockProgress(zone));
        }

        /// <summary>
        /// 이 개구부 너머 공간의 판독값. 보는 사람의 x 로 어느 쪽이 "너머" 인지 정한다.
        ///
        /// 개구부 1·2 는 x 가 구역 판정 경계와 <b>같은 값</b>이라, 경계 평면에서 ε 만큼 민
        /// 좌표로 구역을 정하면 부호를 한 번 잘못 잡았을 때 판독이 통째로 반대편 구역을
        /// 가리키고도 값이 그럴듯해서 안 보인다. 그래서 방·통로의 <b>중심</b>으로 판정한다.
        /// </summary>
        public LastShiftDistressReading DistressBeyondOpening(int opening, float viewerX)
        {
            var beyondX = viewerX <= LastShiftShipDimensions.OpeningX(opening)
                ? LastShiftShipDimensions.SpaceCenterXAfter(opening)
                : LastShiftShipDimensions.SpaceCenterXBefore(opening);
            return DistressOf(LastShiftZoneAtlas.Resolve(new Vector3(beyondX, 0f, 0f)));
        }

        /// <summary>
        /// 개구부에 붙은 <b>게이지가 실제로 표시하는</b> 판독값. 게이지는 통로 쪽 한 면에만
        /// 달리므로 보는 사람이 어디에 있든 값이 같다.
        ///
        /// <see cref="DistressBeyondOpening"/> 를 단면화하지 않고 접근자를 따로 두는 이유는
        /// <b>두 가지 "양쪽이 같음" 이 성질이 다르기 때문</b>이다. 개구부 0·3 이 양쪽에서 같은
        /// 값을 내는 것은 방과 통로가 같은 구역이라는 <b>기하 사실</b>이고, 개구부 1·2 가 한 값을
        /// 내는 것은 게이지를 한쪽에만 달기로 한 <b>배치 결정</b>이다. 한 함수로 합치면 배치가
        /// 바뀔 때 둘 중 하나만 움직여야 하는데 어느 쪽이 움직여야 하는지가 코드에서 사라진다.
        ///
        /// 그래서 이 접근자는 방향을 스스로 정하지 않고 <see cref="LastShiftShipDimensions.GaugeViewerX"/>
        /// 에 묻는다 — "엔진실" 을 값으로 박아 두면 통로가 늘거나 게이지가 옮겨갈 때 조용히 틀린다.
        /// </summary>
        public LastShiftDistressReading GaugeReading(int opening)
        {
            return DistressBeyondOpening(opening, LastShiftShipDimensions.GaugeViewerX(opening));
        }

        /// <summary>
        /// 이 구역에서 아직 억제되지 않은 손상 계통 중 가장 진행한 시계. 손상이 없으면 음수다.
        ///
        /// 계통이 어느 구역에 속하는지는 부품 정위치가 정한다 — PatchPlate 가 산소실에 있으니
        /// 파공은 산소실 일이다. 그 대응이 곧 "고치러 그 구역까지 간다" 이고, 판독도 같은
        /// 대응을 써야 게이지가 가리키는 구역과 실제로 가야 하는 구역이 어긋나지 않는다.
        /// </summary>
        private float WorstUncontainedClockProgress(LastShiftZone zone)
        {
            var worst = -1f;
            var mask = UncontainedSystemMask;
            for (var index = 0; index < LastShiftSystemMap.SystemCount; index++)
            {
                if ((mask & (1 << index)) == 0) continue;
                var system = (LastShiftShipSystem)index;
                if (ZoneOfSystem(system) != zone) continue;
                worst = Mathf.Max(worst, LastShiftDoorDistress.ClockProgress(currentState, system));
            }
            return worst;
        }

        /// <summary>
        /// 손상됐고 아직 억제되지 않은 계통. 판독(T2)이 이 마스크 하나만 읽는다.
        ///
        /// 마스크로 접어 두는 이유는 클라이언트다. 손상 판정(<see cref="damagedSystemMask"/>)과
        /// 수리 장부의 완료 플래그는 서버에만 있고 스냅샷에는 성능 포기 마스크만 실린다. 클라이언트가
        /// 같은 식을 다시 계산하면 "고쳤는데 게이지는 계속 위험" 이 되는데, 그건 화면이 조용히
        /// 틀리는 형태라 눈에 띄지 않는다. 그래서 서버가 접은 결과를 그대로 실어 보낸다.
        /// </summary>
        public byte UncontainedSystemMask =>
            usesReplicatedState ? replicatedUncontainedSystemMask : ComputeUncontainedSystemMask();

        private byte ComputeUncontainedSystemMask()
        {
            byte mask = 0;
            var containment = BuildContainment();
            for (var index = 0; index < LastShiftSystemMap.SystemCount; index++)
            {
                var system = (LastShiftShipSystem)index;
                if (!IsSystemDamaged(system) || IsSystemContained(containment, system)) continue;
                mask |= (byte)(1 << index);
            }
            return mask;
        }

        private static bool IsSystemContained(in LastShiftContainment containment, LastShiftShipSystem system) =>
            system switch
            {
                LastShiftShipSystem.Cooling => containment.CoolingContained,
                LastShiftShipSystem.Power => containment.PowerContained,
                _ => containment.OxygenContained
            };

        /// <summary>계통이 걸린 구역. 부품 정위치가 정본이며 <see cref="BreachZone"/> 과 같은 규칙이다.</summary>
        private LastShiftZone ZoneOfSystem(LastShiftShipSystem system)
        {
            var item = FindItem(LastShiftSystemMap.RoleFor(system));
            if (item != null) return LastShiftZoneAtlas.Resolve(item.NominalPosition);
            // 부품이 없는 최소 조립에서는 치수 정본의 정위치를 본다. Vector3.zero 로 떨어지면
            // 세 계통이 전부 엔진실로 몰려 판독이 한 구역에만 쌓인다.
            return LastShiftZoneAtlas.Resolve(system switch
            {
                LastShiftShipSystem.Cooling => LastShiftShipDimensions.CoolingNominal,
                LastShiftShipSystem.Power => LastShiftShipDimensions.BatteryNominal,
                _ => LastShiftShipDimensions.PatchPlateNominal
            });
        }

        /// <summary>구역 문 개폐 상태. 닫힌 경계는 압력 교환이 0 이 된다(기획 §2.2.1).</summary>
        public LastShiftDoorState Doors => doorState;

        public bool IsDoorOpen(int boundary) => doorState[boundary];

        /// <summary>
        /// 문 개폐 결과를 시뮬레이션에 반영한다. 개폐 애니메이션과 0.8초 소요는 문 쪽
        /// (<see cref="LastShiftZoneDoor"/>)이 갖고, 여기는 "지금 열려 있는가" 만 받는다.
        /// 평준화가 이 값을 매 tick 읽으므로 문이 닫히는 순간 전파가 그 자리에서 멈춘다.
        /// </summary>
        public void SetDoorOpen(int boundary, bool open)
        {
            if (doorState[boundary] == open) return;
            doorState[boundary] = open;
            Debug.Log($"[LAST_SHIFT_DOOR] generation={ResetGeneration} boundary={boundary} " +
                      $"({LastShiftZoneAtlas.ShortLabelOf(LastShiftZoneAtlas.LowZoneOf(boundary))}↔{LastShiftZoneAtlas.ShortLabelOf(LastShiftZoneAtlas.HighZoneOf(boundary))}) " +
                      $"state={(open ? "OPEN" : "CLOSED")} pressures[{zonePressures}]");
        }

        /// <summary>
        /// 갑판 승강구 해치의 개폐 상태(§23.6). <see cref="Doors"/> 와 <b>따로</b> 두는 이유는
        /// <see cref="LastShiftHatchState"/> 주석에 있다 — 저쪽은 압력 평준화가 읽는 구역 경계
        /// 배열이고 §24 가 <c>4</c>구역으로 고정한 그것이다.
        /// </summary>
        public LastShiftHatchState Hatches => hatchState;

        public bool IsHatchOpen(int shaft) => hatchState[shaft];

        /// <summary>
        /// 해치 개폐 결과를 반영한다. 개폐 애니메이션과 <c>0.8초</c> 소요는 해치 쪽
        /// (<see cref="LastShiftDeckHatch"/>)이 갖고 여기는 "지금 열려 있는가" 만 받는다 —
        /// 문과 같은 구조다.
        ///
        /// <b>평준화는 이 값을 안 읽는다.</b> 덕트는 <c>ZonePressure</c> 슬롯이 없으므로(§24)
        /// 해치가 열려도 교환할 상대 구역이 없다. 여기서 열리는 것은 압력이 아니라 통행이고,
        /// 산소 비용은 <see cref="IsZoneVacuum(Vector3)"/> 가 위치로 물린다(§5).
        /// </summary>
        public void SetHatchOpen(int shaft, bool open)
        {
            if (hatchState[shaft] == open) return;
            hatchState[shaft] = open;
            Debug.Log($"[LAST_SHIFT_HATCH] generation={ResetGeneration} shaft={shaft} " +
                      $"({LastShiftZoneAtlas.ShortLabelOf(LastShiftZoneAtlas.Resolve(LastShiftBypassDuct.ShaftMouth(shaft)))}) " +
                      $"state={(open ? "OPEN" : "CLOSED")}");
        }

        /// <summary>
        /// 파공이 난 구역. 봉합 부품(PatchPlate)의 제자리가 곧 파공 지점이므로 그 구역이
        /// 새는 구역이고, 산소 계통을 성능 포기로 밀폐할 때 버려지는 구역도 같다.
        /// 부품이 없는 최소 조립에서는 씬 배치대로 생명유지 구역으로 본다.
        /// </summary>
        public LastShiftZone BreachZone
        {
            get
            {
                var patch = FindItem(LastShiftItemRole.PatchPlate);
                return patch != null ? LastShiftZoneAtlas.Resolve(patch.NominalPosition) : LastShiftZone.LifeSupport;
            }
        }

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
            // OxygenPressure 는 조종석 압력의 파생값이므로, 이 경계로 압력을 써 넣는 것은
            // "조종석을 이 압력으로 만든다" 는 뜻이다. 구역 값을 함께 옮기지 않으면 다음 tick 이
            // 예전 구역 압력에서 파생값을 다시 계산해 방금 쓴 값을 지운다.
            var pressureChanged = !Mathf.Approximately(state.OxygenPressure, currentState.OxygenPressure);
            currentState = state;
            if (pressureChanged) OverrideZonePressuresForProbe(LastShiftZonePressures.Uniform(state.OxygenPressure));
        }

        /// <summary>구역별 압력을 직접 조립하는 테스트 경계. 게임 코드에서는 호출하지 않는다.</summary>
        public void OverrideZonePressuresForProbe(in LastShiftZonePressures pressures)
        {
            zonePressures = pressures;
            currentState.OxygenPressure = zonePressures[LastShiftZone.Cockpit];
        }

        public void ApplyNetworkSnapshot(in LastShiftNetworkSnapshot value)
        {
            // 클라이언트는 ApplyMeteorImpact 를 돌리지 않으므로 충격 연출 트리거가 없다.
            // 스냅샷의 ImpactApplicationCount 증가가 곧 "서버에서 충격이 터졌다" 이므로
            // 그 변화를 연출 트리거로 쓴다. 리셋으로 카운트가 유지되는 동안은 재생하지 않는다.
            var impactAdvanced = value.HasAppliedImpact && value.ImpactApplicationCount > ImpactApplicationCount;

            currentPreset = value.Preset;
            currentState = value.ShipState;
            // 조종석 압력은 ShipState.OxygenPressure 가 나른다. 나머지 둘만 스냅샷에서 받는다.
            zonePressures = new LastShiftZonePressures(
                value.ShipState.OxygenPressure, value.PowerPressure, value.CoolingPressure, value.LifeSupportPressure);
            doorState = new LastShiftDoorState
            {
                Boundary0Open = value.Boundary0DoorOpen,
                Boundary1Open = value.Boundary1DoorOpen,
                Boundary2Open = value.Boundary2DoorOpen
            };
            hatchState = new LastShiftHatchState
            {
                ForeOpen = value.ForeHatchOpen,
                AftOpen = value.AftHatchOpen
            };
            dockingSecondsRemaining = value.DockingSecondsRemaining;
            ResetGeneration = value.ResetGeneration;
            ImpactApplicationCount = value.ImpactApplicationCount;
            HasAppliedImpact = value.HasAppliedImpact;
            verdict = value.Verdict;
            repairLedger.ApplyReplicatedSacrificeMask(value.SacrificedSystemMask);
            replicatedUncontainedSystemMask = value.UncontainedSystemMask;
            usesReplicatedState = true;
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
            // 구역 등급도 사이렌과 같은 이유로 여기서 다시 평가한다. 클라이언트는
            // AdvanceMission 을 안 돌리므로 이 줄이 없으면 HUD 4칸이 영영 "정상" 이다 —
            // 상황을 스냅샷 필드로 늘리지 않는 것은 이미 동기화되는 상태·압력만으로
            // 같은 값이 나오기 때문이다(평가는 순수 계산이다).
            situationTracker.Evaluate(
                LastShiftSituationInput.From(currentState, zonePressures, BuildContainment()), 0f);
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

                // §5.4. 원시 수치를 지우지 않고 이 토글 뒤로 옮겼다 — 개발 중에는 계속
                // 필요하고, 플레이어 화면에 상시로 두면 셋째 층이 다시 새어 나온다.
                if (keyboard.f3Key.wasPressedThisFrame) debugHudVisible = !debugHudVisible;
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

            // 상황 평가는 <b>운석 게이트 위</b>에 있다. 아래 조기 반환은 손상 tick 을 막는
            // 것이지 "아직 볼 것이 없다" 는 뜻이 아니다 — 프리셋은 t=0 에 이미 상황을 켠다
            // (BadAttitudeHighOxygen 은 S-T1·S-O1 둘이 동시에 첫 활성이다). 이 평가를
            // 게이트 아래 두면 SP-01 의 승인 기준인 "세 프리셋의 첫 지배 문제가 다르다" 를
            // 운석을 쏘기 전에는 화면에서 확인할 수 없다.
            situationTracker.Evaluate(
                LastShiftSituationInput.From(currentState, zonePressures, BuildContainment()), deltaTime);

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

            lastTick = LastShiftDeterioration.Tick(
                ref currentState, ref zonePressures, BuildContainment(), BreachZone, doorState, deltaTime);
            RefreshResultAfterImpact();

            dockingSecondsRemaining = Mathf.Max(0f, dockingSecondsRemaining - deltaTime);

            UpdateSiren();
            AdvanceCrewOxygen(deltaTime);

            // 연속 판정은 이제 원인이 둘이다 — 전원 예비산소 고갈(N2)과 연료 소진 표류(CT-06 N3).
            // 트리거 문자열을 하나로 두면 로그만 보고는 어느 쪽으로 끝났는지 알 수 없다.
            var continuous = LastShiftVerdictResolver.EvaluateContinuous(currentState, AnyCrewAlive);
            if (continuous != LastShiftVerdict.Pending)
            {
                SettleVerdict(continuous, continuous == LastShiftVerdict.FailureAdrift
                    ? "fuel-exhausted-dock-progress-short"
                    : "all-crew-suit-oxygen-depleted");
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
                      $"bus={currentState.BusPower:F2} fuel={currentState.FuelReserve:F3} dock={currentState.DockProgress:F1} " +
                      $"T-{dockingSecondsRemaining:F0}s sacrifices={repairLedger.SacrificeCount} bypassLapses={repairLedger.BypassLapseCount}");
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
        /// 이 위치가 진공인가. N0 이후 판정 대상은 <b>그 위치가 속한 구역의</b> 압력이다.
        /// 산소 계통을 성능 포기로 밀폐했다면 밀폐한 구역만 압력과 무관하게 진공이다.
        /// </summary>
        /// <summary>
        /// 이 좌표의 승무원이 진공에 노출돼 있는가.
        ///
        /// <b>갑판 하부 우회 통로를 먼저 본다.</b> <see cref="LastShiftZoneAtlas.Resolve"/> 는
        /// x 하나로 구역을 정하므로 갑판 아래를 구분하지 못한다 — 그대로 두면 덕트 안
        /// 승무원이 머리 위 방의 압력을 그대로 받아 산소를 안 태우고, 그러면 §5 가 우회로에
        /// 걸어 둔 유일한 비용이 사라져 "급할 때만 쓰는 진짜 우회로" 가 지름길이 된다.
        /// </summary>
        public bool IsZoneVacuum(Vector3 position)
        {
            if (LastShiftBypassDuct.IsUnpressurizedSpace(position)) return true;
            return IsZoneVacuum(LastShiftZoneAtlas.Resolve(position));
        }

        public bool IsZoneVacuum(LastShiftZone zone)
        {
            var sealedOff = repairLedger.IsSacrificed(LastShiftShipSystem.Oxygen) && zone == SealedZone;
            return LastShiftVerdictResolver.IsZoneVacuum(zonePressures[zone], sealedOff);
        }

        /// <summary>
        /// 산소 계통을 포기했을 때 밀폐되는 구역. 파공 지점이 있는 구역이며, 씬 빌더의
        /// PatchPlate 배치(x≈4.5, 생명유지 구역)와 함께 움직인다.
        /// </summary>
        private LastShiftZone SealedZone => BreachZone;

        /// <summary>
        /// N9 S-O3 전선 사이렌. 발동 0.15 / 해제 0.20 이라 경계에서 떨리지 않는다.
        /// 예비 산소 소모(압력 0.00)와는 절대 겹치지 않는다 — 겹치면 사이렌이 곧 사망 예고가 되어
        /// 24초 대응 창이 사라진다.
        /// </summary>
        private void UpdateSiren()
        {
            var next = LastShiftVerdictResolver.EvaluateSiren(zonePressures, sirenActive);
            if (next == sirenActive)
            {
                if (next) EnsureSirenAudioPlaying();
                return;
            }

            sirenActive = next;
            if (next)
            {
                EnsureSirenAudioPlaying();
                Debug.Log($"[LAST_SHIFT_SIREN] generation={ResetGeneration} state=ON lowest={zonePressures.Lowest:F2} " +
                          $"zones[{zonePressures}] scope=all-zones");
            }
            else
            {
                StopSirenAudio();
                Debug.Log($"[LAST_SHIFT_SIREN] generation={ResetGeneration} state=OFF lowest={zonePressures.Lowest:F2}");
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
                //
                // A2(구역별 감쇠)에서 <b>유일한 예외</b>다. 값은 그대로 0 이고 이름만 붙였다 —
                // 리터럴 0f 는 "3D 로 일괄 전환" 작업에서 그냥 지워지지만
                // ShipWideSpatialBlend 는 그 자리에서 왜 2D 인지를 읽게 만든다.
                LastShiftZoneAudio.ConfigureShipWide(sirenAudio);
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
            // 프리셋 초기 압력은 세 구역 동일이다(기획 §2.2 A-2 초기값). 문도 전부 열린 상태로
            // 시작한다 — 격리는 플레이어가 내리는 판단이지 시작 조건이 아니다(§2.7 자동 격리 금지).
            zonePressures = LastShiftZonePressures.Uniform(currentState.OxygenPressure);
            doorState = LastShiftDoorState.AllOpen;
            // 래치를 안 지우면 이전 프리셋의 상황이 다음 프리셋 화면에 남는다 — 프리셋마다
            // 첫 지배 문제가 다른지를 보는 카드에서 그건 곧 오답이다.
            situationTracker.Reset();
            // 해치는 반대로 전부 닫고 시작한다. 리셋 직후 갑판에 구멍이 남아 있으면 프리셋이
            // 제자리에 놓은 부품이 저중력에서 그리로 빠져 시작 상태가 프리셋과 달라진다.
            hatchState = LastShiftHatchState.AllClosed;
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

            currentState = LastShiftMeteorApplication.Apply(meteor, currentState, ref zonePressures, BreachZone, items);
            appliedMeteor = meteor;
            HasAppliedImpact = true;
            ImpactApplicationCount++;
            FirstResult = ResolveCurrentState(appliedMeteor);
            LastResult = FirstResult;
            CaptureDamagedSystems();
            // 운석 직후 상태로 상한을 즉시 반영한다. 여기서 계산하지 않으면 첫 tick 까지
            // ThrustCeiling 이 1.0 으로 보이고, 열이 이미 한계인 프리셋에서 잠금이 한 프레임 늦는다.
            lastTick = LastShiftDeterioration.Tick(
                ref currentState, ref zonePressures, BuildContainment(), BreachZone, doorState, 0f);
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

        /// <summary>
        /// CT-01 §5 플레이어 HUD. <b>층이 셋이고 층마다 노출 조건이 다르다</b>(§5.6.3).
        ///
        /// <list type="number">
        /// <item>개인·공용 계기 — 목표 줄과 임계선 막대. 상시. 자기 자신과 조종석 계기판
        /// 값이라 <c>concept-draft.md:166</c> 이 막는 "남의 구역 비밀" 이 아니다</item>
        /// <item>구역 등급 4칸 — 정상/불안정/고장/위기 <b>카테고리만</b>. 상시·전 구역.
        /// 이미 있는 전선 사이렌과 같은 층위다</item>
        /// <item>정확한 수치와 원인 1행 — 거리 조건부. <c>166</c> 이 실제로 막는 지점이 여기다</item>
        /// </list>
        ///
        /// <b>예전 화면이 못 읽혔던 이유는 셋이 안 갈리고 전부 상시였기 때문이다</b>(§5.6.2).
        /// 원시 수치는 지우지 않고 <c>F3</c> 디버그 층으로 통째로 옮겼다(§5.4).
        /// </summary>
        private void OnGUI()
        {
            headingStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            bodyStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = new Color(0.88f, 0.94f, 1f) } };

            GUI.Box(new Rect(16f, 16f, 680f, 214f), GUIContent.none);
            DrawObjectiveLine();
            DrawThresholdBars();
            DrawZonePressureCells(28f, 150f);
            DrawLocalDiagnosis();
            DrawSuitOxygenGauges();
            if (debugHudVisible) DrawDebugHud();
        }

        /// <summary>
        /// 층1-a 목표 줄. 성공 조건 둘과 남은 시간. <b>숫자는 시간만 노출한다</b>(§5.2) —
        /// 성공 조건의 임계는 아래 막대의 선으로 그려지지 텍스트로 적히지 않는다.
        /// </summary>
        private void DrawObjectiveLine()
        {
            var minutes = Mathf.FloorToInt(dockingSecondsRemaining / 60f);
            var seconds = Mathf.FloorToInt(dockingSecondsRemaining % 60f);
            GUI.Label(new Rect(28f, 24f, 480f, 28f), "목표 — 추력과 산소를 선 위로 올려 도킹", headingStyle);
            GUI.Label(new Rect(508f, 24f, 180f, 28f), $"DOCK T-{minutes}:{seconds:00}", headingStyle);
        }

        /// <summary>
        /// 층1-b 임계선 막대 둘. <b>임계를 그림으로 보여주는 것이 요점이다</b>(§5.2) —
        /// <c>thrust=0.28</c> 이라는 숫자보다 "막대가 선 아래다" 가 즉시 읽힌다.
        /// 산소는 조종석 압력을 쓴다. 도킹 성공 판정이 보는 값이 그것이라서다.
        /// </summary>
        private void DrawThresholdBars()
        {
            DrawThresholdBar(28f, 62f, "추력", currentState.ThrustDemand,
                LastShiftRecoveryTuning.DockingSuccessThrust);
            DrawThresholdBar(28f, 96f, "산소", zonePressures[LastShiftZone.Cockpit],
                LastShiftRecoveryTuning.DockingSuccessOxygen);
        }

        private void DrawThresholdBar(float x, float y, string label, float value, float threshold)
        {
            const float barWidth = 520f;
            const float barHeight = 18f;
            var fill = Mathf.Clamp01(value);
            var below = fill < threshold;

            GUI.Label(new Rect(x, y, 46f, 22f), label, bodyStyle);
            var barX = x + 50f;
            GUI.DrawTexture(new Rect(barX, y + 2f, barWidth, barHeight), Texture2D.grayTexture);
            // 선 아래면 색이 바뀐다. 길이만으로는 "모자라다" 가 안 읽히고, 그 판정을
            // 플레이어가 눈대중으로 하게 두면 임계선을 그린 의미가 없다.
            GUI.color = below ? new Color(1f, 0.45f, 0.2f) : new Color(0.45f, 0.85f, 1f);
            GUI.DrawTexture(new Rect(barX, y + 2f, barWidth * fill, barHeight), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(barX + barWidth * Mathf.Clamp01(threshold) - 1f, y, 2f, barHeight + 4f),
                Texture2D.whiteTexture);
        }

        /// <summary>
        /// 층3 — <b>구역 안에서만</b> 원인 1행이 나온다(§5.3). 밖에서는 등급 칸까지가 전부다.
        ///
        /// 관측자는 로컬 플레이어 하나다. 솔로/호스트에서는 자기 캐릭터가 서 있는 구역이고,
        /// 아무도 없으면(에디터에서 플레이어를 안 띄운 경우) 아무것도 안 그린다 — 여기서
        /// 조종석을 기본값으로 삼으면 "구역 안" 조건이 사실상 사라진다.
        /// </summary>
        private void DrawLocalDiagnosis()
        {
            if (!TryResolveLocalZone(out var zone)) return;

            var situation = DominantSituationOf(zone);
            var cause = LastShiftSituationText.CauseLine(situation);
            if (string.IsNullOrEmpty(cause)) return;

            GUI.Label(new Rect(28f, 186f, 650f, 24f),
                $"[{LastShiftZoneAtlas.ShortLabelOf(zone)}] {cause}", headingStyle);
        }

        private bool TryResolveLocalZone(out LastShiftZone zone)
        {
            zone = default;
            if (players == null) return false;
            foreach (var candidate in players)
            {
                if (candidate == null) continue;
                zone = LastShiftZoneAtlas.Resolve(candidate.transform.position);
                return true;
            }

            return false;
        }

        /// <summary>구역 압력 줄 하나의 폭. 클라이언트가 자기 상자를 이 폭에 맞춘다.</summary>
        public const float ZonePressureRowWidth = 34f + 3f * 132f + 2f * 52f;

        /// <summary>
        /// N10 구역 3칸 압력 표시. 격리(문 닫기)의 즉시 가시성이 여기에 걸려 있다 —
        /// 이 칸이 없으면 플레이어는 문을 닫고도 그것이 효과가 있었는지 알 수 없다(기획 §2.2.1).
        ///
        /// 칸 사이에 문 상태를 그린다. 세 칸을 그냥 나열하면 "왜 이 칸만 안 떨어지는가" 가
        /// 안 읽히고, 그 답이 곧 문이기 때문이다.
        ///
        /// 좌표를 인자로 받는 이유는 호출자가 둘이기 때문이다. 솔로/호스트는 sandbox 패널 안에
        /// 끼워 그리고, 클라이언트는 <see cref="LastShiftNetworkPlayer"/> 가 자기 상자를 만들어
        /// 그린다 — 클라이언트에서는 이 컴포넌트가 <c>enabled = IsServer</c> 로 꺼져 있어
        /// OnGUI 가 돌지 않지만, 스냅샷으로 들어온 압력·문 상태 자체는 여기에 최신으로 들어 있다.
        /// </summary>
        public void DrawZonePressureCells(float originX, float originY)
        {
            bodyStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, normal = { textColor = new Color(0.88f, 0.94f, 1f) } };
            sirenStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            sirenStyle.normal.textColor = sirenActive
                ? Color.Lerp(new Color(0.88f, 0.94f, 1f), new Color(1f, 0.25f, 0.18f), BlinkPhase)
                : new Color(0.88f, 0.94f, 1f);

            const float cellWidth = 132f;
            const float doorWidth = 52f;
            var x = originX;
            GUI.Label(new Rect(x, originY, 34f, 24f), "구역", sirenStyle);
            x += 34f;

            for (var index = 0; index < LastShiftZoneAtlas.ZoneCount; index++)
            {
                var zone = (LastShiftZone)index;
                if (index > 0)
                {
                    // 경계 index-1 이 이 두 칸을 잇는 문이다. 문 상태는 수치가 아니라
                    // 격리 여부라 상시로 둔다 — 이게 없으면 "왜 이 칸만 안 떨어지는가" 가
                    // 안 읽히고, 개구부2(전력실↔냉각실)는 거리 판독 대신 이 문이 차단을
                    // 전담한다(CT-01 §5.6.4).
                    var open = doorState[index - 1];
                    GUI.Label(new Rect(x, originY, doorWidth, 24f), open ? "─┤├─" : "─┫┣─", bodyStyle);
                    x += doorWidth;
                }

                // <b>등급만 쓴다. 압력 수치를 여기 적지 않는다</b>(§5.2) — 그게 예전 화면이
                // 셋째 층을 상시로 새어 보내던 자리다. 수치는 개구부 앞에서만 읽힌다(§5.3).
                var grade = ZoneGradeOf(zone);
                var style = grade == LastShiftSituationGrade.Crisis ? sirenStyle : bodyStyle;
                var previous = GUI.color;
                GUI.color = GradeColor(grade);
                GUI.Label(new Rect(x, originY, cellWidth, 24f),
                    $"{LastShiftZoneAtlas.ShortLabelOf(zone)} {LastShiftSituationText.GradeLabel(grade)}", style);
                GUI.color = previous;
                x += cellWidth;
            }

            if (sirenActive)
                GUI.Label(new Rect(originX, originY + 22f, ZonePressureRowWidth, 20f), "⚠ 전선 경보: 산소 위험 (전 구역)", sirenStyle);
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

        /// <summary>
        /// 등급 색. <c>concept-draft.md:46</c> 어휘에 CT-01 §5.2 가 붙인 색 그대로다 —
        /// 정상=무색 / 불안정=노랑 / 고장=주황 / 위기=빨강 점멸.
        /// </summary>
        private static Color GradeColor(LastShiftSituationGrade grade) => grade switch
        {
            LastShiftSituationGrade.Unstable => new Color(1f, 0.86f, 0.35f),
            LastShiftSituationGrade.Fault => new Color(1f, 0.58f, 0.2f),
            LastShiftSituationGrade.Crisis => Color.Lerp(new Color(1f, 0.5f, 0.45f), new Color(1f, 0.2f, 0.15f), BlinkPhase),
            _ => Color.white
        };

        /// <summary>
        /// §5.4 디버그 HUD. <b>기존 정보를 하나도 잃지 않고 전량 이관한 자리다.</b>
        /// 원시 수치와 <c>CauseChain</c> 은 개발자·QA 도구이지 플레이어 정보가 아니라서
        /// 여기 있고, 기본은 꺼져 있다.
        /// </summary>
        private void DrawDebugHud()
        {
            GUI.Box(new Rect(16f, 240f, 680f, 208f), GUIContent.none);
            GUI.Label(new Rect(28f, 246f, 650f, 24f), "[DEBUG F3]", headingStyle);
            GUI.Label(new Rect(28f, 274f, 650f, 170f),
                $"preset={currentPreset}  reset_gen={ResetGeneration}  impact_count={ImpactApplicationCount}  " +
                $"phase={(HasAppliedImpact ? "POST-IMPACT" : "PRE-IMPACT")}\n" +
                $"state: thrust={currentState.ThrustDemand:F2} bus={currentState.BusPower:F2} " +
                $"hull={currentState.HullIntegrity:F2} heat={currentState.EngineHeat:F2} " +
                $"attitude={currentState.ShipAttitudeDegrees:F0} damage={currentState.ExistingDamage:F2}\n" +
                $"zones: {ZonePressureDebugLine()}\n" +
                $"situations: heat={SituationOf(LastShiftSystemChannel.Heat)} " +
                $"power={SituationOf(LastShiftSystemChannel.Power)} " +
                $"prop={SituationOf(LastShiftSystemChannel.Propulsion)} siren={sirenActive}\n" +
                $"fuel={currentState.FuelReserve:F2}  dock={currentState.DockProgress:F0}/" +
                $"{LastShiftRecoveryTuning.DockTargetThrustSeconds:F0} thrust·s  hold={controlHold.RemainingSeconds:F1}s\n" +
                $"first_dominant={(HasAppliedImpact ? FirstResult.Problem.ToString() : "pending meteor")}  " +
                $"current_dominant={(HasAppliedImpact ? LastResult.Problem.ToString() : "-")}\n" +
                $"cause_chain: {(HasAppliedImpact ? LastResult.CauseChain : "-")}\n" +
                "WASD/Space/E/F/Mouse | 1·2·3 프리셋 | R 리셋 | M 운석 | 화살표 조종(8초) | F3 디버그",
                bodyStyle);
        }

        private string ZonePressureDebugLine()
        {
            var line = string.Empty;
            for (var index = 0; index < LastShiftZoneAtlas.ZoneCount; index++)
            {
                var zone = (LastShiftZone)index;
                line += $"{LastShiftZoneAtlas.ShortLabelOf(zone)}={zonePressures[zone]:F2}";
                if (IsZoneVacuum(zone)) line += "(진공)";
                if (index < LastShiftZoneAtlas.ZoneCount - 1) line += "  ";
            }

            return line;
        }

        /// <summary>적색 점멸 위상. 사이렌 칸과 예비 막대가 같은 박자로 뛰어야 같은 사건으로 읽힌다.</summary>
        private static float BlinkPhase => LastShiftCrewOxygen.BlinkPhase;
    }
}
