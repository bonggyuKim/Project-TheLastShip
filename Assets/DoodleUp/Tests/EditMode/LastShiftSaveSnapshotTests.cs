using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 세이브 백본 1단계 — 스냅샷 필드 확장과 주입/권위이관 분리
    /// (<c>docs/tech/save-backbone-feasibility-v1.md</c> §1.3).
    ///
    /// 여기서 지키는 것은 두 가지다. <b>왕복이 값을 잃지 않을 것</b>(§2.2 합격선: 저장→로드 후
    /// B층 전 필드 동일)과 <b>주입이 권위를 같이 넘기지 않을 것</b>(§1.3-가).
    /// </summary>
    public sealed class LastShiftSaveSnapshotTests
    {
        [Test]
        public void RestoringWithLocalAuthorityReproducesEveryCapturedField()
        {
            // 필드를 하나 늘리고 Capture 나 Restore 한쪽만 배선하는 것이 이 카드의 유일한
            // 실패 방식이다. 전 필드를 서로 다른 값으로 채운 뒤 왕복시키면 그게 바로 걸린다.
            var harness = Harness.Create();
            var saved = FullyPopulatedSnapshot();

            harness.Sandbox.ApplyNetworkSnapshot(saved, LastShiftStateAuthority.Local);
            var reloaded = harness.Sandbox.CaptureRuntimeSnapshot();

            Assert.That(reloaded.Equals(saved), Is.True,
                $"왕복이 필드를 잃었다.\n saved   : {Describe(saved)}\n reloaded: {Describe(reloaded)}");
            harness.Dispose();
        }

        [Test]
        public void LocalAuthorityKeepsComputingTheUncontainedMaskInsteadOfTrustingIt()
        {
            // §1.3-가 의 핵심. 종전 주입 경로는 마지막에 usesReplicatedState 를 켜서 파생값을
            // "받은 값" 으로 고정했다. 복원은 반대여야 한다 — 값만 받고 다음 tick 부터는
            // 호스트가 직접 계산해야 하므로, 스냅샷이 거짓말을 해도 계산 결과가 이겨야 한다.
            var harness = Harness.Create();
            var saved = FullyPopulatedSnapshot();
            saved.UncontainedSystemMask = 0xFF;   // 있을 수 없는 값. 그대로 믿으면 여기서 드러난다.

            harness.Sandbox.ApplyNetworkSnapshot(saved, LastShiftStateAuthority.Local);

            Assert.That(harness.Sandbox.UncontainedSystemMask, Is.EqualTo(ExpectedUncontainedMask),
                "복원은 손상 마스크와 장부로 직접 계산해야 한다.");
            harness.Dispose();
        }

        [Test]
        public void ReplicatedAuthorityStillTrustsTheServerMask()
        {
            // 같은 함수의 반대쪽. 멀티플레이 클라이언트는 손상 판정도 장부 완료 플래그도 없어
            // 같은 식을 다시 낼 수 없으므로, 기본 경로는 전과 똑같이 받은 값을 써야 한다.
            var harness = Harness.Create();
            var saved = FullyPopulatedSnapshot();
            saved.UncontainedSystemMask = 1 << (int)LastShiftShipSystem.Power;

            harness.Sandbox.ApplyNetworkSnapshot(saved);

            Assert.That(harness.Sandbox.UncontainedSystemMask,
                Is.EqualTo((byte)(1 << (int)LastShiftShipSystem.Power)));
            Assert.That(harness.Sandbox.UncontainedSystemMask, Is.Not.EqualTo(ExpectedUncontainedMask),
                "이 테스트가 의미를 가지려면 받은 값과 계산값이 실제로 달라야 한다.");
            harness.Dispose();
        }

        [Test]
        public void RestoringKeepsRepairChannelsAndBypassLifetimesRunning()
        {
            // 빠진 상태 중 제일 컸던 항목(§1.3-나). 종전 스냅샷은 성능 포기 마스크만 날라서
            // 복원하면 "0.4초 남은 안전 복구" 가 사라지고 임시 우회가 영구화됐다.
            var harness = Harness.Create();
            harness.Sandbox.ApplyNetworkSnapshot(FullyPopulatedSnapshot(), LastShiftStateAuthority.Local);
            var ledger = harness.Sandbox.Repairs;

            Assert.That(ledger.IsChanneling(LastShiftShipSystem.Cooling), Is.True);
            Assert.That(ledger.ChannelRemaining(LastShiftShipSystem.Cooling), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(ledger.IsSacrificed(LastShiftShipSystem.Power), Is.True);
            Assert.That(ledger.BypassRemaining(LastShiftShipSystem.Oxygen), Is.EqualTo(42.5f).Within(0.0001f));
            Assert.That(ledger.ModeOf(LastShiftShipSystem.Oxygen), Is.EqualTo(LastShiftRepairMode.QuickBypass));
            // 엔트리에서 파생되지 않는 두 카운터도 살아야 결과 화면 요약 4칸이 성립한다.
            Assert.That(ledger.QuickBypassCount, Is.EqualTo(3));
            Assert.That(ledger.BypassLapseCount, Is.EqualTo(2));
            // SacrificeCount 는 받지 않고 엔트리에서 다시 센다 — 같은 사실의 정본은 하나다.
            Assert.That(ledger.SacrificeCount, Is.EqualTo(1));

            // 복원한 채널이 실제로 이어서 돈다(값만 앉힌 것이 아니다).
            var completed = ledger.TryAdvanceChannel(LastShiftShipSystem.Cooling, 0.5f, out var mode);
            Assert.That(completed, Is.True);
            Assert.That(mode, Is.EqualTo(LastShiftRepairMode.SafeRestore));
            harness.Dispose();
        }

        [Test]
        public void RestoringKeepsTheControlHoldAndThePendingSteeringInput()
        {
            var harness = Harness.Create();
            harness.Sandbox.ApplyNetworkSnapshot(FullyPopulatedSnapshot(), LastShiftStateAuthority.Local);

            // 홀드 잔여가 살아 있어야 "언제 프리셋 값으로 돌아가는가" 가 저장 전후로 같다.
            Assert.That(harness.Sandbox.ControlHoldRemaining, Is.EqualTo(3.5f).Within(0.0001f));
            harness.Sandbox.AdvanceControlHold(3.4f);
            Assert.That(harness.Sandbox.ControlHoldRemaining, Is.GreaterThan(0f),
                "8초로 리셋되지도, 0 으로 밀리지도 않아야 한다.");
            harness.Dispose();
        }

        [Test]
        public void DockingTriggerEdgeSurvivesTheRoundTrip()
        {
            // 도킹은 상주가 아니라 진입 엣지로 판정한다. 이 기준값이 false 로 초기화되면
            // 트리거 안에서 저장한 판이 복원 직후 가만히 서 있는 것만으로 도킹한다.
            var harness = Harness.Create();
            var saved = FullyPopulatedSnapshot();
            saved.CrewAtDockingTrigger = true;

            harness.Sandbox.ApplyNetworkSnapshot(saved, LastShiftStateAuthority.Local);

            Assert.That(harness.Sandbox.CaptureRuntimeSnapshot().CrewAtDockingTrigger, Is.True);
            harness.Dispose();
        }

        [Test]
        public void CoolingValveHoldersComeBackAsCrewNotAsACount()
        {
            // 사람 수만 저장하면 다시 붙일 대상을 못 고른다 — 잡은 채로 저장한 판이
            // 복원 후 냉각이 끊긴 채로 이어진다.
            var harness = Harness.Create();
            var saved = FullyPopulatedSnapshot();
            saved.CoolingValveHolderMask = 1 << (int)LastShiftPlayerSlot.PlayerOne;

            harness.Sandbox.ApplyNetworkSnapshot(saved, LastShiftStateAuthority.Local);

            Assert.That(harness.Sandbox.IsCoolingValveHeld, Is.True);
            Assert.That(harness.Sandbox.CoolingValveHolderCount, Is.EqualTo(1));
            harness.Dispose();
        }

        [Test]
        public void RestoringDoesNotReplayTheMeteorImpact()
        {
            // 저장된 판은 충격이 이미 지나간 상태다. 이어하기 첫 프레임에 운석이 다시 터지면
            // 화면이 사실과 다른 말을 한다. (연출은 ImpactApplicationCount 증가에 걸려 있다.)
            var harness = Harness.Create();
            var feedback = harness.Root.AddComponent<LastShiftImpactFeedback>();
            var saved = FullyPopulatedSnapshot();

            harness.Sandbox.ApplyNetworkSnapshot(saved, LastShiftStateAuthority.Local);

            Assert.That(feedback.IsShaking, Is.False, "복원이 충격 연출을 재생하면 안 된다.");
            Assert.That(feedback.HasActiveDamageMarker, Is.False);
            harness.Dispose();
        }

        [Test]
        public void SituationLatchDwellRoundTripsThroughAFlatArray()
        {
            // 래치 위상은 스냅샷 구조체에 넣지 않는다(소비자가 파일 하나뿐이다). 대신 이
            // 평평한 배열이 그 자리를 대신하며, 부호가 활성 여부를 나른다.
            var tracker = new LastShiftSituationTracker();
            var dwell = tracker.CaptureLatchDwell();
            Assert.That(dwell.Length, Is.EqualTo(LastShiftSituationTracker.LatchSlotCount));
            Assert.That(dwell[(int)LastShiftSituation.HeatRunaway], Is.LessThan(0f),
                "초기 래치는 전부 비활성이므로 음수여야 한다.");

            dwell[(int)LastShiftSituation.HeatRunaway] = 2.75f;
            Assert.That(tracker.ApplyLatchDwell(dwell), Is.True);

            var reloaded = tracker.CaptureLatchDwell();
            Assert.That(reloaded[(int)LastShiftSituation.HeatRunaway], Is.EqualTo(2.75f).Within(0.0001f));
        }

        [Test]
        public void MismatchedLatchArrayIsRejectedWholesale()
        {
            // 절반만 덮어써서 히스테리시스가 반쪽만 살아 있는 상태가 조용히 더 나쁘다.
            var tracker = new LastShiftSituationTracker();
            Assert.That(tracker.ApplyLatchDwell(null), Is.False);
            Assert.That(tracker.ApplyLatchDwell(new float[3]), Is.False);
        }

        [Test]
        public void EveryNewSnapshotFieldParticipatesInEquality()
        {
            // NetworkVariable 이 변경 감지에 Equals 를 쓴다. 필드를 늘리고 Equals 에 안 넣으면
            // 값이 바뀌어도 영영 전송되지 않고, 세이브 쪽에서는 왕복 테스트가 통과해 버린다.
            AssertDistinguishes(s => { s.DamagedSystemMask = 1; return s; });
            AssertDistinguishes(s => { s.QuickBypassCount = 1; return s; });
            AssertDistinguishes(s => { s.BypassLapseCount = 1; return s; });
            AssertDistinguishes(s => { s.CoolingRepair.ChannelRemainingSeconds = 1f; return s; });
            AssertDistinguishes(s => { s.PowerRepair.Sacrificed = true; return s; });
            AssertDistinguishes(s => { s.OxygenRepair.BypassRemainingSeconds = 1f; return s; });
            AssertDistinguishes(s => { s.ControlHoldThrustDemand = 1f; return s; });
            AssertDistinguishes(s => { s.ControlHoldAttitudeDegrees = 1f; return s; });
            AssertDistinguishes(s => { s.ControlHoldRemainingSeconds = 1f; return s; });
            AssertDistinguishes(s => { s.SteeringDelayRemainingSeconds = 1f; return s; });
            AssertDistinguishes(s => { s.PendingThrustDemand = 1f; return s; });
            AssertDistinguishes(s => { s.PendingAttitudeDegrees = 1f; return s; });
            AssertDistinguishes(s => { s.HasPendingControl = true; return s; });
            AssertDistinguishes(s => { s.HeatProtectionSeconds = 1f; return s; });
            AssertDistinguishes(s => { s.CrewDeathZone = LastShiftZone.Cooling; return s; });
            AssertDistinguishes(s => { s.HasCrewDeathZone = true; return s; });
            AssertDistinguishes(s => { s.CrewAtDockingTrigger = true; return s; });
            AssertDistinguishes(s => { s.MeteorImpactPoint = Vector3.one; return s; });
            AssertDistinguishes(s => { s.MeteorImpactVector = Vector3.one; return s; });
            AssertDistinguishes(s => { s.MeteorMass = 1f; return s; });
            AssertDistinguishes(s => { s.MeteorSpeed = 1f; return s; });
            AssertDistinguishes(s => { s.CoolingValveHolderMask = 1; return s; });
            AssertDistinguishes(s => { s.SecondsSinceVerdict = 1f; return s; });
        }

        /// <summary>
        /// 기본값 한 벌에서 필드 하나만 바꿔 넣고, 그 차이가 <see cref="LastShiftNetworkSnapshot.Equals"/>
        /// 에 보이는지 본다. 구조체는 값 인자라 람다 안에서 그대로 고쳐 돌려주면 된다.
        /// </summary>
        private static void AssertDistinguishes(
            System.Func<LastShiftNetworkSnapshot, LastShiftNetworkSnapshot> mutate)
        {
            var baseline = default(LastShiftNetworkSnapshot);
            Assert.That(baseline.Equals(mutate(baseline)), Is.False,
                "이 필드의 변화가 스냅샷 차이로 안 보인다 — Equals 에 빠졌다.");
        }

        /// <summary>
        /// 성능 포기한 전력만 억제되고, 채널이 도는 냉각과 부품이 제자리에 없는 산소는
        /// 미억제로 남는다. <see cref="FullyPopulatedSnapshot"/> 의 장부 구성에서 나오는 값이다.
        /// </summary>
        private static byte ExpectedUncontainedMask =>
            (byte)((1 << (int)LastShiftShipSystem.Cooling) | (1 << (int)LastShiftShipSystem.Oxygen));

        /// <summary>
        /// 필드마다 서로 다른 값을 넣은 한 벌. 기본값이 섞여 있으면 배선이 빠진 필드가
        /// "우연히 같은 값" 으로 통과한다.
        ///
        /// 두 필드만 계산 결과를 미리 적는다. <c>SecuredItemMask</c> 는 아이템이 정본이라
        /// 아이템 없는 조립에서 언제나 0 이고, <c>UncontainedSystemMask</c> 는 복원이 권위를
        /// 되찾으므로 받은 값이 아니라 계산값이 남는다 — 그게 이 카드의 요점이다.
        /// </summary>
        private static LastShiftNetworkSnapshot FullyPopulatedSnapshot()
        {
            return new LastShiftNetworkSnapshot
            {
                Preset = LastShiftPreset.BadAttitudeHighOxygen,
                ShipState = new LastShiftShipState
                {
                    ThrustDemand = 0.37f,
                    BusPower = 0.41f,
                    OxygenPressure = 0.62f,
                    HullIntegrity = 0.73f,
                    EngineHeat = 0.58f,
                    ShipAttitudeDegrees = 12.5f,
                    ExistingDamage = 0.19f,
                    FuelReserve = 0.44f,
                    DockProgress = 61.25f
                },
                FirstProblem = LastShiftDominantProblem.CoolingCouplingDetached,
                CurrentProblem = LastShiftDominantProblem.BatteryDisplacedBusDisconnected,
                CoolingScore = 0.81f,
                BatteryScore = 0.66f,
                LeakScore = 0.29f,
                DockingSecondsRemaining = 137.5f,
                ResetGeneration = 4,
                ImpactApplicationCount = 1,
                SecuredItemMask = 0,
                HasAppliedImpact = true,
                Verdict = LastShiftVerdict.Pending,
                SacrificedSystemMask = 1 << (int)LastShiftShipSystem.Power,
                ThrustCeiling = 0.55f,
                HeatProtectionEngaged = true,
                SteeringDelayed = true,
                OxygenPumpRunning = false,
                // 사이렌은 오디오 소스를 만드는 부수 효과가 있어 EditMode 조립에서는 끈 채로 왕복시킨다.
                SirenActive = false,
                PowerPressure = 0.48f,
                CoolingPressure = 0.71f,
                LifeSupportPressure = 0.33f,
                Boundary0DoorOpen = true,
                Boundary1DoorOpen = false,
                Boundary2DoorOpen = true,
                ForeHatchOpen = true,
                AftHatchOpen = false,
                UncontainedSystemMask = ExpectedUncontainedMask,
                CoolingRepair = new LastShiftRepairEntrySnapshot
                {
                    ChannelActive = true,
                    ChannelMode = LastShiftRepairMode.SafeRestore,
                    ChannelRemainingSeconds = 0.4f
                },
                PowerRepair = new LastShiftRepairEntrySnapshot
                {
                    Mode = LastShiftRepairMode.PerformanceSacrifice,
                    Sacrificed = true
                },
                OxygenRepair = new LastShiftRepairEntrySnapshot
                {
                    Mode = LastShiftRepairMode.QuickBypass,
                    HasCompletedRepair = true,
                    BypassRemainingSeconds = 42.5f
                },
                QuickBypassCount = 3,
                BypassLapseCount = 2,
                DamagedSystemMask = (1 << (int)LastShiftShipSystem.Cooling) |
                                    (1 << (int)LastShiftShipSystem.Power) |
                                    (1 << (int)LastShiftShipSystem.Oxygen),
                ControlHoldThrustDemand = 0.25f,
                ControlHoldAttitudeDegrees = -18f,
                ControlHoldRemainingSeconds = 3.5f,
                SteeringDelayRemainingSeconds = 0.65f,
                PendingThrustDemand = 0.9f,
                PendingAttitudeDegrees = 22f,
                HasPendingControl = true,
                HeatProtectionSeconds = 11.25f,
                CrewDeathZone = LastShiftZone.LifeSupport,
                HasCrewDeathZone = true,
                CrewAtDockingTrigger = false,
                MeteorImpactPoint = new Vector3(1.5f, 2.25f, -3.75f),
                MeteorImpactVector = new Vector3(0.5f, -0.25f, 0.125f),
                MeteorMass = 42f,
                MeteorSpeed = 8f,
                CoolingValveHolderMask = 0,
                SecondsSinceVerdict = 0f
            };
        }

        private static string Describe(in LastShiftNetworkSnapshot s) =>
            $"gen={s.ResetGeneration} damaged={s.DamagedSystemMask} uncontained={s.UncontainedSystemMask} " +
            $"hold={s.ControlHoldRemainingSeconds:F2} steer={s.SteeringDelayRemainingSeconds:F2} " +
            $"heatLock={s.HeatProtectionSeconds:F2} valve={s.CoolingValveHolderMask} " +
            $"coolChan={s.CoolingRepair.ChannelRemainingSeconds:F2} oxyBypass={s.OxygenRepair.BypassRemainingSeconds:F2}";

        /// <summary>승무원 한 명과 아이템 0개짜리 최소 조립. 밸브 홀더 복원에 승무원이 필요하다.</summary>
        private readonly struct Harness
        {
            public readonly GameObject Root;
            public readonly LastShiftSandboxController Sandbox;
            private readonly GameObject player;

            private Harness(GameObject root, LastShiftSandboxController sandbox, GameObject player)
            {
                Root = root;
                Sandbox = sandbox;
                this.player = player;
            }

            public static Harness Create()
            {
                // CharacterController 를 컨트롤러보다 <b>먼저</b> 붙인다 — 순서가 바뀌면 Awake 가
                // 캐시할 대상이 아직 없어 ResetPreset 안의 ResetPlayer 가 NRE 로 터진다.
                var playerObject = new GameObject("Crew");
                playerObject.transform.position = LastShiftSandboxController.PlayerSpawn;
                playerObject.AddComponent<CharacterController>();
                var cameraObject = new GameObject("Camera");
                cameraObject.transform.SetParent(playerObject.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                var socket = new GameObject("HoldSocket").transform;
                socket.SetParent(cameraObject.transform, false);
                var crew = playerObject.AddComponent<LastShiftPlayerController>();
                crew.Configure(camera, socket);
                var root = new GameObject("Runtime");
                var sandbox = root.AddComponent<LastShiftSandboxController>();
                sandbox.Configure(crew, new LastShiftGrabbable[0]);
                sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
                return new Harness(root, sandbox, playerObject);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(player);
            }
        }
    }
}
