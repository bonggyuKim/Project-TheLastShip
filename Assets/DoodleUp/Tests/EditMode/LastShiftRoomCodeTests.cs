using System.Collections.Generic;
using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    public sealed class LastShiftRoomCodeTests
    {
        /// <summary>디스커버리 기본 포트를 피해 잡는다. 같은 PC 에서 진짜 방이 떠 있어도 안 겹치게 한다.</summary>
        private const int TestDiscoveryPort = 7991;

        private const ushort TestGamePort = 7995;

        [Test]
        public void GeneratedCodesAvoidLookAlikeGlyphsAndStayValid()
        {
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var code = LastShiftRoomCode.Generate();
                Assert.That(code.Length, Is.EqualTo(LastShiftRoomCode.Length));
                Assert.That(LastShiftRoomCode.IsValid(code), Is.True, $"generated code rejected itself: {code}");
                Assert.That(code, Does.Not.Contain("I").And.Not.Contain("L").And.Not.Contain("O")
                    .And.Not.Contain("0").And.Not.Contain("1"));
            }
        }

        [Test]
        public void GeneratedCodesAreNotAllTheSame()
        {
            var codes = new HashSet<string>();
            for (var attempt = 0; attempt < 50; attempt++) codes.Add(LastShiftRoomCode.Generate());
            Assert.That(codes.Count, Is.GreaterThan(40));
        }

        [Test]
        public void NormalizeAcceptsHowPeopleActuallyTypeACode()
        {
            Assert.That(LastShiftRoomCode.Normalize(" ab-cd 23 "), Is.EqualTo("ABCD23"));
            Assert.That(LastShiftRoomCode.Normalize("ab_cd23"), Is.EqualTo("ABCD23"));
            Assert.That(LastShiftRoomCode.Normalize(null), Is.Empty);
        }

        [Test]
        public void ValidationRejectsWrongLengthAndExcludedGlyphs()
        {
            Assert.That(LastShiftRoomCode.IsValid("ABCD2"), Is.False);
            Assert.That(LastShiftRoomCode.IsValid("ABCD234"), Is.False);
            Assert.That(LastShiftRoomCode.IsValid("ABCD2O"), Is.False, "O 는 0 과 헷갈려서 뺐다");
            Assert.That(LastShiftRoomCode.IsValid("ABCD2I"), Is.False, "I 는 1 과 헷갈려서 뺐다");
            Assert.That(LastShiftRoomCode.IsValid("ABCD23"), Is.True);
        }

        [Test]
        public void ProtocolRoundTripsQueryAndReply()
        {
            Assert.That(LastShiftRoomProtocol.TryParseQuery(LastShiftRoomProtocol.BuildQuery("ABCD23"), out var asked), Is.True);
            Assert.That(asked, Is.EqualTo("ABCD23"));

            Assert.That(
                LastShiftRoomProtocol.TryParseReply(LastShiftRoomProtocol.BuildReply("ABCD23", 7979), out var code, out var port),
                Is.True);
            Assert.That(code, Is.EqualTo("ABCD23"));
            Assert.That(port, Is.EqualTo(7979));
        }

        [Test]
        public void ProtocolRejectsForeignAndMalformedTraffic()
        {
            // 디스커버리 포트에는 다른 프로그램의 패킷도 날아온다. 그것을 방 응답으로 읽으면 안 된다.
            Assert.That(LastShiftRoomProtocol.TryParseQuery("hello", out _), Is.False);
            Assert.That(LastShiftRoomProtocol.TryParseQuery("LASTSHIFT/2 QUERY ABCD23", out _), Is.False);
            Assert.That(LastShiftRoomProtocol.TryParseQuery("LASTSHIFT/1 QUERY ABCD2O", out _), Is.False);
            Assert.That(LastShiftRoomProtocol.TryParseReply("LASTSHIFT/1 ROOM ABCD23 0", out _, out _), Is.False);
            Assert.That(LastShiftRoomProtocol.TryParseReply("LASTSHIFT/1 ROOM ABCD23", out _, out _), Is.False);
        }

        [Test]
        public void BeaconResolvesItsOwnCodeToTheHostEndpoint()
        {
            var code = LastShiftRoomCode.Generate();
            using var beacon = new LastShiftRoomBeacon(code, TestGamePort, TestDiscoveryPort);

            Assert.That(
                LastShiftRoomResolver.TryResolve(code, 3000, out var endpoint, TestDiscoveryPort),
                Is.True,
                "코드로 자기 방을 못 찾으면 로비 입장 경로 전체가 죽는다");
            Assert.That(endpoint.Port, Is.EqualTo((int)TestGamePort));
        }

        [Test]
        public void BeaconIgnoresOtherRoomsCode()
        {
            var hosted = "ABCD23";
            var asked = "ABCD24";
            using var beacon = new LastShiftRoomBeacon(hosted, TestGamePort, TestDiscoveryPort);

            // 같은 LAN 에 방이 여럿일 때 코드가 방을 가르는지. 타임아웃은 짧게 — 실패가 정답이다.
            Assert.That(LastShiftRoomResolver.TryResolve(asked, 700, out _, TestDiscoveryPort), Is.False);
        }

        [Test]
        public void ResolverRefusesACodeThatCannotExist()
        {
            Assert.That(LastShiftRoomResolver.TryResolve("nope", 200, out _, TestDiscoveryPort), Is.False);
        }
    }
}
