using DoodleUp.Editor;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 설비가 붙은 끝벽에 <b>바닥 소품을 두지 않는다</b>(game-art 확정 2026-08-12).
    ///
    /// 설비를 방 중앙에서 끝벽으로 옮기고 나니 그 벽에 이미 서 있던 드레싱과 겹쳤다 —
    /// 냉각실 <c>CrateStack_Aft</c> 가 교환기 몸체 안에 박혔다. 규칙을 코드에 안 남기면
    /// 다음에 설비나 소품이 움직일 때 같은 일이 조용히 다시 난다.
    ///
    /// <b>씬을 안 본다.</b> 여기서 재는 것은 규칙 자체이고, 실제 배에서 누가 어겼는지는
    /// 조립할 때 <c>[LAST_SHIFT_DRESSING_FEATURE]</c> 로 좌표까지 찍힌다. 둘을 섞으면
    /// 아트가 소품을 옮기는 동안 이 검사가 붉어져서 아무도 못 고친다.
    /// </summary>
    public sealed class LastShiftDressingKeepOutTests
    {
        private const float DeckY = 0.12f;

        /// <summary>냉각 교환기의 실측 상자 — 끝벽에 붙은 뒤 값이다.</summary>
        private static Bounds Exchanger() =>
            new(new Vector3(0f, 1.10f, 13.24f), new Vector3(2.20f, 2.20f, 0.84f));

        private static bool Violates(Bounds prop) =>
            LastShiftModularKitImporter.ViolatesFeatureKeepOut(
                Exchanger(), prop, LastShiftModularKitImporter.DressingKeepOut,
                DeckY, LastShiftModularKitImporter.DressingFloorReach);

        /// <summary>
        /// <b>실제로 났던 그 겹침을 잡는다.</b> 좌표는 조립 후 실측값이다 — 크레이트가
        /// 교환기 하부에 들어가 있었다.
        /// </summary>
        [Test]
        public void TheCrateThatEndedUpInsideTheExchangerIsCaught()
        {
            var crate = new Bounds(new Vector3(-0.18f, 0.38f, 13.45f), new Vector3(0.72f, 0.52f, 0.48f));

            Assert.That(Violates(crate), Is.True,
                "교환기 몸체 안에 있는 크레이트가 안 잡힌다 — 이 규칙이 잡으라고 있는 그 상황이다");
        }

        /// <summary>
        /// <b>여유 <c>0.10m</c> 가 실제로 붙는다.</b> 상자 밖이지만 그 안쪽인 소품도 잡혀야
        /// 설비 앞에서 손이 닿는다. 딱 붙여 놓는 것을 허용하면 규칙이 이름만 남는다.
        /// </summary>
        [Test]
        public void TheMarginPushesPropsFurtherThanTheBodyItself()
        {
            // 교환기 앞면은 z=12.82. 그 앞 0.05m 에 놓인 얇은 소품은 몸체와는 안 닿지만
            // 여유 안이다.
            var justInFront = new Bounds(new Vector3(0f, 0.30f, 12.75f), new Vector3(0.3f, 0.4f, 0.06f));
            Assert.That(Violates(justInFront), Is.True, "여유 0.10m 가 안 붙었다");

            // 0.30m 앞이면 여유 밖이다.
            var clear = new Bounds(new Vector3(0f, 0.30f, 12.50f), new Vector3(0.3f, 0.4f, 0.06f));
            Assert.That(Violates(clear), Is.False, "여유 밖 소품까지 잡으면 끝벽 근처가 통째로 금지된다");
        }

        /// <summary>
        /// <b>벽에 거는 소품은 이 규칙 밖이다.</b> art 가 금지한 것은 <b>바닥</b> 소품이고,
        /// 높이 걸린 것은 설비 위를 지나가도 통행이나 손닿음을 안 막는다.
        /// </summary>
        [Test]
        public void ThingsHungOnTheWallAreNotFloorProps()
        {
            var onTheWall = new Bounds(new Vector3(0f, 1.80f, 13.45f), new Vector3(0.5f, 0.3f, 0.1f));

            Assert.That(Violates(onTheWall), Is.False,
                "벽에 건 소품까지 잡으면 설비가 붙은 벽에는 아무것도 못 걸게 된다");
        }

        /// <summary>같은 방 반대쪽 소품은 안 잡힌다 — 규칙이 방 전체로 번지면 안 된다.</summary>
        [Test]
        public void PropsElsewhereInTheRoomAreLeftAlone()
        {
            var sideWall = new Bounds(new Vector3(-3.4f, 0.38f, 13.45f), new Vector3(0.72f, 0.52f, 0.48f));
            var midRoom = new Bounds(new Vector3(0f, 0.38f, 10.0f), new Vector3(0.72f, 0.52f, 0.48f));

            Assert.That(Violates(sideWall), Is.False, "측벽으로 옮긴 소품이 여전히 잡힌다");
            Assert.That(Violates(midRoom), Is.False, "방 중간 소품이 잡힌다");
        }

        /// <summary>여유가 <c>0</c> 이면 딱 붙는 배치가 통과한다 — 상수가 살아 있어야 한다.</summary>
        [Test]
        public void TheKeepOutIsAnActualDistance()
        {
            Assert.That(LastShiftModularKitImporter.DressingKeepOut, Is.EqualTo(0.10f).Within(0.001f));
            Assert.That(LastShiftModularKitImporter.DressingFloorReach, Is.GreaterThan(0f));
        }
    }
}
