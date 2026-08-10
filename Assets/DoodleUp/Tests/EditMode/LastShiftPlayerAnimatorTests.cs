using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 애니메이터 어댑터(<see cref="LastShiftPlayerAnimator"/>)의 순간이동 판정을 잰다.
    ///
    /// <b>왜 이 판정만 따로 재는가.</b> 어댑터는 복제된 <c>transform</c> 의 프레임 위치차로
    /// <c>Speed</c> 를 만드는데, <see cref="LastShiftNetworkPlayer.ResetToSlotRpc"/>
    /// (프리셋 리셋 · 슬롯 배치 · 산소 고갈 자동 복귀)가 승무원을 수십 m 옮긴다. 그 한 프레임을
    /// 걸은 것으로 세면 <c>Speed</c> 가 자릿수로 튀고, 위로 옮겨 앉으면 <b>점프 트리거가
    /// 헛발사된다</b>. 둘 다 화면에서만 보이고 어떤 게임플레이 테스트도 안 잡는다.
    ///
    /// <b>경계가 <see cref="LastShiftPlayerController.MoveSpeed"/> 에 매여 있는 것이 요지다.</b>
    /// 이동 속도를 올리는 날 이 상수를 같이 안 올리면 정상 보행이 순간이동으로 읽혀
    /// 애니메이션이 통째로 멎는다 — 그 연결을 아래 첫 시험이 붙잡는다.
    /// </summary>
    public sealed class LastShiftPlayerAnimatorTests
    {
        private const float Frame = 1f / 60f;

        [Test]
        public void TeleportThresholdStaysAboveEveryLegitimateWalkSpeed()
        {
            // CurrentMoveSpeed 는 웅크림·운반에서 더 느려질 뿐 MoveSpeed 를 안 넘는다.
            Assert.Greater(
                LastShiftPlayerAnimator.TeleportSpeedMetersPerSecond,
                LastShiftPlayerController.MoveSpeed,
                "경계가 정상 보행 속도 아래로 내려왔다 — 걷기가 순간이동으로 읽힌다");
        }

        [Test]
        public void FullSpeedWalkingIsNotATeleport()
        {
            var delta = new Vector3(LastShiftPlayerController.MoveSpeed * Frame, 0f, 0f);

            Assert.IsFalse(LastShiftPlayerAnimator.IsTeleport(delta, Frame));
        }

        [Test]
        public void ResetToSlotSizedJumpIsATeleport()
        {
            // 광장에서 리셋 지점까지는 한 프레임에 넘을 수 없는 거리다.
            var delta = new Vector3(28f, 0f, -14f);

            Assert.IsTrue(LastShiftPlayerAnimator.IsTeleport(delta, Frame));
        }

        [Test]
        public void FallingFastIsNotATeleport()
        {
            // 낙하는 y 로만 빠르다. 이것을 순간이동으로 읽으면 떨어지는 내내 Speed 가 멎는다.
            var delta = new Vector3(0f, -40f * Frame, 0f);

            Assert.IsFalse(LastShiftPlayerAnimator.IsTeleport(delta, Frame));
        }

        [Test]
        public void ZeroDeltaTimeIsNeverATeleport()
        {
            // 0 으로 나누면 무한대가 나오고, 그러면 정지 프레임 하나가 순간이동이 된다.
            Assert.IsFalse(LastShiftPlayerAnimator.IsTeleport(new Vector3(5f, 0f, 0f), 0f));
        }
    }
}
