using System.Linq;
using System.Text.RegularExpressions;
using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 대본이 <b>자기 조항을 어기지 않는가</b>. 문안 자체의 좋고 나쁨은 game-writer 소관이고,
    /// 여기서 지키는 것은 대본이 스스로 적어 둔 규칙 넷이다.
    /// </summary>
    public sealed class LastShiftNarrationScriptTests
    {
        [Test]
        public void EveryLineIdIsUnique()
        {
            var ids = LastShiftNarrationScript.InPlayOrder.Select(line => line.Id).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length),
                "같은 id 가 두 번 있다 — Of() 가 어느 쪽을 줄지가 정의에 안 남는다");
            Assert.That(ids, Has.All.Not.Null.And.All.Not.Empty);
        }

        /// <summary>
        /// 조항 <c>N-7</c> — 임계 숫자를 문안에 박지 않는다. 경고를 띄우는 조건과 화면에 적히는
        /// 숫자가 두 벌이 되면, 임계를 옮겼을 때 "45%에서 뜨는데 화면에는 40%" 가 조용히 생긴다.
        /// 실제로 오늘 <c>40/25</c> 가 <c>45/30</c> 으로 한 번 움직였고, 문안을 한 줄도 안 고쳤다.
        /// </summary>
        [Test]
        public void NoLineHardCodesAThreshold()
        {
            foreach (var line in LastShiftNarrationScript.InPlayOrder)
            {
                Assert.That(Regex.IsMatch(line.Text, @"\d+\s*%"), Is.False,
                    $"{line.Id} 본문에 임계 숫자가 박혀 있다: {line.Text}");
                Assert.That(Regex.IsMatch(line.Nudge ?? string.Empty, @"\d+\s*%"), Is.False,
                    $"{line.Id} 재촉에 임계 숫자가 박혀 있다: {line.Nudge}");
            }
        }

        /// <summary>
        /// 조항 <c>N-6</c> — 마무리 두 줄은 도면 뒤에 온다. <c>AI_F_15</c> 가 요약하는 루프 셋 중
        /// 하나가 도면이라, 도면을 안 본 사람에게는 셋 중 하나가 거짓말이 된다.
        /// </summary>
        [Test]
        public void TheWrapUpComesAfterTheBlueprintBlock()
        {
            var order = LastShiftNarrationScript.InPlayOrder.Select(line => line.Id).ToList();
            Assert.That(order.IndexOf("AI_F_15"), Is.GreaterThan(order.IndexOf("AI_B_17")),
                "루프 요약이 도면 블록보다 먼저 온다 — 셋 중 하나가 아직 거짓말이다");
            Assert.That(order.IndexOf("AI_F_16"), Is.EqualTo(order.Count - 1),
                "손 떼기가 마지막이 아니다");
        }

        /// <summary>
        /// 긴 신호음이 어디에 붙어 있는가. 정본 §2 는 <b>"단계 전환 첫 줄에만 · 이 대본에서
        /// 정확히 5회"</b> 라고 적는다.
        ///
        /// <b>그런데 표에는 6회다.</b> 적재한 구간에서 <c>AI_F_01</c>(승강기 사거리 최초 진입)이
        /// 블록 첫 줄이 아닌데 긴 신호음을 갖는다 — <c>AI_B_01</c> 이 이미 "나가는 길" 을 열고
        /// 있기 때문이다. v1.13 이 블록을 재매핑하면서 <c>AI_F_01</c> 이 블록 <b>중간</b>으로
        /// 옮겨간 자국으로 보인다(예전에는 파밍이 승강기에서 시작했다).
        ///
        /// <b>데이터를 임의로 고치지 않는다.</b> 문안은 game-writer 소관이고, 여기서 소리를
        /// 하나 지우면 대본과 코드가 갈린다. 대신 지금 상태를 그대로 못박아 둔다 — 정본이
        /// 고쳐지면 이 검사가 먼저 깨지고, 그때 같이 맞춘다.
        /// </summary>
        [Test]
        public void TheLongChimeSitsWhereTheScriptPutIt()
        {
            var longs = LastShiftNarrationScript.InPlayOrder
                .Where(line => line.Sfx == LastShiftNarrationSfx.ChimeLong)
                .Select(line => line.Id)
                .ToArray();

            Assert.That(longs, Is.EqualTo(new[] { "AI_B_01", "AI_F_01", "AI_B_11", "AI_F_15" }),
                "긴 신호음 자리가 대본과 달라졌다 — 정본 §2 와 대조할 것");

            // 넷 중 셋은 블록 첫 줄이다. AI_F_01 하나만 중간이고 그것이 위에 적은 불일치다.
            var openers = new[]
            {
                LastShiftNarrationScript.Exit[0].Id,
                LastShiftNarrationScript.Blueprint[0].Id,
                LastShiftNarrationScript.HandsOff[0].Id
            };
            Assert.That(longs.Intersect(openers).Count(), Is.EqualTo(3));
            Assert.That(longs.Except(openers), Is.EqualTo(new[] { "AI_F_01" }),
                "블록 중간의 긴 신호음이 AI_F_01 말고 또 생겼다");
        }

        /// <summary>재촉이 있으면 전환 초도 있어야 한다. 없으면 영영 안 갈린다.</summary>
        [Test]
        public void EveryNudgeHasATime()
        {
            foreach (var line in LastShiftNarrationScript.InPlayOrder)
            {
                if (!line.HasNudge) continue;
                Assert.That(line.NudgeAfterSeconds, Is.GreaterThan(0f),
                    $"{line.Id} 에 재촉은 있는데 전환 초가 0 이다");
            }
        }

        /// <summary>
        /// 적재한 줄 수. 트리거가 이미 코드에 있는 블록만 담았으므로, 기상 일곱과 순회 여섯은
        /// 여기 없다 — 그 둘이 들어오면 이 수가 오른다.
        /// </summary>
        [Test]
        public void TheLoadedBlocksAreAccountedFor()
        {
            Assert.That(LastShiftNarrationScript.Exit.Length, Is.EqualTo(9));
            Assert.That(LastShiftNarrationScript.Farming.Length, Is.EqualTo(8));
            Assert.That(LastShiftNarrationScript.Blueprint.Length, Is.EqualTo(7));
            Assert.That(LastShiftNarrationScript.HandsOff.Length, Is.EqualTo(2));
            Assert.That(LastShiftNarrationScript.Count, Is.EqualTo(26));
        }
    }
}
