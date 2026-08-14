using DoodleUp.Editor;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 방 설비를 <b>문 맞은편 끝벽</b>에 붙이는 규칙(game-art 승인 2026-08-12, (가)안).
    ///
    /// <b>이 규칙이 없던 동안 무슨 일이 났는가.</b> 설비가 <c>BoundsCenter</c> 에 서면서
    /// 그 방의 통행이 설비를 도는 고리가 됐고, 냉각실에서 승무원이 방 중앙에서 막혔다
    /// (<c>FourCrewLeaveTheQuarters…</c>). 방에 볼일이 있는 사람이 목표 지점에 못 갔다.
    ///
    /// art 가 건 조건이 셋이다 — <b>문 개구부 · 접근면 · 통행폭</b>을 안 건드릴 것.
    /// 셋 다 "문에서 가장 먼 벽" 이라는 한 가지 선택에서 따라 나오므로, 여기서는 그
    /// 선택이 실제로 그렇게 되는지를 네 방향에서 잰다.
    /// </summary>
    public sealed class LastShiftFeaturePlacementTests
    {
        private const float Tolerance = 0.001f;

        /// <summary>가로 <c>8</c> × 세로 <c>6</c> 짜리 방 하나. 숙소와 같은 치수다.</summary>
        private static readonly float[] Room = { 4f, 12f, 6f, 12f };

        /// <summary>방 한가운데에 놓인 <c>2×2</c> 설비. 밀기 전 상태다.</summary>
        private static Bounds Centred() =>
            new(new Vector3(8f, 1f, 9f), new Vector3(2f, 2f, 2f));

        private static Vector3 Shift(float[] door) =>
            LastShiftModularKitImporter.EndWallShift(
                Room, door, Centred(), LastShiftModularKitImporter.FeatureWallInset);

        /// <summary>
        /// <b>문이 어느 변에 붙어도 설비는 반대편으로 간다.</b> 네 방향을 전부 재는 이유는,
        /// 한 방향만 맞고 나머지가 뒤집혀도 그 방에서만 안 보이기 때문이다.
        /// </summary>
        [Test]
        public void TheFixtureGoesToTheWallOppositeTheDoor()
        {
            var inset = LastShiftModularKitImporter.FeatureWallInset;
            var half = 1f; // 설비 반폭

            // 문이 -X 변(x=4)에 있으면 설비는 +X 끝벽(x=12)에 붙는다.
            var toPlusX = Shift(new[] { 4f, 0f, 9f });
            Assert.That(toPlusX.x, Is.GreaterThan(0f), "문 반대편이 아니라 문 쪽으로 갔다");
            Assert.That(8f + toPlusX.x + half, Is.EqualTo(12f - inset).Within(Tolerance),
                "설비 끝면이 +X 끝벽에서 inset 만큼 떨어져 있지 않다");
            Assert.That(toPlusX.z, Is.EqualTo(0f).Within(Tolerance), "안 밀어도 되는 축이 움직였다");

            // +X 변(x=12) 이면 -X 끝벽(x=4).
            var toMinusX = Shift(new[] { 12f, 0f, 9f });
            Assert.That(8f + toMinusX.x - half, Is.EqualTo(4f + inset).Within(Tolerance));

            // -Z 변(z=6) 이면 +Z 끝벽(z=12).
            var toPlusZ = Shift(new[] { 8f, 0f, 6f });
            Assert.That(9f + toPlusZ.z + half, Is.EqualTo(12f - inset).Within(Tolerance));
            Assert.That(toPlusZ.x, Is.EqualTo(0f).Within(Tolerance));

            // +Z 변(z=12) 이면 -Z 끝벽(z=6).
            var toMinusZ = Shift(new[] { 8f, 0f, 12f });
            Assert.That(9f + toMinusZ.z - half, Is.EqualTo(6f + inset).Within(Tolerance));
        }

        /// <summary>
        /// <b>art 조건 1·2 — 개구부와 접근면.</b> 문 앞 접근 구역은 문 폭 × 접근 깊이만큼
        /// 문에 붙어 있다. 설비가 그 상자와 안 겹쳐야 한다.
        /// </summary>
        [Test]
        public void TheFixtureNeverEntersTheDoorApproach()
        {
            var half = 1f;
            var depth = LastShiftDoorways.ApproachDepth;

            // 문이 -Z 변 한가운데(x=8, z=6)에 있는 방. 접근면은 z = 6 … 6+depth 다.
            var shift = Shift(new[] { 8f, 0f, 6f });
            var nearFace = 9f + shift.z - half;

            Assert.That(nearFace, Is.GreaterThanOrEqualTo(6f + depth),
                $"설비 앞면이 z={nearFace:0.###} 라 문 앞 접근면(6 … {6f + depth:0.###})을 먹는다");
        }

        /// <summary>
        /// <b>art 조건 3 — 통행폭.</b> 설비를 끝벽에 붙이고 남는 방 깊이가 1인 통행 폭
        /// 밑으로 내려가면, 중앙을 비운 대가로 반대편이 막힌 것이라 규칙이 뒤집힌 것이다.
        /// </summary>
        [Test]
        public void ClearingTheMiddleDoesNotChokeTheRoom()
        {
            var half = 1f;
            var shift = Shift(new[] { 8f, 0f, 6f });
            var nearFace = 9f + shift.z - half;
            var free = nearFace - 6f;

            Assert.That(free, Is.GreaterThanOrEqualTo(LastShiftDoorways.MinClearWidth),
                $"설비를 붙이고 남은 깊이가 {free:0.###}m 라 통행 폭 " +
                $"{LastShiftDoorways.MinClearWidth:0.##}m 밑이다");
        }

        /// <summary>
        /// 방 <b>한가운데는 비어 있어야 한다</b> — 이 규칙의 목적 자체다. 설비가 중심점을
        /// 안 덮는지로 잰다.
        /// </summary>
        [Test]
        public void TheMiddleOfTheRoomComesOutEmpty()
        {
            foreach (var door in new[]
                     {
                         new[] { 4f, 0f, 9f }, new[] { 12f, 0f, 9f },
                         new[] { 8f, 0f, 6f }, new[] { 8f, 0f, 12f },
                     })
            {
                var box = Centred();
                box.center += Shift(door);
                Assert.That(box.Contains(new Vector3(8f, 1f, 9f)), Is.False,
                    $"문 {door[0]},{door[2]} 인 방에서 설비가 여전히 한가운데를 덮는다");
            }
        }

        /// <summary>
        /// 벽에 <b>딱 붙이지 않는다</b>. 벽 메시와 같은 평면이면 z-fighting 이 나고, 벽 안쪽
        /// 마감 띠가 있는 조각에서는 설비가 그 띠를 뚫은 것처럼 보인다.
        /// </summary>
        [Test]
        public void TheFixtureKeepsAHairOffTheWall()
        {
            Assert.That(LastShiftModularKitImporter.FeatureWallInset, Is.GreaterThan(0f));
            Assert.That(LastShiftModularKitImporter.FeatureWallInset, Is.LessThanOrEqualTo(0.05f),
                "간격이 0.05m 를 넘으면 '끝벽 flush' 가 아니라 그냥 벽 근처다");
        }

        /// <summary>문 좌표가 없으면 안 민다 — 없는 기준으로 방향을 정하지 않는다.</summary>
        [Test]
        public void NoDoorMeansNoShift()
        {
            Assert.That(LastShiftModularKitImporter.EndWallShift(Room, null, Centred(), 0.05f),
                Is.EqualTo(Vector3.zero));
            Assert.That(LastShiftModularKitImporter.EndWallShift(null, new[] { 8f, 0f, 6f }, Centred(), 0.05f),
                Is.EqualTo(Vector3.zero));
        }
    }
}
