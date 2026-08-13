using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 대사·문구 표(<see cref="LastShiftText"/>)와 그 파서.
    ///
    /// <b>여기서 지키는 계약은 둘이다.</b> 파일이 표로 정확히 들어오는가, 그리고 코드가 쓰는
    /// 키가 파일에 실제로 있는가. 둘째가 더 중요하다 — 키를 바꾸고 파일을 안 고치면 게임은
    /// 안 죽고 화면에 <c>⟨key⟩</c> 가 뜬 채로 나간다.
    /// </summary>
    public sealed class LastShiftTextTests
    {
        [TearDown]
        public void Cleanup() => LastShiftText.Clear();

        // ── 파서 ────────────────────────────────────────────────────────────

        [Test]
        public void ParsesAFlatObjectIntoPairs()
        {
            var table = LastShiftTextTable.Parse("{ \"a.b\": \"값\", \"c\": \"\" }");

            Assert.That(table.Count, Is.EqualTo(2));
            Assert.That(table["a.b"], Is.EqualTo("값"));
            Assert.That(table["c"], Is.EqualTo(string.Empty), "빈 문안은 빈 문자열이어야 한다");
        }

        [Test]
        public void ParsesAnEmptyObject()
        {
            Assert.That(LastShiftTextTable.Parse("{}").Count, Is.Zero);
        }

        [Test]
        public void ReadsEscapesAndUnicode()
        {
            var table = LastShiftTextTable.Parse(
                "{ \"k\": \"줄\\n바꿈 \\\"인용\\\" \\\\역슬래시 \\uAC00\" }");

            Assert.That(table["k"], Is.EqualTo("줄\n바꿈 \"인용\" \\역슬래시 가"));
        }

        [Test]
        public void LaterDuplicateWinsAndIsReported()
        {
            var duplicates = new List<string>();
            var table = LastShiftTextTable.Parse("{ \"k\": \"먼저\", \"k\": \"나중\" }", duplicates);

            Assert.That(table["k"], Is.EqualTo("나중"));
            Assert.That(duplicates, Is.EqualTo(new[] { "k" }));
        }

        /// <summary>
        /// 넓게 받으면 안 된다. 값에 구조가 생긴 파일을 조용히 절반만 읽으면, 화면 절반이 옛
        /// 문안인 채로 나가고 그걸 화면만 보고는 못 가른다.
        /// </summary>
        [Test]
        public void RejectsNestedOrNonStringValues()
        {
            Assert.Throws<LastShiftTextFormatException>(
                () => LastShiftTextTable.Parse("{ \"k\": { \"n\": \"값\" } }"));
            Assert.Throws<LastShiftTextFormatException>(
                () => LastShiftTextTable.Parse("{ \"k\": 3 }"));
            Assert.Throws<LastShiftTextFormatException>(
                () => LastShiftTextTable.Parse("{ \"k\": \"값\""));
        }

        // ── 표 ──────────────────────────────────────────────────────────────

        [Test]
        public void LoadsTheShippedKoreanFile()
        {
            Assert.That(LastShiftText.Load(), Is.True, "Resources/Text/ko.json 을 못 읽는다");
            Assert.That(LastShiftText.Locale, Is.EqualTo("ko"));
            Assert.That(LastShiftText.Count, Is.GreaterThan(0));
        }

        [Test]
        public void MissingKeyShowsItselfInsteadOfThrowing()
        {
            LastShiftText.Load();
            LogAssert.Expect(LogType.Warning, new Regex("MISSING_KEY"));

            Assert.That(LastShiftText.Get("없는.키"), Is.EqualTo("⟨없는.키⟩"),
                "빠진 문안은 화면에 그대로 보여야 다음 판에서 잡힌다");
            Assert.That(LastShiftText.MissingKeys, Does.Contain("없는.키"));
        }

        [Test]
        public void FormatFillsPlaceholders()
        {
            LastShiftText.Load();

            Assert.That(LastShiftText.Format("hud.carried", 2, 2), Is.EqualTo("들고 있음 2/2"));
        }

        /// <summary>
        /// <b>코드가 부르는 키가 파일에 다 있는가.</b> 목록을 손으로 들고 있는 것은, 컴파일된
        /// 코드에서 문자열 인자를 되짚을 방법이 없어서다 — 키를 지우면 이 검사가 먼저 빨개진다.
        /// </summary>
        [Test]
        public void EveryKeyTheGameAsksForExists()
        {
            LastShiftText.Load();

            var used = new[]
            {
                "hud.inputBar.ghost", "hud.inputBar.crew", "hud.map.hint", "hud.carried",
                "prompt.ghost.blocked",
                "prompt.item.drop", "prompt.item.dropAndSecure", "prompt.item.dropWithDistance",
                "prompt.item.identifying", "prompt.item.heldByOther", "prompt.item.grab",
                "prompt.item.serverRejected", "prompt.item.securedByCrew", "prompt.item.securedInitial",
                "prompt.valve.sustaining", "prompt.valve.dead", "prompt.valve.hold",
                "prompt.recovery.actions", "prompt.recovery.abandon",
                "prompt.deckHatch.dead", "prompt.deckHatch.close", "prompt.deckHatch.open",
                "prompt.door.dead", "prompt.door.close", "prompt.door.open",
                "prompt.core.dead", "prompt.core.depressurizing", "prompt.core.repressurizing",
                "prompt.core.openGate", "prompt.core.closeGate", "prompt.core.ascend",
                "prompt.core.descend", "prompt.core.blockedBySegment", "prompt.core.blockedByLiftAway",
                "prompt.salvage.depleted", "prompt.salvage.handsFull",
                "prompt.salvage.harvesting", "prompt.salvage.harvest",
                "term.zone.cockpit", "term.zone.power", "term.zone.cooling", "term.zone.lifeSupport",
                "term.boundary.join",
                "term.room.plaza", "term.room.quarters",
                "term.room.purpose.plaza", "term.room.purpose.cockpit",
                "term.room.purpose.lifeSupport", "term.room.purpose.power",
                "term.room.purpose.cooling", "term.room.purpose.quarters",
                "term.core.name", "term.core.purpose",
                "term.compartment.quarters"
            };

            var absent = used.Where(key => !LastShiftText.Has(key)).ToArray();
            Assert.That(absent, Is.Empty, $"파일에 없는 키: {string.Join(", ", absent)}");
        }

        /// <summary>
        /// 파일에만 있고 아무도 안 쓰는 문안은 번역 비용만 늘린다. 반대 방향도 같이 잰다.
        /// </summary>
        [Test]
        public void EveryShippedKeyIsNamespaced()
        {
            LastShiftText.Load();

            var stray = LastShiftText.Keys.Where(key => !key.Contains('.')).ToArray();
            Assert.That(stray, Is.Empty, $"이름 공간이 없는 키: {string.Join(", ", stray)}");
        }

        /// <summary>
        /// 자리표가 <c>{0}</c> 부터 빈칸 없이 이어지는가. 번역문이 <c>{2}</c> 만 남기면
        /// 그 줄은 실행 시점에 서식 예외로 원문으로 되돌아간다.
        /// </summary>
        [Test]
        public void PlaceholdersAreContiguousFromZero()
        {
            LastShiftText.Load();

            foreach (var key in LastShiftText.Keys)
            {
                var indexes = Regex.Matches(LastShiftText.Get(key), @"\{(\d+)\}")
                    .Select(match => int.Parse(match.Groups[1].Value))
                    .Distinct()
                    .OrderBy(index => index)
                    .ToArray();
                if (indexes.Length == 0) continue;

                Assert.That(indexes, Is.EqualTo(Enumerable.Range(0, indexes.Length).ToArray()),
                    $"{key} 의 자리표가 0부터 이어지지 않는다");
            }
        }
    }
}
