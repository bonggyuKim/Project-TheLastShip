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

        /// <summary>
        /// 상자는 <b>대상 위에</b> 앉는다 — 사용자 피드백 두 번이 이 자리를 만들었다.
        /// 처음엔 화면 정중앙이라 "다 가린다" 였고, 하단 고정으로 내렸더니 "밑에는 잘 안 보인다"
        /// 였다. 고정 좌표로는 대상을 덮거나 대상에서 멀거나 둘 중 하나다.
        /// </summary>
        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        [TestCase(2560f, 1440f)]
        public void PromptSitsDirectlyAboveItsTarget(float width, float height)
        {
            // 대상이 화면 한가운데보다 살짝 위, 오른쪽에 보이는 상황.
            var anchor = new Vector2(width * 0.62f, height * 0.40f);
            var box = LastShiftPlayerController.ResolvePromptRect(width, height, 240f, anchor);

            Assert.That(box.center.x, Is.EqualTo(anchor.x).Within(0.01f),
                "가로 중앙이 대상과 안 맞으면 어느 물건에 대한 안내인지가 다시 글자 문제로 돌아간다.");
            Assert.That(box.yMax, Is.LessThan(anchor.y),
                "상자 아랫변이 대상보다 아래면 '위에 뜬다'가 아니라 대상을 덮는다.");
            Assert.That(anchor.y - box.yMax,
                Is.EqualTo(LastShiftPlayerController.PromptAnchorGap).Within(0.01f));
        }

        /// <summary>
        /// 대상이 화면 가장자리에 있어도 상자는 잘리지 않는다. 살짝 어긋나게 붙는 편이
        /// 반쯤 잘려 못 읽는 것보다 낫다.
        /// </summary>
        [TestCase(0f, 0f)]
        [TestCase(1920f, 0f)]
        [TestCase(0f, 1080f)]
        [TestCase(1920f, 1080f)]
        [TestCase(960f, 12f)]
        public void PromptStaysOnScreenWhenTheTargetIsAtTheEdge(float anchorX, float anchorY)
        {
            var box = LastShiftPlayerController.ResolvePromptRect(
                1920f, 1080f, 300f, new Vector2(anchorX, anchorY));

            Assert.That(box.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(box.xMax, Is.LessThanOrEqualTo(1920f));
            Assert.That(box.yMin, Is.GreaterThanOrEqualTo(0f));

            // 아래쪽 한계는 화면 가장자리다 — 이 자리를 막고 있던 상시 조작줄은 걷어냈다.
            Assert.That(box.yMax,
                Is.LessThanOrEqualTo(1080f - LastShiftPlayerController.ScreenEdgeMargin));
        }

        /// <summary>
        /// 가리킬 대상이 없는 문장(유령 상태·서버 거부)은 조준점 바로 아래로 떨어진다.
        /// 앵커 규칙(앵커 위에 상자)을 그대로 태우므로 갈래가 하나다.
        /// </summary>
        [Test]
        public void AnchorlessPromptLandsJustBelowTheCrosshair()
        {
            var floating = LastShiftPlayerController.ResolveFloatingAnchor(1920f, 1080f);
            var box = LastShiftPlayerController.ResolvePromptRect(1920f, 1080f, 240f, floating);
            var crosshair = LastShiftPlayerController.ResolveCrosshairRect(1920f, 1080f);

            Assert.That(box.yMin, Is.GreaterThanOrEqualTo(crosshair.yMax),
                "조준점을 덮으면 상시 표시로 바꾼 의미가 없다.");
            Assert.That(box.Overlaps(crosshair), Is.False);
            Assert.That(box.center.x, Is.EqualTo(960f).Within(0.01f));
        }

        /// <summary>
        /// 폭은 문장을 따라간다. <c>[E] 놓기</c> 같은 짧은 줄에 화면 폭짜리 띠가 깔리면
        /// 프롬프트가 아니라 띠가 먼저 보인다.
        /// </summary>
        [Test]
        public void PromptWidthFollowsTheSentenceAndStaysOnScreen()
        {
            var center = new Vector2(960f, 540f);
            var narrow = LastShiftPlayerController.ResolvePromptRect(1920f, 1080f, 80f, center);
            var wide = LastShiftPlayerController.ResolvePromptRect(1920f, 1080f, 400f, center);

            Assert.That(narrow.width, Is.LessThan(wide.width));
            Assert.That(narrow.width, Is.LessThan(1920f * 0.25f),
                "짧은 문장이 화면 1/4 을 먹으면 폭이 문장을 따라간다고 할 수 없다.");

            // 문장이 아무리 길어도 화면 밖으로 나가지 않는다.
            var huge = LastShiftPlayerController.ResolvePromptRect(1920f, 1080f, 9000f, center);
            Assert.That(huge.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(huge.xMax, Is.LessThanOrEqualTo(1920f));
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
