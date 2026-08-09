using System;
using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>계통 하나(또는 산소의 구역 하나)의 대표 상황과 그 등급.</summary>
    public readonly struct LastShiftSystemStatus
    {
        public readonly LastShiftSituation Situation;
        public readonly LastShiftSituationGrade Grade;

        public LastShiftSystemStatus(LastShiftSituation situation)
        {
            Situation = situation;
            Grade = LastShiftSituationTable.GradeOf(situation);
        }

        public static LastShiftSystemStatus Normal => new(LastShiftSituation.None);
        public bool IsActive => Situation != LastShiftSituation.None;
    }

    /// <summary>
    /// 상황 전이 1회. 연출은 <b>전이 시점에만</b> 재생한다(기획 §3.5) — 상황을 이벤트가 아니라
    /// 상태로 정의했으므로 "조건이 계속 참" 은 전이가 아니다.
    /// </summary>
    public readonly struct LastShiftSituationTransition
    {
        public readonly LastShiftSystemChannel Channel;
        /// <summary>산소 계통만 의미가 있다. 나머지 계통은 배 전체라 조종석으로 채운다.</summary>
        public readonly LastShiftZone Zone;
        public readonly LastShiftSituation From;
        public readonly LastShiftSituation To;

        public LastShiftSituationTransition(
            LastShiftSystemChannel channel, LastShiftZone zone,
            LastShiftSituation from, LastShiftSituation to)
        {
            Channel = channel;
            Zone = zone;
            From = from;
            To = to;
        }

        /// <summary>
        /// 같은 계통 안에서 등급이 오른 전이인가(§3.5). 이 경우 하위 상황 해제 연출을
        /// 재생하지 않고 상승 연출만 재생한다.
        /// </summary>
        public bool IsEscalation =>
            From != LastShiftSituation.None &&
            To != LastShiftSituation.None &&
            LastShiftSituationTable.GradeOf(To) > LastShiftSituationTable.GradeOf(From);

        public override string ToString() => $"{Channel}/{Zone}: {From} → {To}";
    }

    /// <summary>
    /// N6 — 상황 12개 조건 평가층(기획 §3.3, §3.5).
    ///
    /// 세 가지를 한다.
    /// <list type="number">
    /// <item>상황별 <b>래치</b>: 발동선에서 켜지고 해제선에서 꺼진다. 두 임계가 달라 임계
    /// 근처에서 깜빡이지 않는다. 켜진 뒤 <c>1.5s</c> 동안은 조건이 반전돼도 꺼지지 않는다.</item>
    /// <item>계통별 <b>대표</b> 선출: 켜진 것 중 최고 등급 하나. 동급이면 테이블 순서상 뒤쪽이
    /// 이긴다(§3.2 — <c>S-O3</c>·<c>S-O4</c> 가 유일한 사례이고 <c>S-O4</c> 가 이긴다).</item>
    /// <item>대표가 바뀐 순간을 <b>전이</b>로 보고 1회 보고. 연출은 여기에만 건다.</item>
    /// </list>
    ///
    /// <b>래치와 대표를 나눈 것이 이 설계의 요점이다.</b> 대표만 들고 있으면 히스테리시스가
    /// 대표에만 걸려, 등급이 올랐다 내려올 때 하위 상황이 자기 해제선을 무시하고 되살아난다.
    /// 반대로 래치만 들고 있으면 §3.2 의 "계통 내 최대 1개" 가 깨진다. 둘 다 필요하다.
    ///
    /// 산소 계통은 구역마다 독립 래치를 갖고, <c>S-O3</c> 만 전역 래치 하나를 공유한다.
    /// 전역인 <c>S-O3</c> 가 각 구역의 대표 선출에 함께 들어가는 것은 의도된 것이다 — 사이렌이
    /// 울리는 동안은 모든 구역이 최소 위기 등급으로 표시돼야 배 전체 경보라는 성질이 산다.
    /// </summary>
    public sealed class LastShiftSituationTracker
    {
        /// <summary>구역별로 평가되는 산소 상황 셋 + 전역 <c>S-O3</c>.</summary>
        private static readonly LastShiftSituation[] PerZoneOxygen =
        {
            LastShiftSituation.HullLeak,
            LastShiftSituation.ZoneLowPressure,
            LastShiftSituation.ZoneVacuum
        };

        private static readonly LastShiftSituation[] HeatSituations =
        {
            LastShiftSituation.HeatCouplingLoose,
            LastShiftSituation.HeatRunaway,
            LastShiftSituation.HeatProtectionLock
        };

        private static readonly LastShiftSituation[] PowerSituations =
        {
            LastShiftSituation.BusDetached,
            LastShiftSituation.PowerCascade,
            LastShiftSituation.PowerBlackout
        };

        private static readonly LastShiftSituation[] PropulsionSituations =
        {
            LastShiftSituation.AttitudeDrift,
            LastShiftSituation.FuelMarginLost
        };

        private struct Latch
        {
            public bool Active;
            public float ActiveSeconds;
        }

        // 전역 래치는 상황 번호로, 구역별 래치는 [구역][상황 번호] 로 민다. 상황 번호를 그대로
        // 인덱스로 쓰면 12개 중 몇 칸이 비지만, 번호와 인덱스가 어긋나 생기는 버그보다 싸다.
        private readonly Latch[] globalLatches = new Latch[LastShiftSituationTable.SituationCount + 1];
        private readonly Latch[][] zoneLatches;

        private readonly LastShiftSituation[] channelRepresentative =
            new LastShiftSituation[4];
        private readonly LastShiftSituation[] oxygenRepresentative =
            new LastShiftSituation[LastShiftZoneAtlas.ZoneCount];

        public LastShiftSituationTracker()
        {
            zoneLatches = new Latch[LastShiftZoneAtlas.ZoneCount][];
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                zoneLatches[zone] = new Latch[LastShiftSituationTable.SituationCount + 1];
            Reset();
        }

        public void Reset()
        {
            Array.Clear(globalLatches, 0, globalLatches.Length);
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                Array.Clear(zoneLatches[zone], 0, zoneLatches[zone].Length);
            for (var channel = 0; channel < channelRepresentative.Length; channel++)
                channelRepresentative[channel] = LastShiftSituation.None;
            for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                oxygenRepresentative[zone] = LastShiftSituation.None;
        }

        /// <summary>
        /// 래치 체류 시간을 담는 평평한 배열의 칸 수. 전역 한 줄 + 구역별 한 줄씩이며,
        /// 줄마다 상황 번호를 그대로 인덱스로 쓴다(0번 칸은 <c>None</c> 자리라 비어 있다).
        /// </summary>
        public const int LatchSlotStride = LastShiftSituationTable.SituationCount + 1;
        public static readonly int LatchSlotCount = LatchSlotStride * (1 + LastShiftZoneAtlas.ZoneCount);

        /// <summary>
        /// 래치 위상을 값으로 접는다(<c>docs/tech/save-backbone-feasibility-v1.md</c> §1.3-나).
        ///
        /// <b>이 값을 스냅샷 구조체에 넣지 않는 것이 결정이다.</b> 클라이언트는 이것 없이도
        /// <see cref="Evaluate"/> 를 0초로 한 번 돌려 같은 표시를 얻고(순수 계산이다), 반대로
        /// 세이브는 히스테리시스 위상까지 이어야 한다 — 그래서 소비자가 한쪽뿐인 상태를
        /// 0.25초마다 전원에게 보내는 대신, 파일 층이 따로 가져가는 별도 경로로 둔다.
        ///
        /// 부호가 활성 여부를 나른다: <b>음수면 비활성</b>, 0 이상이면 그만큼 체류 중이다.
        /// 불리언 배열을 따로 두지 않는 이유는 체류 시간이 활성일 때만 정의되기 때문이다.
        /// </summary>
        public float[] CaptureLatchDwell()
        {
            var dwell = new float[LatchSlotCount];
            for (var id = 0; id < LatchSlotStride; id++)
            {
                dwell[id] = Encode(globalLatches[id]);
                for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                    dwell[(zone + 1) * LatchSlotStride + id] = Encode(zoneLatches[zone][id]);
            }
            return dwell;
        }

        /// <summary>
        /// <see cref="CaptureLatchDwell"/> 가 접은 위상을 되살린다. 길이가 맞지 않으면
        /// <b>아무것도 하지 않고 거짓을 돌려준다</b> — 스키마가 어긋난 파일이 래치를 절반만
        /// 덮어써서 히스테리시스가 반쪽만 살아 있는 상태를 만드는 것이 조용히 더 나쁘다.
        /// </summary>
        public bool ApplyLatchDwell(float[] dwell)
        {
            if (dwell == null || dwell.Length != LatchSlotCount) return false;
            for (var id = 0; id < LatchSlotStride; id++)
            {
                Decode(ref globalLatches[id], dwell[id]);
                for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                    Decode(ref zoneLatches[zone][id], dwell[(zone + 1) * LatchSlotStride + id]);
            }
            return true;
        }

        private static float Encode(in Latch latch) => latch.Active ? Mathf.Max(0f, latch.ActiveSeconds) : -1f;

        private static void Decode(ref Latch latch, float encoded)
        {
            latch.Active = encoded >= 0f;
            latch.ActiveSeconds = latch.Active ? encoded : 0f;
        }

        /// <summary>열·전력·추진 계통의 현재 대표 상황. 산소는 구역별이라 여기서 못 읽는다.</summary>
        public LastShiftSystemStatus StatusOf(LastShiftSystemChannel channel) =>
            new(channelRepresentative[(int)channel]);

        /// <summary>산소 계통의 구역별 대표 상황. HUD 구역 3칸이 이 값을 읽는다.</summary>
        public LastShiftSystemStatus OxygenStatusOf(LastShiftZone zone) =>
            new(oxygenRepresentative[(int)zone]);

        /// <summary>
        /// <c>S-O3</c> 전선 사이렌이 울리고 있는가. 계통 등급과 별개로 물어야 한다 — 어느 구역이
        /// <c>S-O4</c> 로 올라가도 사이렌은 꺼지지 않고, 격리로 문제를 가둬도 계속 울린다(§3.3).
        /// </summary>
        public bool IsDecompressionAlarmActive =>
            globalLatches[(int)LastShiftSituation.DecompressionAlarm].Active;

        /// <summary>지금 활성인 계통 수. §3.2 의 "계통 간 최대 4개 동시" 를 관측하는 값이다.</summary>
        public int ActiveChannelCount
        {
            get
            {
                var count = 0;
                for (var channel = 0; channel < channelRepresentative.Length; channel++)
                {
                    if (channel == (int)LastShiftSystemChannel.Oxygen) continue;
                    if (channelRepresentative[channel] != LastShiftSituation.None) count++;
                }
                for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                {
                    if (oxygenRepresentative[zone] == LastShiftSituation.None) continue;
                    count++;
                    break;   // 산소는 구역이 여럿이어도 계통 하나로 센다
                }
                return count;
            }
        }

        /// <summary>
        /// 한 tick. 반환값은 이번 tick 에 일어난 대표 전이 목록이며, 연출은 이것만 보고 재생한다.
        /// <paramref name="transitions"/> 를 넘기면 그 리스트에 담고, 안 넘기면 전이를 세기만 한다.
        /// </summary>
        public int Evaluate(
            in LastShiftSituationInput input,
            float deltaTime,
            System.Collections.Generic.List<LastShiftSituationTransition> transitions = null)
        {
            UpdateLatches(input, deltaTime);

            var count = 0;
            count += ResolveChannel(LastShiftSystemChannel.Heat, HeatSituations, transitions);
            count += ResolveChannel(LastShiftSystemChannel.Power, PowerSituations, transitions);
            count += ResolveChannel(LastShiftSystemChannel.Propulsion, PropulsionSituations, transitions);
            count += ResolveOxygen(transitions);
            return count;
        }

        private void UpdateLatches(in LastShiftSituationInput input, float deltaTime)
        {
            for (var id = 1; id <= LastShiftSituationTable.SituationCount; id++)
            {
                var situation = (LastShiftSituation)id;
                if (LastShiftSituationTable.IsPerZone(situation))
                {
                    for (var zone = 0; zone < LastShiftZoneAtlas.ZoneCount; zone++)
                        Step(ref zoneLatches[zone][id], situation, input, (LastShiftZone)zone, deltaTime);
                }
                else
                {
                    Step(ref globalLatches[id], situation, input, LastShiftZone.Cockpit, deltaTime);
                }
            }
        }

        /// <summary>
        /// 래치 하나. 켜질 때는 발동선, 꺼질 때는 해제선을 본다.
        ///
        /// 최소 활성 시간은 <b>해제만</b> 막는다. 발동을 막으면 조건이 참인데 상황이 안 뜨는
        /// 구간이 생기고, 그건 연출 보호가 아니라 판정 지연이다.
        /// </summary>
        private static void Step(
            ref Latch latch,
            LastShiftSituation situation,
            in LastShiftSituationInput input,
            LastShiftZone zone,
            float deltaTime)
        {
            if (!latch.Active)
            {
                if (!LastShiftSituationTable.Triggers(situation, input, zone)) return;
                latch.Active = true;
                latch.ActiveSeconds = 0f;
                return;
            }

            latch.ActiveSeconds += Mathf.Max(0f, deltaTime);
            if (latch.ActiveSeconds < LastShiftSituationTable.MinimumActiveSeconds) return;
            if (!LastShiftSituationTable.Releases(situation, input, zone)) return;
            latch.Active = false;
            latch.ActiveSeconds = 0f;
        }

        private int ResolveChannel(
            LastShiftSystemChannel channel,
            LastShiftSituation[] candidates,
            System.Collections.Generic.List<LastShiftSituationTransition> transitions)
        {
            var winner = PickWinner(candidates, globalLatches);
            var previous = channelRepresentative[(int)channel];
            if (winner == previous) return 0;

            channelRepresentative[(int)channel] = winner;
            transitions?.Add(new LastShiftSituationTransition(channel, LastShiftZone.Cockpit, previous, winner));
            return 1;
        }

        private int ResolveOxygen(System.Collections.Generic.List<LastShiftSituationTransition> transitions)
        {
            var count = 0;
            for (var index = 0; index < LastShiftZoneAtlas.ZoneCount; index++)
            {
                var zone = (LastShiftZone)index;
                var winner = LastShiftSituation.None;

                foreach (var situation in PerZoneOxygen)
                {
                    if (!zoneLatches[index][(int)situation].Active) continue;
                    winner = Better(winner, situation);
                }

                // 전역 사이렌은 모든 구역의 후보에 함께 들어간다. 등급이 같은 S-O4 가 있으면
                // 테이블 뒤쪽인 S-O4 가 이긴다 — §3.2 가 예로 든 바로 그 경우다.
                if (globalLatches[(int)LastShiftSituation.DecompressionAlarm].Active)
                    winner = Better(winner, LastShiftSituation.DecompressionAlarm);

                var previous = oxygenRepresentative[index];
                if (winner == previous) continue;

                oxygenRepresentative[index] = winner;
                transitions?.Add(new LastShiftSituationTransition(
                    LastShiftSystemChannel.Oxygen, zone, previous, winner));
                count++;
            }
            return count;
        }

        private static LastShiftSituation PickWinner(LastShiftSituation[] candidates, Latch[] latches)
        {
            var winner = LastShiftSituation.None;
            foreach (var situation in candidates)
            {
                if (!latches[(int)situation].Active) continue;
                winner = Better(winner, situation);
            }
            return winner;
        }

        /// <summary>
        /// 둘 중 계통 대표가 될 쪽. 등급이 높은 쪽이고, 같으면 테이블 순서상 뒤쪽이다(§3.2).
        /// 열거형 값이 곧 테이블 순서이므로 비교가 그대로 타이브레이크가 된다.
        /// </summary>
        private static LastShiftSituation Better(LastShiftSituation left, LastShiftSituation right)
        {
            var leftGrade = LastShiftSituationTable.GradeOf(left);
            var rightGrade = LastShiftSituationTable.GradeOf(right);
            if (rightGrade > leftGrade) return right;
            if (rightGrade < leftGrade) return left;
            return (int)right > (int)left ? right : left;
        }
    }
}
