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
            var target = new Vector3(0f, LastShiftSandboxController.PlayerSpawn.y, -1.3f);
            for (var slot = 0; slot < LastShiftNetworkSession.MaxPlayers; slot++)
            {
                var direction = (target - LastShiftNetworkSession.SpawnForSlot(slot)).normalized;
                var forward = LastShiftNetworkSession.RotationForSlot(slot) * Vector3.forward;
                Assert.That(Vector3.Dot(forward, direction), Is.GreaterThan(0.999f));
                Assert.That(Vector3.Distance(LastShiftNetworkSession.SpawnForSlot(slot), target), Is.LessThan(LastShiftPlayerController.AwarenessDistance));
            }
        }

        [Test]
        public void NetworkItemUsesExplicitUnclaimedSentinel()
        {
            Assert.That(LastShiftNetworkGrabbable.NoHolder, Is.EqualTo(ulong.MaxValue));
        }
    }
}
