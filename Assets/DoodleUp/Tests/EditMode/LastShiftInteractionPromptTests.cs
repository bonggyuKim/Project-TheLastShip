using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 화면 중앙 UI 의 노출 조건 — <b>상호작용이 성립할 때만 뜬다</b>(CT-01 §1.1 L3 국소 프롬프트).
    ///
    /// 예전에는 <see cref="LastShiftPlayerController.InteractionPrompt"/> 가 어떤 자리에서도
    /// 문자열을 돌려줬고(빈손·대상 없음이면 <c>+   E 잡기: 대상을 조준하세요</c>),
    /// <c>OnGUI</c> 는 그 값을 무조건 그렸다. 그래서 조준점과 폭 <c>460px</c> 상자가 배를 도는
    /// 내내 화면 한가운데 붙어 있었다. <b>프롬프트가 상시면 프롬프트가 떠 있다는 사실이
    /// 아무것도 말하지 않는다</b> — 이 파일이 고정하는 것이 그 경계다.
    ///
    /// 그리는 쪽(<c>OnGUI</c>)은 EditMode 에서 돌릴 수 없으므로, 그리는 판단이 오직
    /// <see cref="LastShiftPlayerController.HasInteractionPrompt"/> 하나에서 나오도록 두고
    /// 여기서는 그 값을 본다.
    /// </summary>
    public sealed class LastShiftInteractionPromptTests
    {
        /// <summary>아무것도 없는 자리에서는 중앙에 그릴 것이 없다.</summary>
        [Test]
        public void EmptySpaceDrawsNothingInTheCenter()
        {
            var player = CreatePlayer();

            Assert.That(player.HasInteractionPrompt, Is.False,
                "빈 통로에서 조준점과 프롬프트 상자가 뜨면 그 둘은 신호가 아니라 배경이 된다.");
            Assert.That(player.InteractionPrompt, Is.Empty,
                "읽는 쪽(로그·프로브)이 null 검사를 하지 않아도 되도록 빈 문자열이어야 한다.");

            Object.DestroyImmediate(player.gameObject);
        }

        /// <summary>사거리 안에 동사가 있으면 그 자리에서 뜬다 — 밸브가 가장 단순한 경로다.</summary>
        [Test]
        public void ReachableVerbBringsThePromptBack()
        {
            var player = CreatePlayer();
            player.transform.position = LastShiftCoolingValve.Position;

            Assert.That(player.HasInteractionPrompt, Is.True);
            Assert.That(player.InteractionPrompt, Does.Contain("[T]"),
                "노출 조건만 바뀌었지 문장이 사라진 것이 아니다.");

            // 한 걸음 밖으로 나가면 다시 비어야 한다. 조건이 '한 번 뜨면 남는' 것이 되면
            // 상시 UI 가 이름만 바꿔 돌아온다.
            player.transform.position = LastShiftCoolingValve.Position
                + new Vector3(LastShiftCoolingValve.ReachDistance + 1f, 0f, 0f);
            Assert.That(player.HasInteractionPrompt, Is.False);

            Object.DestroyImmediate(player.gameObject);
        }

        /// <summary>물건을 든 동안은 어디서든 뜬다 — 놓을 곳이 곧 지금 서 있는 자리다.</summary>
        [Test]
        public void HeldItemKeepsThePromptWhileCarrying()
        {
            var player = CreatePlayer();
            var item = CreateGrabbable();

            Assert.That(player.HasInteractionPrompt, Is.False);
            Assert.That(player.TryGrabForProbe(item), Is.True);

            Assert.That(player.HasInteractionPrompt, Is.True);
            Assert.That(player.InteractionPrompt, Does.Contain("[E]"),
                "손에 든 것을 놓는 방법은 상시 보여야 한다 — 그 자리에서 실제로 되는 동사다.");

            player.DropForProbe();
            Assert.That(player.HasInteractionPrompt, Is.False);

            Object.DestroyImmediate(item.gameObject);
            Object.DestroyImmediate(player.gameObject);
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
            var playerObject = new GameObject("Prompt Crew");
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
