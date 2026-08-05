using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 개구부 너머 상태의 3단계 등급. 기획 §3.1.2 의 "정상 / 이상 / 위험" 그대로다.
    ///
    /// 등급이 셋인 것이 이 채널의 전부다. 네 단계로 늘리면 통로에서 수치를 읽는 것과 같아져
    /// "앞까지 가서 게이지를 본다" 는 왕복(§3.1.2 의 3초)이 사라진다.
    /// </summary>
    public enum LastShiftDistressGrade
    {
        Nominal = 0,
        Abnormal = 1,
        Critical = 2
    }

    /// <summary>
    /// 한 공간의 이상도 판독 결과. 등급이 표현용 정본이고 <see cref="Scalar"/> 는 등급이 어디서
    /// 나왔는지 확인·검증하기 위한 연속값이다. 판정에 스칼라를 직접 쓰면 임계 근처에서 등급과
    /// 어긋나므로, 게임 로직과 표현은 언제나 <see cref="Grade"/> 를 본다.
    /// </summary>
    public readonly struct LastShiftDistressReading
    {
        /// <summary>판독 대상 공간이 속한 구역.</summary>
        public readonly LastShiftZone Zone;

        public readonly float Scalar;
        public readonly LastShiftDistressGrade Grade;

        public LastShiftDistressReading(LastShiftZone zone, float scalar)
        {
            Zone = zone;
            Scalar = Mathf.Clamp01(scalar);
            Grade = LastShiftDoorDistress.Quantize(Scalar);
        }

        public override string ToString() => $"{LastShiftZoneAtlas.ShortLabelOf(Zone)} {Grade} ({Scalar:F2})";
    }

    /// <summary>
    /// 개구부 너머가 얼마나 나쁜지를 하나의 정규화 스칼라로 접는다(CT-10 T2).
    ///
    /// <b>계통 정체성은 여기서 버린다.</b> 열인지 전력인지 파공인지는 그 구역에 들어가야
    /// 읽히는 것이고(CT-01 §3.3 국소 정보 규칙), 통로에서 읽히는 것은 "어느 쪽이 더 급한가"
    /// 하나뿐이다(기획 §3.1). 그래서 여러 상태 중 <b>가장 나쁜 하나</b>만 남기고 합산하지
    /// 않는다 — 합산하면 가벼운 이상 셋이 심각한 이상 하나를 앞질러 비교가 뒤집힌다.
    ///
    /// <b>판독 거리는 여기서 정하지 않는다.</b> 얼마나 떨어져서 읽히는가는 표현 요건이고
    /// (글자 크기·발광 세기·게이지 치수) 그건 아트 소관이다. 지금 그 거리 자체가 기획 판정을
    /// 기다리는 중이므로 코드에 상수로 박으면 안 된다. 이 파일이 아는 것은 값과 등급뿐이다.
    ///
    /// <b>문 상태와 무관하다.</b> 스칼라 어디에도 <see cref="LastShiftZoneDoor.IsOpen"/> 이
    /// 들어가지 않는다. 열어야 수치가 보이면 "확인 = 압력 혼합" 이 되어 판단 자체가 사라진다
    /// (기획 §3.1.2 제약).
    /// </summary>
    public static class LastShiftDoorDistress
    {
        /// <summary>등급 수. 경계는 이 수에서 나오므로 등급을 늘리면 경계도 함께 따라온다.</summary>
        public const int GradeCount = 3;

        /// <summary>이 값 이상이 "이상". 0..1 을 등급 수로 균등 분할한 첫 경계다.</summary>
        public const float AbnormalScalar = 1f / GradeCount;

        /// <summary>이 값 이상이 "위험".</summary>
        public const float CriticalScalar = 2f / GradeCount;

        public static LastShiftDistressGrade Quantize(float scalar)
        {
            if (scalar >= CriticalScalar) return LastShiftDistressGrade.Critical;
            if (scalar >= AbnormalScalar) return LastShiftDistressGrade.Abnormal;
            return LastShiftDistressGrade.Nominal;
        }

        /// <summary>
        /// 구역 압력의 이상도. 두 구간으로 나눠 <b>사이렌 발동선이 정확히 위험 경계</b>가 되게 한다.
        ///
        /// <code>
        /// 압력 1.00 → 0.00        (정상)
        /// 압력 0.15 → 0.67        (위험 경계 = LastShiftRecoveryTuning.OxygenSirenTrigger)
        /// 압력 0.00 → 1.00        (진공)
        /// </code>
        ///
        /// 선을 맞추는 이유는 채널이 둘이기 때문이다. 사이렌(N9)은 전 구역에서 들리고 이
        /// 게이지는 개구부마다 다르게 읽히는데, 둘이 다른 선을 쓰면 "사이렌은 우는데 게이지는
        /// 이상" 같은 상태가 생겨 어느 쪽을 믿어야 하는지가 사라진다.
        ///
        /// 기획 §3.1.2 의 대화가 그대로 나온다 — 산소실 0.11 은 위험(0.76), 조종석 0.29 는
        /// 이상(0.56)이다. 둘 다 정상이 아니고, 그중 어느 쪽이 급한지는 등급으로 갈린다.
        /// </summary>
        public static float PressureDistress(float pressure)
        {
            var trigger = LastShiftRecoveryTuning.OxygenSirenTrigger;
            var clamped = Mathf.Clamp01(pressure);
            if (clamped <= trigger)
                return Mathf.Lerp(1f, CriticalScalar, trigger <= 0f ? 1f : clamped / trigger);
            return Mathf.Lerp(CriticalScalar, 0f, (clamped - trigger) / (1f - trigger));
        }

        /// <summary>
        /// 미억제 손상 계통 하나의 이상도. <b>손상이 있으면 최소 "이상"</b>이고, 그 계통의 시계가
        /// 진행할수록 위험으로 올라간다.
        ///
        /// 하한을 두는 것이 요점이다. 계통이 막 터진 직후에는 어떤 수치도 아직 안 움직였는데,
        /// 그때 정상으로 읽히면 통로에서 "저쪽은 괜찮다" 는 잘못된 비교가 성립한다. 무엇이
        /// 잘못됐는지는 여전히 안 보이지만, 잘못됐다는 것 자체는 보여야 한다.
        /// </summary>
        public static float SystemDistress(float clockProgress) =>
            Mathf.Lerp(AbnormalScalar, 1f, Mathf.Clamp01(clockProgress));

        /// <summary>
        /// 계통 시계의 진행도. 각 계통이 자기 한계까지 얼마나 갔는가를 0..1 로 편다.
        ///
        /// 세 시계를 하나로 합치지 말라는 것은 CT-01 §2.3 이고 여기서도 합치지 않는다 —
        /// 셋을 각각 편 뒤 <b>최댓값 하나만</b> 고른다. 고른 뒤 어느 계통이었는지는 버린다.
        /// 통로에서 계통이 읽히면 그 구역에 들어갈 이유가 없어진다.
        /// </summary>
        public static float ClockProgress(in LastShiftShipState state, LastShiftShipSystem system)
        {
            switch (system)
            {
                case LastShiftShipSystem.Cooling:
                    // 엔진 보호 발동선까지의 거리. 발동하면 추력 상한이 성공선 아래로 떨어진다.
                    return LastShiftRecoveryTuning.HeatProtectionTrigger <= 0f
                        ? 1f
                        : state.EngineHeat / LastShiftRecoveryTuning.HeatProtectionTrigger;
                case LastShiftShipSystem.Power:
                    // bus 미연결 하한(0.40)까지의 거리. 그 아래로는 안 내려가므로 하한이 곧 만점이다.
                    return (1f - state.BusPower) / (1f - LastShiftRecoveryTuning.UnpoweredBusCeiling);
                default:
                    // 파공 크기. 누출률 공식이 쓰는 기준 손상(0.5)과 같은 척도라, 게이지가 올라가는
                    // 속도와 압력이 빠지는 속도가 같은 값에서 나온다.
                    return (1f - state.HullIntegrity) / LastShiftRecoveryTuning.OxygenLeakHullReference;
            }
        }

        /// <summary>
        /// 한 공간의 판독값. 여러 상태 중 최댓값 하나만 남는다.
        /// </summary>
        /// <param name="vacuum">진공이면 무조건 최대다. 성능 포기로 밀폐한 구역은 압력과 무관하게 진공이다.</param>
        /// <param name="worstSystemProgress">그 구역의 미억제 손상 계통 중 가장 진행한 시계. 손상이 없으면 음수.</param>
        public static LastShiftDistressReading Evaluate(
            LastShiftZone zone, float pressure, bool vacuum, float worstSystemProgress)
        {
            if (vacuum) return new LastShiftDistressReading(zone, 1f);
            var scalar = PressureDistress(pressure);
            if (worstSystemProgress >= 0f) scalar = Mathf.Max(scalar, SystemDistress(worstSystemProgress));
            return new LastShiftDistressReading(zone, scalar);
        }
    }
}
