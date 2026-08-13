using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// CT-08 N11 — 사망 이후의 참여 범위(기획 §4.4).
    ///
    /// 원칙 문장이 검사 기준 그대로다: <b>유령은 이동 제약만 잃는다. 관측 규칙과 물리 규칙은
    /// 살아 있을 때와 같다.</b> 그래서 이 파일이 보는 것은 넷이다 —
    /// 이동이 <b>남는가</b>, 조작이 <b>전부 사라지는가</b>, 구역 판정이 <b>따라오는가</b>,
    /// 표현이 반투명으로 <b>바뀌는가</b>.
    ///
    /// 네트워크 경로(서버 RPC 거부·복제)는 <see cref="LastShiftNetworkPlayer"/> 소관이라
    /// PlayMode 에서 host 를 띄워야 하고, 여기서는 그 진입점들이 공통으로 보는 상태
    /// (<see cref="LastShiftCrewOxygen.IsDead"/> → <see cref="LastShiftPlayerController.IsGhost"/>)
    /// 가 실제로 전환되는지까지를 고정한다.
    /// </summary>
    public sealed class LastShiftGhostTests
    {
        /// <summary>구현물 1: 콜라이더 off, 중력 off, 이동 유지.</summary>
        [Test]
        public void DeathKeepsMovementButDropsColliderAndGravity()
        {
            var player = CreatePlayer();
            var characterController = player.GetComponent<CharacterController>();
            var crew = LastShiftCrewOxygen.Ensure(player);

            Assert.That(player.IsGhost, Is.False);
            Assert.That(characterController.enabled, Is.True);

            crew.KillForProbe();

            Assert.That(player.IsGhost, Is.True, "사망은 유령 전환이다.");
            Assert.That(player.enabled, Is.True,
                "컨트롤러를 끄면 이동까지 잃는다 — 원칙 문장은 '이동 제약만 잃는다' 이다.");
            Assert.That(characterController.enabled, Is.False,
                "몸이 없으므로 콜라이더가 남으면 안 된다(통로가 시신으로 막힌다).");

            // 중력이 꺼졌는지는 "가만히 두면 떨어지지 않는가" 로 본다. 산 승무원이라면
            // 저중력 적분이 돌아 y 가 내려간다.
            var start = player.transform.position;
            for (var i = 0; i < 60; i++) player.MoveForProbe(Vector2.zero, 0f, 1f / 60f);
            Assert.That(Vector3.Distance(player.transform.position, start), Is.LessThan(0.001f),
                "입력이 없으면 유령은 그 자리에 뜬 채로 있어야 한다.");

            // 이동은 그대로 남고, 속도는 산 사람과 같다(§4.4 v0.4 — 우위는 속도가 아니라 접근).
            player.MoveForProbe(Vector2.up, 0f, 1f);
            Assert.That(player.transform.position.z - start.z,
                Is.EqualTo(LastShiftPlayerController.GhostFloatSpeed).Within(0.001f));
            Assert.That(LastShiftPlayerController.GhostFloatSpeed,
                Is.EqualTo(LastShiftPlayerController.MoveSpeed).Within(0.0001f),
                "유령이 더 빠르면 v0.4 에서 폐기된 '1/3 시간' 논거를 코드가 되살린다.");

            // 위아래는 Space·Ctrl 이 직접 준다 — 바닥이라는 기준면이 없기 때문이다.
            player.MoveForProbe(Vector2.zero, 1f, 1f);
            Assert.That(player.transform.position.y - start.y,
                Is.EqualTo(LastShiftPlayerController.GhostFloatSpeed).Within(0.001f));

            Object.DestroyImmediate(player.gameObject);
        }

        /// <summary>구현물 3: 유령이 움직인 만큼 구역 판정이 따라온다(국소 정보 규칙 유지).</summary>
        [Test]
        public void GhostZoneJudgementFollowsItsOwnPosition()
        {
            var player = CreatePlayer();
            var crew = LastShiftCrewOxygen.Ensure(player);

            // 조종석에서 죽고, 배 반대편 산소실까지 부유해 간다. 그 사이에 경계(닫힌 문 포함)가
            // 몇 개 있든 유령은 통과한다 — CharacterController 를 쓰지 않고 transform 을 옮긴다.
            player.transform.position = new Vector3(LastShiftShipDimensions.CockpitCenterX, 0.1f, 0f);
            crew.KillForProbe();
            Assert.That(LastShiftZoneAtlas.Resolve(player.transform.position), Is.EqualTo(LastShiftZone.Cockpit));

            var travel = LastShiftShipDimensions.LifeSupportCenterX - LastShiftShipDimensions.CockpitCenterX;
            // +x 를 보게 세운 뒤 앞으로 민다. 유령 이동은 시선 기준이다.
            player.SetAimDirectionForProbe(Vector3.right);
            player.MoveForProbe(Vector2.up, 0f, Mathf.Abs(travel) / LastShiftPlayerController.GhostFloatSpeed);

            Assert.That(player.transform.position.x,
                Is.EqualTo(LastShiftShipDimensions.LifeSupportCenterX).Within(0.05f),
                "닫힌 문·격벽이 있어도 유령의 이동은 막히지 않는다(§4.4 — 통과는 이동이다).");
            Assert.That(LastShiftZoneAtlas.Resolve(player.transform.position),
                Is.EqualTo(LastShiftZone.LifeSupport),
                "구역 판정은 N0 의 위치 판정을 그대로 쓴다 — 유령도 그 구역에 가야 그 구역이 읽힌다.");

            Object.DestroyImmediate(player.gameObject);
        }

        /// <summary>구현물 2: 잡기·수리·문·고정이 전부 막힌다.</summary>
        [Test]
        public void GhostCannotTouchTheShip()
        {
            var player = CreatePlayer();
            var crew = LastShiftCrewOxygen.Ensure(player);
            var item = CreateGrabbable();

            player.HoldSocket.position = item.transform.position;
            Assert.That(player.TryGrabForProbe(item), Is.True, "살아 있을 때는 잡힌다.");

            crew.KillForProbe();

            Assert.That(player.HeldItem, Is.Null,
                "죽는 순간이 부품의 유일한 반환 시점이다 — 시신이 물고 있으면 남은 1인이 못 쓴다.");
            Assert.That(player.TryGrabForProbe(item), Is.False, "유령은 물건을 잡을 수 없다.");
            Assert.That(player.TryOperateNearestDoor(), Is.False, "유령은 문을 열거나 닫을 수 없다.");
            Assert.That(player.InteractionPrompt, Does.Contain("유령"),
                "눌러도 아무 일이 없는 것보다 왜 안 되는지가 보여야 한다.");

            crew.ResetCrewOxygen();
            Assert.That(player.IsGhost, Is.False, "리셋은 유령을 되돌린다.");
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(player.TryGrabForProbe(item), Is.True, "되살아나면 다시 잡힌다.");

            Object.DestroyImmediate(item.gameObject);
            Object.DestroyImmediate(player.gameObject);
        }

        /// <summary>
        /// 웅크린 채 죽어도 자세가 남지 않는다. 덕트(단면 0.9m)에서 죽는 것이 실제로 가능한
        /// 경로이고(§5 의 우회로는 진공이다), 남으면 유령의 시선이 바닥에 붙는다.
        /// </summary>
        [Test]
        public void GhostStandsUpEvenWhereItCouldNotStandAlive()
        {
            var player = CreatePlayer();
            var crew = LastShiftCrewOxygen.Ensure(player);

            player.SetCrouching(true);
            Assert.That(player.IsCrouching, Is.True);

            crew.KillForProbe();

            Assert.That(player.IsCrouching, Is.False, "유령에게는 자세가 없다.");
            player.SetCrouching(true);
            Assert.That(player.IsCrouching, Is.False, "유령은 다시 웅크릴 수도 없다.");

            Object.DestroyImmediate(player.gameObject);
        }

        /// <summary>구현물 4: 반투명 실루엣. 색은 유지하고 투명도만 바뀐다.</summary>
        [Test]
        public void GhostMaterialTurnsTranslucentAndBack()
        {
            // 승무원 메시가 실제로 쓰는 셰이더를 직접 세운다. Renderer.material 은 EditMode 에서
            // 사본을 만들며 오류를 찍고, 그 오류 자체가 테스트를 떨어뜨린다.
            var material = new Material(Shader.Find("Standard"));
            var playerColor = new Color(0.2f, 0.65f, 1f);

            LastShiftGhostVisuals.Apply(material, true, playerColor);
            Assert.That(material.color.a, Is.EqualTo(LastShiftGhostVisuals.GhostAlpha).Within(0.001f));
            Assert.That(material.color.r, Is.EqualTo(playerColor.r).Within(0.001f),
                "색까지 바꾸면 누가 죽었는지 알아보는 단서가 사라진다.");
            Assert.That(material.renderQueue, Is.EqualTo(LastShiftGhostVisuals.TransparentRenderQueue),
                "알파만 넣고 큐를 안 바꾸면 화면에서는 그대로 불투명하다.");

            LastShiftGhostVisuals.Apply(material, false, playerColor);
            Assert.That(material.color.a, Is.EqualTo(1f).Within(0.001f));
            Assert.That(material.renderQueue, Is.EqualTo(LastShiftGhostVisuals.OpaqueRenderQueue));

            Object.DestroyImmediate(material);
        }

        private static LastShiftGrabbable CreateGrabbable()
        {
            var itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.name = "Battery";
            itemObject.transform.position = new Vector3(0f, 1f, 1f);
            itemObject.transform.localScale = new Vector3(0.65f, 0.65f, 0.90f);
            itemObject.AddComponent<Rigidbody>();
            var item = itemObject.AddComponent<LastShiftGrabbable>();
            item.Configure(LastShiftItemRole.Battery, false);
            return item;
        }

        private static LastShiftPlayerController CreatePlayer()
        {
            var playerObject = new GameObject("Ghost Crew");
            playerObject.transform.position = LastShiftSandboxController.PlayerSpawn;
            var characterController = playerObject.AddComponent<CharacterController>();
            characterController.radius = LastShiftShipPhysics.CrewRadius;
            characterController.height = LastShiftShipPhysics.StandingHeight;
            characterController.center = new Vector3(0f, LastShiftShipPhysics.StandingHeight * 0.5f, 0f);
            var cameraObject = new GameObject("Camera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, LastShiftShipPhysics.EyeHeight, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            var socket = new GameObject("HoldSocket").transform;
            socket.SetParent(cameraObject.transform, false);
            var controller = playerObject.AddComponent<LastShiftPlayerController>();
            controller.Configure(camera, socket);
            return controller;
        }
    }
}
