using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 선내 저중력 점프의 튜닝 창을 고정한다.
    ///
    /// <see cref="LastShiftShipPhysics.GravityY"/> 와 <see cref="LastShiftShipPhysics.JumpSpeed"/>
    /// 는 <b>따로 만질 수 없는 짝</b>이다. 한쪽만 바꾸면 정점이 움직이고, 정점은 취향이 아니라
    /// 위아래에서 눌린 값이다 — 아래로는 승강구 바닥에서 갑판으로 돌아오는 상승, 위로는
    /// 카메라가 천장을 뚫지 않을 여유. 여기까지는 기존 덕트·해치 검사가 이미 지키고 있었지만,
    /// 그 검사들은 정점만 보므로 <b>낙하가 얼마나 걸리는지는 아무도 안 보고 있었다.</b>
    /// 실제로 그래서 달 중력(-1.62)이 "점프 후 너무 늦게 떨어진다"로 나왔다(2026-08-10 플레이).
    ///
    /// 그래서 이 파일은 시간축의 양끝을 잡는다 — 너무 느리면 조작이 멈춰 서고, 너무 빠르면
    /// 저중력이라는 설정 자체가 화면에서 사라진다.
    /// </summary>
    public sealed class LastShiftJumpEnvelopeTests
    {
        private const float Tolerance = 0.0001f;

        /// <summary>
        /// 낙하 시간의 상한. 사용자가 답답하다고 판정한 달 중력의 낙하가 1.36초였으므로,
        /// 그보다 확실히 아래여야 한다. 1.0초는 "뛰면 곧 내려온다"가 성립하는 선이다.
        /// </summary>
        private const float MaxFallSeconds = 1.0f;

        /// <summary>
        /// 체공의 지구 대비 하한 배율. 이 아래로 내려가면 저중력이 연출로도 안 읽혀서,
        /// 낙하를 빠르게 만드는 튜닝이 설정을 지워 버린 것이 된다.
        /// </summary>
        private const float MinEarthHangRatio = 1.4f;

        [Test]
        public void FallingBackDownDoesNotStallTheControls()
        {
            Assert.That(LastShiftShipPhysics.JumpFallDuration, Is.LessThan(MaxFallSeconds),
                $"정점에서 낙하가 {LastShiftShipPhysics.JumpFallDuration:F2}초 — 점프 한 번이 조작을 멈춰 세운다.");
            Assert.That(LastShiftShipPhysics.JumpHangTime,
                Is.EqualTo(LastShiftShipPhysics.JumpFallDuration * 2f).Within(Tolerance),
                "상승과 낙하가 비대칭이 됐다 — 정점 계산의 전제가 깨진다.");
        }

        [Test]
        public void LowGravityIsStillReadableInTheAir()
        {
            var ratio = LastShiftShipPhysics.JumpHangTime / LastShiftShipPhysics.EarthHangTime;
            Assert.That(ratio, Is.GreaterThan(MinEarthHangRatio),
                $"체공이 지구의 {ratio:F2}배뿐이다 — 낙하를 당기다가 저중력을 지웠다.");
        }

        /// <summary>
        /// 중력을 올릴 때 <see cref="LastShiftShipPhysics.JumpSpeed"/> 를 같이 안 올리면
        /// 정점이 조용히 내려앉아 승강구에 갇힌다. 덕트 검사가 그 상황을 잡아 주긴 하지만,
        /// 여기서도 같은 것을 본다 — 이 파일이 중력을 만지는 사람이 여는 파일이기 때문이다.
        /// </summary>
        [Test]
        public void TheApexStaysBetweenTheShaftFloorAndTheCeiling()
        {
            Assert.That(LastShiftShipPhysics.JumpApexHeight,
                Is.GreaterThan(LastShiftBypassDuct.RecoveryRise),
                $"정점 {LastShiftShipPhysics.JumpApexHeight:F2}m 가 회수 상승 " +
                $"{LastShiftBypassDuct.RecoveryRise:F2}m 에 못 미친다 — 떨어지면 못 올라온다.");
            Assert.That(LastShiftShipPhysics.JumpApexHeight + LastShiftShipPhysics.EyeHeight,
                Is.LessThan(LastShiftShipPhysics.CeilingInnerHeight),
                "정점에서 카메라가 천장 밖으로 나간다.");
        }
    }
}
