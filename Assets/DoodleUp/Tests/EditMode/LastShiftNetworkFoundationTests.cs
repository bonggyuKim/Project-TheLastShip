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
        public void SpawnSlotsFaceTheQuartersDoor()
        {
            // 과녁이 <b>광장 원점에서 숙소 문으로</b> 옮겨 왔다. 스폰이 조종석에서 숙소로
            // 가면서(온보딩 1단계가 "기상(숙소)" 이다) 원점을 겨누면 벽을 비스듬히 본다.
            // 눈을 뜨면 나갈 곳이 정면이어야 AI_W_02~07 이 가리키는 곳과 화면이 맞는다.
            var quarters = LastShiftPlazaLayout.Of(LastShiftPlazaSpace.Quarters);
            var target = new Vector3(quarters.MinX + LastShiftShipDimensions.QuartersDoorInset,
                LastShiftSandboxController.PlayerSpawn.y, quarters.MinZ);
            for (var slot = 0; slot < LastShiftNetworkSession.MaxPlayers; slot++)
            {
                var spawn = LastShiftNetworkSession.SpawnForSlot(slot);
                var direction = (target - spawn).normalized;
                var forward = LastShiftNetworkSession.RotationForSlot(slot) * Vector3.forward;
                Assert.That(Vector3.Dot(forward, direction), Is.GreaterThan(0.999f));
                // 문이 <c>-z</c> 쪽이므로 시선도 그쪽이다. "배 안쪽을 향한다" 를 x 로 재던
                // 줄은 스폰이 조종석에 있을 때의 것이라 여기서 뜻을 잃었다.
                Assert.That(forward.z, Is.LessThan(0f), "스폰 시선이 숙소 문 쪽이 아니다.");

                // 네 슬롯이 모두 <b>숙소 안</b>에서 시작해야 한다 — 한 명이라도 벽을 뚫고
                // 서면 그 사람의 도입부는 다른 방에서 시작한다.
                Assert.That(spawn.x, Is.InRange(quarters.MinX, quarters.MaxX),
                    "슬롯이 숙소 x 밖이다.");
                Assert.That(spawn.z, Is.InRange(quarters.MinZ, quarters.MaxZ),
                    "슬롯이 숙소 z 밖이다 — 4인 슬롯 폭이 방을 넘는다.");
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
