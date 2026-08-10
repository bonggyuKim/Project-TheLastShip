using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>게이지 한 줄이 화면에 낼 것 전부. 값·문장·등급이 한 자리에서 같이 정해진다.</summary>
    public readonly struct LastShiftGaugeReadout
    {
        /// <summary>이 축에 실제로 계통이 있는가. 거짓이면 줄 자체를 안 그린다.</summary>
        public readonly bool Available;

        /// <summary>채움 비율 0..1.</summary>
        public readonly float Fill;

        /// <summary>채움 <b>바깥</b>에 적을 숫자. 30% 이하 잔량에서 채움 위 글자는 안 읽힌다.</summary>
        public readonly string ValueLabel;

        public readonly LastShiftSituationGrade Grade;

        public LastShiftGaugeReadout(bool available, float fill, string valueLabel, LastShiftSituationGrade grade)
        {
            Available = available;
            Fill = Mathf.Clamp01(float.IsNaN(fill) ? 0f : fill);
            ValueLabel = valueLabel ?? string.Empty;
            Grade = grade;
        }

        public static readonly LastShiftGaugeReadout Missing = new(false, 0f, string.Empty, LastShiftSituationGrade.Normal);
    }

    /// <summary>
    /// 자원 축 넷을 게이지가 쓸 수 있는 형태로 바꾼다.
    ///
    /// <b>여기가 순수 함수인 것이 요점이다.</b> 값이 옳은지는 EditMode 가 답하고, 화면은
    /// 그 값을 <c>Image.fillAmount</c> 에 꽂기만 한다 — <c>OnGUI</c> 시절에는 환산과
    /// 그리기가 같은 줄에 있어서 잔액이 게이지로 맞게 바뀌는지 검증할 방법이 없었다.
    ///
    /// <b>남은 문제 둘을 여기 적어 둔다.</b>
    /// <list type="bullet">
    /// <item>식량은 아직 계통이 없다. <see cref="Food"/> 가 <c>Available=false</c> 를 돌려주고
    /// 화면은 그 줄을 비운다 — 0 으로 채워 그리면 "굶고 있다" 는 없는 사실이 생긴다.</item>
    /// <item><c>campaign-scale-and-combat-balance-v1.md</c> 조항 <c>B-4</c> 는 산소·식량을
    /// 백분율이 아니라 <b>남은 구간 수</b>로 세라고 한다. 지금 산소 게이지가 재는 것은 구간
    /// 재고가 아니라 <b>지금 이 방의 압력</b>이라 그 조항의 대상이 아니지만, 구간 재고가
    /// 들어오면 그 축은 게이지가 아니라 숫자로 붙어야 한다.</item>
    /// </list>
    /// </summary>
    public static class LastShiftResourceGauges
    {
        /// <summary>
        /// 자재 게이지가 가득 차는 지점. <b>표시용 자일 뿐이다</b> — 자재에는 상한이 없고,
        /// 정확한 값은 언제나 옆의 숫자가 말한다. 잔해밭 하나가 4덩이(<c>ChunksPerField</c>)라
        /// 밭 여섯 개분을 한 화면 길이로 잡았다.
        /// </summary>
        public const int MaterialsDisplaySpan = 24;

        /// <summary>여력이 이 아래로 내려가면 불안정. 개방 하나 값(2)을 못 내는 지점이다.</summary>
        public const int MaintenanceUnstableBelow = 2;

        /// <summary>정비여력. 상한이 실제로 있어서(조항 B-2) 게이지가 재는 값이 진짜 비율이다.</summary>
        public static LastShiftGaugeReadout Maintenance()
        {
            var balance = LastShiftMaintenance.Balance;
            var grade = balance <= 0
                ? LastShiftSituationGrade.Fault
                : balance < MaintenanceUnstableBelow
                    ? LastShiftSituationGrade.Unstable
                    : LastShiftSituationGrade.Normal;
            return new LastShiftGaugeReadout(
                true,
                balance / (float)Mathf.Max(1, LastShiftMaintenance.MaxBalance),
                $"{balance}/{LastShiftMaintenance.MaxBalance}",
                grade);
        }

        /// <summary>자재. 상한이 없어 게이지는 눈대중이고 <b>숫자가 정본이다</b>.</summary>
        public static LastShiftGaugeReadout Materials()
        {
            var balance = LastShiftMaterials.Balance;
            return new LastShiftGaugeReadout(
                true,
                balance / (float)MaterialsDisplaySpan,
                balance.ToString(),
                balance <= 0 ? LastShiftSituationGrade.Unstable : LastShiftSituationGrade.Normal);
        }

        /// <summary>
        /// 산소 — <b>지금 서 있는 구역의 압력</b>이다. 배 전체 평균이 아닌 이유는 격리(문 닫기)의
        /// 효과가 평균에서는 안 보이기 때문이다. 등급은 시뮬레이션이 이미 매긴 것을 받는다.
        /// </summary>
        public static LastShiftGaugeReadout Oxygen(float pressure01, LastShiftSituationGrade grade)
        {
            return new LastShiftGaugeReadout(true, pressure01, $"{Mathf.Clamp01(pressure01):P0}", grade);
        }

        /// <summary>식량. 계통이 아직 없다 — 있는 척하지 않는다.</summary>
        public static LastShiftGaugeReadout Food() => LastShiftGaugeReadout.Missing;

        /// <summary>
        /// 도킹 진척. <b>임계선이 없다</b> — 목표는 가득 차는 것 자체이고, "지금 충분한가" 는
        /// 추력 게이지의 이동선이 답한다(G-2).
        /// </summary>
        public static LastShiftGaugeReadout Docking(float thrustSeconds, float targetThrustSeconds)
        {
            var target = Mathf.Max(0.0001f, targetThrustSeconds);
            var fill = thrustSeconds / target;
            return new LastShiftGaugeReadout(true, fill, $"{Mathf.Clamp01(fill):P0}", LastShiftSituationGrade.Normal);
        }

        /// <summary>등급 → 채움 색. 위기만 명도 펄스가 붙는다.</summary>
        public static Color ToneOf(LastShiftSituationGrade grade, float unscaledTime) => grade switch
        {
            LastShiftSituationGrade.Unstable => LastShiftUiTheme.Unstable,
            LastShiftSituationGrade.Fault => LastShiftUiTheme.Fault,
            LastShiftSituationGrade.Crisis => LastShiftUiTheme.PulseCrisis(unscaledTime),
            _ => LastShiftUiTheme.Nominal
        };
    }
}
