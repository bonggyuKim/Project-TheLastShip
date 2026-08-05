using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftNetworkFoundationTests
    {
        [Test]
        public void FourSlotsReceiveDistinctDeterministicSpawns()
        {
            var spawns = new HashSet<Vector3>();
            for (var slot = 0; slot < LastShiftNetworkSession.MaxPlayers; slot++)
                spawns.Add(LastShiftNetworkSession.SpawnForSlot(slot));

            Assert.That(spawns.Count, Is.EqualTo(4));
            Assert.That(Vector3.Distance(LastShiftNetworkSession.SpawnForSlot(0), LastShiftNetworkSession.SpawnForSlot(1)), Is.GreaterThan(0.8f));
        }

        [Test]
        public void SlotAllocatorRejectsFifthAndReusesDisconnectedSlot()
        {
            var allocator = new LastShiftNetworkSlotAllocator();
            Assert.That(allocator.TryReserve(10, out var first), Is.True);
            Assert.That(allocator.TryReserve(11, out var second), Is.True);
            Assert.That(allocator.TryReserve(12, out var third), Is.True);
            Assert.That(allocator.TryReserve(13, out var fourth), Is.True);
            Assert.That(new[] { first, second, third, fourth }, Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
            Assert.That(allocator.TryReserve(14, out _), Is.False);

            Assert.That(allocator.Release(11), Is.True);
            Assert.That(allocator.TryReserve(15, out var reused), Is.True);
            Assert.That(reused, Is.EqualTo(second));
            Assert.That(allocator.Count, Is.EqualTo(4));
        }

        [Test]
        public void SessionDefaultsToLoopbackTransportEndpoint()
        {
            Assert.That(LastShiftNetworkSession.DefaultPort, Is.EqualTo(7979));
            Assert.That(LastShiftNetworkSession.MaxPlayers, Is.EqualTo(4));
        }

        [Test]
        public void SpawnSlotsFaceTheInitialLooseCoolingCanister()
        {
            var target = new Vector3(
                LastShiftShipDimensions.CoolingNominal.x,
                LastShiftSandboxController.PlayerSpawn.y,
                LastShiftShipDimensions.CoolingNominal.z);
            for (var slot = 0; slot < LastShiftNetworkSession.MaxPlayers; slot++)
            {
                var spawn = LastShiftNetworkSession.SpawnForSlot(slot);
                var direction = (target - spawn).normalized;
                var forward = LastShiftNetworkSession.RotationForSlot(slot) * Vector3.forward;
                Assert.That(Vector3.Dot(forward, direction), Is.GreaterThan(0.999f));
                // 36m 선체에서는 냉각통이 다른 구역에 있어 인지 거리(8m) 안에 들어오지 않는다.
                // 그래서 "보인다" 가 아니라 "배 안쪽을 향한다" 만 검증한다 — 스폰 시선이 조종석
                // 끝벽을 향하면 첫 프레임에 갈 방향이 읽히지 않는다.
                Assert.That(forward.x, Is.GreaterThan(0f), "스폰 시선은 선미(엔진실) 쪽이어야 한다.");
                Assert.That(spawn.x, Is.LessThan(LastShiftShipDimensions.ZoneMaxX(LastShiftZone.Cockpit)),
                    "스폰은 조종석 안이어야 한다.");
            }
        }

        [Test]
        public void NetworkItemUsesExplicitUnclaimedSentinel()
        {
            Assert.That(LastShiftNetworkGrabbable.NoHolder, Is.EqualTo(ulong.MaxValue));
        }

        [Test]
        public void ItemSafetyBoundsContainEveryCanonicalNominalPosition()
        {
            var canonicalPositions = new[]
            {
                LastShiftShipDimensions.BatteryNominal,
                LastShiftShipDimensions.CoolingNominal,
                LastShiftShipDimensions.PatchPlateNominal,
                LastShiftShipDimensions.TetherNominal
            };

            Assert.That(canonicalPositions, Is.All.Matches<Vector3>(LastShiftNetworkSandbox.ItemSafetyBounds.Contains));
            // 경계 밖 표본도 선체 치수에서 파생시킨다. 20m 같은 리터럴은 12.5m 배에서만
            // 밖이었고 36m 배에서는 안쪽이라, 그대로 두면 이 검사가 조용히 무력해진다.
            var bounds = LastShiftNetworkSandbox.ItemSafetyBounds;
            Assert.That(bounds.Contains(new Vector3(0f, bounds.min.y - 4f, 0f)), Is.False);
            Assert.That(bounds.Contains(new Vector3(bounds.max.x + 4f, 1f, 0f)), Is.False);
            Assert.That(bounds.Contains(new Vector3(0f, bounds.max.y + 4f, 0f)), Is.False);
            Assert.That(bounds.Contains(new Vector3(0f, 1f, bounds.max.z + 4f)), Is.False);
        }
    }
}
