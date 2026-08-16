using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// <c>AdvanceMission</c> 이 시간을 크게 미는 호출에서도 잘게 미는 호출과 같은 결과를 내는가.
    ///
    /// <b>왜 이 성질이 필요한가.</b> 예비 산소 소모의 유일한 조건은 "지금 있는 구역이 진공인가"
    /// 이고(<c>N1</c>), 그 진공 여부를 만드는 것은 같은 <c>AdvanceMission</c> 안의 악화 tick 이다.
    /// 진공을 걸음 <b>끝</b> 압력 하나로 판정하면 큰 걸음이 양쪽으로 어긋난다 — 감압 중에는
    /// 아직 공기가 있던 구간까지 태우고, 재가압 중에는 진짜 진공이던 구간이 통째로 공짜가 된다.
    /// 어긋나는 폭이 걸음 길이에 비례하므로 "튜닝 값이 곧 관측 결과" 가 성립하지 않게 되고,
    /// 시간을 크게 미는 검사가 게임과 다른 시뮬레이션을 보게 된다.
    ///
    /// 이 검사는 압력 평준화(<see cref="LastShiftZonePressures.Equalize"/>)가 이미 같은 이유로
    /// 안에서 나눠 적분하는 것과 짝이다. 만드는 쪽만 정확하고 읽는 쪽이 표본 하나면 정확도는
    /// 굵은 쪽에서 끊긴다.
    /// </summary>
    public sealed class LastShiftMissionStepTests
    {
        /// <summary>진공이 오기까지 기다려 주는 예산. 도킹 타이머보다 짧게 둬 판정과 안 겹친다.</summary>
        private const int ProbeBudgetSeconds = 200;

        /// <summary>진공에 닿은 <b>뒤</b> 재는 구간. 예비 산소 80초 예산의 절반이라 죽지 않는다.</summary>
        private const float VacuumWindowSeconds = 40f;

        [Test]
        public void BigStepChargesTheSameVacuumExposureAsManySmallSteps()
        {
            var setup = CreateSetup();

            // 1) 진공이 언제 오는지부터 잰다. 상수로 적으면 밸런스가 누출·선체를 만질 때마다
            //    이 검사가 "진공 전" 이나 "이미 사망" 구간을 재고도 통과한다.
            Arm(setup);
            Assert.That(setup.sandbox.IsZoneVacuum(setup.player.transform.position), Is.False,
                "출발선은 공기가 있는 구역이어야 한다 — 진공에서 시작하면 두 걸음이 같은 것이 당연해진다.");
            var onsetSeconds = AdvanceUntilCrewInVacuum(setup, ProbeBudgetSeconds);
            Assert.That(onsetSeconds, Is.GreaterThan(0).And.LessThan(ProbeBudgetSeconds),
                "격리한 파공 구역은 예산 안에서 진공에 닿아야 한다.");

            var windowSeconds = onsetSeconds + VacuumWindowSeconds;
            Assert.That(windowSeconds, Is.LessThan(LastShiftRecoveryTuning.DockingTimerSeconds),
                "검사 창이 도킹 타이머를 넘으면 재는 것이 진공 노출이 아니라 시간 초과 판정이다.");

            // 2) 잘게 민 경우. 1초 걸음이라 진공 개시 시각이 1초 해상도로 잡힌다.
            Arm(setup);
            for (var second = 0; second < windowSeconds; second++) setup.sandbox.AdvanceMission(1f);
            var fineSuitOxygen = LastShiftCrewOxygen.Ensure(setup.player).SuitOxygen;

            Assert.That(fineSuitOxygen, Is.LessThan(LastShiftRecoveryTuning.SuitOxygenInitial),
                "진공 구간이 있으므로 예비 산소가 줄어 있어야 한다.");
            Assert.That(fineSuitOxygen, Is.GreaterThan(0f),
                "창의 앞부분은 아직 공기가 있으므로 한 통을 다 태우면 안 된다 — " +
                "여기서 0 이 나오면 창이 너무 길어 두 걸음의 차이를 잴 수 없다.");

            // 3) 같은 시간을 한 걸음으로 민 경우. 예전에는 걸음 끝 압력(=진공)이 창 전체에
            //    적용돼 앞부분의 공기까지 태웠고, 창이 80초를 넘는 순간 곧바로 사망이었다.
            Arm(setup);
            setup.sandbox.AdvanceMission(windowSeconds);
            var coarseSuitOxygen = LastShiftCrewOxygen.Ensure(setup.player).SuitOxygen;

            Assert.That(LastShiftCrewOxygen.Ensure(setup.player).IsDead, Is.False,
                "큰 걸음 하나가 승무원을 죽이면 안 된다 — 잘게 밀면 살아 있는 같은 상황이다.");
            Assert.That(coarseSuitOxygen, Is.EqualTo(fineSuitOxygen).Within(0.03f),
                "한 걸음으로 민 결과가 잘게 민 결과와 같아야 한다(진공 노출 적분).");

            Teardown(setup);
        }

        /// <summary>
        /// 걸음을 잘게 나눠도 <b>압력</b>은 그대로여야 한다. 누출은 시간에 선형이라 분할이
        /// 결과를 바꿀 이유가 없고, 바뀐다면 그건 정확도 개선이 아니라 회귀다.
        /// </summary>
        [Test]
        public void SubSteppingDoesNotChangeZonePressureItself()
        {
            var setup = CreateSetup();

            Arm(setup);
            for (var second = 0; second < 60; second++) setup.sandbox.AdvanceMission(1f);
            var finePressure = setup.sandbox.PressureOf(LastShiftZone.Cockpit);

            Arm(setup);
            setup.sandbox.AdvanceMission(60f);
            var coarsePressure = setup.sandbox.PressureOf(LastShiftZone.Cockpit);

            Assert.That(coarsePressure, Is.EqualTo(finePressure).Within(0.001f));

            Teardown(setup);
        }

        /// <summary>
        /// 승무원이 진공에 닿기까지 몇 초인가. 예산 안에 안 닿으면 예산을 그대로 돌려준다.
        /// 구역 압력이 아니라 <b>승무원 위치의</b> 진공 여부를 본다 — 예비 산소를 태우는 판정이
        /// 그것이므로, 여기서 구역만 보면 검사와 대상이 어긋난다.
        /// </summary>
        private static int AdvanceUntilCrewInVacuum(Setup setup, int budgetSeconds)
        {
            var seconds = 0;
            while (seconds < budgetSeconds &&
                   !setup.sandbox.IsZoneVacuum(setup.player.transform.position))
            {
                setup.sandbox.AdvanceMission(1f);
                seconds++;
            }
            return seconds;
        }

        /// <summary>
        /// 새 항해로 되돌리고 <b>파공 구역을 격리한 채</b> 승무원을 그 안에 세운다.
        ///
        /// 격리가 요점이다. 문을 열어 두면 배 전체가 함께 내려가 파공 구역이 진공에 닿는 데
        /// 245초가 걸리고(<c>LastShiftRecovery</c> 누출 상수 표), 그러면 노출 구간을 뒤에 붙일
        /// 자리가 도킹 타이머 안에 안 남는다. 격리하면 그 구역이 자기 공기만으로 빠져 훨씬 빨리
        /// 닿는다 — 재려는 것은 격리의 대가가 아니라 걸음 길이의 영향이므로, 진공을 빨리 부르는
        /// 쪽을 쓴다.
        /// </summary>
        private static void Arm(Setup setup)
        {
            setup.sandbox.ResetPreset(LastShiftPreset.BadAttitudeHighOxygen);
            Assert.That(setup.sandbox.ApplyMeteorImpact(), Is.True);
            Assert.That(setup.sandbox.BreachZone, Is.EqualTo(LastShiftZone.LifeSupport));

            // 파공 구역을 가르는 경계를 번호로 안 적는다. 방사형에서 경계는 사슬이 아니라
            // 별이라 "하나 낮은 번호" 같은 산수가 안 통한다.
            for (var boundary = 0; boundary < LastShiftZoneAtlas.BoundaryCount; boundary++)
                if (LastShiftZoneAtlas.HighZoneOf(boundary) == setup.sandbox.BreachZone)
                    setup.sandbox.SetDoorOpen(boundary, false);

            // 봉합 지점은 파공 구역 안이고 덕트·샤프트·선외 어디에도 안 걸린다. 위치는
            // 운석 적용 <b>뒤</b>에 세운다 — 충격이 승무원을 밀어낸다.
            setup.player.transform.position = LastShiftShipDimensions.PatchPlateNominal;
            LastShiftCrewOxygen.Ensure(setup.player).ResetCrewOxygen();
        }

        private struct Setup
        {
            public LastShiftSandboxController sandbox;
            public LastShiftPlayerController player;
            public LastShiftGrabbable patch;
            public GameObject runtimeObject;
        }

        private static Setup CreateSetup()
        {
            var runtimeObject = new GameObject("Runtime");
            var sandbox = runtimeObject.AddComponent<LastShiftSandboxController>();
            var player = CreatePlayer();
            var patch = CreateItem(LastShiftItemRole.PatchPlate, LastShiftShipDimensions.PatchPlateNominal);
            sandbox.Configure(player, new[] { patch });
            return new Setup
            {
                sandbox = sandbox,
                player = player,
                patch = patch,
                runtimeObject = runtimeObject
            };
        }

        private static LastShiftPlayerController CreatePlayer()
        {
            var playerObject = new GameObject("Crew");
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

        private static LastShiftGrabbable CreateItem(LastShiftItemRole role, Vector3 position)
        {
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.transform.position = position;
            itemObject.AddComponent<Rigidbody>();
            var item = itemObject.AddComponent<LastShiftGrabbable>();
            item.Configure(role, true);
            return item;
        }

        private static void Teardown(Setup setup)
        {
            Object.DestroyImmediate(setup.patch.gameObject);
            Object.DestroyImmediate(setup.player.gameObject);
            Object.DestroyImmediate(setup.runtimeObject);
        }
    }
}
