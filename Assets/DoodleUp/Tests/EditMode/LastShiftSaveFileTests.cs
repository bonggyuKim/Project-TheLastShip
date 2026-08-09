using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 세이브 백본 2단계 — <b>파일 포맷과 복원</b>
    /// (<c>docs/tech/save-backbone-feasibility-v1.md</c> §2 · §4).
    ///
    /// <b>디스크를 안 만진다.</b> 재는 것이 문자열 왕복과 정적 상태 복원이라
    /// <see cref="LastShiftSaveFormat"/>·<see cref="LastShiftSaveCapture"/> 만으로 전부 선다 —
    /// 합격선(§2.2 "저장→로드 후 B층 전 필드 비트 동일")이 디스크 상태에 매달리면 안 된다.
    /// 실제 파일 쓰기와 재진입 가드는 PlayMode 쪽이 잰다.
    /// </summary>
    public sealed class LastShiftSaveFileTests
    {
        [SetUp]
        public void ClearBefore() => ClearAll();

        [TearDown]
        public void ClearAfter() => ClearAll();

        private static void ClearAll()
        {
            LastShiftCompartments.ClearModules();
            LastShiftPlacedModules.Clear();
            LastShiftVoyage.Clear();
            LastShiftPlacementAuthority.Revoke();
        }

        // ── 포맷 ────────────────────────────────────────────────────────────

        /// <summary>
        /// §2.2 합격선 그대로다 — <b>저장→로드 후 B층 전 필드 비트 동일</b>.
        ///
        /// 문서가 "정밀도는 관문이 아니다(여유가 오차보다 <c>10^5</c> 배 크다)" 로 결론지었지만
        /// 권고는 왕복 보존이었고, 그 권고를 여기서 <b>검사로</b> 바꾼다. 근사 비교로 두면
        /// 포맷을 바꾼 날 "정밀도 때문일 리 없다" 를 다시 증명해야 한다.
        /// </summary>
        [Test]
        public void SavedSegmentRoundTripsEveryFieldBitExact()
        {
            var saved = AwkwardSnapshot();
            var file = new LastShiftSaveFile
            {
                SchemaA = LastShiftSaveFormat.SchemaA,
                SchemaB = LastShiftSaveFormat.SchemaB,
                HasSegment = true,
                Segment = new LastShiftSegmentSave
                {
                    Snapshot = saved,
                    SituationLatchDwell = new[] { 0.1f, -1f, 12.3456789f }
                }
            };

            var load = LastShiftSaveFormat.Read(LastShiftSaveFormat.Write(file));

            Assert.That(load.Outcome, Is.EqualTo(LastShiftSaveLoadOutcome.Loaded), load.Reason);
            Assert.That(load.File.HasSegment, Is.True);
            // 구조체 Equals 는 float 를 .Equals(성분별 정확)로 본다 — 근사가 아니다.
            Assert.That(load.File.Segment.Snapshot.Equals(saved), Is.True,
                "왕복이 B층 필드를 잃었다 — §2.2 합격선 위반이다.");

            // 대표 float 셋은 비트로 못을 박는다. Equals 를 나중에 느슨하게 고쳐도 여기가 남는다.
            AssertBitEqual(saved.ShipState.EngineHeat, load.File.Segment.Snapshot.ShipState.EngineHeat);
            AssertBitEqual(saved.DockingSecondsRemaining, load.File.Segment.Snapshot.DockingSecondsRemaining);
            AssertBitEqual(
                saved.OxygenRepair.BypassRemainingSeconds,
                load.File.Segment.Snapshot.OxygenRepair.BypassRemainingSeconds);

            Assert.That(load.File.Segment.SituationLatchDwell, Is.EqualTo(new[] { 0.1f, -1f, 12.3456789f }),
                "래치 위상도 같은 왕복 규약을 지켜야 한다 — 음수가 비활성이다.");
        }

        /// <summary>
        /// 기항 세이브는 <c>segment</c> 키 자체가 없다(§4.4 "키 부재는 오류가 아니라 정상 경로다").
        /// 그리고 그 파일이 <see cref="LastShiftSaveLoadOutcome.Loaded"/> 로 읽혀야 한다 —
        /// 여기서 실패로 읽으면 기항에서 저장한 사람이 세이브를 못 쓴다.
        /// </summary>
        [Test]
        public void PortSaveOmitsTheSegmentKeyAndStillLoads()
        {
            var json = LastShiftSaveFormat.Write(LastShiftSaveCapture.Capture(null, false));

            Assert.That(json, Does.Not.Contain("\"Segment\""),
                "구간이 없는 세이브에 빈 구간 객체를 적으면 '전부 0 인 구간' 과 구분이 안 된다.");

            var load = LastShiftSaveFormat.Read(json);
            Assert.That(load.Outcome, Is.EqualTo(LastShiftSaveLoadOutcome.Loaded), load.Reason);
            Assert.That(load.File.HasSegment, Is.False);
        }

        /// <summary>
        /// <c>schemaB</c> 불일치 → 구간을 버리고 A만 싣는다(§4.4). 잔액이 살아남는 것이 이 항목의
        /// 값이다 — 플레이어가 잃는 것은 <b>그 구간의 진행분 하나</b>이고 캠페인은 온전하다.
        /// </summary>
        [Test]
        public void SchemaBMismatchDropsTheSegmentAndKeepsTheCampaign()
        {
            LastShiftMaintenance.Clear();
            LastShiftMaintenance.ArriveAtPort(3);
            var balance = LastShiftMaintenance.Balance;

            var file = LastShiftSaveCapture.Capture(null);
            file.HasSegment = true;
            file.SchemaB = LastShiftSaveFormat.SchemaB + 7;

            var load = LastShiftSaveFormat.Read(LastShiftSaveFormat.Write(file));

            Assert.That(load.Outcome, Is.EqualTo(LastShiftSaveLoadOutcome.SegmentDropped), load.Reason);
            Assert.That(load.File.HasSegment, Is.False, "버린 구간이 다시 실리면 폴백이 아니다.");
            Assert.That(load.File.Campaign.Ledger.Balance, Is.EqualTo(balance),
                "구간을 버리면서 잔액까지 잃으면 §4.2 의 '캠페인은 온전하다' 가 깨진다.");
        }

        /// <summary>
        /// <c>schemaA</c> 불일치 → <b>명시적 실패</b>다. 조용한 부분 로드를 하지 않는다(§4.4) —
        /// 캠페인층을 반쯤 읽어 앉히면 그 배가 무엇인지 아무도 모른다.
        /// </summary>
        [Test]
        public void SchemaAMismatchFailsWithoutLoadingAnything()
        {
            var file = LastShiftSaveCapture.Capture(null);
            file.SchemaA = LastShiftSaveFormat.SchemaA + 1;

            var load = LastShiftSaveFormat.Read(LastShiftSaveFormat.Write(file));

            Assert.That(load.Outcome, Is.EqualTo(LastShiftSaveLoadOutcome.Failed), load.Reason);
            Assert.That(load.File, Is.Null);
            Assert.That(load.CanRestore, Is.False);
        }

        /// <summary>헤더가 통째로 없는 파일은 스키마 <c>0</c> 으로 읽혀 실패한다. 조용히 통과하면 안 된다.</summary>
        [Test]
        public void HeaderlessJsonFailsInsteadOfDefaultingToTheCurrentSchema()
        {
            var load = LastShiftSaveFormat.Read("{\"Campaign\":{}}");

            Assert.That(load.Outcome, Is.EqualTo(LastShiftSaveLoadOutcome.Failed), load.Reason);
        }

        // ── A층 복원 ────────────────────────────────────────────────────────

        /// <summary>
        /// 캠페인 왕복 — <b>표·구역 오버레이·원장·항해가 같이 돌아온다</b>. 하나라도 빠지면 그
        /// 배는 발자국만 같고 효과나 환수액이 다르다(조항 M-4 · <see cref="LastShiftModuleEffects"/>).
        /// </summary>
        [Test]
        public void CampaignRoundTripRebuildsTableLedgerAndModuleKinds()
        {
            var placed = PlaceChain();
            var balance = LastShiftMaintenance.Balance;
            var portIndex = LastShiftMaintenance.PortIndex;

            var json = LastShiftSaveFormat.Write(LastShiftSaveCapture.Capture(null, false));
            WipeAsIfFreshProcess();

            var report = LastShiftSaveCapture.Restore(LastShiftSaveFormat.Read(json), null);

            Assert.That(report.CampaignComplete, Is.True,
                "저장이 통과시킨 배치가 로드 판정에 물렸다 — 파일과 규칙이 갈렸다는 신호다.");
            Assert.That(LastShiftCompartments.ModuleCount, Is.EqualTo(placed.Count));
            for (var slot = 0; slot < placed.Count; slot++)
            {
                var index = LastShiftCompartments.FixedCount + slot;
                var actual = LastShiftCompartments.At(index);
                Assert.That(actual.MinX, Is.EqualTo(placed[slot].MinX));
                Assert.That(actual.MaxZ, Is.EqualTo(placed[slot].MaxZ));
                Assert.That(actual.ParentIndex, Is.EqualTo(placed[slot].ParentIndex), "사슬이 어긋났다.");
                Assert.That(LastShiftCompartments.CatalogIndexOf(index),
                    Is.Not.EqualTo(LastShiftPlacedModule.NoCatalogIndex), "종류가 안 실렸다 — 효과가 통째로 사라진다.");
            }

            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(balance));
            Assert.That(LastShiftMaintenance.PortIndex, Is.EqualTo(portIndex));
            Assert.That(LastShiftMaintenance.PurchaseCount, Is.EqualTo(placed.Count),
                "구매 기록이 없으면 환수액이 기항 회차를 못 본다(조항 M-4).");
        }

        /// <summary>
        /// §7.6 이 붙인 조건. 구간 <b>중</b>에 저장할 때 <b>래치 수를 그 자리에서 다시 세면 안 된다</b> —
        /// 구간 중 래치 수는 판정 전 값이고, 그걸 <c>LastLatchCount</c> 로 저장하면 폴백 후
        /// 기항 수입(<see cref="LastShiftMaintenance.IncomeFor"/>)이 달라진다.
        /// </summary>
        [Test]
        public void MidSegmentCaptureStoresTheFoldedLatchCountNotTheLiveOne()
        {
            var harness = Harness.Create();
            var live = harness.Sandbox.LatchCount;
            var folded = live == LastShiftMaintenance.MaxLatches ? 0 : live + 1;
            LastShiftVoyage.ApplyNetworkState(1, LastShiftSegmentTransition.Pending, folded, true);

            var file = LastShiftSaveCapture.Capture(harness.Sandbox);

            Assert.That(file.Campaign.Ledger.LatchCount, Is.EqualTo(folded),
                "A층 캡처는 접힌 값을 그대로 복사하는 것이지 재계산이 아니다(§4.3 불변식).");
            Assert.That(file.Campaign.Ledger.LatchCount, Is.Not.EqualTo(live),
                "이 테스트가 의미를 가지려면 접힌 값과 실시간 값이 실제로 달라야 한다.");
            harness.Dispose();
        }

        /// <summary>커서 주인은 세션 안에서만 뜻이 있다. 파일에 실으면 접속하지도 않은 클라이언트가 커서를 든다.</summary>
        [Test]
        public void CursorHolderIsNotPersisted()
        {
            LastShiftPlacementAuthority.TryClaim(3);
            Assume.That(LastShiftPlacementAuthority.HolderId, Is.EqualTo(3));

            var file = LastShiftSaveCapture.Capture(null, false);

            Assert.That(file.Campaign.Ledger.CursorHolder, Is.EqualTo(LastShiftPlacementAuthority.NoHolder));
        }

        // ── B층 복원과 폴백 ─────────────────────────────────────────────────

        /// <summary>
        /// 전체 복원 — 파일을 거쳐도 B층 전 필드가 그대로 돌아온다. 스냅샷 왕복(1단계)과 파일
        /// 왕복(위)이 각각 서 있어도, <b>둘을 이어 붙인 경로</b>가 서는지는 따로 봐야 한다.
        /// </summary>
        [Test]
        public void FullRestoreReproducesTheCapturedSegmentThroughTheFile()
        {
            var harness = Harness.Create();
            harness.Sandbox.ApplyMeteorImpact();
            for (var step = 0; step < 12; step++) harness.Sandbox.AdvanceMission(0.1f);

            var saved = harness.Sandbox.CaptureRuntimeSnapshot();
            var json = LastShiftSaveFormat.Write(LastShiftSaveCapture.Capture(harness.Sandbox));

            // 저장 뒤에도 판이 계속 돈다(조항 S-10). 그 진행이 파일에 안 섞이는 것도 같이 본다.
            for (var step = 0; step < 20; step++) harness.Sandbox.AdvanceMission(0.1f);

            var report = LastShiftSaveCapture.Restore(LastShiftSaveFormat.Read(json), harness.Sandbox);

            Assert.That(report.SegmentRestored, Is.True);
            Assert.That(harness.Sandbox.CaptureRuntimeSnapshot().Equals(saved), Is.True,
                "파일을 거친 왕복이 B층 필드를 잃었다.");
            harness.Dispose();
        }

        /// <summary>
        /// §4.2 폴백 — <b>구간 시작은 저장해서 얻는 상태가 아니라 만들어 낼 수 있는 상태다.</b>
        /// 그래서 구간을 버려도 배가 선다. 그리고 <b>원장은 안 건드린다</b> — 폴백이 잔액을
        /// 초기화하거나 수입을 두 번 넣으면 그게 §4.2 가 결정적이라고 적은 자리의 위반이다.
        /// </summary>
        [Test]
        public void DroppedSegmentRestartsTheSegmentAndLeavesTheLedgerAlone()
        {
            var harness = Harness.Create();
            LastShiftMaintenance.Clear();
            LastShiftMaintenance.ArriveAtPort(2);
            var balance = LastShiftMaintenance.Balance;
            var portIndex = LastShiftMaintenance.PortIndex;
            LastShiftVoyage.ApplyNetworkState(2, LastShiftSegmentTransition.ToPort, 2, true);

            var file = LastShiftSaveCapture.Capture(harness.Sandbox);
            file.SchemaB = LastShiftSaveFormat.SchemaB + 1;
            var json = LastShiftSaveFormat.Write(file);

            // 파일을 쓴 뒤 판을 망가뜨린다. 폴백이 정말로 구간 시작을 만들어 내는지 보려면
            // 복원 전 상태가 시작 상태와 달라야 한다.
            harness.Sandbox.ApplyMeteorImpact();
            for (var step = 0; step < 30; step++) harness.Sandbox.AdvanceMission(0.1f);
            Assume.That(harness.Sandbox.HasAppliedImpact, Is.True);

            var report = LastShiftSaveCapture.Restore(LastShiftSaveFormat.Read(json), harness.Sandbox);

            Assert.That(report.Outcome, Is.EqualTo(LastShiftSaveLoadOutcome.SegmentDropped));
            Assert.That(report.SegmentRestored, Is.False);
            Assert.That(LastShiftVoyage.SegmentIndex, Is.EqualTo(2), "회차는 A층이라 살아남아야 한다.");
            Assert.That(LastShiftVoyage.LastTransition, Is.EqualTo(LastShiftSegmentTransition.Pending),
                "구간을 다시 시작했으면 판정은 아직 안 난 상태여야 한다.");
            Assert.That(LastShiftMaintenance.Balance, Is.EqualTo(balance), "폴백이 잔액을 건드렸다.");
            Assert.That(LastShiftMaintenance.PortIndex, Is.EqualTo(portIndex), "폴백이 기항 회차를 건드렸다.");
            Assert.That(harness.Sandbox.HasAppliedImpact, Is.False,
                "구간 시작은 운석이 아직 안 온 상태다 — 폴백이 시작을 만들어 내지 못했다.");
            harness.Dispose();
        }

        // ── 표본 ────────────────────────────────────────────────────────────

        private static void AssertBitEqual(float expected, float actual)
        {
            Assert.That(
                System.BitConverter.SingleToInt32Bits(actual),
                Is.EqualTo(System.BitConverter.SingleToInt32Bits(expected)),
                $"비트가 달라졌다: {expected:R} -> {actual:R}");
        }

        /// <summary>왕복이 가장 깨지기 쉬운 모양으로 채운다 — 반올림이 개입하면 여기서 드러난다.</summary>
        private static LastShiftNetworkSnapshot AwkwardSnapshot()
        {
            var state = LastShiftPresetFactory.Create(LastShiftPreset.HighHeatHighThrust);
            state.EngineHeat = 0.1f + 0.2f;
            state.BusPower = 1f / 3f;
            state.OxygenPressure = 0.7499999f;
            state.HullIntegrity = 0.90000004f;

            return new LastShiftNetworkSnapshot
            {
                Preset = LastShiftPreset.HighHeatHighThrust,
                ShipState = state,
                CoolingScore = 123456.789f,
                BatteryScore = -0.000123456f,
                LeakScore = float.MaxValue / 3f,
                DockingSecondsRemaining = 149.99998f,
                ResetGeneration = 7,
                ImpactApplicationCount = 3,
                SecuredItemMask = 0b1011,
                HasAppliedImpact = true,
                Verdict = LastShiftVerdict.SuccessCompromised,
                ThrustCeiling = 0.6666667f,
                PowerPressure = 0.15f,
                CoolingPressure = 0.2f,
                LifeSupportPressure = 0.05f,
                DamagedSystemMask = 0b101,
                ControlHoldThrustDemand = 0.33333334f,
                ControlHoldAttitudeDegrees = -12.345678f,
                ControlHoldRemainingSeconds = 0.7f,
                SteeringDelayRemainingSeconds = 0.35f,
                HeatProtectionSeconds = 4.5678f,
                MeteorImpactPoint = new Vector3(1.1f, -2.2f, 3.3f),
                MeteorImpactVector = new Vector3(-0.1f, 0.2f, -0.3f),
                MeteorMass = 12.345f,
                MeteorSpeed = 987.654f,
                CoolingValveHolderMask = 0b10,
                SecondsSinceVerdict = 3.14159265f,
                QuickBypassCount = 3,
                BypassLapseCount = 2,
                OxygenRepair = new LastShiftRepairEntrySnapshot
                {
                    Mode = LastShiftRepairMode.QuickBypass,
                    BypassRemainingSeconds = 42.499998f,
                    ChannelRemainingSeconds = 0.4f
                }
            };
        }

        /// <summary>
        /// 복제 테스트가 쓰는 것과 같은 표본 사슬이다. 냉각실 문에 붙은 방 둘 — 판정기를
        /// 통과하는 모양이라 세이브가 아닌 이유로 물리지 않는다.
        /// </summary>
        private static List<LastShiftCompartmentSpec> PlaceChain()
        {
            LastShiftMaintenance.Clear();
            LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);

            var first = Register(Spur(LastShiftCompartments.NextModuleIndex, 0, -1), LastShiftModuleCatalog.Corridor);
            Assert.That(LastShiftMaintenance.TryChargeModule(0, LastShiftModuleCatalog.Corridor), Is.True);
            var second = Register(Spur(LastShiftCompartments.NextModuleIndex, 1, first), LastShiftModuleCatalog.Radiator);
            Assert.That(LastShiftMaintenance.TryChargeModule(1, LastShiftModuleCatalog.Radiator), Is.True);

            return new List<LastShiftCompartmentSpec>
            {
                LastShiftCompartments.At(first), LastShiftCompartments.At(second)
            };
        }

        private static LastShiftCompartmentSpec Spur(int index, int link, int parentIndex)
        {
            const float roomDepth = 2f;
            var doorX = LastShiftShipDimensions.RoomCenterX(LastShiftZone.Cooling);
            var minZ = LastShiftShipDimensions.RoomMaxZ(LastShiftZone.Cooling) + link * roomDepth;

            return new LastShiftCompartmentSpec(
                index, doorX - 3f, doorX + 3f, minZ, minZ + roomDepth,
                LastShiftDoorPlane.AlongZ, minZ, doorX,
                parentIndex, LastShiftCompartmentAccess.Open);
        }

        private static int Register(in LastShiftCompartmentSpec candidate, int catalogIndex)
        {
            Assert.That(LastShiftCompartments.TryRegister(candidate, out var index, out var verdict, catalogIndex),
                Is.True, $"표본이 판정기에 물린다({verdict.Rejection}) — 세이브와 무관한 사유다.");
            return index;
        }

        private static void WipeAsIfFreshProcess()
        {
            LastShiftCompartments.ClearModules();
            LastShiftPlacedModules.Clear();
            LastShiftMaintenance.Clear();
            LastShiftVoyage.Clear();
        }

        /// <summary>승무원 한 명짜리 최소 조립. 1단계 테스트와 같은 모양이다.</summary>
        private readonly struct Harness
        {
            public readonly LastShiftSandboxController Sandbox;
            private readonly GameObject root;
            private readonly GameObject player;

            private Harness(GameObject root, LastShiftSandboxController sandbox, GameObject player)
            {
                this.root = root;
                Sandbox = sandbox;
                this.player = player;
            }

            public static Harness Create()
            {
                // CharacterController 를 컨트롤러보다 먼저 붙인다 — 순서가 바뀌면 ResetPreset 안의
                // ResetPlayer 가 아직 없는 캐시를 만져 NRE 로 터진다.
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
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(player);
            }
        }
    }
}
