using DoodleUp.Runtime;
using NUnit.Framework;

namespace DoodleUp.Tests.EditMode
{
    /// <summary>
    /// <c>G-1</c> 결과 화면 카피와 <c>G-2</c> 필요 추력선
    /// (<c>docs/game-feel-loop-review-v1.md</c> §3.1·§3.2).
    ///
    /// <b>겨누는 것은 "판정마다 다른 문장이 나오는가" 지 문장 자체가 아니다.</b> 카피 문구는
    /// 기획·아트 소관이라 바뀔 수 있고, 리터럴을 전수 비교하면 문구 수정이 곧 테스트 실패가
    /// 된다. 대신 <b>원인 줄에 그 판정을 설명하는 값이 실제로 실려 있는가</b>를 본다 — 이
    /// 카드가 고친 것이 정확히 그 결핍이기 때문이다(지금까지는 로그를 열어야 <c>dock=138.0</c>
    /// 이 보였다).
    /// </summary>
    public sealed class LastShiftResultScreenTests
    {
        private static LastShiftRunSummary Summary(
            LastShiftVerdict verdict,
            float dockProgress = 150f,
            float elapsedSeconds = 300f,
            float thrustAtSettle = 0.5f,
            float heatProtectionSeconds = 0f,
            int sacrificeCount = 0,
            int quickBypassCount = 0,
            int bypassLapseCount = 0,
            LastShiftZone asphyxiationZone = LastShiftZone.LifeSupport)
        {
            return new LastShiftRunSummary(verdict, dockProgress, elapsedSeconds, thrustAtSettle,
                heatProtectionSeconds, sacrificeCount, quickBypassCount, bypassLapseCount, asphyxiationZone);
        }

        // ── G-1(a) 판정 5종 ───────────────────────────────────────────────

        /// <summary>
        /// 판정 <c>5</c>종이 전부 큰 줄을 갖는다. 하나라도 비면 그 판은 결과 화면이 떠도
        /// 무슨 일이 있었는지 안 적힌 채로 뜬다.
        /// </summary>
        [TestCase(LastShiftVerdict.SuccessNominalDocking)]
        [TestCase(LastShiftVerdict.SuccessCompromised)]
        [TestCase(LastShiftVerdict.FailureAsphyxiation)]
        [TestCase(LastShiftVerdict.FailureAdrift)]
        [TestCase(LastShiftVerdict.FailureInsufficientThrust)]
        public void EveryVerdictHasHeadlineAndChip(LastShiftVerdict verdict)
        {
            Assert.IsNotEmpty(LastShiftResultCopy.HeadlineOf(verdict), $"{verdict} 큰 줄이 비었다");
            Assert.IsNotEmpty(LastShiftResultCopy.ChipOf(verdict), $"{verdict} 칩이 비었다");
        }

        /// <summary>
        /// 큰 줄이 판정마다 다르다. 같은 문장이 둘이면 플레이어는 두 판을 구분하지 못한다.
        /// </summary>
        [Test]
        public void HeadlinesAreDistinctAcrossVerdicts()
        {
            var verdicts = new[]
            {
                LastShiftVerdict.SuccessNominalDocking,
                LastShiftVerdict.SuccessCompromised,
                LastShiftVerdict.FailureAsphyxiation,
                LastShiftVerdict.FailureAdrift,
                LastShiftVerdict.FailureInsufficientThrust
            };
            CollectionAssert.AllItemsAreUnique(
                System.Array.ConvertAll(verdicts, LastShiftResultCopy.HeadlineOf));
        }

        /// <summary>
        /// <b>정상 도킹만 원인 줄이 빈다.</b> 성공에는 설명할 실패가 없고 그 여백 자체가
        /// 정보다(아트 §2). 나머지 넷은 반드시 원인을 적는다.
        /// </summary>
        [Test]
        public void OnlyNominalDockingHasEmptyCauseLine()
        {
            Assert.IsEmpty(LastShiftResultCopy.CauseOf(Summary(LastShiftVerdict.SuccessNominalDocking)));
            Assert.IsNotEmpty(LastShiftResultCopy.CauseOf(Summary(LastShiftVerdict.SuccessCompromised, sacrificeCount: 2)));
            Assert.IsNotEmpty(LastShiftResultCopy.CauseOf(Summary(LastShiftVerdict.FailureAsphyxiation)));
            Assert.IsNotEmpty(LastShiftResultCopy.CauseOf(Summary(LastShiftVerdict.FailureAdrift, 138f)));
            Assert.IsNotEmpty(LastShiftResultCopy.CauseOf(Summary(LastShiftVerdict.FailureInsufficientThrust)));
        }

        /// <summary>
        /// 표류 원인 줄이 진척과 평균 추력을 <b>둘 다</b> 싣는다. §3.1-a 의 예시가 그대로
        /// 재현되는지를 본다 — <c>138/150</c> 에 <c>5</c>분 평균 <c>0.46</c>.
        /// </summary>
        [Test]
        public void AdriftCauseCarriesProgressAndAverageThrust()
        {
            var summary = Summary(LastShiftVerdict.FailureAdrift, 138f);
            var cause = LastShiftResultCopy.CauseOf(summary);

            Assert.AreEqual(0.46f, summary.AverageThrust, 0.005f, "평균 추력은 진척/경과다");
            StringAssert.Contains("138", cause);
            StringAssert.Contains("150", cause);
            StringAssert.Contains("5분", cause);
            StringAssert.Contains("0.46", cause);
        }

        /// <summary>
        /// 질식 원인 줄이 <b>어느 방인지</b>를 말한다. 방 이름이 없으면 "질식" 한 줄만 남아
        /// 다음 판에 무엇을 다르게 할지가 안 나온다.
        /// </summary>
        [Test]
        public void AsphyxiationCauseNamesTheZone()
        {
            var cause = LastShiftResultCopy.CauseOf(
                Summary(LastShiftVerdict.FailureAsphyxiation, asphyxiationZone: LastShiftZone.Cooling));
            StringAssert.Contains(LastShiftZoneAtlas.ShortLabelOf(LastShiftZone.Cooling), cause);
        }

        /// <summary>
        /// 열 잠금이 한 번도 안 걸린 판에는 괄호를 안 붙인다. 없던 사건을 원인으로 읽게
        /// 만드는 <c>(엔진 보호 잠금 0초)</c> 를 막는다.
        /// </summary>
        [Test]
        public void HeatLockParentheticalAppearsOnlyWhenLockHappened()
        {
            var locked = LastShiftResultCopy.CauseOf(
                Summary(LastShiftVerdict.FailureInsufficientThrust, thrustAtSettle: 0.25f, heatProtectionSeconds: 42f));
            var never = LastShiftResultCopy.CauseOf(
                Summary(LastShiftVerdict.FailureInsufficientThrust, thrustAtSettle: 0.25f));

            StringAssert.Contains("42", locked);
            StringAssert.DoesNotContain("잠금", never);
            StringAssert.Contains("0.25", never);
        }

        // ── G-1(b) 요약 4칸 ───────────────────────────────────────────────

        /// <summary>
        /// 요약 <c>4</c>칸이 장부 값을 그대로 싣는다. 마지막 칸만 판정색이고, <c>0</c> 인
        /// 칸은 색을 낮춘다(아트 §3).
        /// </summary>
        [Test]
        public void SummaryCellsCarryLedgerCounts()
        {
            var cells = LastShiftResultCopy.CellsOf(Summary(
                LastShiftVerdict.SuccessNominalDocking,
                sacrificeCount: 0, quickBypassCount: 3, bypassLapseCount: 2));

            Assert.AreEqual(4, cells.Length);
            StringAssert.Contains("0", cells[0].Value);
            StringAssert.Contains("3", cells[1].Value);
            StringAssert.Contains("2", cells[2].Value);
            StringAssert.Contains("150/150", cells[3].Value);

            Assert.IsTrue(cells[0].Muted, "0 인 칸은 색을 낮춘다");
            Assert.IsFalse(cells[1].Muted);
            Assert.IsTrue(cells[3].UsesVerdictColor, "도킹 진척만 판정색이다");
            Assert.IsFalse(cells[0].UsesVerdictColor);
        }

        /// <summary>실패 <c>3</c>종은 한 색이다. 색으로 쪼개면 매 판 새 색을 배우게 된다(아트 §1).</summary>
        [Test]
        public void FailureVerdictsShareOneColor()
        {
            Assert.AreEqual(LastShiftResultCopy.FailureColor, LastShiftResultCopy.ColorOf(LastShiftVerdict.FailureAdrift));
            Assert.AreEqual(LastShiftResultCopy.FailureColor, LastShiftResultCopy.ColorOf(LastShiftVerdict.FailureAsphyxiation));
            Assert.AreEqual(LastShiftResultCopy.FailureColor, LastShiftResultCopy.ColorOf(LastShiftVerdict.FailureInsufficientThrust));
            Assert.AreNotEqual(LastShiftResultCopy.FailureColor, LastShiftResultCopy.ColorOf(LastShiftVerdict.SuccessNominalDocking));
            Assert.AreNotEqual(
                LastShiftResultCopy.ColorOf(LastShiftVerdict.SuccessNominalDocking),
                LastShiftResultCopy.ColorOf(LastShiftVerdict.SuccessCompromised));
        }

        // ── G-1(c) 다음 판 ────────────────────────────────────────────────

        /// <summary>
        /// 다음 판 줄이 프리셋 이름을 싣고, 이름 셋이 서로 다르다
        /// (<c>docs/last-shift-preset-names-v1.md</c> §4).
        /// </summary>
        [Test]
        public void NextRunLineNamesThePreset()
        {
            var names = new[]
            {
                LastShiftSituationText.PresetDisplayName(LastShiftPreset.HighHeatHighThrust),
                LastShiftSituationText.PresetDisplayName(LastShiftPreset.PowerOverloadLooseBattery),
                LastShiftSituationText.PresetDisplayName(LastShiftPreset.BadAttitudeHighOxygen)
            };
            CollectionAssert.AllItemsAreUnique(names);
            foreach (var name in names) Assert.IsNotEmpty(name);

            StringAssert.Contains(names[1],
                LastShiftResultCopy.NextRunLineOf(LastShiftPreset.PowerOverloadLooseBattery));
        }

        // ── G-2(b) 필요 추력선 ────────────────────────────────────────────

        /// <summary>
        /// 런 시작 시 필요 추력은 <c>0.50</c> 이고, <b>HUD 가 가르치는 <c>0.30</c> 과 다르다.</b>
        /// 이 차이가 이 카드가 푸는 문제 자체다 — 프리셋2 는 초기 추력 <c>0.46</c> 이라
        /// 막대가 파란색인데 5분 뒤 표류한다.
        /// </summary>
        [Test]
        public void RequiredThrustStartsAboveTheDockingThreshold()
        {
            var state = LastShiftPresetFactory.Create(LastShiftPreset.PowerOverloadLooseBattery);
            var required = LastShiftVerdictResolver.RequiredThrust(
                state, LastShiftRecoveryTuning.DockingTimerSeconds);

            Assert.AreEqual(0.50f, required, 0.001f);
            Assert.Greater(required, LastShiftRecoveryTuning.DockingSuccessThrust,
                "필요선이 임계선보다 낮으면 임계선만 보고도 이길 수 있어 이 선이 필요 없다");
            Assert.Greater(required, state.ThrustDemand,
                "프리셋2 초기 추력은 필요선 아래다 — 그대로 두면 진척이 모자란다");
        }

        /// <summary>추력을 필요선보다 높게 내면 선이 내려가고, 낮게 내면 올라간다(§3.2-b).</summary>
        [Test]
        public void RequiredThrustFallsWhenAheadAndRisesWhenBehind()
        {
            var ahead = LastShiftPresetFactory.Create(LastShiftPreset.HighHeatHighThrust);
            ahead.DockProgress = 48f;   // 60초를 0.80 으로 밀었다
            var behind = LastShiftPresetFactory.Create(LastShiftPreset.HighHeatHighThrust);
            behind.DockProgress = 0f;   // 60초를 수리로 썼다

            Assert.Less(LastShiftVerdictResolver.RequiredThrust(ahead, 240f), 0.50f);
            Assert.Greater(LastShiftVerdictResolver.RequiredThrust(behind, 240f), 0.50f);
        }

        /// <summary>진척이 목표에 닿으면 더 낼 것이 없으므로 필요선은 <c>0</c> 이다.</summary>
        [Test]
        public void RequiredThrustIsZeroOnceTargetIsReached()
        {
            var state = LastShiftPresetFactory.Create(LastShiftPreset.HighHeatHighThrust);
            state.DockProgress = LastShiftRecoveryTuning.DockTargetThrustSeconds;
            Assert.AreEqual(0f, LastShiftVerdictResolver.RequiredThrust(state, 10f));
            Assert.IsFalse(LastShiftVerdictResolver.IsDockUnreachable(state, 10f),
                "이미 채운 판은 남은 시간이 아무리 짧아도 도달 불가가 아니다");
        }

        // ── G-2(c) 도달 불가 즉시 판정 ────────────────────────────────────

        /// <summary>
        /// 필요선이 추력 상한을 넘으면 최대로 밀어도 못 채우므로 도달 불가다. 지금까지는
        /// 그 상태로 타이머 끝까지 시계만 보게 뒀고, 그건 <c>RG-3</c> 이 금지한 영구 잠금과 같다.
        /// </summary>
        [Test]
        public void DockBecomesUnreachableWhenRequiredThrustExceedsTheCeiling()
        {
            var state = LastShiftPresetFactory.Create(LastShiftPreset.HighHeatHighThrust);
            state.DockProgress = 40f;   // 110 thrust·s 남았는데

            Assert.IsTrue(LastShiftVerdictResolver.IsDockUnreachable(state, 100f),
                "100초에 110 thrust·s 는 상한 1.0 으로도 못 채운다");
            Assert.IsFalse(LastShiftVerdictResolver.IsDockUnreachable(state, 120f),
                "120초면 0.92 로 아직 성립한다 — 여기서 끊으면 이길 수 있는 판을 끝낸다");
        }

        /// <summary>
        /// 남은 시간 <c>0</c> 은 도달 불가로 판정하지 않는다. 그 시점은 타이머 만료의
        /// 자리이고, 여기서 가로채면 <c>FailureInsufficientThrust</c> 가 영영 안 나온다.
        /// </summary>
        [Test]
        public void TimerExpiryIsNotTreatedAsUnreachable()
        {
            var state = LastShiftPresetFactory.Create(LastShiftPreset.HighHeatHighThrust);
            state.DockProgress = 40f;
            Assert.IsFalse(LastShiftVerdictResolver.IsDockUnreachable(state, 0f));
            Assert.AreEqual(LastShiftVerdict.FailureInsufficientThrust,
                LastShiftVerdictResolver.EvaluateTimeout(new LastShiftShipState { ThrustDemand = 0.25f }));
        }

        // ── 장부: 임시 수리 횟수 ──────────────────────────────────────────

        /// <summary>
        /// <c>임시 수리</c> 칸의 분모가 실제로 세어진다. 재이탈만 세면 "3회 중 2회 풀렸다"
        /// 라는 읽기가 성립하지 않는다.
        /// </summary>
        [Test]
        public void LedgerCountsCompletedQuickBypasses()
        {
            var ledger = new LastShiftRepairLedger();
            ledger.BeginChannel(LastShiftShipSystem.Cooling, LastShiftRepairMode.QuickBypass);
            ledger.TryAdvanceChannel(LastShiftShipSystem.Cooling, LastShiftRecoveryTuning.QuickBypassSeconds, out _);
            ledger.BeginChannel(LastShiftShipSystem.Power, LastShiftRepairMode.SafeRestore);
            ledger.TryAdvanceChannel(LastShiftShipSystem.Power, LastShiftRecoveryTuning.SafeRestoreSeconds, out _);

            Assert.AreEqual(1, ledger.QuickBypassCount, "안전 복구는 임시 수리로 세지 않는다");

            ledger.Reset();
            Assert.AreEqual(0, ledger.QuickBypassCount, "리셋은 새 항해다");
        }
    }
}
