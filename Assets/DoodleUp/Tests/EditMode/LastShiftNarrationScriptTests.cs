using System.Linq;
using System.Text.RegularExpressions;
using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

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
        /// 조항 §2 — <b>긴 신호음은 블록 첫 줄에만.</b> 이제 예외가 없으므로 자리를 나열하는
        /// 대신 <b>규칙 자체</b>를 건다. 블록을 추가하면 그 첫 줄도 자동으로 이 검사에 걸린다.
        ///
        /// v1.15 이전에는 <c>AI_F_01</c> 이 블록 중간인데 긴 신호음을 갖고 있었다. v1.13 이
        /// 파밍의 시작을 승강기에서 잔해로 옮기면서 소리가 안 따라간 자국이었고, 적재 중
        /// 발견해 정본이 <c>AI_F_01 → AI_F_07</c> 로 고쳤다. 그때 이 검사가 먼저 깨졌다.
        /// </summary>
        [Test]
        public void EveryLongChimeOpensABlock()
        {
            var blocks = new[]
            {
                LastShiftNarrationScript.Wake,
                LastShiftNarrationScript.Exit,
                LastShiftNarrationScript.Farming,
                LastShiftNarrationScript.Blueprint,
                LastShiftNarrationScript.HandsOff
            };
            var openers = blocks.Select(block => block[0].Id).ToArray();

            var longs = LastShiftNarrationScript.InPlayOrder
                .Where(line => line.Sfx == LastShiftNarrationSfx.ChimeLong)
                .Select(line => line.Id)
                .ToArray();

            Assert.That(longs, Is.EquivalentTo(openers),
                "긴 신호음이 블록 첫 줄과 일대일이 아니다 — 정본 §2 의 여섯 목록과 대조할 것");

            // 블록 안쪽 줄에는 하나도 없어야 한다.
            foreach (var block in blocks)
                foreach (var line in block.Skip(1))
                    Assert.That(line.Sfx, Is.Not.EqualTo(LastShiftNarrationSfx.ChimeLong),
                        $"{line.Id} 은 블록 중간인데 긴 신호음을 갖는다");
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
            Assert.That(LastShiftNarrationScript.Wake.Length, Is.EqualTo(7));
            Assert.That(LastShiftNarrationScript.Exit.Length, Is.EqualTo(9));
            Assert.That(LastShiftNarrationScript.Farming.Length, Is.EqualTo(8));
            Assert.That(LastShiftNarrationScript.Blueprint.Length, Is.EqualTo(7));
            Assert.That(LastShiftNarrationScript.HandsOff.Length, Is.EqualTo(2));
            Assert.That(LastShiftNarrationScript.Standing.Length, Is.EqualTo(4));
            Assert.That(LastShiftNarrationScript.Count, Is.EqualTo(33));
            // 정본 총계 50 = 블록 46 + 상시 4. 남은 13 이 순회 블록이고, 그것이 들어오면
            // 이 수가 46 으로 닫힌다.
            Assert.That(LastShiftNarrationScript.All.Length, Is.EqualTo(37));
        }

        /// <summary>
        /// 상시 경고 두 줄이 <b>지금 임계</b>를 말하는가. 정본 표에는 <c>40%</c>·<c>25%</c> 가
        /// 문자열에 박혀 있는데 값은 <c>45</c>/<c>30</c> 으로 옮겨갔다 — 박힌 채로 뒀으면
        /// 경고가 뜨는 조건과 화면 숫자가 갈렸을 자리다.
        /// </summary>
        [Test]
        public void TheStandingWarningsSpeakTheLiveThreshold()
        {
            foreach (var line in LastShiftNarrationScript.Standing)
                Assert.That(Regex.IsMatch(line.Text, @"\d+\s*%"), Is.False,
                    $"{line.Id} 에 임계 숫자가 박혀 있다: {line.Text}");

            var warning = LastShiftNarrationScript.Format(LastShiftNarrationScript.Of("AI_F_W1"));
            var critical = LastShiftNarrationScript.Format(LastShiftNarrationScript.Of("AI_F_W2"));
            Assert.That(warning, Does.Contain(
                Mathf.RoundToInt(LastShiftRecoveryTuning.SuitOxygenWarningThreshold * 100f).ToString()));
            Assert.That(critical, Does.Contain(
                Mathf.RoundToInt(LastShiftRecoveryTuning.SuitOxygenCriticalThreshold * 100f).ToString()));
            Assert.That(warning, Does.Not.Contain("{threshold}"));
            Assert.That(critical, Does.Not.Contain("{threshold}"));
        }
    }
}
