using DoodleUp.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// 자원 넷이 게이지로 바뀌는 환산.
    ///
    /// <b>여기서 고정하는 것은 "없는 정보를 지어내지 않는다" 는 규칙이다.</b> 상한이 없는
    /// 축을 비율로 그리면 눈금이 거짓말을 하고, 계통이 없는 축을 0 으로 그리면 "바닥났다"
    /// 는 없는 사실이 생긴다. 둘 다 실제로 하기 쉬운 실수라 검사로 못 박는다.
    /// </summary>
    public sealed class LastShiftResourceGaugeTests
    {
        [SetUp]
        public void SetUp()
        {
            LastShiftMaintenance.Clear();
            LastShiftMaterials.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            LastShiftMaintenance.Clear();
            LastShiftMaterials.Clear();
        }

        /// <summary>정비여력은 상한이 실제로 있어(조항 B-2) 게이지가 재는 값이 진짜 비율이다.</summary>
        [Test]
        public void MaintenanceFillIsARealRatioAgainstItsCap()
        {
            LastShiftMaintenance.ArriveAtPort(LastShiftMaintenance.MaxLatches);
            var readout = LastShiftResourceGauges.Maintenance();

            Assert.That(readout.Available, Is.True);
            Assert.That(readout.Fill,
                Is.EqualTo(LastShiftMaintenance.Balance / (float)LastShiftMaintenance.MaxBalance).Within(0.0001f));
            Assert.That(readout.ValueLabel, Does.Contain(LastShiftMaintenance.MaxBalance.ToString()),
                "숫자는 채움 바깥에 남는다 — 채움 위 흰 글자는 30% 이하에서 안 읽힌다.");
        }

        /// <summary>여력 0 은 고장 등급이다. 아무것도 못 사는 상태가 정상색이면 안 읽힌다.</summary>
        [Test]
        public void EmptyMaintenanceReadsAsFaulted()
        {
            Assert.That(LastShiftResourceGauges.Maintenance().Grade,
                Is.EqualTo(LastShiftSituationGrade.Fault));
        }

        /// <summary>자재는 상한이 없다 — 게이지는 눈대중이고 숫자가 정본이다.</summary>
        [Test]
        public void MaterialsNumberIsAuthoritativeWhileTheGaugeIsOnlyAHint()
        {
            LastShiftMaterials.Deposit(LastShiftResourceGauges.MaterialsDisplaySpan * 3);
            var readout = LastShiftResourceGauges.Materials();

            Assert.That(readout.ValueLabel, Is.EqualTo(LastShiftMaterials.Balance.ToString()),
                "표시 자를 넘어선 잔액도 숫자로는 정확히 보여야 한다.");
            Assert.That(readout.Fill, Is.EqualTo(1f).Within(0.0001f),
                "게이지는 가득 차서 멈춘다 — 넘치는 눈금은 없는 상한을 지어내는 것이다.");
        }

        /// <summary>식량은 계통이 아직 없다. 있는 척하지 않는다.</summary>
        [Test]
        public void FoodStaysUnavailableUntilTheSystemExists()
        {
            Assert.That(LastShiftResourceGauges.Food().Available, Is.False,
                "0 으로 채워 그리면 '정보 없음' 이 '굶고 있다' 로 읽힌다.");
        }

        /// <summary>산소 등급은 시뮬레이션이 이미 매긴 것을 그대로 받는다.</summary>
        [Test]
        public void OxygenCarriesTheSimulationGradeThrough()
        {
            var readout = LastShiftResourceGauges.Oxygen(0.12f, LastShiftSituationGrade.Crisis);

            Assert.That(readout.Available, Is.True);
            Assert.That(readout.Fill, Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(readout.Grade, Is.EqualTo(LastShiftSituationGrade.Crisis));
        }

        /// <summary>도킹은 목표 추력·초에 대한 비율이고 임계선이 없다.</summary>
        [Test]
        public void DockingFillsAgainstTheThrustSecondTarget()
        {
            var target = LastShiftRecoveryTuning.DockTargetThrustSeconds;

            Assert.That(LastShiftResourceGauges.Docking(target * 0.5f, target).Fill,
                Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(LastShiftResourceGauges.Docking(target * 2f, target).Fill,
                Is.EqualTo(1f).Within(0.0001f));
        }

        /// <summary>
        /// 등급 색은 키트가 정한 셋을 그대로 쓴다. 위기만 <b>명도</b> 펄스이고 알파는 안 건드린다 —
        /// 알파 점멸은 밝은 조종석 위에서 글자를 통째로 지운다.
        /// </summary>
        [Test]
        public void CrisisPulsesBrightnessWithoutTouchingAlpha()
        {
            Assert.That(LastShiftResourceGauges.ToneOf(LastShiftSituationGrade.Normal, 0f),
                Is.EqualTo(LastShiftUiTheme.Nominal));
            Assert.That(LastShiftResourceGauges.ToneOf(LastShiftSituationGrade.Unstable, 0f),
                Is.EqualTo(LastShiftUiTheme.Unstable));

            var dim = LastShiftResourceGauges.ToneOf(LastShiftSituationGrade.Crisis, 0f);
            var bright = LastShiftResourceGauges.ToneOf(LastShiftSituationGrade.Crisis, 1f / 3f);

            Assert.That(dim.a, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(bright.a, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(bright.r + bright.g + bright.b,
                Is.GreaterThan(dim.r + dim.g + dim.b),
                "펄스가 명도를 안 움직이면 위기가 다른 등급과 구분되지 않는다.");
        }

        /// <summary>펄스 주기는 1.5Hz 를 안 넘는다(키트 §"UGUI 연결 규격").</summary>
        [Test]
        public void CrisisPulseStaysAtOrBelowOnePointFiveHertz()
        {
            var start = LastShiftUiTheme.PulseCrisis(0f);
            var oneCycleLater = LastShiftUiTheme.PulseCrisis(1f / 0.75f);

            Assert.That(oneCycleLater.r, Is.EqualTo(start.r).Within(0.001f),
                "sin(π·1.5·t) 는 t=1/0.75 에서 한 바퀴를 돈다 — 주기가 그보다 짧으면 상한을 넘는다.");
        }
    }

    /// <summary>
    /// 프롬프트 문장에서 키캡을 떼어내는 규칙. <b>문장을 만드는 열 곳을 안 고치려고</b>
    /// 형식 해석을 한 자리에 몰아넣었고, 그래서 그 한 자리가 정확해야 한다.
    /// </summary>
    public sealed class LastShiftPromptTextTests
    {
        [Test]
        public void LeadingBracketBecomesTheKeycap()
        {
            LastShiftPromptText.Split("[E] 잔해 뜯기 (남은 3)", out var key, out var body);

            Assert.That(key, Is.EqualTo("E"));
            Assert.That(body, Is.EqualTo("잔해 뜯기 (남은 3)"));
        }

        [Test]
        public void SentencesWithoutAKeyKeepTheirWholeText()
        {
            LastShiftPromptText.Split("에어록: 구간 중에는 봉인 (기항에서만 열린다)", out var key, out var body);

            Assert.That(key, Is.Empty);
            Assert.That(body, Is.EqualTo("에어록: 구간 중에는 봉인 (기항에서만 열린다)"));
        }

        /// <summary>문장 중간의 대괄호를 키로 오인하면 본문 앞부분이 통째로 키캡에 들어간다.</summary>
        [Test]
        public void LongBracketsAreNotKeys()
        {
            LastShiftPromptText.Split("[조종석] 압력 위험", out var key, out var body);

            Assert.That(key, Is.Empty);
            Assert.That(body, Is.EqualTo("[조종석] 압력 위험"));
        }

        [Test]
        public void EmptyAndNullPromptsAreHarmless()
        {
            LastShiftPromptText.Split(null, out var nullKey, out var nullBody);
            Assert.That(nullKey, Is.Empty);
            Assert.That(nullBody, Is.Empty);

            LastShiftPromptText.Split("[]", out var emptyKey, out var emptyBody);
            Assert.That(emptyKey, Is.Empty);
            Assert.That(emptyBody, Is.EqualTo("[]"));
        }

        /// <summary>키가 있으면 판이 그만큼 넓어야 글자가 키캡 밑으로 안 깔린다.</summary>
        [Test]
        public void KeycapWidensThePlate()
        {
            Assert.That(LastShiftPromptView.ChromeWidth(true),
                Is.GreaterThan(LastShiftPromptView.ChromeWidth(false)));
        }
    }
}
