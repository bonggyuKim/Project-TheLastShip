using System.Linq;
using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 신호음의 대응과 울리는 조건. <b>소리 자체는 안 듣는다</b> — 여기서 지키는 것은
    /// 대본의 태그가 실제 파일에 닿는가와, 조항 <c>N-1</c>(재촉에는 소리가 없다)이
    /// 배선으로 지켜지는가 둘이다.
    /// </summary>
    public sealed class LastShiftNarrationAudioTests
    {
        [SetUp]
        public void SetUp()
        {
            LastShiftNarrationAudio.Clear();
            LastShiftNarrationAudio.Muted = true;
        }

        [TearDown]
        public void TearDown()
        {
            LastShiftNarrationAudio.Muted = false;
            LastShiftNarrationAudio.Clear();
        }

        /// <summary>
        /// <b>대본이 쓰는 태그 전부에 파일이 있는가.</b> 음원이 옮겨지거나 이름이 갈리면
        /// 게임에서는 조용해질 뿐 아무 오류도 안 나므로, 그 침묵을 여기서 먼저 깬다.
        /// TA 통합본이 <c>Resources/Audio/LastShift/Onboarding</c> 으로 모았다.
        /// </summary>
        [Test]
        public void EveryTagTheScriptUsesHasAClip()
        {
            var used = LastShiftNarrationScript.All
                .Select(line => line.Sfx)
                .Where(sfx => sfx != LastShiftNarrationSfx.None)
                .Distinct()
                .ToArray();

            Assert.That(used, Is.Not.Empty);
            foreach (var sfx in used)
            {
                Assert.That(LastShiftNarrationAudio.ClipNameOf(sfx), Is.Not.Null,
                    $"{sfx} 에 파일 이름이 안 붙어 있다");
                Assert.That(LastShiftNarrationAudio.HasClip(sfx), Is.True,
                    $"{sfx} -> {LastShiftNarrationAudio.ResourceFolder}" +
                    $"{LastShiftNarrationAudio.ClipNameOf(sfx)} 가 프로젝트에 없다");
            }
        }

        /// <summary>같은 줄이 여러 프레임 떠 있어도 한 번만 운다.</summary>
        [Test]
        public void TheSameLineOnlySoundsOnce()
        {
            LastShiftNarrationAudio.Announce("AI_W_01", LastShiftNarrationSfx.ChimeLong);
            Assert.That(LastShiftNarrationAudio.LastPlayedId, Is.EqualTo("AI_W_01"));

            LastShiftNarrationAudio.Announce("AI_W_02", LastShiftNarrationSfx.None);
            Assert.That(LastShiftNarrationAudio.LastPlayedId, Is.EqualTo("AI_W_01"),
                "태그가 없는 줄이 앞줄의 기록을 지웠다");

            LastShiftNarrationAudio.Announce("AI_W_02", LastShiftNarrationSfx.None);
            LastShiftNarrationAudio.Announce("AI_W_03", LastShiftNarrationSfx.ChimeShort);
            Assert.That(LastShiftNarrationAudio.LastPlayedSfx,
                Is.EqualTo(LastShiftNarrationSfx.ChimeShort));
        }

        /// <summary>
        /// 조항 <c>N-1</c>. 재촉은 <b>같은 줄의 다른 말</b>이라 id 가 안 바뀌고, 그래서
        /// 배선상 소리가 두 번 날 길이 없다. 대본 쪽 데이터도 재촉에 태그를 안 단다.
        /// </summary>
        [Test]
        public void NudgesCarryNoSoundOfTheirOwn()
        {
            foreach (var line in LastShiftNarrationScript.All)
            {
                if (!line.HasNudge) continue;
                LastShiftNarrationAudio.Clear();

                LastShiftNarrationAudio.Announce(line.Id, line.Sfx);
                var afterFirst = LastShiftNarrationAudio.LastPlayedId;

                // 재촉으로 갈려도 부르는 쪽은 같은 id 를 넘긴다.
                LastShiftNarrationAudio.Announce(line.Id, line.Sfx);
                Assert.That(LastShiftNarrationAudio.LastPlayedId, Is.EqualTo(afterFirst),
                    $"{line.Id} 재촉에서 신호음이 다시 났다");
            }
        }
    }
}
