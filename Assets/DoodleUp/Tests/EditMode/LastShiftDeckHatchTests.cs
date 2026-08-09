using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 갑판 승강구 해치(<c>docs/corridor-4p-redesign-v1.md</c> §23.6). 압력 경계가 구역 경계 밖으로
    /// 나가는 첫 사례라, 여기서 지키는 것은 개폐 동작이 아니라 <b>그 경계선</b>이다.
    ///
    /// 셋을 동시에 만족해야 한다 — §5(우회로에 산소 비용이 있어야 한다), §23.6(승강구는 압력
    /// 경계이고 <c>DOOR_TIME</c> 은 문과 같다), §24(압력존은 <c>4</c>구역 고정). 셋 중 하나만 놓쳐도
    /// 우회로가 공짜 지름길이 되거나, 반대로 <c>Resolve()</c>·게이지·<c>SIMUL_ZONES</c>·<c>RG-1</c>
    /// 이 통째로 다시 열린다.
    ///
    /// 개폐 애니메이션은 <see cref="LastShiftDeckHatch.Update"/> 가 도는 PlayMode 소관이고,
    /// 여기서는 판정값과 기하만 본다.
    /// </summary>
    public sealed class LastShiftDeckHatchTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void HatchesStartClosedSoNobodyOpensTheDeckByAccident()
        {
            // 문의 기본값(전부 열림)과 반대인 것이 요점이다. 열린 해치는 갑판에 뚫린 구멍이고,
            // 저중력에서 뜬 물건이 아무도 열지 않은 구멍으로 빠지면 플레이어 판단이 아니라 사고다.
            var setup = CreateHatchSetup(LastShiftBypassDuct.ForeShaft);
            Assert.That(setup.sandbox.IsHatchOpen(LastShiftBypassDuct.ForeShaft), Is.False);
            Assert.That(setup.sandbox.IsHatchOpen(LastShiftBypassDuct.AftShaft), Is.False);
            Assert.That(setup.hatch.IsOpen, Is.False);

            // 리셋이 이 성질을 지켜야 한다. 프리셋이 제자리에 놓은 부품이 남은 구멍으로 빠지면
            // 시작 상태가 프리셋과 달라진다.
            setup.sandbox.SetHatchOpen(LastShiftBypassDuct.ForeShaft, true);
            setup.sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
            Assert.That(setup.sandbox.IsHatchOpen(LastShiftBypassDuct.ForeShaft), Is.False,
                "리셋 후에도 갑판에 구멍이 남아 있다.");

            Teardown(setup);
        }

        [Test]
        public void HatchIsAPressureBoundaryButNotAPressureZone()
        {
            // §23.6 이 DOOR_TIME 을 문과 같게 둔 결론. 상수를 갈라 두면 한쪽만 조정되고
            // 문서가 조용히 거짓이 된다.
            Assert.That(LastShiftRecoveryTuning.ZoneDoorTransitionSeconds, Is.EqualTo(0.8f).Within(Tolerance));

            // §24 — 해치가 생겨도 압력존은 4 구역이고 구역 경계도 셋 그대로다. 해치를
            // LastShiftDoorState 에 얹었다면 이 둘 중 하나가 움직였을 것이다.
            Assert.That(LastShiftZoneAtlas.ZoneCount, Is.EqualTo(4));
            Assert.That(LastShiftZoneAtlas.BoundaryCount, Is.EqualTo(3),
                "승강구가 구역 경계로 편입되면 평준화가 없는 상대와 압력을 교환한다.");
            Assert.That(LastShiftBypassDuct.ShaftCount, Is.EqualTo(2),
                "해치 수는 승강구 수이며 경계 수와 무관하다.");
        }

        [Test]
        public void OpeningTheHatchDoesNotChangeZonePressure()
        {
            // 압력 경계인데 열어도 압력이 안 움직이는 것이 §24 의 귀결이다 — 덕트에는
            // ZonePressure 슬롯이 없어 교환할 상대가 없다. 우회로의 비용은 위치로 물리는
            // SuitOxygen 이고(§5), 방의 공기를 빼는 것이 아니다.
            var setup = CreateHatchSetup(LastShiftBypassDuct.ForeShaft);
            setup.sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
            var before = setup.sandbox.PressureOf(LastShiftZone.Cockpit);

            setup.sandbox.SetHatchOpen(LastShiftBypassDuct.ForeShaft, true);
            for (var i = 0; i < 60; i++) setup.sandbox.AdvanceMission(1f);
            var withHatchOpen = setup.sandbox.PressureOf(LastShiftZone.Cockpit);

            setup.sandbox.ResetPreset(LastShiftPreset.HighHeatHighThrust);
            for (var i = 0; i < 60; i++) setup.sandbox.AdvanceMission(1f);
            Assert.That(withHatchOpen, Is.EqualTo(setup.sandbox.PressureOf(LastShiftZone.Cockpit)).Within(Tolerance),
                "해치 개폐가 구역 압력을 움직인다 — §24 가 막으려던 편입이 우회로 들어왔다.");
            Assert.That(before, Is.GreaterThan(0f));

            Teardown(setup);
        }

        [Test]
        public void CrewInTheShaftBurnsSuitOxygenBeforeReachingTheDuct()
        {
            // 승강구 목(덕트 천장 -0.3 ~ 갑판 0)은 Contains 가 안 잡는 구간이다. 그대로 두면
            // 구멍 안에 있는데 머리 위 방의 압력을 받아 산소를 안 태우는 순간이 생긴다.
            var mouth = LastShiftBypassDuct.ShaftMouth(LastShiftBypassDuct.ForeShaft);
            var inNeck = new Vector3(mouth.x, LastShiftBypassDuct.CeilingY + 0.1f, mouth.z);
            Assert.That(LastShiftBypassDuct.Contains(inNeck), Is.False, "목 구간은 덕트 본체가 아니다.");
            Assert.That(LastShiftBypassDuct.ShaftContains(inNeck), Is.True);
            Assert.That(LastShiftBypassDuct.IsUnpressurizedSpace(inNeck), Is.True);

            // 갑판 위는 안 잡아야 한다. 닫힌 해치 판 위에 선 승무원은 발이 정확히 갑판 면이고,
            // 그걸 잡으면 방 안에 서 있는데 산소가 타기 시작한다.
            var onDeck = new Vector3(mouth.x, LastShiftBypassDuct.DeckY, mouth.z);
            Assert.That(LastShiftBypassDuct.IsUnpressurizedSpace(onDeck), Is.False,
                "해치 판 위가 진공으로 잡힌다.");
            Assert.That(LastShiftBypassDuct.IsUnpressurizedSpace(LastShiftShipDimensions.SpawnPoint), Is.False);

            // 덕트 바닥에 선 승무원은 여전히 잡힌다(3단계에서 이미 성립하던 성질).
            var onDuctFloor = new Vector3(mouth.x, LastShiftBypassDuct.FloorY, mouth.z);
            Assert.That(LastShiftBypassDuct.IsUnpressurizedSpace(onDuctFloor), Is.True);
        }

        [Test]
        public void WhatFallsThroughTheOpenHatchCanBeCarriedBackUp()
        {
            // 이 카드가 실제로 여는 위험은 하나다 — 저중력에서 뜬 물건이 갑판 구멍으로 떨어져
            // 회수 불가가 되는 것. 답은 두 가지가 함께 성립하는 것이다.
            //
            // (1) 최저점이 덕트 바닥이다. 에어록 안쪽 해치가 닫혀 있고 덕트 바닥 판이 에어록
            //     천장을 그대로 덮으므로 3m 더 아래로는 못 간다.
            LastShiftAirlock.Clear();
            Assert.That(LastShiftBypassDuct.AirlockInnerHatchSealed, Is.True);
            Assert.That(LastShiftBypassDuct.DeepestFallY,
                Is.EqualTo(LastShiftBypassDuct.FloorY).Within(Tolerance),
                "최저점이 덕트 바닥이 아니다 — 에어록 바닥까지 떨어지면 되올라올 방법이 없다.");

            // (2) 거기서 갑판까지 되올라올 수 있다. 단을 밟는 것까지 세어도 점프 정점 안이다.
            Assert.That(LastShiftBypassDuct.RecoveryRise,
                Is.LessThan(LastShiftShipPhysics.JumpApexHeight),
                $"최저점에서 상승 {LastShiftBypassDuct.RecoveryRise:F2}m 가 점프 정점 " +
                $"{LastShiftShipPhysics.JumpApexHeight:F2}m 를 넘는다 — 떨어지면 못 돌아온다.");

            // 에어록 안쪽 해치가 열리면 최저점이 3m 더 내려간다. 예전에는 그것을 "여는 날
            // 이 검사가 깨진다" 로 적어 뒀는데, EVA 카드가 실제로 열었으므로 이제 그 3m 를
            // 두 가지가 함께 막는다 — 인터록(동시 개방 금지)과 에어록 계단이다.
            var riseFromAirlock = LastShiftBypassDuct.DeckY - LastShiftBypassDuct.AirlockFloorY
                                  - LastShiftBypassDuct.StepHeight;
            Assert.That(riseFromAirlock, Is.GreaterThan(LastShiftShipPhysics.JumpApexHeight),
                "에어록 바닥이 점프로 한 번에 나올 수 있는 깊이면 계단이 필요 없다는 뜻이다.");
            Assert.That(LastShiftBypassDuct.AirlockStepRise,
                Is.LessThan(LastShiftShipPhysics.JumpApexHeight),
                $"에어록 계단 한 단 {LastShiftBypassDuct.AirlockStepRise:F2}m 가 점프 정점을 넘는다.");
        }

        /// <summary>
        /// <b>인터록 — 갑판 구멍과 에어록이 동시에 안 열린다.</b>
        /// 이게 없으면 갑판에서 떨어진 물건이 덕트 바닥을 지나 에어록 바닥까지 <c>3m</c> 더
        /// 내려가고, 위 검사가 지키던 성질이 통째로 죽는다. 양방향을 다 건다 — 한쪽만 막으면
        /// 순서만 바꿔서 같은 구성에 도달한다.
        /// </summary>
        [Test]
        public void TheDeckHoleAndTheAirlockNeverOpenTogether()
        {
            var setup = CreateHatchSetup(LastShiftBypassDuct.ForeShaft);
            var mouth = LastShiftBypassDuct.ShaftMouth(LastShiftBypassDuct.ForeShaft);
            setup.player.transform.position = new Vector3(mouth.x, 0f, mouth.z);
            LastShiftVoyage.Clear();
            LastShiftVoyage.SettleSegment(LastShiftVerdict.SuccessNominalDocking, 2);
            Assert.That(LastShiftAirlock.IsAtPort, Is.True, "기항이 아니면 인터록을 잴 수가 없다.");

            // (가) 갑판 해치가 열려 있으면 에어록 안쪽이 안 열린다.
            setup.sandbox.SetHatchOpen(LastShiftBypassDuct.ForeShaft, true);
            Assert.That(LastShiftAirlock.TryOpenInner(anyDeckHatchOpen: true), Is.False);
            Assert.That(LastShiftAirlock.IsInnerHatchOpen, Is.False);

            // (나) 에어록 안쪽이 열려 있으면 갑판 해치가 안 열린다.
            setup.sandbox.SetHatchOpen(LastShiftBypassDuct.ForeShaft, false);
            Assert.That(LastShiftAirlock.TryOpenInner(anyDeckHatchOpen: false), Is.True);
            Assert.That(setup.hatch.TryOperate(setup.player), Is.False,
                "에어록이 열린 채로 갑판 구멍이 뚫렸다 — 떨어진 물건이 4.2m 아래로 간다.");
            Assert.That(setup.sandbox.IsHatchOpen(LastShiftBypassDuct.ForeShaft), Is.False);

            // 그 상태에서도 최저점은 회수 가능하다 — 계단이 한 걸음을 점프 정점 안으로 자른다.
            Assert.That(LastShiftBypassDuct.DeepestFallY,
                Is.EqualTo(LastShiftBypassDuct.AirlockFloorY).Within(Tolerance));
            Assert.That(LastShiftBypassDuct.RecoveryRise,
                Is.LessThan(LastShiftShipPhysics.JumpApexHeight));

            LastShiftVoyage.Clear();
            Teardown(setup);
        }

        [Test]
        public void ClosedHatchBlocksTheDeckHoleAndOpenHatchDoesNot()
        {
            // 차단 콜라이더가 곧 "구멍이 막혀 있다" 이다. 여는 순간부터 구멍이고 판 위에 있던
            // 물건은 그때 떨어진다 — 문과 같은 규칙(완전히 닫혔을 때만 막는다)의 귀결이다.
            var setup = CreateHatchSetupWithParts(LastShiftBypassDuct.ForeShaft);
            Assert.That(setup.blocker.enabled, Is.True, "닫힌 해치가 갑판 구멍을 안 막는다.");
            Assert.That(setup.hatch.OpenAmount, Is.EqualTo(0f).Within(Tolerance));

            setup.sandbox.SetHatchOpen(LastShiftBypassDuct.ForeShaft, true);
            setup.hatch.SnapToState();
            Assert.That(setup.blocker.enabled, Is.False, "열린 해치가 여전히 막고 있다.");
            Assert.That(setup.panel.localPosition.x,
                Is.EqualTo(LastShiftDeckHatch.PanelTravel).Within(Tolerance),
                "판이 구멍 밖으로 안 물러났다.");

            // 열어 둔 판은 갑판 위에 얹힌다. stepOffset 기본값을 넘으면 걸림돌이 된다.
            Assert.That(LastShiftDeckHatch.PanelThickness,
                Is.LessThan(LastShiftBypassDuct.StepHeight),
                "열어 둔 해치 판을 걸어서 못 넘는다.");

            Teardown(setup);
        }

        [Test]
        public void HatchOpeningMatchesTheDuctSection()
        {
            // 좁으면 웅크려도 못 들어가고, 넓으면 통로보다 갑판이 더 뚫려 낙하 위험만 커진다.
            Assert.That(LastShiftDeckHatch.OpeningSpan,
                Is.EqualTo(LastShiftBypassDuct.Section).Within(Tolerance));
            Assert.That(LastShiftDeckHatch.OpeningSpan,
                Is.EqualTo(LastShiftShipPhysics.CrouchHeight).Within(Tolerance),
                "구멍이 웅크림 자세와 어긋나면 통로 단면과 갑판 구멍 중 하나가 거짓이다.");

            // 판은 구멍 밖으로 완전히 물러나야 한다. 덜 물러나면 열려 있는데 절반이 막힌다.
            Assert.That(LastShiftDeckHatch.PanelTravel,
                Is.GreaterThanOrEqualTo(LastShiftDeckHatch.OpeningSpan));
        }

        [Test]
        public void HatchIsOperableFromTheDeckAndFromInsideTheDuct()
        {
            // 아래에서 못 열면 우회로에 들어간 승무원이 자기 뒤로 닫힌 해치에 갇힌다.
            // 그건 우회로가 아니라 함정이고, 문이 안팎을 구분하지 않는 것과 같은 이유다.
            var setup = CreateHatchSetup(LastShiftBypassDuct.AftShaft);
            var mouth = LastShiftBypassDuct.ShaftMouth(LastShiftBypassDuct.AftShaft);

            setup.player.transform.position = new Vector3(mouth.x + 1f, 0f, mouth.z);
            Assert.That(setup.hatch.TryOperate(setup.player), Is.True, "갑판 위에서 조작이 안 된다.");
            Assert.That(setup.hatch.IsOpen, Is.True);

            setup.player.transform.position = new Vector3(mouth.x, LastShiftBypassDuct.FloorY, mouth.z);
            Assert.That(setup.hatch.TryOperate(setup.player), Is.True, "덕트 안에서 조작이 안 된다.");
            Assert.That(setup.hatch.IsOpen, Is.False);

            // 사거리 밖에서는 안 된다. 잡기 사거리보다 짧아야 부품과 대상이 안 겹친다.
            setup.player.transform.position = new Vector3(
                mouth.x + LastShiftDeckHatch.ReachDistance + 0.5f, 0f, mouth.z);
            Assert.That(setup.hatch.TryOperate(setup.player), Is.False);
            Assert.That(LastShiftDeckHatch.ReachDistance,
                Is.LessThan(LastShiftPlayerController.GrabDistance),
                "해치 사거리가 잡기 사거리보다 길면 부품을 잡으려다 해치가 열린다.");

            Teardown(setup);
        }

        [Test]
        public void HatchAndDoorReachesDoNotOverlap()
        {
            // 같은 Q 키를 나눠 쓰므로 사거리가 겹치면 어느 쪽이 조작될지가 호출 순서에 달린다.
            // 진입점이 방 안쪽(§5)이라는 규정이 이 성질을 만든다 — 겹치면 그 규정이 깨진 것이다.
            for (var shaft = 0; shaft < LastShiftBypassDuct.ShaftCount; shaft++)
            {
                var mouth = LastShiftBypassDuct.ShaftMouth(shaft);
                for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                {
                    var gap = Mathf.Abs(mouth.x - LastShiftZoneAtlas.BoundaryX(boundary));
                    Assert.That(gap,
                        Is.GreaterThan(LastShiftDeckHatch.ReachDistance + LastShiftZoneDoor.ReachDistance),
                        $"승강구 {shaft} 와 경계 {boundary} 의 조작 사거리가 겹친다.");
                }
            }
        }

        [Test]
        public void FindOperablePicksTheNearestShaft()
        {
            var fore = CreateHatchSetup(LastShiftBypassDuct.ForeShaft);
            var aft = CreateHatch(LastShiftBypassDuct.AftShaft);

            var foreMouth = LastShiftBypassDuct.ShaftMouth(LastShiftBypassDuct.ForeShaft);
            Assert.That(LastShiftDeckHatch.FindOperable(foreMouth), Is.EqualTo(fore.hatch));
            Assert.That(LastShiftDeckHatch.FindOperable(LastShiftBypassDuct.ShaftMouth(LastShiftBypassDuct.AftShaft)),
                Is.EqualTo(aft));

            // 스폰 지점은 어느 승강구도 아니어야 한다. 승강구가 방 안(§5)이라 이게 저절로
            // 성립하지 않는다 — 선수 승강구는 조종석 스폰에서 1.6m 이고, 사거리를 그만큼 주면
            // 시작하자마자 "해치 열기" 프롬프트가 떠서 첫 화면이 우회로 안내가 된다.
            Assert.That(LastShiftDeckHatch.FindOperable(LastShiftShipDimensions.SpawnPoint), Is.Null);
            var spawnGap = new Vector2(
                LastShiftShipDimensions.SpawnPoint.x - foreMouth.x,
                LastShiftShipDimensions.SpawnPoint.z - foreMouth.z);
            Assert.That(Mathf.Max(Mathf.Abs(spawnGap.x), Mathf.Abs(spawnGap.y)),
                Is.GreaterThan(LastShiftDeckHatch.ReachDistance),
                "스폰 지점이 승강구 조작 사거리 안이다.");

            Object.DestroyImmediate(aft.gameObject);
            Teardown(fore);
        }

        [Test]
        public void DeadCrewCannotOperateTheHatch()
        {
            // 기획 §4.4 — 유령은 배를 만질 수 없다. 문과 같은 자리에 같은 조건을 둔다.
            var setup = CreateHatchSetup(LastShiftBypassDuct.ForeShaft);
            var mouth = LastShiftBypassDuct.ShaftMouth(LastShiftBypassDuct.ForeShaft);
            setup.player.transform.position = new Vector3(mouth.x, 0f, mouth.z);

            var crew = LastShiftCrewOxygen.Ensure(setup.player);
            crew.KillForProbe();
            Assert.That(setup.hatch.TryOperate(setup.player), Is.False);
            Assert.That(setup.hatch.IsOpen, Is.False);

            crew.ResetCrewOxygen();
            Assert.That(setup.hatch.TryOperate(setup.player), Is.True, "되살아나면 다시 조작된다.");

            Teardown(setup);
        }

        private struct HatchSetup
        {
            public LastShiftSandboxController sandbox;
            public LastShiftPlayerController player;
            public LastShiftDeckHatch hatch;
            public Transform panel;
            public BoxCollider blocker;
            public GameObject runtimeObject;
        }

        /// <summary>
        /// 해치 하나 + sandbox + 승무원. sandbox 를 먼저 만들어야 해치가 찾는다 — 해치는 자기
        /// 상태를 안 들고 sandbox 를 따라가기만 하기 때문이다(문과 같은 구조).
        /// </summary>
        private static HatchSetup CreateHatchSetup(int shaft)
        {
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            var player = CreatePlayer();
            sandbox.Configure(player, System.Array.Empty<LastShiftGrabbable>());
            return new HatchSetup
            {
                sandbox = sandbox,
                player = player,
                hatch = CreateHatch(shaft),
                runtimeObject = runtimeObject
            };
        }

        /// <summary>판과 차단 콜라이더까지 붙인 조립. 개폐가 실제 오브젝트에 반영되는지를 보는 검사용이다.</summary>
        private static HatchSetup CreateHatchSetupWithParts(int shaft)
        {
            var setup = CreateHatchSetup(shaft);
            var panel = new GameObject("Panel").transform;
            panel.SetParent(setup.hatch.transform, false);
            var blockerObject = new GameObject("Blocker");
            blockerObject.transform.SetParent(setup.hatch.transform, false);
            var blocker = blockerObject.AddComponent<BoxCollider>();
            setup.hatch.Configure(shaft, panel, blocker);
            setup.hatch.SnapToState();
            setup.panel = panel;
            setup.blocker = blocker;
            return setup;
        }

        private static LastShiftDeckHatch CreateHatch(int shaft)
        {
            var hatchObject = new GameObject($"DeckHatch_{shaft}");
            hatchObject.transform.position = LastShiftBypassDuct.ShaftMouth(shaft);
            var hatch = hatchObject.AddComponent<LastShiftDeckHatch>();
            hatch.Configure(shaft, null, null);
            return hatch;
        }

        private static void Teardown(HatchSetup setup)
        {
            Object.DestroyImmediate(setup.hatch.gameObject);
            Object.DestroyImmediate(setup.player.gameObject);
            Object.DestroyImmediate(setup.runtimeObject);
        }

        private static LastShiftPlayerController CreatePlayer()
        {
            var playerObject = new GameObject("Player");
            playerObject.transform.position = LastShiftSandboxController.PlayerSpawn;
            playerObject.AddComponent<CharacterController>();
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            var camera = cameraObject.AddComponent<Camera>();
            var socket = new GameObject("HoldSocket").transform;
            socket.SetParent(cameraObject.transform, false);
            var player = playerObject.AddComponent<LastShiftPlayerController>();
            player.Configure(camera, socket);
            return player;
        }
    }
}
