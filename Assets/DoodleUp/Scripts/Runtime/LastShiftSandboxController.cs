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

        /// <summary>자재 배지가 튀어 있는 시간. 왕복 한 번이 <c>24</c>초대라 이 정도면 겹치지 않는다.</summary>
        private const float DepositBadgePopSeconds = 1.8f;

        private int seenDepositRevision;
        private float depositBadgePopSeconds;
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

        /// <summary>
        /// 엔진 보호 잠금이 걸려 있던 누적 시간. <c>추력 부족</c> 판정의 원인 줄이 유일한
        /// 소비자다 — "도착 시점 추력 0.25" 만 적으면 플레이어가 <b>왜</b> 0.25 였는지를
        /// 못 읽고, 그 답이 열 잠금이다(<c>docs/game-feel-loop-review-v1.md</c> §3.1-a).
        /// </summary>
        private float heatProtectionSeconds;

        /// <summary>
        /// 마지막으로 승무원이 죽은 구역. 질식 판정의 원인 줄 <c>○○실</c> 자리다.
        /// 죽은 자리를 그때 기록해 두지 않으면 판정 시점에는 이미 시신 위치밖에 없고,
        /// 그 사이에 압력이 평준화되면 "어느 방이 문제였는가" 가 사라진다.
        /// </summary>
        private LastShiftZone lastCrewDeathZone;
        private bool hasCrewDeathZone;

        /// <summary>판정 순간에 얼린 런 요약과 그 실시간 시각. 결과 화면 모션이 여기 걸려 있다.</summary>
        private LastShiftRunSummary runSummary;
        private float verdictRealtime;
        private LastShiftDoorState doorState = LastShiftDoorState.AllOpen;

        /// <summary>
        /// 승강구 해치. 문과 달리 <b>닫힌 상태로 시작</b>한다 — 근거는
        /// <see cref="LastShiftHatchState.AllClosed"/> 주석에 있다.
        /// </summary>
        private LastShiftHatchState hatchState = LastShiftHatchState.AllClosed;

        /// <summary>
        /// 냉각실 수동 순환 밸브를 지금 붙잡고 있는 승무원들(<c>C-3</c>, §4.3).
        ///
        /// <b>불리언 하나가 아니라 목록인 이유가 §6-3 의 검사 항목 둘이다.</b> "두 사람이 같은
        /// 밸브를 동시에 잡는 경우" 는 목록이면 자명하게 풀리고(둘 다 들어가고, 한 명이 놓아도
        /// 다른 한 명이 남는다), "잡은 사람이 연결을 잃는 경우" 는 매 tick 파괴된 참조를
        /// 걷어내는 것으로 풀린다. 불리언이면 후자에서 밸브가 <b>영구히 잡힌 채로</b> 남아
        /// 아무도 없는 배의 열이 계속 내려간다.
        ///
        /// 효과 자체는 홀더 수와 무관하게 <see cref="LastShiftRecoveryTuning.SustainedCoolingPerSecond"/>
        /// 하나다 — 근거는 그 상수 주석에 있다.
        /// </summary>
        private readonly System.Collections.Generic.List<LastShiftPlayerController> coolingValveHolders = new();

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
        /// CT-01 §5.7.3 구역 칸 하나가 표시할 등급. <b>산소 전용이다.</b>
        ///
        /// 열·전력·추진을 여기 겹쳐 걸지 않는 이유가 §5.7.3 에 있다 — 그 셋은 방마다 다른
        /// 값이 아니라 배 전체에 하나뿐인 상태라, 방 칸에 걸면 "전력실 칸이 나쁘다" 로
        /// 읽히지만 실제로는 "배 전체 전력이 나쁘다" 다. 방이라는 공간의 속성이 아니다.
        /// 산소만 구역별로 실제 다른 값을 갖는다(ship-elements §2.2).
        /// </summary>
        public LastShiftSituationGrade ZoneOxygenGradeOf(LastShiftZone zone) =>
            LastShiftSituationTable.GradeOf(OxygenSituationOf(zone));

        /// <summary>
        /// 상시 패널의 지배 문제 1행이 가리킬 계통. 계통 셋 중 등급이 가장 높은 것이며
        /// 동급이면 먼저 선언된 쪽이 남는다. <b>원인은 말하지 않는다</b> — 어느 계통인지까지다
        /// (§5.7.3, §3.1). 원인과 인과사슬은 <c>F3</c> 과 구역 안 진단에만 있다.
        /// </summary>
        public bool TryResolveDominantChannel(out LastShiftSystemChannel channel,
            out LastShiftSituationGrade grade)
        {
            channel = default;
            grade = LastShiftSituationGrade.Normal;
            foreach (var candidate in new[]
                     {
                         LastShiftSystemChannel.Heat,
                         LastShiftSystemChannel.Power,
                         LastShiftSystemChannel.Propulsion
                     })
            {
                var candidateGrade = LastShiftSituationTable.GradeOf(SituationOf(candidate));
                if (candidateGrade <= grade) continue;
                grade = candidateGrade;
                channel = candidate;
            }

            return grade > LastShiftSituationGrade.Normal;
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

        /// <summary>판정 순간에 얼린 런 요약(<c>G-1</c>). 판정 전에는 <c>Pending</c> 요약이다.</summary>
        public LastShiftRunSummary RunSummary => runSummary;

        /// <summary>
        /// 결과 화면의 <c>다음 판</c> 이 가리키는 프리셋. enum 순환이며 새 상수를 두지 않는다
        /// (<c>docs/last-shift-preset-names-v1.md</c> §4.3).
        /// </summary>
        /// <summary>
        /// 다음 판의 자극. <b>프리셋 순환이 아니라 항해 회차가 정한다</b>
        /// (<see cref="LastShiftVoyage.NextPreset"/>) — 값은 고정 순서 <c>1→2→3</c> 이라
        /// 종전 순환과 같지만, 이제 그 순서를 아는 곳이 항해 하나다.
        /// </summary>
        public LastShiftPreset NextPreset => LastShiftVoyage.NextPreset;

        /// <summary>
        /// 지금 물려 있는 도킹 래치 수(<c>0</c>~<c>4</c>). 구간이 끝날 때 그대로 정비 여력이
        /// 되는 값이다(<c>voyage-run-structure-v1.md</c> §4.1).
        ///
        /// <b>여기서 새로 재는 것은 없다</b> — 구역 압력과 봉인 여부는 이미 있는 상태이고,
        /// 판정선은 <see cref="LastShiftVerdictResolver.IsLatched"/> 하나다.
        /// </summary>
        public int LatchCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < LastShiftZoneAtlas.ZoneCount; index++)
                {
                    var zone = (LastShiftZone)index;
                    if (LastShiftVerdictResolver.IsLatched(zonePressures[zone], IsZoneSealedOff(zone))) count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 남은 시간 안에 도킹을 채우려면 지금부터 유지해야 하는 추력(<c>G-2</c>).
        /// HUD 추력 막대 위의 움직이는 선이 읽는 값이다.
        /// </summary>
        public float RequiredThrust =>
            LastShiftVerdictResolver.RequiredThrust(currentState, dockingSecondsRemaining);

        /// <summary>판정 이후 경과한 실시간. 결과 화면 모션과 입력 지연이 읽는다.</summary>
        public float SecondsSinceVerdict => IsResolved ? Mathf.Max(0f, Time.unscaledTime - verdictRealtime) : 0f;

        /// <summary>
        /// 다음 판 입력을 받아도 되는가. <b>줄이 보이는 시각부터 받는다</b>(아트 §7) —
        /// 보이지 않는 입력을 먼저 받으면 결과를 못 읽고 넘어간 판이 생긴다.
        /// </summary>
        public bool CanAdvanceToNextRun =>
            IsResolved && SecondsSinceVerdict >= LastShiftResultScreen.NextRunInputDelay;

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
        /// 이 압력문 너머 공간의 판독값. 보는 사람의 위치로 어느 쪽이 "너머" 인지 정한다.
        ///
        /// <b>문 평면 위 좌표를 안 쓴다.</b> 문 평면은 방 경계와 <b>같은 값</b>이라, 평면에서
        /// ε 만큼 민 좌표로 구역을 정하면 부호를 한 번 잘못 잡았을 때 판독이 통째로 반대편
        /// 구역을 가리키고도 값이 그럴듯해서 안 보인다. 그래서 <b>방과 광장의 중심</b>으로 잰다.
        ///
        /// <b>축을 문에서 뽑는다</b>(§9.3-2) — 전력실·냉각실 문은 <c>z</c> 평면이라
        /// <c>x</c> 로 비교하던 옛 식은 두 문에서 언제나 같은 답을 낸다.
        /// </summary>
        public LastShiftDistressReading DistressBeyondDoor(int boundary, Vector3 viewer)
        {
            var door = LastShiftZoneAtlas.BoundaryDoor(boundary);
            var room = LastShiftPlazaLayout.Of(LastShiftPlazaLayout.RoomOf(LastShiftZoneAtlas.HighZoneOf(boundary)));
            var roomCenter = new Vector3((room.MinX + room.MaxX) * 0.5f, 0f, (room.MinZ + room.MaxZ) * 0.5f);

            var through = door.PlaneIsX ? viewer.x : viewer.z;
            var roomThrough = door.PlaneIsX ? roomCenter.x : roomCenter.z;

            // 보는 사람이 방과 같은 쪽이면 "너머" 는 광장이다. 광장 중심은 원점이고, 거기
            // 코어가 서 있어도 상관없다 — 구역 소속만 묻는 좌표다.
            var viewerIsInRoom = (through - door.Plane) * (roomThrough - door.Plane) > 0f;
            return DistressOf(LastShiftZoneAtlas.Resolve(viewerIsInRoom ? Vector3.zero : roomCenter));
        }

        /// <summary>
        /// 압력문 게이지가 <b>실제로 표시하는</b> 판독값. 게이지는 문 너머 방 안쪽 끝벽에
        /// 달리므로(§4.1) 광장에서 보든 방에서 보든 값이 같고, 그 값은 언제나 <b>문 너머
        /// 구역</b>이다.
        ///
        /// <see cref="DistressBeyondDoor"/> 를 단면화하지 않고 접근자를 따로 두는 이유는
        /// <b>두 가지 "양쪽이 같음" 이 성질이 다르기 때문</b>이다. 문 없는 개구부(조종석)가
        /// 양쪽에서 같은 값을 내는 것은 광장과 조종석 방이 같은 구역이라는 <b>기하 사실</b>이고,
        /// 압력문이 한 값을 내는 것은 게이지를 방 끝벽에 달기로 한 <b>배치 결정</b>이다
        /// (<c>SIMUL_ZONES ≤ 2</c> 의 장치 1). 한 함수로 합치면 배치가 바뀔 때 둘 중 하나만
        /// 움직여야 하는데 어느 쪽이 움직여야 하는지가 코드에서 사라진다.
        /// </summary>
        public LastShiftDistressReading GaugeReading(int boundary) =>
            DistressOf(LastShiftZoneAtlas.HighZoneOf(boundary));

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

        /// <summary>
        /// 지금 구간 런타임(B층) 상태 전부를 값 한 벌로 접는다. <b>동기 대입 한 덩어리</b>이며
        /// 이 안에서 시뮬을 세우지 않는다 — 캡처는 tick <c>N</c> 직후이거나 <c>N-1</c> 직후이지
        /// tick 중간일 수 없으므로 찢어진 스냅샷이 구조적으로 나올 수 없다
        /// (<c>docs/tech/save-backbone-feasibility-v1.md</c> §1.4-나).
        ///
        /// 네트워크 계층이 아니라 여기 있는 이유는 소비자가 둘이기 때문이다. 파일 층은
        /// <see cref="LastShiftNetworkSandbox"/> 없이도 캡처할 수 있어야 한다.
        /// </summary>
        public LastShiftNetworkSnapshot CaptureRuntimeSnapshot()
        {
            byte securedMask = 0;
            if (items != null)
            {
                foreach (var item in items)
                {
                    if (item == null || !item.Secured) continue;
                    securedMask |= (byte)(1 << (int)item.Role);
                }
            }

            byte valveMask = 0;
            foreach (var holder in coolingValveHolders)
                if (holder != null) valveMask |= (byte)(1 << (int)holder.PlayerSlot);

            return new LastShiftNetworkSnapshot
            {
                Preset = currentPreset,
                ShipState = currentState,
                FirstProblem = FirstResult.Problem,
                CurrentProblem = LastResult.Problem,
                CoolingScore = LastResult.CoolingScore,
                BatteryScore = LastResult.BatteryScore,
                LeakScore = LastResult.LeakScore,
                DockingSecondsRemaining = dockingSecondsRemaining,
                ResetGeneration = ResetGeneration,
                ImpactApplicationCount = ImpactApplicationCount,
                SecuredItemMask = securedMask,
                HasAppliedImpact = HasAppliedImpact,
                Verdict = verdict,
                SacrificedSystemMask = repairLedger.SacrificeMask,
                ThrustCeiling = lastTick.ThrustCeiling,
                HeatProtectionEngaged = lastTick.HeatProtectionEngaged,
                SteeringDelayed = lastTick.SteeringDelayed,
                OxygenPumpRunning = lastTick.OxygenPumpRunning,
                SirenActive = sirenActive,
                PowerPressure = PressureOf(LastShiftZone.Power),
                CoolingPressure = PressureOf(LastShiftZone.Cooling),
                LifeSupportPressure = PressureOf(LastShiftZone.LifeSupport),
                Boundary0DoorOpen = IsDoorOpen(0),
                Boundary1DoorOpen = IsDoorOpen(1),
                Boundary2DoorOpen = IsDoorOpen(2),
                ForeHatchOpen = IsHatchOpen(LastShiftBypassDuct.ForeShaft),
                AftHatchOpen = IsHatchOpen(LastShiftBypassDuct.AftShaft),
                UncontainedSystemMask = UncontainedSystemMask,
                CoolingRepair = repairLedger.Capture(LastShiftShipSystem.Cooling),
                PowerRepair = repairLedger.Capture(LastShiftShipSystem.Power),
                OxygenRepair = repairLedger.Capture(LastShiftShipSystem.Oxygen),
                QuickBypassCount = repairLedger.QuickBypassCount,
                BypassLapseCount = repairLedger.BypassLapseCount,
                DamagedSystemMask = (byte)damagedSystemMask,
                ControlHoldThrustDemand = controlHold.ThrustDemand,
                ControlHoldAttitudeDegrees = controlHold.AttitudeDegrees,
                ControlHoldRemainingSeconds = controlHold.RemainingSeconds,
                SteeringDelayRemainingSeconds = steeringInputDelayRemaining,
                PendingThrustDemand = pendingThrust,
                PendingAttitudeDegrees = pendingAttitude,
                HasPendingControl = hasPendingControl,
                HeatProtectionSeconds = heatProtectionSeconds,
                CrewDeathZone = lastCrewDeathZone,
                HasCrewDeathZone = hasCrewDeathZone,
                CrewAtDockingTrigger = wasCrewAtDockingTrigger,
                MeteorImpactPoint = appliedMeteor.ImpactPoint,
                MeteorImpactVector = appliedMeteor.ImpactVector,
                MeteorMass = appliedMeteor.Mass,
                MeteorSpeed = appliedMeteor.Speed,
                CoolingValveHolderMask = valveMask,
                SecondsSinceVerdict = SecondsSinceVerdict
            };
        }

        /// <summary>
        /// 상황 래치 위상을 값으로 접는다. 스냅샷 구조체에 안 넣는 근거는
        /// <see cref="LastShiftSituationTracker.CaptureLatchDwell"/> 주석에 있다.
        /// </summary>
        public float[] CaptureSituationLatches() => situationTracker.CaptureLatchDwell();

        public void ApplyNetworkSnapshot(in LastShiftNetworkSnapshot value)
        {
            ApplyNetworkSnapshot(value, LastShiftStateAuthority.Replicated);
        }

        /// <summary>
        /// 스냅샷 주입. <paramref name="authority"/> 가 <b>주입 이후 누가 계산하는가</b> 를 가른다 —
        /// 이 인자가 생기기 전에는 주입이 언제나 "나는 클라이언트다" 를 같이 켰고, 그래서
        /// 세이브 복원이 이 경로를 쓸 수 없었다(<c>save-backbone-feasibility-v1.md</c> §1.3-가).
        ///
        /// <paramref name="situationLatchDwell"/> 는 히스테리시스 위상이며 없으면(네트워크 경로)
        /// 지금처럼 0초 재평가로 다시 세운다 — 표시에는 맞고 위상만 초기화된다.
        /// </summary>
        public void ApplyNetworkSnapshot(
            in LastShiftNetworkSnapshot value,
            LastShiftStateAuthority authority,
            float[] situationLatchDwell = null)
        {
            var restoring = authority == LastShiftStateAuthority.Local;

            // 클라이언트는 ApplyMeteorImpact 를 돌리지 않으므로 충격 연출 트리거가 없다.
            // 스냅샷의 ImpactApplicationCount 증가가 곧 "서버에서 충격이 터졌다" 이므로
            // 그 변화를 연출 트리거로 쓴다. 리셋으로 카운트가 유지되는 동안은 재생하지 않는다.
            //
            // 복원은 이 트리거를 쓰지 않는다. 저장된 판은 충격이 <b>이미 지나간</b> 상태이고,
            // 이어하기 첫 프레임에 운석이 다시 터지는 연출은 사실과 다르다.
            var impactAdvanced = !restoring &&
                                 value.HasAppliedImpact && value.ImpactApplicationCount > ImpactApplicationCount;

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
            // 판정이 스냅샷으로 처음 도착한 순간이 곧 이 화면에서의 판정 시각이다. 여기서
            // 찍지 않으면 결과 화면 모션이 t=0 을 잃고 첫 프레임부터 완성된 상태로 뜬다.
            //
            // 복원은 반대다 — 저장한 판의 결과 화면이 이미 얼마나 오래 떠 있었는지가 사실이므로
            // 절대 시각 대신 경과를 되돌린다(절대 시각은 프로세스마다 달라 실을 수 없다).
            if (restoring)
                verdictRealtime = Time.unscaledTime - Mathf.Max(0f, value.SecondsSinceVerdict);
            else if (verdict != value.Verdict && LastShiftVerdictResolver.IsResolved(value.Verdict))
                verdictRealtime = Time.unscaledTime;
            verdict = value.Verdict;
            if (restoring)
            {
                // 권위를 되찾는 쪽은 결과가 아니라 <b>입력</b>을 받아야 다음 tick 이 성립한다.
                // 장부 전체와 손상 마스크가 그 입력이고, 이 둘이 있으면 UncontainedSystemMask 를
                // 스스로 다시 계산할 수 있으므로 usesReplicatedState 를 켜지 않는다.
                repairLedger.RestoreFrom(
                    value.CoolingRepair, value.PowerRepair, value.OxygenRepair,
                    value.QuickBypassCount, value.BypassLapseCount);
                damagedSystemMask = value.DamagedSystemMask;
                usesReplicatedState = false;
            }
            else
            {
                repairLedger.ApplyReplicatedSacrificeMask(value.SacrificedSystemMask);
                usesReplicatedState = true;
            }
            replicatedUncontainedSystemMask = value.UncontainedSystemMask;
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
            var origin = restoring ? "save restore" : "server snapshot";
            FirstResult = new LastShiftResolverResult(value.FirstProblem, 0f, 0f, 0f, origin);
            LastResult = new LastShiftResolverResult(value.CurrentProblem, value.CoolingScore, value.BatteryScore, value.LeakScore, origin);
            if (restoring) RestoreLocalAuthorityState(value);
            // 래치 위상은 있으면 되돌리고 없으면 아래 0초 재평가가 다시 세운다. 되돌린 뒤에도
            // 재평가를 거치는 것이 요점이다 — 래치는 상태이고 대표 상황은 그 파생값이라,
            // 파생값까지 저장했다가 서로 어긋나게 두는 것보다 한 번 다시 접는 편이 안전하다.
            if (situationLatchDwell != null) situationTracker.ApplyLatchDwell(situationLatchDwell);
            // 구역 등급도 사이렌과 같은 이유로 여기서 다시 평가한다. 클라이언트는
            // AdvanceMission 을 안 돌리므로 이 줄이 없으면 HUD 4칸이 영영 "정상" 이다 —
            // 상황을 스냅샷 필드로 늘리지 않는 것은 이미 동기화되는 상태·압력만으로
            // 같은 값이 나오기 때문이다(평가는 순수 계산이다).
            situationTracker.Evaluate(
                LastShiftSituationInput.From(currentState, zonePressures, BuildContainment()), 0f);
            if (impactAdvanced) PlayImpactFeedback(Meteor);
        }

        /// <summary>
        /// 복원 전용 주입분. 여기 있는 것들은 <b>클라이언트가 굳이 알 필요가 없던 상태</b>라
        /// 네트워크 경로가 한 번도 안 건드렸다 — 표시만 하는 쪽은 이 값들 없이도 화면이 맞기
        /// 때문이다. 판정을 이어서 내는 쪽은 전부 필요하다(§1.3-나).
        /// </summary>
        private void RestoreLocalAuthorityState(in LastShiftNetworkSnapshot value)
        {
            controlHold.Restore(
                value.ControlHoldThrustDemand, value.ControlHoldAttitudeDegrees, value.ControlHoldRemainingSeconds);
            steeringInputDelayRemaining = Mathf.Max(0f, value.SteeringDelayRemainingSeconds);
            pendingThrust = value.PendingThrustDemand;
            pendingAttitude = value.PendingAttitudeDegrees;
            hasPendingControl = value.HasPendingControl;
            heatProtectionSeconds = Mathf.Max(0f, value.HeatProtectionSeconds);
            lastCrewDeathZone = value.CrewDeathZone;
            hasCrewDeathZone = value.HasCrewDeathZone;
            // 도킹 판정은 상주가 아니라 진입 엣지로 난다. 이 기준값을 false 로 두고 복원하면
            // 트리거 안에서 저장한 판이 다음 tick 에 가만히 서 있는 것만으로 도킹한다.
            wasCrewAtDockingTrigger = value.CrewAtDockingTrigger;
            appliedMeteor = new LastShiftMeteorStimulus
            {
                ImpactPoint = value.MeteorImpactPoint,
                ImpactVector = value.MeteorImpactVector,
                Mass = value.MeteorMass,
                Speed = value.MeteorSpeed
            };
            RestoreCoolingValveHolders(value.CoolingValveHolderMask);
            // 런 요약은 저장하지 않는다. 얼린 값이 전부 위에서 복원한 상태의 파생이라
            // (판정·도킹 진행도·남은 시간·열 잠금·장부 카운터·죽은 구역) 다시 접으면 같은 값이
            // 나오고, 두 벌로 두면 어긋날 자리만 는다. 판정 전에는 요약 자체가 의미 없다.
            // verdictRealtime 은 위에서 경과로 되돌렸으므로 여기서 다시 찍지 않는다.
            if (IsResolved) runSummary = BuildRunSummary();
        }

        /// <summary>
        /// 밸브 홀더를 슬롯 마스크로 되돌린다. 사거리·생사는 다시 보지 않는다 — 저장 시점에
        /// 이미 통과한 판정이고, 복원 직후 승무원 위치가 아직 제자리로 오기 전이면 여기서
        /// 다시 재는 것이 오히려 손을 떼게 만든다. 자격을 잃은 홀더는 다음 tick 의
        /// <see cref="PruneCoolingValveHolders"/> 가 정상 경로로 걷어낸다.
        /// </summary>
        private void RestoreCoolingValveHolders(byte mask)
        {
            coolingValveHolders.Clear();
            if (mask == 0 || players == null) return;
            foreach (var targetPlayer in players)
            {
                if (targetPlayer == null) continue;
                if ((mask & (1 << (int)targetPlayer.PlayerSlot)) == 0) continue;
                if (!coolingValveHolders.Contains(targetPlayer)) coolingValveHolders.Add(targetPlayer);
            }
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
            // 항해가 여기서 시작한다 — 여력 0, 구간 1(§3.1). 네트워크 경로는 아직 이 루프를
            // 안 돈다(구간 전이 입력이 서버에만 있고 원장은 피어마다 정적이라, 클라이언트
            // 원장을 맞추는 것은 기항 화면의 합의 입력과 같은 카드다 — §10-4).
            LastShiftVoyage.BeginVoyage();
            ResetPreset(LastShiftVoyage.CurrentPreset);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var networkSandbox = GetComponent<LastShiftNetworkSandbox>();
            if (keyboard != null && (networkSandbox == null || !networkSandbox.IsSpawned))
            {
                // G-1(c) 다음 판. <b>결과 화면에 입력 하나만 둔다</b> — 1·2·3 은 디버그로
                // 남고, 프리셋 순환이 여기 붙어야 세 판에 세 가지 사고를 겪는다(§3.1-c).
                // wasPressedThisFrame 이라 판정 순간 눌려 있던 키는 결과를 넘기지 못한다.
                //
                // 이 입력이 이제 구간을 넘긴다. 항해가 끝났으면 같은 키가 새 항해를 열고
                // (§6 사례 D), 그때 여력이 0 으로 돌아간다 — 이월이 항해를 넘지 않는다는 것이
                // 화면에서 보이는 자리다.
                if (IsResolved && CanAdvanceToNextRun && keyboard.spaceKey.wasPressedThisFrame)
                {
                    LastShiftVoyage.Advance();
                    RequestPresetReset(LastShiftVoyage.CurrentPreset);
                }
                // 디버그 단일 구간 진입(§8-2). 회차도 같이 옮겨야 결과 화면의 "다음" 과
                // 실제로 서는 판이 안 갈린다.
                else if (keyboard.digit1Key.wasPressedThisFrame) EnterSegmentForDebug(LastShiftPreset.HighHeatHighThrust);
                else if (keyboard.digit2Key.wasPressedThisFrame) EnterSegmentForDebug(LastShiftPreset.PowerOverloadLooseBattery);
                else if (keyboard.digit3Key.wasPressedThisFrame) EnterSegmentForDebug(LastShiftPreset.BadAttitudeHighOxygen);
                else if (keyboard.rKey.wasPressedThisFrame) EnterSegmentForDebug(currentPreset);
                // 운석은 K 다. M 은 도면(LastShiftMapView)이 가져갔다 — 검증 도구 하나와
                // 상시 화면 하나가 같은 키를 쓰고 있었고, 플레이 중에 쓰는 쪽이 M 이다.
                else if (keyboard.kKey.wasPressedThisFrame) ApplyMeteorImpact();
                else if (keyboard.fKey.wasPressedThisFrame) TrySecureHeldItem();
                // 부품을 제자리에 놓는 것(F)과 계통에 연결하는 것(C·V·G)은 다른 행동이다.
                // 놓기만 해서는 악화가 멈추지 않는다. E 는 이미 잡기/놓기라 쓰지 않는다.
                else if (keyboard.cKey.wasPressedThisFrame) TryBeginRepair(LastShiftRepairMode.SafeRestore);
                else if (keyboard.vKey.wasPressedThisFrame) TryBeginRepair(LastShiftRepairMode.QuickBypass);
                else if (keyboard.gKey.wasPressedThisFrame) TryBeginRepair(LastShiftRepairMode.PerformanceSacrifice);

                // 냉각실 밸브 유지(T). 누르고 <b>있는 동안</b>이라 wasPressedThisFrame 이 아니고,
                // 그래서 위의 else-if 사슬 밖에 있다 — 사슬 안에 두면 같은 프레임의 다른 키가
                // 밸브 상태를 삼킨다. §4.3 이 지정한 R 은 이미 프리셋 리셋이다(§4.3 표의
                // "기존 hold 입력 재사용" 은 조종석 hold 를 가리키고 키 이름이 아니다).
                SetLocalCoolingValveHeld(keyboard.tKey.isPressed);

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
            // 밸브 홀더 정리도 운석 게이트 <b>위</b>다. 아래로 내리면 손상 전에 잡은 사람이
            // 죽거나 사라져도 목록에 남고, 운석이 떨어지는 순간 아무도 없는 배의 열이 내려간다.
            PruneCoolingValveHolders();

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

            // 선외 tick 은 운석 게이트 <b>위</b>다 — 아니 위여야만 한다. 아래 조기 반환은
            // 판정이 확정되면 시계를 멈추는데, 기항이 바로 그 "판정이 확정된 뒤" 이고
            // (조항 O-4 가 에어록을 여는 유일한 창이 그때다) 게이트 아래 두면 감압 사이클도
            // 선외 산소도 한 프레임도 안 돈다.
            AdvanceExtravehicular(deltaTime);

            // 기상 도입부도 같은 이유로 게이트 위다 — 이 아래로 내리면 판정이 확정되기 전에는
            // 암전이 안 걷히고, 잠긴 입력이 그대로 남는다. 안 도는 동안은 즉시 반환한다.
            LastShiftWakeSequence.Tick(deltaTime);
            // 신호음은 줄이 바뀌는 프레임에만 운다. 판정은 Announce 안에 있어서 매 프레임
            // 불러도 되고, 재촉으로 문장이 갈려도 id 가 같아 다시 안 운다(조항 N-1).
            if (LastShiftWakeSequence.HasLine)
                LastShiftNarrationAudio.Announce(
                    LastShiftWakeSequence.Current.Id, LastShiftWakeSequence.Current.Sfx);

            // 어느 빌드가 도는지 한 번만 적는다. 진단을 붙였는데 로그가 0 건이면 "그 상태가
            // 아니었다" 와 "그 코드가 아니었다" 가 안 갈리는데, 이 줄이 그 둘을 가른다.
            if (!buildMarkerLogged)
            {
                buildMarkerLogged = true;
                Debug.Log("[LAST_SHIFT_BUILD] banner-diagnostics=3 " +
                          "detail=lobby/no-layer/blank 세 출구에 계기가 붙은 빌드다");
            }

            // 외부 자극 시계. <b>충격 전에도 돈다</b> — 아래 줄이 충격 전 tick 을 통째로
            // 막으므로, 여기서 안 돌리면 자극이 영영 안 뜨고 디버그 키로만 존재하던 예전
            // 상태가 그대로 남는다. 도입부 연출 중에는 안 돌린다: 조작이 잠긴 동안 사고가
            // 나면 대응할 방법이 없는 시간이 생긴다(미결 3에 대한 판단).
            AdvanceExternalStimulus(deltaTime);

            if (!HasAppliedImpact || IsResolved) return;

            // 기존 우회의 수명을 먼저 줄여야 이 tick 끝에 막 완성된 우회가 작업 시간까지
            // 소급해서 잃지 않는다. 0.8초 작업 완료 순간부터 온전한 60초가 시작된다.
            LapseExpiredBypasses(deltaTime);
            AdvanceRepairChannels(deltaTime);

            lastTick = LastShiftDeterioration.Tick(
                ref currentState, ref zonePressures, BuildContainment(), BreachZone, doorState, deltaTime);
            RefreshResultAfterImpact();
            if (lastTick.HeatProtectionEngaged) heatProtectionSeconds += deltaTime;

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

            // G-2(c). 필요 추력이 상한을 넘으면 최대로 밀어도 진척이 안 차므로 여기서 끝낸다.
            // 연료 소진 표류와 판정이 같고 트리거 문자열만 다르다 — 로그만 보고 어느 쪽으로
            // 끝났는지가 갈려야 하고, 이 경로는 연료가 남았는데도 시간이 모자란 경우다.
            if (LastShiftVerdictResolver.IsDockUnreachable(currentState, dockingSecondsRemaining))
            {
                SettleVerdict(LastShiftVerdict.FailureAdrift, "dock-progress-unreachable");
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
            CaptureRunSummary();
            // 조항 T-5 의 인원 배수는 잔해가 뜨기 전에 서 있어야 한다 — 바로 아래
            // SettleSegment 안에서 LastShiftSalvage.ArriveAtPort 가 총량을 확정하고, 그 총량이
            // 이 값을 곱한다.
            LastShiftTutorial.SetCrewCount(LivingCrewCount);
            // 구간 판정 → 항해 전이. 여기가 정본 자리다(§5 표) — 배치 화면이 처음 열릴 때
            // 임시로 기항을 열던 다리를 이 한 줄이 대신한다. 래치 수는 판정 순간의 구역
            // 압력이고, 상태를 얼린 직후라 결과 화면이 읽는 값과 같은 시점이다.
            var transition = LastShiftVoyage.SettleSegment(value, LatchCount);
            Debug.Log($"[LAST_SHIFT_VOYAGE] segment={LastShiftVoyage.SegmentIndex}/{LastShiftVoyage.SegmentCount} " +
                      $"verdict={value} transition={transition} latches={LastShiftVoyage.LastLatchCount} " +
                      $"port={LastShiftMaintenance.PortIndex} income={LastShiftMaintenance.LastPortIncome} " +
                      $"carried={LastShiftMaintenance.LastCarriedOver} balance={LastShiftMaintenance.Balance}");
            Debug.Log($"[LAST_SHIFT_VERDICT] generation={ResetGeneration} verdict={value} trigger={trigger} " +
                      $"thrust={currentState.ThrustDemand:F2} O2={currentState.OxygenPressure:F2} heat={currentState.EngineHeat:F2} " +
                      $"bus={currentState.BusPower:F2} fuel={currentState.FuelReserve:F3} dock={currentState.DockProgress:F1} " +
                      $"T-{dockingSecondsRemaining:F0}s sacrifices={repairLedger.SacrificeCount} bypassLapses={repairLedger.BypassLapseCount}");
        }

        /// <summary>
        /// 판정 순간의 값을 얼린다(<c>G-1</c>). <b>얼리는 것이 요점이다</b> — 결과 화면이 떠
        /// 있는 동안 배경 상태가 계속 변하면(판정 후에도 tick 은 멈추지만 승무원·아이템은
        /// 움직인다) 원인 줄의 숫자가 같이 흔들린다.
        ///
        /// 새로 계산하는 값은 하나도 없다. 경과 시간은 제한시간에서 남은 시간을 뺀 것이고,
        /// 평균 추력은 <c>DockProgress</c>(=추력적분)를 그 경과로 나눈 것뿐이다.
        /// </summary>
        private void CaptureRunSummary()
        {
            runSummary = BuildRunSummary();
            verdictRealtime = Time.unscaledTime;
        }

        /// <summary>
        /// 요약을 지금 상태에서 접는다. <b>새로 계산하는 값이 없다</b>는 성질 덕에 세이브
        /// 복원이 요약을 따로 저장하지 않고 이 함수를 한 번 더 부르는 것으로 끝난다 —
        /// 같은 사실을 파일에 두 벌 두면 어긋날 자리만 는다.
        /// </summary>
        private LastShiftRunSummary BuildRunSummary()
        {
            var elapsed = LastShiftRecoveryTuning.DockingTimerSeconds - dockingSecondsRemaining;
            return new LastShiftRunSummary(
                verdict,
                currentState.DockProgress,
                elapsed,
                currentState.ThrustDemand,
                heatProtectionSeconds,
                repairLedger.SacrificeCount,
                repairLedger.QuickBypassCount,
                repairLedger.BypassLapseCount,
                // 죽은 자리가 기록되지 않은 경로(승무원 없는 최소 조립 등)에서는 가장 낮은
                // 구역을 쓴다. 질식 판정이 났다면 그 구역이 곧 원인이다.
                hasCrewDeathZone ? lastCrewDeathZone : zonePressures.LowestZone);
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
                {
                    // 죽은 자리를 그때 잡는다. 결과 화면 원인 줄의 ○○실 자리이고, 판정
                    // 시점에 다시 찾으면 이미 압력이 평준화돼 어느 방이었는지가 사라진다.
                    lastCrewDeathZone = LastShiftZoneAtlas.Resolve(targetPlayer.transform.position);
                    hasCrewDeathZone = true;
                    Debug.Log($"[LAST_SHIFT_CREW_DEATH] generation={ResetGeneration} crew={targetPlayer.PlayerSlot} " +
                              $"zone={LastShiftZoneAtlas.KeyOf(lastCrewDeathZone)} " +
                              $"livingCrew={LivingCrewCount} O2={currentState.OxygenPressure:F2} T-{dockingSecondsRemaining:F0}s");
                }
            }
        }

        /// <summary>
        /// 선외 tick — <b>기항에서만 실제로 일이 일어난다.</b>
        /// 기획 정본은 <c>docs/outboard-outpost-and-map-final-v1.md</c> §4.1·§5.5 다.
        ///
        /// <b>왜 <see cref="AdvanceCrewOxygen"/> 과 따로인가.</b> 저쪽은 구간 안 tick 이라
        /// 판정이 확정되면 시계가 멈춘다. 기항은 정확히 그 멈춘 뒤이고, 에어록이 열리는 창도
        /// 거기뿐이다(조항 <c>O-4</c>) — 같은 함수에 얹으면 산소가 안 돌거나, 돌게 고치면
        /// 구간 판정 뒤에도 시뮬레이션이 계속 도는 회귀가 된다.
        ///
        /// <b>기항에서는 승무원이 세 자리 중 하나에 있다.</b> 가압 구역(반입·재충전),
        /// 선외(소모·조항 <c>O-7</c>), 그 사이 덕트·에어록(소모만). 셋을 여기서 한 번에 가른다.
        /// </summary>
        private void AdvanceExtravehicular(float deltaTime)
        {
            // <b>리프트와 에어록은 같은 프레임에 돌아야 한다.</b> 감압을 승강 중에 겹쳐
            // 돌리는 것이 balance 통과 조건이라(순차면 여유 0.3초), 둘 중 하나만 여기 있으면
            // 겹침이 성립하지 않는다. 실제로 리프트 tick 이 빠져 있었고 EditMode 검사가
            // 직접 돌려서 통과하고 있었다 - 게임에서는 리프트가 영영 안 움직이는 상태였다.
            LastShiftEvaLift.Tick(deltaTime);
            LastShiftAirlock.Tick(deltaTime);
            LastShiftSalvage.Tick(deltaTime);
            if (!LastShiftAirlock.IsAtPort || players == null) return;

            // 튜토리얼 관측 셋. 이 루프가 이미 승무원마다 좌표를 한 번씩 보므로 여기서 같이
            // 접는다 — 따로 돌면 "선외인가" 판정이 두 벌이 되고, 그 둘이 갈리는 순간
            // 화면에 산소 게이지가 뜬 단계와 상태기가 센 단계가 어긋난다.
            var crewLeftCockpit = false;
            var crewInChamber = false;
            var crewVacuum = false;
            // 코어에 다가섰는가. 두 거리를 쓴다 — 접근(AI_B_01)과 도착(AI_F_01)이 대본에서
            // 다른 줄이기 때문이다. 문 사거리를 그대로 빌려 쓰는 것은 새 숫자를 안 만들려는
            // 것이고, 실제로 코어에 붙는 조작이 문 개폐와 같은 키다.
            var crewNearCore = false;
            // 상시 경고가 읽는 둘. "선외에서" 가 조건이라 진공 노출과 같이 본다.
            var crewWarningOutside = false;
            var crewCriticalOutside = false;
            var crewAtSalvage = false;
            var crewRoom = LastShiftPlazaSpace.Plaza;
            var crewInRoom = false;
            // 순회 블록이 읽는 것들. 방 판정은 이 루프가 이미 하고 있어서 여기서 접는다 —
            // 따로 돌면 "어느 방인가" 가 두 벌이 되고 그 둘이 갈리는 순간을 못 찾는다.
            var crewInPlaza = false;
            var crewFacingScreen = false;
            var crewAtFixture = LastShiftPlazaSpace.Plaza;
            var crewAtAnyFixture = false;

            foreach (var targetPlayer in players)
            {
                if (targetPlayer == null) continue;
                var crew = LastShiftCrewOxygen.Ensure(targetPlayer);
                if (crew == null || crew.IsDead) continue;

                var position = targetPlayer.transform.position;
                var resolved = LastShiftPlazaLayout.TryResolveSpace(position.x, position.z, out var space);
                crewLeftCockpit |= !resolved || space != LastShiftPlazaSpace.CockpitRoom;
                // EVA 가 상향으로 뒤집히면서(2026-08-11) 신호원이 갑판 아래에서 승강
                // 샤프트로 옮겨 왔다. 3단계는 "감압 챔버에 들어섰다" 이고 그 챔버가 곧
                // 광장 중앙 코어다 - 평면 판정은 코어 그대로이고 높이는 배 안이면 된다.
                crewInChamber |= LastShiftEvaShaft.Contains(position.x, position.z)
                                 && LastShiftEvaShaft.IsInsideHull(position.y);
                var outside = LastShiftAirlock.IsOutside(position);
                crewVacuum |= outside;
                crewAtSalvage |= LastShiftSalvage.IsWithinReach(position);
                crewWarningOutside |= outside && crew.IsWarning;
                crewCriticalOutside |= outside && crew.IsCritical;
                crewNearCore |= LastShiftEvaShaft.Contains(
                    position.x, position.z, LastShiftZoneDoor.ReachDistance);

                if (resolved)
                {
                    LastShiftPatrol.Observe(space);
                    crewInPlaza |= space == LastShiftPlazaSpace.Plaza;
                    if (space != LastShiftPlazaSpace.Plaza)
                    {
                        crewRoom = space;
                        crewInRoom = true;
                    }
                    if (space != LastShiftPlazaSpace.Plaza
                        && LastShiftPatrol.IsAtFixture(space, position))
                    {
                        crewAtFixture = space;
                        crewAtAnyFixture = true;
                    }
                    crewFacingScreen |= space == LastShiftPlazaSpace.CockpitRoom
                                        && LastShiftPatrol.IsFacingCockpitScreen(
                                            position, targetPlayer.AimDirection);
                }

                if (!IsZoneVacuum(position))
                {
                    // 배 안으로 들어오는 것이 곧 반입이다. 에어록 안에서 받지 않는 이유는
                    // 챔버도 비가압이라 "안전해진 순간" 이 아니기 때문이고, 좌표를 따로 재지
                    // 않는 이유는 가압 판정이 이미 그 경계를 알고 있기 때문이다.
                    LastShiftSalvage.Deposit();
                    crew.RefillAtPort(deltaTime);
                    crew.Tick(false, deltaTime);
                    continue;
                }

                if (LastShiftAirlock.IsOutside(position) &&
                    crew.SuitOxygen <= LastShiftAirlock.EvaReturnReserve)
                {
                    RescueFromExtravehicular(targetPlayer, crew);
                    LastShiftStandingNarration.NotifyAutoReturn();
                    continue;
                }

                crew.Tick(true, deltaTime);
            }

            LastShiftTutorial.Observe(
                new LastShiftTutorialObservation(
                    crewLeftCockpit, crewInChamber, crewVacuum,
                    LastShiftSalvage.Carried, LastShiftSalvage.CarryCapacity,
                    LastShiftSalvage.Remaining, LastShiftMaterials.Balance),
                deltaTime);
            AdvanceNarration(
                new NarrationSignals(crewNearCore, crewInChamber, crewVacuum,
                    crewInPlaza, crewFacingScreen, crewAtFixture, crewAtAnyFixture,
                    crewWarningOutside, crewCriticalOutside, crewAtSalvage,
                    crewRoom, crewInRoom),
                deltaTime);
            AdvanceDepositBadge(deltaTime);
        }

        /// <summary>
        /// 내레이션 진행 — <b>나가는 길</b> 블록(정본 §4-3). 관측을 한 벌 더 만들지 않고
        /// 튜토리얼이 이미 세는 값을 그대로 읽는다.
        ///
        /// <b>조건을 여기서 안 센다.</b> 디렉터가 "다음 줄인가" 하나만 보므로, 사거리에
        /// 드나들 때마다 오는 신호도 두 번째부터는 그냥 버려진다. 그래서 아래가 전부
        /// <c>if</c> 없이 나열이다 — 실제로 뜨는 것은 그중 순서가 맞는 하나뿐이다.
        ///
        /// <b>아직 안 걸린 블록이 있다.</b> 파밍(<c>AI_F_07</c>~<c>14</c>)과 도면
        /// (<c>AI_B_11</c>~<c>17</c>)의 신호는 각각 잔해와 배치 화면 쪽에 있어서 여기서 안
        /// 보인다. 그 둘이 걸릴 때까지 디렉터는 <c>AI_F_06</c> 에서 선다.
        /// </summary>
        /// <summary>내레이션이 읽는 신호 한 벌. 인자를 일곱 개 늘어놓지 않으려고 묶는다.</summary>
        private readonly struct NarrationSignals
        {
            public NarrationSignals(bool nearCore, bool inChamber, bool vacuum,
                bool inPlaza, bool facingScreen, LastShiftPlazaSpace atFixture, bool anyFixture,
                bool warningOutside, bool criticalOutside, bool atSalvage,
                LastShiftPlazaSpace room, bool inRoom)
            {
                Room = room;
                InRoom = inRoom;
                WarningOutside = warningOutside;
                CriticalOutside = criticalOutside;
                AtSalvage = atSalvage;
                NearCore = nearCore;
                InChamber = inChamber;
                Vacuum = vacuum;
                InPlaza = inPlaza;
                FacingScreen = facingScreen;
                AtFixture = atFixture;
                AnyFixture = anyFixture;
            }

            public bool NearCore { get; }
            public bool InChamber { get; }
            public bool Vacuum { get; }
            public bool InPlaza { get; }
            public bool FacingScreen { get; }
            public LastShiftPlazaSpace AtFixture { get; }
            public bool AnyFixture { get; }
            public bool WarningOutside { get; }
            public bool CriticalOutside { get; }
            public bool AtSalvage { get; }
            public LastShiftPlazaSpace Room { get; }
            public bool InRoom { get; }

            public bool At(LastShiftPlazaSpace space) => AnyFixture && AtFixture == space;
        }

        private static void AdvanceNarration(in NarrationSignals signals, float deltaTime)
        {
            // 상시 경고는 <b>디렉터 밖</b>이다. 진행이 어디까지 갔든 상태로 뜨고, 실제로
            // 안내가 이미 끝난 뒤(손 떼기 이후)에도 산소는 계속 마른다.
            LastShiftStandingNarration.Observe(
                signals.WarningOutside, signals.CriticalOutside, deltaTime);
            if (LastShiftStandingNarration.HasLine)
                LastShiftNarrationAudio.Announce(
                    LastShiftStandingNarration.Current.Id, LastShiftStandingNarration.Current.Sfx);

            if (!LastShiftNarrationDirector.IsRunning) return;

            // 순회는 <b>순서가 없다</b>(조항 N-3) — 어느 문으로 들어가든 그 방 줄이 그 자리에서
            // 나온다. 그래서 디렉터가 아니라 자기 상태기가 민다. 어느 방에 있는지는 이미
            // 재고 있으므로 여기서는 그대로 넘기기만 한다.
            if (signals.InPlaza) LastShiftPatrolNarration.NotifyInPlaza();
            if (signals.NearCore) LastShiftPatrolNarration.NotifyNearCore();
            if (signals.InRoom) LastShiftPatrolNarration.NotifyRoomEntered(signals.Room);
            // 조종석만 <b>시야</b>로 줄이 뜬다(전면 스크린은 멀리서도 읽힌다). 나머지 셋은
            // 근접이고, "방을 건넜는가" 는 네 방 모두 근접 하나로 잰다(조항 N-11).
            if (signals.FacingScreen)
                LastShiftPatrolNarration.NotifyAtFixture(LastShiftPlazaSpace.CockpitRoom);
            if (signals.AnyFixture)
            {
                LastShiftPatrolNarration.NotifyFixtureApproached(signals.AtFixture);
                LastShiftPatrolNarration.NotifyAtFixture(signals.AtFixture);
            }
            LastShiftPatrolNarration.Tick(deltaTime);
            if (LastShiftPatrolNarration.HasLine)
                LastShiftNarrationAudio.Announce(
                    LastShiftPatrolNarration.Current.Id, LastShiftPatrolNarration.Current.Sfx);

            // 코어 접근 -> 승강기 도착 -> 탑승. 셋이 거리로 갈린다.
            LastShiftNarrationDirector.Notify("AI_B_01",
                signals.NearCore && LastShiftPatrolNarration.IsComplete);
            LastShiftNarrationDirector.Notify("AI_F_01",
                signals.InChamber && LastShiftEvaLift.IsAtDeck);
            LastShiftNarrationDirector.Notify("AI_F_01B", signals.InChamber);

            // 승강과 감압이 겹쳐 돌므로 네 줄이 그 겹침의 네 국면이다.
            LastShiftNarrationDirector.Notify("AI_F_02", LastShiftEvaLift.IsMoving);
            LastShiftNarrationDirector.Notify("AI_F_03",
                LastShiftAirlock.IsCycling && !LastShiftEvaLift.IsMoving);
            LastShiftNarrationDirector.Notify("AI_F_04",
                LastShiftAirlock.IsOuterHatchOpen && LastShiftEvaLift.IsMoving);
            LastShiftNarrationDirector.Notify("AI_F_05",
                LastShiftEvaLift.IsAtHullTop && LastShiftAirlock.IsOuterHatchOpen);
            LastShiftNarrationDirector.Notify("AI_F_06", signals.Vacuum);

            // 파밍 - 잔해에 붙어 뜯고, 손이 차면 돌아와 쌓는다.
            LastShiftNarrationDirector.Notify("AI_F_07", signals.AtSalvage);
            LastShiftNarrationDirector.Notify("AI_F_08", LastShiftSalvage.Carried > 0);
            LastShiftNarrationDirector.Notify("AI_F_09",
                LastShiftSalvage.Carried >= LastShiftSalvage.CarryCapacity);
            LastShiftNarrationDirector.Notify("AI_F_10", LastShiftEvaLift.IsMoving);
            // 반입은 <b>횟수</b>로 본다. 잔액 차이로 잡으면 골조를 사서 줄어든 것과 구분이 안 된다
            // - 배지 팝이 같은 이유로 같은 값을 읽는다.
            LastShiftNarrationDirector.Notify("AI_F_11",
                LastShiftMaterials.DepositRevision > 0);
            // AI_F_13 은 선택 줄이다. 한 번에 다 뜯으면 "아직 남음" 이 거짓말이 되므로
            // 그때는 AI_F_14 가 이 줄을 건너뛴다.
            LastShiftNarrationDirector.Notify("AI_F_13", LastShiftSalvage.Remaining > 0);
            LastShiftNarrationDirector.Notify("AI_F_14", LastShiftSalvage.Remaining == 0);

            // 도면 첫 줄. 나머지 여섯은 배치 화면이 직접 민다 - 커서와 확정은 여기서 안 보인다.
            LastShiftNarrationDirector.Notify("AI_B_11",
                LastShiftMaterials.Balance >= LastShiftOutpostCatalog.CheapestMaterialCost);

            // "앞줄 후 N초" 형은 여기서 흐른다. 사건을 먼저 받고 시간을 미는 순서인 것은,
            // 같은 프레임에 사건이 들어온 줄의 시계가 0 에서 시작해야 하기 때문이다.
            LastShiftNarrationDirector.Tick(deltaTime);

            if (LastShiftNarrationDirector.HasLine)
                LastShiftNarrationAudio.Announce(
                    LastShiftNarrationDirector.Current.Id, LastShiftNarrationDirector.Current.Sfx);
        }

        /// <summary>
        /// 자재 배지의 팝 — 조항 <c>T-2</c> 의 결과 셋 중 하나다. <b>반입 횟수를 보고
        /// 잔액을 안 본다</b>: 잔액 차이로 잡으면 골조를 사서 줄어든 것과 구분이 안 되고,
        /// 같은 프레임에 반입과 지불이 겹치면 아무 일도 없던 것으로 읽힌다.
        /// </summary>
        private void AdvanceDepositBadge(float deltaTime)
        {
            if (LastShiftMaterials.DepositRevision != seenDepositRevision)
            {
                seenDepositRevision = LastShiftMaterials.DepositRevision;
                depositBadgePopSeconds = DepositBadgePopSeconds;
                return;
            }

            if (depositBadgePopSeconds > 0f)
                depositBadgePopSeconds = Mathf.Max(0f, depositBadgePopSeconds - deltaTime);
        }

        /// <summary>
        /// 조항 <c>O-7</c> — 선외에서 산소가 마르면 <b>죽지 않고 수확만 잃는다.</b>
        ///
        /// 네 가지가 한 덩어리로 일어나야 한다. 미회수 자재 소실(대가), 갑판 해치 봉인
        /// (에어록 인터록의 셋째 조건을 구조 경로가 건너뛰므로 여기서 되메운다), 에어록
        /// 안쪽 개방(배 안으로 올라올 길), 우주복 재충전(챔버도 비가압이라 이게 없으면
        /// 내려놓자마자 죽는다 — "죽지 않는다" 가 말뿐이 된다).
        /// </summary>
        private void RescueFromExtravehicular(LastShiftPlayerController targetPlayer, LastShiftCrewOxygen crew)
        {
            var lost = LastShiftSalvage.AbandonCarried();
            SetHatchOpen(LastShiftBypassDuct.ForeShaft, false);
            SetHatchOpen(LastShiftBypassDuct.AftShaft, false);
            LastShiftAirlock.ForceRescueEntry();
            targetPlayer.ResetPlayer(LastShiftAirlock.ReturnPoint);
            crew.RefillForRescue();
            Debug.Log($"[LAST_SHIFT_EVA] generation={ResetGeneration} crew={targetPlayer.PlayerSlot} " +
                      $"event=AUTO_RETURN lostChunks={lost} materials={LastShiftMaterials.Balance}");
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
            // <b>산소 시계는 탑승 순간에 시작한다</b>(PM 확정 2026-08-11, 옛 에어록 규칙을
            // 방향만 뒤집어 물려받음). 그전에는 해치를 나가야 탔는데, 그러면 상승 2단과
            // 감압까지 다 지난 뒤라 <c>6.2</c>초가 공짜가 되고 온보딩 문안이 거짓말이 된다 -
            // "발판이 시계의 시작선" 이라고 말하는 순간 아무 일도 안 일어난다.
            //
            // 봉인이 풀린 샤프트 안이 그 조건이다. 게이트가 열리는 순간부터 봉인이 아니고,
            // 승무원이 발판에 올라서는 것도 그때다. 봉인 상태의 샤프트(아무도 안 탄 챔버)는
            // 배 안이므로 여기 안 걸린다.
            if (LastShiftEvaShaft.Contains(position.x, position.z) && !LastShiftAirlock.IsSealed) return true;
            // 선외는 덕트보다 더 확실한 진공이다. 덕트 판정 아래 두는 것은 순서 문제가
            // 아니라 읽는 순서다 — 덕트·에어록은 배 안 좌표라 원반 안이고, 선외 판정은
            // 그 밖을 본다. 이 줄이 없으면 바깥 해치로 나간 승무원이 머리 위 방의 압력을
            // 그대로 받아 산소를 안 태우고, 그러면 §5.5(조항 O-7)가 잴 것이 없어진다.
            if (LastShiftAirlock.IsOutside(position)) return true;
            return IsZoneVacuum(LastShiftZoneAtlas.Resolve(position));
        }

        public bool IsZoneVacuum(LastShiftZone zone)
        {
            return LastShiftVerdictResolver.IsZoneVacuum(zonePressures[zone], IsZoneSealedOff(zone));
        }

        /// <summary>
        /// 산소 계통을 포기해서 밀폐된 구역인가. 진공 판정과 래치 판정이 <b>같은 조건</b>을
        /// 읽어야 한다 — 두 벌로 두면 진공인 구역의 래치가 켜지는 배가 생긴다.
        /// </summary>
        private bool IsZoneSealedOff(LastShiftZone zone) =>
            repairLedger.IsSacrificed(LastShiftShipSystem.Oxygen) && zone == SealedZone;

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
                OxygenSacrificed = repairLedger.IsSacrificed(LastShiftShipSystem.Oxygen),
                CoolingValveHeld = IsCoolingValveHeld
            };
        }

        /// <summary>
        /// 냉각실 수동 순환 밸브가 지금 붙잡혀 있는가(<c>C-3</c>, §4.3). 열 tick 의 하강 항
        /// 하나가 이 값에 걸린다.
        /// </summary>
        public bool IsCoolingValveHeld => coolingValveHolders.Count > 0;

        /// <summary>표시·검증용. 지금 밸브를 잡고 있는 사람 수이며 효과 크기와는 무관하다.</summary>
        public int CoolingValveHolderCount => coolingValveHolders.Count;

        /// <summary>
        /// 네트워크가 없는 경로(솔로 씬·EditMode)의 밸브 입력. 누를 때는 <b>사거리 안에 있는</b>
        /// 승무원을 고르고, 뗄 때는 지금 잡고 있는 사람을 전부 놓는다.
        ///
        /// 누를 때 <c>players[0]</c> 을 쓰지 않는 것이 요점이다 — 로컬 경로도 승무원이 둘 이상일
        /// 수 있고(<c>Configure</c> 가 배열을 받는다), 그러면 밸브 앞에 서 있지 않은 사람이
        /// 대표로 잡히면서 <see cref="PruneCoolingValveHolders"/> 가 같은 프레임에 그를 떼어낸다.
        /// </summary>
        public void SetLocalCoolingValveHeld(bool held)
        {
            if (players == null) return;
            if (!held)
            {
                foreach (var targetPlayer in players)
                    if (targetPlayer != null) SetCoolingValveHeld(targetPlayer, false);
                return;
            }

            var crewMember = players.FirstOrDefault(targetPlayer =>
                targetPlayer != null && LastShiftCoolingValve.IsWithinReach(targetPlayer.transform.position));
            if (crewMember != null) SetCoolingValveHeld(crewMember, true);
        }

        /// <summary>
        /// 밸브 잡기·놓기. <b>유일한 진입점이다</b> — 로컬 키 입력과 서버 RPC 가 둘 다 여기로
        /// 모여야 "누가 잡고 있는가" 가 한 벌로 남는다.
        ///
        /// 거절 조건 셋을 여기서 본다: 사망한 승무원, 사거리 밖, 그리고 판정이 끝난 뒤다.
        /// <b>운석 이전은 막지 않는다</b> — 열 tick 자체가 운석 게이트 아래에 있어 효과가 없고,
        /// 여기서 한 번 더 막으면 같은 규칙이 두 곳에 적히기만 한다. 손잡이는 돌아가고 열은
        /// 안 움직이는 그림이 되지만, 그건 손상 전에는 세 시계가 전부 멎어 있는 것과 같은 사실이다.
        /// </summary>
        public bool SetCoolingValveHeld(LastShiftPlayerController crewMember, bool held)
        {
            if (crewMember == null) return false;
            var wasHeld = IsCoolingValveHeld;

            if (!held)
            {
                if (!coolingValveHolders.Remove(crewMember)) return false;
                LogValve(crewMember, "RELEASE", "input", wasHeld);
                return true;
            }

            if (IsResolved) return false;
            var crew = crewMember.GetComponent<LastShiftCrewOxygen>();
            if (crew != null && crew.IsDead)
            {
                Debug.Log($"[LAST_SHIFT_VALVE] generation={ResetGeneration} action=GRAB result=REJECT reason=crew-dead");
                return false;
            }
            if (!LastShiftCoolingValve.IsWithinReach(crewMember.transform.position)) return false;
            if (coolingValveHolders.Contains(crewMember)) return false;

            coolingValveHolders.Add(crewMember);
            LogValve(crewMember, "GRAB", "input", wasHeld);
            return true;
        }

        /// <summary>
        /// 잡고 있을 자격을 잃은 홀더를 걷어낸다. 매 tick 도는 자리이며 §6-3 의 두 검사 항목
        /// (동시 홀더 · 연결 상실)이 여기서 닫힌다.
        ///
        /// 사거리를 <b>매 tick 다시 보는 것</b>이 §4.3 의 "밸브에서 벗어나면 즉시 0" 이다.
        /// 붙잡은 사람은 이동이 막히지만(<see cref="LastShiftPlayerController"/>), 충격 넉백·
        /// 리스폰·프리셋 리셋처럼 위치가 밖에서 바뀌는 경로가 있고 그때 손이 떨어져야 한다.
        /// </summary>
        private void PruneCoolingValveHolders()
        {
            for (var index = coolingValveHolders.Count - 1; index >= 0; index--)
            {
                var holder = coolingValveHolders[index];
                var reason = ResolveValveDropReason(holder);
                if (reason == null) continue;

                coolingValveHolders.RemoveAt(index);
                if (holder != null) LogValve(holder, "RELEASE", reason, true);
                else Debug.Log($"[LAST_SHIFT_VALVE] generation={ResetGeneration} action=RELEASE reason={reason} held={IsCoolingValveHeld}");
            }
        }

        /// <summary>홀더가 손을 놓아야 하는 사유. 자격이 남아 있으면 <c>null</c> 이다.</summary>
        private static string ResolveValveDropReason(LastShiftPlayerController holder)
        {
            if (holder == null) return "holder-lost";
            var crew = holder.GetComponent<LastShiftCrewOxygen>();
            if (crew != null && crew.IsDead) return "crew-dead";
            return LastShiftCoolingValve.IsWithinReach(holder.transform.position) ? null : "out-of-reach";
        }

        private void LogValve(LastShiftPlayerController crewMember, string action, string reason, bool wasHeld)
        {
            Debug.Log($"[LAST_SHIFT_VALVE] generation={ResetGeneration} crew={crewMember.PlayerSlot} " +
                      $"action={action} reason={reason} holders={coolingValveHolders.Count} " +
                      $"heldBefore={wasHeld} heldAfter={IsCoolingValveHeld} heat={currentState.EngineHeat:F2}");
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

        /// <summary>
        /// 지금 서 있는 자리에서 어떤 수리 프롬프트가 떠야 하는가(<c>C-2</c>, §4.2).
        ///
        /// <b><c>G</c> 는 코드가 멀쩡한데 아무도 안 쓴다.</b> §4.2 가 실측한 이유는 <c>G</c> 를
        /// 누를 수 있는 자리에 서 있으면 대개 <c>C</c> 도 누를 수 있고, <c>C</c> 는 <c>4</c>초에
        /// 계통을 되돌리는데 포기는 악화만 멈추고 회복이 없기 때문이다 — <b>구조적으로 열등</b>하다.
        /// 열등하지 <i>않은</i> 자리가 딱 하나 있다: <b>물건을 못 가져왔을 때</b>. 그때
        /// <c>C</c>·<c>V</c> 는 <see cref="IsRepairSubjectInPlace"/> 에서 조용히 실패하고 <c>G</c> 만
        /// 남는다. 지금까지 화면이 그 사실을 한 번도 말하지 않았다.
        ///
        /// <b>정답 아이콘 금지(<c>concept-draft.md:164</c>)에 안 걸린다.</b> 어느 물건을 가져와야
        /// 하는지는 여전히 말하지 않는다. 바뀌는 것은 "지금 여기서 누를 수 있는 것" 뿐이다.
        ///
        /// 대상 계통은 <see cref="UncontainedSystemMask"/> 로 고른다 — 서버는 계산하고 클라이언트는
        /// 스냅샷으로 받는 값이라 양쪽 화면이 같은 프롬프트를 띄운다.
        /// </summary>
        public bool TryResolveRepairPrompt(Vector3 crewPosition, out LastShiftShipSystem system, out bool subjectInPlace)
        {
            return TryResolveRepairPrompt(crewPosition, out system, out subjectInPlace, out _);
        }

        /// <summary>
        /// <inheritdoc cref="TryResolveRepairPrompt(Vector3, out LastShiftShipSystem, out bool)"/>
        ///
        /// <paramref name="subjectNominal"/> 은 <b>수리 대상 부품의 제자리</b> 좌표다. 화면이
        /// 프롬프트를 그 자리 위에 띄우기 위해 쓴다 — 여기서 이미 고르는 값이므로 다시
        /// 찾게 하면 같은 탐색이 프레임마다 한 번 더 돈다. 이 판정이 애초에 <b>부품의 제자리와
        /// 승무원 거리</b>로 대상을 고르기 때문에, 안내가 붙을 자리도 정의상 그 좌표다.
        /// </summary>
        public bool TryResolveRepairPrompt(
            Vector3 crewPosition,
            out LastShiftShipSystem system,
            out bool subjectInPlace,
            out Vector3 subjectNominal)
        {
            system = LastShiftShipSystem.Cooling;
            subjectInPlace = false;
            subjectNominal = crewPosition;
            if (!HasAppliedImpact || IsResolved) return false;

            var mask = UncontainedSystemMask;
            var best = float.PositiveInfinity;
            var found = false;
            for (var index = 0; index < LastShiftSystemMap.SystemCount; index++)
            {
                if ((mask & (1 << index)) == 0) continue;
                var candidate = (LastShiftShipSystem)index;
                var item = FindItem(LastShiftSystemMap.RoleFor(candidate));
                if (item == null) continue;
                var distance = Vector3.Distance(crewPosition, item.NominalPosition);
                if (distance > SacrificeReachDistance || distance >= best) continue;
                best = distance;
                system = candidate;
                subjectNominal = item.NominalPosition;
                found = true;
            }
            if (!found) return false;

            subjectInPlace = IsRepairSubjectInPlace(system);
            return true;
        }

        /// <summary>부품이 제자리(nominal) 반경 안에 있는가. 들고 있는 상태도 포함한다.</summary>
        private bool IsRepairSubjectInPlace(LastShiftShipSystem system)
        {
            var item = FindItem(LastShiftSystemMap.RoleFor(system));
            return item != null && Vector3.Distance(item.transform.position, item.NominalPosition) <= SecureDistance;
        }

        /// <summary>
        /// 디버그 키가 구간 하나에 직접 들어간다(§8-2). <b>여력은 안 건드린다</b> — 회차만
        /// 옮기므로 이미 받은 기항 수입은 그대로이고, 같은 구간을 다시 판정해도 수입이
        /// 두 번 안 들어온다(<see cref="LastShiftVoyage.SettleSegment"/> 의 회차 조건).
        /// </summary>
        private void EnterSegmentForDebug(LastShiftPreset preset)
        {
            LastShiftVoyage.EnterSegment(LastShiftVoyage.SegmentOf(preset));
            RequestPresetReset(preset);
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
            // 밸브도 놓은 상태로 시작한다. 붙잡음은 상태가 아니라 그 순간의 입력이므로
            // 리셋을 넘겨 살아남으면 안 된다 — 승무원은 스폰 지점으로 돌아가는데 목록에는
            // 냉각실 밸브를 잡고 있는 것으로 남는다.
            coolingValveHolders.Clear();
            dockingSecondsRemaining = LastShiftRecoveryTuning.DockingTimerSeconds;
            ResetGeneration++;
            HasAppliedImpact = false;
            appliedMeteor = default;
            FirstResult = default;
            LastResult = default;
            repairLedger.Reset();
            damagedSystemMask = 0;
            verdict = LastShiftVerdict.Pending;
            // 결과 화면이 읽는 것도 전부 새 항해로 되돌린다. 안 지우면 다음 판 결과 화면이
            // 지난 판의 열 잠금 시간과 죽은 자리를 그대로 원인 줄에 적는다.
            heatProtectionSeconds = 0f;
            hasCrewDeathZone = false;
            lastCrewDeathZone = default;
            runSummary = default;
            verdictRealtime = 0f;
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
            // 새 구간이 열리면 이번 구간의 자극을 미리 굴려 둔다. 씨앗을 세대와 구간에서
            // 뽑으므로 같은 판을 다시 돌리면 같은 사고가 난다 — 재현이 안 되면 "왜 졌는지"
            // 를 못 가리고, 그것이 랜덤화를 미뤄 온 원래 이유였다.
            LastShiftExternalStimulus.BeginSegment(ResetGeneration * 397 + LastShiftVoyage.SegmentIndex);
            Debug.Log($"[LAST_SHIFT_RESET] generation={ResetGeneration} preset={preset} phase=pre-impact " +
                      $"stimulus={LastShiftExternalStimulus.Room}@{LastShiftExternalStimulus.FireAtSeconds:F1}s " +
                      $"severity={LastShiftExternalStimulus.Severity:F3}");
        }

        public bool ApplyMeteorImpact()
        {
            return ApplyMeteorImpact(Meteor);
        }

        /// <summary>
        /// 외부 랜덤 자극의 시계를 돌리고, 터지면 그 방·그 강도로 충격을 넣는다.
        ///
        /// <b>충격 자체는 기존 경로를 그대로 쓴다.</b> 새 손상 타입을 안 만들고
        /// <see cref="ApplyMeteorImpact(LastShiftMeteorStimulus)"/> 로 들어가므로, 강도가
        /// <c>0.7~0.99</c> 인 한 <c>RG-4</c> 가 훑은 범위(상한 <c>0.9924</c>) 안에 있다.
        /// 방마다 다른 부분(§2.1-1 의 (B))만 뒤이어 <see cref="DamageSeconds"/> 동안 서서히
        /// 들어가고, 그쪽이 <c>RG-4</c> 가 못 본 새 조합이라 즉발로 안 넣는다.
        /// </summary>
        private void AdvanceExternalStimulus(float deltaTime)
        {
            // 도입부가 도는 동안은 시계를 멈춘다. 암전·입력 잠금 중에 사고가 나면
            // "대응할 수 없는 시간" 이 생겨 RG-3 이 막으려는 상태와 같아진다.
            if (LastShiftWakeSequence.IsRunning || IsResolved) return;

            var wasFired = LastShiftExternalStimulus.HasFired;
            var delta = LastShiftExternalStimulus.Tick(deltaTime);

            if (!wasFired && LastShiftExternalStimulus.HasFired)
            {
                var room = LastShiftExternalStimulus.Room;
                var severity = LastShiftExternalStimulus.Severity;
                Debug.Log($"[LAST_SHIFT_STIMULUS] action=FIRE room={room} severity={severity:F3} " +
                          $"at={LastShiftExternalStimulus.FireAtSeconds:F1}s zone=" +
                          $"{LastShiftExternalStimulus.BreachZoneOf(room)}");
                ApplyMeteorImpact(ScaleMeteorTo(Meteor, severity));
            }

            if (delta.IsEmpty) return;

            // (A) 맞은 방의 압력. <b>봉합 지점(BreachZone)은 안 옮긴다</b> — 봉합 부품의
            // 정위치가 곧 파공이라, 파공을 피격 방으로 옮기면 그 방을 막을 수단이 없어져
            // RG-3(영구 잠금 금지)이 깨진다. 그래서 맞은 방은 압력을 잃되, 막을 수 있는
            // 구멍은 원래 자리에 남는다.
            // <b>바닥은 여기서 댄다.</b> 자극 기여분에만 걸어야 한다 — 기존 누출·평준화까지
            // 같은 바닥을 쓰면 자극이 도는 20초 동안 그 방의 사고가 통째로 멈춘다.
            zonePressures[delta.Zone] = Mathf.Clamp01(
                LastShiftExternalStimulus.ApplyStimulusPressure(
                    zonePressures[delta.Zone], delta.ZonePressure));

            // (B) 방 고유 계통. 전부 증분이라 기존 감쇠와 같은 자리에서 합쳐진다.
            currentState.BusPower = Mathf.Clamp01(currentState.BusPower + delta.BusPower);
            currentState.EngineHeat = Mathf.Clamp01(currentState.EngineHeat + delta.EngineHeat);
            currentState.FuelReserve = Mathf.Clamp01(currentState.FuelReserve + delta.FuelReserve);
            currentState.ShipAttitudeDegrees =
                Mathf.Clamp(currentState.ShipAttitudeDegrees + delta.AttitudeDegrees, -90f, 90f);
        }

        /// <summary>
        /// 같은 운석을 원하는 심각도로 다시 만든다. <b>속도만 바꾼다</b> — 질량과 방향을
        /// 건드리면 <see cref="LastShiftMeteorApplication.CalculateSeverity"/> 의 입사각 항까지
        /// 같이 움직여서 원하는 값이 안 나온다. 에너지가 속도의 제곱이라 비율의 제곱근을 쓴다.
        /// </summary>
        public static LastShiftMeteorStimulus ScaleMeteorTo(
            LastShiftMeteorStimulus source, float targetSeverity)
        {
            var current = LastShiftMeteorApplication.CalculateSeverity(source);
            if (current <= 0.0001f) return source;
            var scaled = source;
            scaled.Speed = source.Speed * Mathf.Sqrt(Mathf.Max(0f, targetSeverity / current));
            return scaled;
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
            // 방 코드 로비가 떠 있는 동안은 한 줄도 그리지 않는다. 이 컴포넌트는 씬에 있어
            // 세션이 서기 전부터 돌기 때문에, 막지 않으면 로비 위로 목표 줄·계기 막대가
            // 겹쳐 나온다 — 아직 아무도 스폰되지 않아 값도 전부 초기값이다.
            if (LastShiftRoomLobby.IsBlockingGameplay)
            {
                // <b>가장 바깥 출구다.</b> 여기서 빠지면 상시 HUD 도 배너도 한 줄도 안 그려진다.
                // 앞서 붙인 진단 둘이 모두 이 아래에 있어서, 이 자리로 빠지는 판은 로그가
                // 하나도 안 남았다 — 재현 뒤에도 0 건이던 이유가 여기일 수 있다.
                if (LastShiftTutorial.IsArmed && lastBlankBannerState != "lobby")
                {
                    lastBlankBannerState = "lobby";
                    Debug.LogError("[LAST_SHIFT_BANNER] result=LOBBY_BLOCKING " +
                                   "detail=로비가 화면을 잡고 있어 게임 UI 를 한 줄도 안 그린다");
                }

                return;
            }

            // 판정이 나면 상시 패널을 숨긴다(아트 §8). 시뮬레이션이 멎어 있어서 막대가
            // 살아 있는 값처럼 오독되고, 결과 화면과 같은 무게로 읽히면 위계가 무너진다.
            // F3 디버그 층은 QA 도구라 그대로 둔다.
            if (!IsResolved)
            {
                // <b>상시 HUD 는 우측 상단 아이콘 셋뿐이다</b>(아트 규격
                // last-shift-hud-icon-only-v1.md). 좌상단 패널·목표·자원·구역압력·진단은
                // 여기서 안 그린다 — 화면을 가리는 쪽이었고, 아이콘 채움이 같은 것을 말한다.
                //
                // 내레이션과 상호작용 프롬프트는 <b>상시 HUD 가 아니라 상황 안내</b>라 각자
                // 자기 경로로 그대로 뜬다.
                DrawStatusIcons(LastShiftUiLayer.Instance);
            }
            else
            {
                // 상시 HUD 는 이제 프리팹 인스턴스 하나를 계속 들고 산다 — 임대가 아니라서
                // 안 그리는 것만으로는 안 꺼진다. 판정 화면이 뜨면 명시적으로 물러난다(아트 §8).
                var resolvedHud = LastShiftUiLayer.Instance != null ? LastShiftUiLayer.Instance.Hud : null;
                if (resolvedHud != null) resolvedHud.SetVisible(false);
                LastShiftResultScreen.Draw(runSummary, NextPreset, SecondsSinceVerdict);
            }

            DrawTutorialBanner();

            if (debugHudVisible) DrawDebugHud();
        }

        /// <summary>
        /// 튜토리얼 띠 — 조항 <c>T-2</c> 가 요구한 결과 셋 중 <b>화면 몫 둘</b>이다.
        /// 들고 있는 개수와 잔액 배지 팝. 셋째(하치대 누적 프롭)는 아트 몫이라 여기 없다.
        ///
        /// <b>판정 화면 갈래 밖에 그린다.</b> 기항은 구간 판정이 이미 난 뒤라
        /// <see cref="IsResolved"/> 가 참이고, 그 갈래 안에 두면 결과 화면과 같은 층에 겹친다.
        ///
        /// <b>문안은 이 파일에 없다</b> — 단계 문안은 <see cref="LastShiftTutorialCopy"/> 한 곳에
        /// 모여 있다(기획 §8-<c>5</c>). 띠와 배치 화면이 각자 문자열을 들면 같은 단계를 두 말로
        /// 부르게 되므로, 여기서는 머리줄과 안내줄을 <b>받아 그리기만 한다</b>.
        ///
        /// <b>호스트 화면에만 뜬다.</b> 클라이언트는 이 컴포넌트가 꺼져 있고
        /// (<c>LastShiftNetworkSandbox</c> 의 <c>sandbox.enabled = IsServer</c>), 잔해·자재 자체가
        /// 아직 복제 경로를 안 탄다 — 선외 파밍을 서버 권위로 옮기는 카드와 같이 붙는다.
        /// </summary>
        /// <summary>배너가 빈 채로 빠져나간 마지막 상태. 같은 상태를 두 번 안 찍는다.</summary>
        private string lastBlankBannerState;

        /// <summary>어느 빌드가 도는지 한 번만 적었는가.</summary>
        private static bool buildMarkerLogged;

        private void DrawTutorialBanner()
        {
            var layer = LastShiftUiLayer.Instance;
            if (layer == null)
            {
                // <b>여기가 더 나쁜 출구다.</b> 층이 없으면 이 함수뿐 아니라 화면 전체가 비는데,
                // 아래 진단보다 위에 있어서 그 경우 아무 로그도 안 남았다 — 진단을 붙이고도
                // 0 건이 나온 이유가 이 자리일 수 있다.
                if (LastShiftTutorial.IsArmed && lastBlankBannerState != "no-layer")
                {
                    lastBlankBannerState = "no-layer";
                    Debug.LogError("[LAST_SHIFT_BANNER] result=NO_UI_LAYER " +
                                   "detail=UI 층이 없다 — 화면이 통째로 빈다");
                }

                return;
            }

            // 암전이 먼저다 — 튜토리얼 검사보다 위에 둔다. 알파가 0 이면 이 호출은 아무것도
            // 안 빌리므로 평상시 비용이 없다.
            layer.Fade(LastShiftWakeSequence.BlackoutAlpha);
            // 기상이 먼저다 — 도입부가 도는 동안은 디렉터가 아직 한 줄도 안 띄운 상태라
            // 실제로 겹치지 않지만, 순서를 코드에도 남겨 둔다.
            // 상시 경고가 가장 위다 — 지금 하던 일과 무관하게 끼어드는 상태 통지라,
            // 뜨는 동안은 진행 대사도 도입부도 덮는다.
            if (LastShiftStandingNarration.HasLine)
            {
                DrawNarrationBanner(layer, LastShiftStandingNarration.Current,
                    LastShiftStandingNarration.LineElapsedSeconds, 1f, 1f,
                    LastShiftStandingNarration.PanelTone);
                return;
            }

            if (LastShiftWakeSequence.HasLine)
            {
                DrawNarrationBanner(layer, LastShiftWakeSequence.Current,
                    LastShiftWakeSequence.LineElapsedSeconds,
                    LastShiftWakeSequence.PanelAlpha, LastShiftWakeSequence.LineAlpha,
                    typed: !LastShiftWakeSequence.IsOpeningLine);
                return;
            }

            if (LastShiftPatrolNarration.HasLine && !LastShiftNarrationDirector.HasLine)
            {
                DrawNarrationBanner(layer, LastShiftPatrolNarration.Current,
                    LastShiftPatrolNarration.LineElapsedSeconds, 1f, 1f,
                    typingSeconds: LastShiftPatrolNarration.TypingSeconds);
                return;
            }

            if (LastShiftNarrationDirector.HasLine)
            {
                DrawNarrationBanner(layer, LastShiftNarrationDirector.Current,
                    LastShiftNarrationDirector.LineElapsedSeconds, 1f, 1f);
                return;
            }

            if (!LastShiftTutorial.IsRunning)
            {
                // <b>여기가 화면이 비는 유일한 출구다.</b> 위 세 분기가 전부 안 걸리고 이 조건도
                // 거짓이면 이 함수는 아무것도 안 그린다. 자동 재현으로는 이 자리에 못 갔는데
                // 사용자는 계속 재현하므로, 실제로 여기 닿는 순간의 값을 스스로 남긴다.
                //
                // <b>상태가 바뀔 때만 찍는다.</b> 프레임마다 찍으면 로그가 초당 60줄로 불어나
                // 정작 그 순간을 못 찾는다. 온보딩이 끝난 평시에도 여기로 오므로(무장이 풀린
                // 뒤) 무장 중일 때만 본다 — 그때가 "안내가 있어야 하는데 없는" 구간이다.
                if (LastShiftTutorial.IsArmed)
                {
                    var blank = $"patrol={LastShiftPatrolNarration.HasLine}" +
                                $" director={LastShiftNarrationDirector.HasLine}" +
                                $" standing={LastShiftStandingNarration.HasLine}" +
                                $" wake={LastShiftWakeSequence.IsRunning}" +
                                $" step={LastShiftTutorial.Step}" +
                                $" patrolComplete={LastShiftPatrolNarration.IsComplete}";
                    if (blank != lastBlankBannerState)
                    {
                        lastBlankBannerState = blank;
                        Debug.LogWarning($"[LAST_SHIFT_BANNER] result=BLANK {blank}");
                    }
                }

                return;
            }

            lastBlankBannerState = null;

            // 머리줄 · 안내줄 · 계기줄 셋이라 자리를 한 줄 더 잡는다. 안내줄이 머리줄보다
            // 길어서 계기 셋과 같은 줄에 붙이면 잘린다.
            var screen = LastShiftUiLayer.ScreenSize;
            var banner = LastShiftHudLayout.OnboardingNarrationRect(screen.x, screen.y);
            layer.OnboardingPanel("tutorial", banner, 1f);
            var top = banner.y;
            layer.Label("tutorialSpeaker", new Rect(banner.x + 42f, top + 18f, 420f, 24f),
                "선내 관리 시스템", 16, new Color(0.32f, 0.82f, 0.82f), fontStyle: FontStyle.Bold);
            layer.Label("tutorialHeading", new Rect(banner.x + 42f, top + 48f, banner.width - 220f, 24f),
                LastShiftTutorialCopy.Heading(LastShiftTutorial.Step),
                LastShiftHudLayout.HeadingFontSize, LastShiftUiTheme.Ivory, fontStyle: FontStyle.Bold);

            // 안내는 단계에 머문 시간으로 재촉으로 갈린다(LastShiftTutorialCopy 주석) —
            // 그 시간은 상태기가 이미 세고 있어서 여기서 따로 잴 것이 없다.
            layer.Label("tutorialGuide", new Rect(banner.x + 42f, top + 80f, banner.width - 84f, 48f),
                LastShiftTutorialCopy.Guide(LastShiftTutorial.Step, LastShiftTutorial.StepElapsedSeconds),
                38, LastShiftUiTheme.Ivory, wrap: true);

            layer.Label("tutorialCarried", new Rect(banner.x + 42f, top + 142f, 240f, 24f),
                $"들고 있음 {LastShiftSalvage.Carried}/{LastShiftSalvage.CarryCapacity}",
                LastShiftHudLayout.BodyFontSize, LastShiftUiTheme.BodyText);
            layer.Label("tutorialRemaining", new Rect(banner.x + 286f, top + 142f, 220f, 24f),
                $"잔해 남음 {LastShiftSalvage.Remaining}",
                LastShiftHudLayout.BodyFontSize, LastShiftUiTheme.BodyText);

            // 배지는 팝 하는 동안만 굵고 밝다. 조각을 하나로 두고 모양만 갈아 끼우는 이유는
            // 둘로 나누면 팝이 끝나는 프레임에 두 조각이 같이 보이기 때문이다.
            var popping = depositBadgePopSeconds > 0f;
            layer.Label("tutorialBalance", new Rect(banner.x + 510f, top + 142f, 250f, 24f),
                popping
                    ? $"자재 {LastShiftMaterials.Balance}  ▲ +{LastShiftMaterials.LastDeposited}"
                    : $"자재 {LastShiftMaterials.Balance}",
                popping ? LastShiftHudLayout.BadgeFontSize : LastShiftHudLayout.BodyFontSize,
                popping ? DepositBadgeColor : LastShiftUiTheme.BodyText,
                fontStyle: popping ? FontStyle.Bold : FontStyle.Normal);
            layer.Label("tutorialLog", new Rect(banner.x + banner.width - 180f, top + 20f, 138f, 20f),
                $"LOG / {(int)LastShiftTutorial.Step:00}", 14, new Color(0.32f, 0.82f, 0.82f),
                anchor: TextAnchor.UpperRight);
        }

        /// <summary>
        /// 기상 도입부의 대사 한 줄(정본 §4-1). 뜬 동안은 <b>단계 띠를 대신 그린다</b> —
        /// 도입부는 아직 단계 열거형에 자리가 없어서(§7-<c>5</c> 의 <c>+2</c> 재매김이 아직
        /// 안 왔다) 그 사이 <c>Step</c> 은 잔해 확인에 가 있고, 그 문안이 같이 뜨면 방에서
        /// 못 일어난 사람에게 잔해 이야기를 하게 된다.
        ///
        /// <b>머리줄과 계기줄을 안 그린다.</b> 번호는 아직 없는 것이고, 자재·잔해 수는
        /// 숙소를 나가기 전에는 셋 다 <c>0</c> 이라 읽을 것이 없다.
        /// </summary>
        private static void DrawNarrationBanner(LastShiftUiLayer layer,
            in LastShiftNarrationScript.Line line, float lineElapsed,
            float panelAlpha, float lineAlpha,
            LastShiftOnboardingPanelTone tone = LastShiftOnboardingPanelTone.Normal,
            bool typed = true, float typingSeconds = 0f)
        {
            var screen = LastShiftUiLayer.ScreenSize;
            var banner = LastShiftHudLayout.OnboardingNarrationRect(screen.x, screen.y);

            // 검정 위에서는 글자 한 줄이 <b>화면 한가운데</b> 있고, 월드가 떠오르는 동안
            // 계기판 자리로 내려온다. 띠도 같은 구간에 같이 떠오른다 — 배가 보이는 것과
            // 계기가 돌아오는 것이 한 동작이어야 두 번 페이드한 것으로 안 보인다(art 규격).
            var seated = Rect.MinMaxRect(
                banner.xMin,
                Mathf.Lerp((screen.y - banner.height) * 0.5f, banner.yMin, panelAlpha),
                banner.xMax,
                Mathf.Lerp((screen.y + banner.height) * 0.5f, banner.yMax, panelAlpha));

            if (panelAlpha > 0f) layer.OnboardingPanel("tutorial", seated, tone, panelAlpha);

            // 화자 이름은 띠와 함께 온다. 검정 위에 두 줄이 뜨면 "로그 한 줄" 이 아니게 된다.
            if (panelAlpha > 0f)
                layer.Label("tutorialSpeaker", new Rect(seated.x + 42f, seated.y + 18f, 420f, 24f),
                    "선내 관리 시스템", 16,
                    new Color(0.32f, 0.82f, 0.82f, panelAlpha), fontStyle: FontStyle.Bold);

            // 재촉은 그 줄이 뜬 뒤 시간으로 갈린다(조항 N-1 — 재촉에는 신호음이 없다).
            // 재촉으로 갈리면 <b>다시 찍는다</b> — 새 문장이 통째로 나타나면 앞 문장이
            // 그 자리에서 갈아치워진 것으로 보여 읽던 사람이 흐름을 놓친다.
            var swapped = line.HasNudge && lineElapsed >= line.NudgeAfterSeconds;
            var text = swapped ? line.Nudge : LastShiftNarrationScript.Format(line);
            if (typed)
                text = LastShiftNarrationScript.Reveal(
                    text, swapped ? lineElapsed - line.NudgeAfterSeconds : lineElapsed,
                    typingSeconds > 0f ? typingSeconds : LastShiftNarrationScript.TypingSeconds);
            var ivory = LastShiftUiTheme.Ivory;
            layer.Label("tutorialGuide", new Rect(seated.x + 42f, seated.y + 60f, seated.width - 84f, 96f),
                text, 38, new Color(ivory.r, ivory.g, ivory.b, ivory.a * Mathf.Clamp01(lineAlpha)),
                wrap: true, anchor: panelAlpha > 0f ? TextAnchor.UpperLeft : TextAnchor.UpperCenter);

            // 조작 프롬프트가 있는 줄은 그 한 줄을 더 단다(조항 T-3 — 실패 사유 대신 조작).
            if (line.HasPrompt && panelAlpha > 0f)
                layer.Label("tutorialPrompt",
                    new Rect(seated.x + 42f, seated.y + 150f, seated.width - 84f, 24f),
                    line.Prompt, LastShiftHudLayout.BodyFontSize, LastShiftUiTheme.BodyText);
        }

        /// <summary>
        /// 상시 HUD — <b>우측 상단 아이콘 셋</b>. 숫자도 이름도 눈금도 없고, 아이콘이 아래에서
        /// 위로 차오르는 것이 상태의 전부다(아트 규격).
        ///
        /// <b>산소·전력은 잔량, 열은 축적</b>이다 — 앞의 둘은 비면 나쁘고 열은 차면 나쁘다.
        /// 셋을 <b>같은 프레임에</b> 읽는다.
        /// </summary>
        private void DrawStatusIcons(LastShiftUiLayer layer)
        {
            // <b>자리 계산이 여기 없다.</b> 아이콘 위치·크기·간격은 프리팹
            // (Resources/LastShiftHud)의 RectTransform 이 정본이고, 여기서는 값과 색만 민다.
            // 예전에는 이 자리에서 매 프레임 Rect 를 계산해 얹었는데, 그러면 아이콘을 조금
            // 옮기는 일이 코드 수정과 재컴파일이 되어 에디터에서 끌 수가 없었다.
            var hud = layer != null ? layer.Hud : null;
            if (hud == null) return;

            hud.SetVisible(true);
            hud.Set(LastShiftUiIcon.Oxygen, currentState.OxygenPressure,
                StatusTone(currentState.OxygenPressure, true, LastShiftUiIcon.Oxygen));
            hud.Set(LastShiftUiIcon.Power, currentState.BusPower,
                StatusTone(currentState.BusPower, true, LastShiftUiIcon.Power));
            hud.Set(LastShiftUiIcon.Heat, currentState.EngineHeat,
                StatusTone(currentState.EngineHeat, false, LastShiftUiIcon.Heat));
        }

        /// <summary>상태색 경계(아트 규격). <b>산소 내레이션 임계 45/35 와 다른 값이다.</b></summary>
        private const float StatusToneLow = 0.35f;

        /// <summary>같은 규격의 위쪽 경계.</summary>
        private const float StatusToneHigh = 0.60f;

        /// <summary>
        /// 아이콘 색(아트 규격 <c>last-shift-hud-icon-only-v1.md</c>).
        ///
        ///   잔량형(산소·전력)  <c>&gt;0.60</c> 청록 · <c>0.35~0.60</c> 주황 · <c>&lt;0.35</c> 적색
        ///   축적형(열)         <c>&lt;0.35</c> 청록 · <c>0.35~0.60</c> 주황 · <c>&gt;0.60</c> 적색
        ///
        /// <b>값을 뒤집지 않고 비교를 뒤집는다.</b> 처음에는 "나쁜 정도" 로 정규화해서
        /// <c>1-value</c> 에 같은 경계를 댔는데, 그러면 잔량형에서 경계가 <c>0.40/0.65</c> 로
        /// 밀린다 — 실제로 <c>0.61~0.65</c> 가 청록이어야 하는데 주황이었고 <c>0.36</c> 은
        /// 주황이어야 하는데 적색이었다(game-art 지적).
        /// </summary>
        private static Color StatusTone(float value, bool higherIsBetter, LastShiftUiIcon icon)
        {
            var v = Mathf.Clamp01(value);
            var nominal = higherIsBetter ? v > StatusToneHigh : v < StatusToneLow;
            if (nominal) return LastShiftUiTheme.Nominal;

            var fault = higherIsBetter
                ? v >= StatusToneLow
                : v <= StatusToneHigh;
            if (fault) return LastShiftUiTheme.Fault;

            // 산소만 위기에서 밝기 펄스를 쓴다 — 다른 계기와 같은 박자여야 같은 사건으로 읽힌다.
            return icon == LastShiftUiIcon.Oxygen
                ? LastShiftUiTheme.PulseCrisis(Time.unscaledTime)
                : LastShiftUiTheme.Crisis;
        }

        /// <summary>검사가 색 구간을 직접 재는 자리. 규격이 바뀌면 여기서 먼저 걸린다.</summary>
        public static Color StatusToneForProbe(float value, bool higherIsBetter, LastShiftUiIcon icon)
            => StatusTone(value, higherIsBetter, icon);

        /// <summary>잔액 배지 팝 색. 정상 초록보다 밝아 한 프레임 안에 눈에 든다.</summary>
        private static readonly Color DepositBadgeColor = new(0.6f, 1f, 0.7f);

        /// <summary>
        /// 층1-a 목표 줄. 성공 조건 둘과 남은 시간. <b>숫자는 시간만 노출한다</b>(§5.2) —
        /// 성공 조건의 임계는 아래 막대의 선으로 그려지지 텍스트로 적히지 않는다.
        /// </summary>
        private void DrawObjectiveLine(LastShiftUiLayer layer)
        {
            if (layer == null) return;

            layer.Label("objective", new Rect(28f, 24f, 480f, 28f),
                "목표 — 추력과 산소를 선 위로 올려 도킹",
                LastShiftHudLayout.HeadingFontSize, Color.white, fontStyle: FontStyle.Bold);

            // <b>운석 전에는 카운트다운을 그리지 않는다.</b> 도킹 제한시간은 사고 이후 예산이라
            // 그 전에는 흐르지 않는데(AdvanceMission 의 HasAppliedImpact 게이트), 멈춘 숫자를
            // 카운트다운 모양으로 띄우면 "시간이 아예 안 간다" 로 읽힌다. 실제로 플레이
            // 확인에서 그 오해가 났다.
            //
            // 근본은 CT-01 §3.2 의 T-20초 예고 + 자동 운석(백로그 R5)이 아직 없어서 이 구간이
            // 20초가 아니라 무한정이라는 것이다. R5 가 들어오면 이 분기는 예고 카운트다운으로
            // 바뀐다.
            // 두 갈래가 같은 조각을 쓴다. 한 프레임에 하나만 그려지므로 자리가 겹치지 않고,
            // 조각을 둘로 나누면 운석이 떨어지는 프레임에 둘이 같이 보인다.
            var timer = new Rect(508f, 24f, 180f, 28f);
            if (!HasAppliedImpact)
            {
                layer.Label("dockTimer", timer, "사건 대기 · M",
                    LastShiftHudLayout.HeadingFontSize, Color.white, fontStyle: FontStyle.Bold);
                return;
            }

            var minutes = Mathf.FloorToInt(dockingSecondsRemaining / 60f);
            var seconds = Mathf.FloorToInt(dockingSecondsRemaining % 60f);
            layer.Label("dockTimer", timer, $"DOCK T-{minutes}:{seconds:00}",
                LastShiftHudLayout.HeadingFontSize, Color.white, fontStyle: FontStyle.Bold);
        }

        /// <summary>
        /// 층1-b 계통 막대 셋(§5.7.3). <b>배 전역 단일값만 막대로 그린다</b> — 추력·전력·열.
        /// 산소는 방마다 값이 달라 막대가 아니라 아래 구역 4칸이 맡는다.
        ///
        /// <b>임계를 그림으로 보여주는 것이 요점이다</b>(§5.2) — <c>thrust=0.28</c> 이라는
        /// 숫자보다 "막대가 선 아래다" 가 즉시 읽힌다.
        /// </summary>
        private void UpdateSystemGauges(LastShiftUiLayer layer)
        {
            if (layer == null) return;

            var thrust = ApplyGauge(layer, "thrust", LastShiftUiIcon.Thrust,
                0, "추력", currentState.ThrustDemand, HigherIsBetter,
                LastShiftRecoveryTuning.DockingSuccessThrust);

            // G-2(b) 필요 추력선. <c>(150 − DockProgress) / 남은 초</c> 를 추력 게이지 위에
            // 얹는다. <b>고정 임계선과 다른 색·다른 굵기인 것이 요점이다</b> — 0.30 은 도킹
            // 순간의 조건이라 안 움직이고, 이 선은 내가 추력을 어떻게 썼는지에 따라 매 초
            // 움직인다. 운석 전에는 타이머가 안 흘러 선이 멈춰 있으므로 그리지 않는다.
            thrust.SetMovingMarker(HasAppliedImpact && !IsResolved && RequiredThrust > 0f ? RequiredThrust : -1f);

            // §5.7.6 미결2 는 "전력에도 등급 경계가 있다면" 이었는데, 이미 있다 —
            // S-P1/P2/P3 발동선 0.65/0.40/0.15 다. 새로 정할 값이 없어 그대로 쓴다.
            ApplyGauge(layer, "power", LastShiftUiIcon.Power,
                1, "전력", currentState.BusPower, HigherIsBetter,
                LastShiftSituationTable.BusDetachedTrigger,
                LastShiftSituationTable.PowerCascadeTrigger,
                LastShiftSituationTable.PowerBlackoutTrigger);

            // 열만 반대다 — 올라가는 것이 나쁘다. 선 셋은 S-H1/H2/H3 등급 경계 그대로이며
            // 여기서 다시 계산하지 않는다(§5.7.2).
            ApplyGauge(layer, "heat", LastShiftUiIcon.Heat,
                2, "열", currentState.EngineHeat, HigherIsWorse,
                LastShiftSituationTable.HeatCouplingTrigger,
                LastShiftSituationTable.HeatRunawayTrigger,
                LastShiftSituationTable.HeatLockTrigger);

            // G-2(a) 넷째 줄. 승리 조건 셋 중 하나가 F3 뒤에만 있어서 아무도 못 보던 자리다.
            // 임계선이 없다 — 목표는 게이지가 가득 차는 것 자체이고, "지금 충분한가" 는 위의
            // 필요 추력선이 답한다.
            var docking = LastShiftResourceGauges.Docking(
                currentState.DockProgress, LastShiftRecoveryTuning.DockTargetThrustSeconds);
            var dockGauge = layer.Gauge("docking", LastShiftUiIcon.Docking, LastShiftHudLayout.SystemGaugeRect(3));
            dockGauge.SetName("도킹");
            dockGauge.SetValue(docking.Fill);
            dockGauge.SetValueLabel(docking.ValueLabel);
            dockGauge.SetTone(LastShiftResourceGauges.ToneOf(docking.Grade, Time.unscaledTime));
            dockGauge.SetThresholds();
            dockGauge.SetMovingMarker(-1f);
        }

        /// <summary>
        /// 계통 게이지 한 줄. <b>"나쁜 쪽" 판정은 첫 임계선 하나로 한다</b> — 열은 첫 선
        /// (S-H1)을 넘는 순간부터 나쁘고, 추력·전력은 선 아래로 내려가는 순간부터 나쁘다.
        /// 길이만으로는 "모자라다" 가 안 읽히고, 그 판정을 눈대중에 맡기면 임계선을 그린
        /// 의미가 없다.
        /// </summary>
        private static LastShiftGaugeView ApplyGauge(
            LastShiftUiLayer layer, string id, LastShiftUiIcon icon,
            int row, string label, float value, bool higherIsWorse, params float[] thresholds)
        {
            var fill = Mathf.Clamp01(float.IsNaN(value) ? 0f : value);
            var first = thresholds.Length > 0 ? thresholds[0] : 0f;
            var bad = higherIsWorse ? fill >= first : fill < first;

            var gauge = layer.Gauge(id, icon, LastShiftHudLayout.SystemGaugeRect(row));
            gauge.SetName(label);
            gauge.SetValue(fill);
            gauge.SetValueLabel(string.Empty);
            gauge.SetTone(bad ? LastShiftUiTheme.Crisis : LastShiftUiTheme.Nominal);
            gauge.SetThresholds(thresholds);
            return gauge;
        }

        /// <summary>
        /// 자원 넷 — 정비여력·자재·산소·식량. 값 환산은
        /// <see cref="LastShiftResourceGauges"/> 가 하고 여기서는 자리만 잡는다.
        ///
        /// <b>산소는 지금 서 있는 구역의 압력이다.</b> 배 전체 평균이 아닌 이유는 격리의
        /// 효과가 평균에서는 안 보이기 때문이고, 그건 구역 4칸이 존재하는 이유와 같다.
        /// 승무원이 아직 안 떴으면(에디터에서 플레이어 없이 켠 경우) 줄을 비운다.
        /// </summary>
        private void UpdateResourceGauges(LastShiftUiLayer layer)
        {
            if (layer == null) return;

            ApplyResourceGauge(layer, "maintenance", LastShiftUiIcon.Maintenance, 0, "정비여력", LastShiftResourceGauges.Maintenance());
            ApplyResourceGauge(layer, "materials", LastShiftUiIcon.Materials, 1, "자재", LastShiftResourceGauges.Materials());

            var oxygen = TryResolveLocalZone(out var zone)
                ? LastShiftResourceGauges.Oxygen(zonePressures[zone], ZoneOxygenGradeOf(zone))
                : LastShiftGaugeReadout.Missing;
            ApplyResourceGauge(layer, "oxygen", LastShiftUiIcon.Oxygen, 2, "산소", oxygen);

            ApplyResourceGauge(layer, "food", LastShiftUiIcon.Food, 3, "식량", LastShiftResourceGauges.Food());
        }

        private static void ApplyResourceGauge(
            LastShiftUiLayer layer, string id, LastShiftUiIcon icon,
            int row, string label, in LastShiftGaugeReadout readout)
        {
            // 계통이 없는 축은 <b>0 으로 채워 그리지 않는다</b> — 빈 게이지는 "정보 없음" 이
            // 아니라 "바닥났다" 로 읽힌다. 아예 안 빌리면 다음 프레임에 줄이 사라진다.
            if (!readout.Available) return;

            var gauge = layer.Gauge(id, icon, LastShiftHudLayout.ResourceGaugeRect(row));
            gauge.SetName(label);
            gauge.SetValue(readout.Fill);
            gauge.SetValueLabel(readout.ValueLabel);
            gauge.SetTone(LastShiftResourceGauges.ToneOf(readout.Grade, Time.unscaledTime));
            gauge.SetThresholds();
        }

        private const bool HigherIsBetter = false;
        private const bool HigherIsWorse = true;

        /// <summary>
        /// 상시 지배 문제 1행(§5.7.3·§5.7.5). <b>운석 전에도 채워져 있다</b> — 프리셋이 t=0 에
        /// 이미 갖고 있는 상황을 그대로 읽는다. 운석 이후 전용인 <c>FIRST DOMINANT</c> 와는
        /// 다른 값이고, 그쪽은 <c>F3</c> 에만 남겼다.
        ///
        /// <b>어느 계통인지까지만 말한다.</b> 원인과 인과사슬은 §3.1 이 금지한 자리다.
        /// </summary>
        private void DrawDominantProblemLine(LastShiftUiLayer layer)
        {
            if (layer == null) return;

            var line = "계통 이상 없음";
            var color = Color.white;

            if (TryResolveDominantChannel(out var channel, out var grade))
            {
                line = $"{LastShiftSituationText.ChannelLocationLabel(channel)} 이상 · " +
                       $"{LastShiftSituationText.GradeLabel(grade)}";
                color = GradeColor(grade);
            }
            else if (TryResolveWorstOxygenZone(out var zone, out var oxygenGrade))
            {
                line = $"{LastShiftZoneAtlas.ShortLabelOf(zone)} 산소 · " +
                       $"{LastShiftSituationText.GradeLabel(oxygenGrade)}";
                color = GradeColor(oxygenGrade);
            }

            layer.Label("dominant",
                new Rect(LastShiftHudLayout.ContentX, LastShiftHudLayout.DominantLineTop, 430f, 26f),
                line, LastShiftHudLayout.HeadingFontSize, color, fontStyle: FontStyle.Bold);
        }

        private bool TryResolveWorstOxygenZone(out LastShiftZone zone, out LastShiftSituationGrade grade)
        {
            zone = default;
            grade = LastShiftSituationGrade.Normal;
            for (var index = 0; index < LastShiftZoneAtlas.ZoneCount; index++)
            {
                var candidate = (LastShiftZone)index;
                var candidateGrade = ZoneOxygenGradeOf(candidate);
                if (candidateGrade <= grade) continue;
                grade = candidateGrade;
                zone = candidate;
            }

            return grade > LastShiftSituationGrade.Normal;
        }

        /// <summary>
        /// 배터리 상태(§5.7.3). <b>막대가 아니다</b> — 장착 여부는 이산값이고, 연속값으로
        /// 그리면 없는 정보를 지어내는 것이다. 미장착이면 어느 구역에 있는지까지 말한다.
        /// 그건 원인이 아니라 위치라서 §3.1 에 걸리지 않는다.
        /// </summary>
        private void DrawBatteryState(LastShiftUiLayer layer)
        {
            if (layer == null) return;

            var battery = FindItem(LastShiftItemRole.Battery);
            if (battery == null) return;

            var text = battery.Secured
                ? "배터리 장착됨"
                : $"배터리 미장착 · {LastShiftZoneAtlas.ShortLabelOf(LastShiftZoneAtlas.Resolve(battery.transform.position))}";

            layer.Label("battery", new Rect(478f, LastShiftHudLayout.DominantLineTop + 2f, 240f, 24f),
                text, LastShiftHudLayout.BodyFontSize,
                battery.Secured ? Color.white : LastShiftUiTheme.Unstable);
        }

        /// <summary>
        /// 층3 — <b>구역 안에서만</b> 원인 1행이 나온다(§5.3). 밖에서는 등급 칸까지가 전부다.
        ///
        /// 관측자는 로컬 플레이어 하나다. 솔로/호스트에서는 자기 캐릭터가 서 있는 구역이고,
        /// 아무도 없으면(에디터에서 플레이어를 안 띄운 경우) 아무것도 안 그린다 — 여기서
        /// 조종석을 기본값으로 삼으면 "구역 안" 조건이 사실상 사라진다.
        /// </summary>
        private void DrawLocalDiagnosis(LastShiftUiLayer layer)
        {
            if (layer == null) return;
            if (!TryResolveLocalZone(out var zone)) return;

            var situation = DominantSituationOf(zone);
            var cause = LastShiftSituationText.CauseLine(situation);
            if (string.IsNullOrEmpty(cause)) return;

            layer.Label("diagnosis",
                new Rect(LastShiftHudLayout.ContentX, LastShiftHudLayout.DiagnosisTop, 664f, 24f),
                $"[{LastShiftZoneAtlas.ShortLabelOf(zone)}] {cause}",
                LastShiftHudLayout.BodyFontSize, LastShiftUiTheme.BodyText, wrap: true);
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
            var layer = LastShiftUiLayer.Instance;
            if (layer == null) return;

            // 사이렌 색은 프레임마다 흔들린다. IMGUI 시절에는 스타일 객체 하나의 색을 덮어
            // 썼는데, 그러면 그 스타일을 같이 쓰는 다른 줄까지 같이 깜빡였다.
            var sirenColor = sirenActive
                ? Color.Lerp(LastShiftUiTheme.BodyText, new Color(1f, 0.25f, 0.18f), BlinkPhase)
                : LastShiftUiTheme.BodyText;

            const float cellWidth = 132f;
            const float doorWidth = 52f;
            var x = originX;
            layer.Label("zoneHeader", new Rect(x, originY, 34f, 24f), "구역",
                LastShiftHudLayout.BodyFontSize, sirenColor, fontStyle: FontStyle.Bold);
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
                    layer.Label($"zoneDoor{index}", new Rect(x, originY, doorWidth, 24f),
                        open ? "─┤├─" : "─┫┣─", LastShiftHudLayout.BodyFontSize, LastShiftUiTheme.BodyText);
                    x += doorWidth;
                }

                // <b>등급만 쓴다. 압력 수치를 여기 적지 않는다</b>(§5.2) — 그게 예전 화면이
                // 셋째 층을 상시로 새어 보내던 자리다. 수치는 개구부 앞에서만 읽힌다(§5.3).
                // 그리고 <b>산소만 본다</b> — 계통값을 방 칸에 겹치지 않는 이유는 §5.7.3 이다.
                var grade = ZoneOxygenGradeOf(zone);
                layer.Label($"zoneCell{index}", new Rect(x, originY, cellWidth, 24f),
                    $"{LastShiftZoneAtlas.ShortLabelOf(zone)} {LastShiftSituationText.GradeLabel(grade)}",
                    LastShiftHudLayout.BodyFontSize, GradeColor(grade),
                    fontStyle: grade == LastShiftSituationGrade.Crisis ? FontStyle.Bold : FontStyle.Normal);
                x += cellWidth;
            }

            if (sirenActive)
                layer.Label("zoneSiren", new Rect(originX, originY + 22f, ZonePressureRowWidth, 20f),
                    "⚠ 전선 경보: 산소 위험 (전 구역)",
                    LastShiftHudLayout.BodyFontSize, sirenColor, fontStyle: FontStyle.Bold);
        }

        /// <summary>
        /// N8 조건부 개인 예비 산소 막대. 소모가 시작된 승무원에게만 나타난다. 사이렌 시점에
        /// 이 막대를 띄우지 않는 것이 의도다 — 겹치면 사이렌이 곧 사망 예고가 된다.
        /// </summary>
        private void DrawSuitOxygenGauges(LastShiftUiLayer layer)
        {
            if (players == null || layer == null) return;
            var row = 0;
            foreach (var targetPlayer in players)
            {
                if (targetPlayer == null) continue;
                var crew = targetPlayer.GetComponent<LastShiftCrewOxygen>();
                if (crew == null || !crew.ShowsSuitGauge) continue;
                LastShiftCrewOxygen.ApplyGauge(layer, crew, $"suit{row}", targetPlayer.PlayerSlot.ToString(), row);
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
            var layer = LastShiftUiLayer.Instance;
            if (layer == null) return;

            var debugPanel = LastShiftHudLayout.DebugPanelRect;
            layer.Panel("debug", debugPanel, 0.92f);
            layer.Label("debugHeading", new Rect(debugPanel.x + 12f, debugPanel.y + 6f, 650f, 24f),
                "[DEBUG F3]", LastShiftHudLayout.HeadingFontSize, Color.white, fontStyle: FontStyle.Bold);
            layer.Label("debugBody", new Rect(debugPanel.x + 12f, debugPanel.y + 34f, 690f, 170f),
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
                $"{LastShiftRecoveryTuning.DockTargetThrustSeconds:F0} thrust·s  req_thrust={RequiredThrust:F2}  " +
                $"heat_lock={heatProtectionSeconds:F0}s  hold={controlHold.RemainingSeconds:F1}s\n" +
                $"first_dominant={(HasAppliedImpact ? FirstResult.Problem.ToString() : "pending meteor")}  " +
                $"current_dominant={(HasAppliedImpact ? LastResult.Problem.ToString() : "-")}\n" +
                $"cause_chain: {(HasAppliedImpact ? LastResult.CauseChain : "-")}\n" +
                "WASD/Space/E/F/Mouse | 1·2·3 프리셋 | R 리셋 | M 운석 | 화살표 조종(8초) | " +
                "판정 후 Space 다음 판 | F3 디버그",
                LastShiftHudLayout.BodyFontSize, LastShiftUiTheme.BodyText, wrap: true);
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
