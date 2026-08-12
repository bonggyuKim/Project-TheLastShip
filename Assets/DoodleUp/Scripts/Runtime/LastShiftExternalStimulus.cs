using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>피격당하는 방. 배에 있는 방 다섯을 그대로 쓴다.</summary>
    public enum LastShiftStimulusRoom
    {
        Cockpit = 0,
        Power = 1,
        Cooling = 2,
        LifeSupport = 3,
        Quarters = 4
    }

    /// <summary>
    /// 한 tick 동안 자극이 계통에 미는 양. <b>전부 증분이다</b> — 목표값이 아니라 이번 프레임에
    /// 더할 값이라, 받는 쪽이 기존 감쇠와 같은 자리에서 합치기만 하면 된다.
    /// </summary>
    public readonly struct LastShiftStimulusDelta
    {
        public LastShiftStimulusDelta(LastShiftZone zone, float zonePressure, float busPower,
            float engineHeat, float fuelReserve, float attitudeDegrees)
        {
            Zone = zone;
            ZonePressure = zonePressure;
            BusPower = busPower;
            EngineHeat = engineHeat;
            FuelReserve = fuelReserve;
            AttitudeDegrees = attitudeDegrees;
        }

        /// <summary>파공이 난 구역. <see cref="ZonePressure"/> 가 이 구역에만 들어간다.</summary>
        public LastShiftZone Zone { get; }

        public float ZonePressure { get; }
        public float BusPower { get; }
        public float EngineHeat { get; }
        public float FuelReserve { get; }
        public float AttitudeDegrees { get; }

        public bool IsEmpty =>
            ZonePressure == 0f && BusPower == 0f && EngineHeat == 0f
            && FuelReserve == 0f && AttitudeDegrees == 0f;
    }

    /// <summary>
    /// 외부 랜덤 자극(운석) — <c>docs/external-random-stimulus-layer-v1.md</c> 1단계.
    ///
    /// <b>새 상황도 새 동사도 안 만든다.</b> 하는 일은 기존 계통 값(<c>BusPower</c>·
    /// <c>EngineHeat</c>·<c>FuelReserve</c>·자세·구역 압력)을 미는 것뿐이다. 상황 표
    /// <c>S-*</c> 12개는 그 값들이 임계를 넘으면 알아서 뜨므로, 여기서 상황을 직접 켜지 않는다 —
    /// <c>ship-elements-and-situations-v1.md</c> §3.4 가 동결한 12개 선을 넘지 않는 유일한 방법이다.
    ///
    /// <b>즉발이 아니라 서서히 민다.</b> 한 번에 최고 등급으로 점프시키면 <c>RG-4</c> 전수검증이
    /// 훑은 1,920 조합 밖의 상태(중간 등급을 건너뛴 조합)가 생겨 재검증 대상이 된다. 총량을
    /// <see cref="DamageSeconds"/> 에 나눠 매 tick 조금씩 밀면 계통이 기존 감쇠와 같은 속도로
    /// 임계를 지나가므로, 상태 공간이 안 늘고 <c>RG-4</c> 를 다시 열 필요가 없다(§3 결론).
    ///
    /// <b>구간당 정확히 한 번이다.</b> 확률로 0회가 되는 구간이 생기면
    /// <c>outboard-outpost-and-map-final-v1.md</c> §4.2 의 "직전 구간 자극이 이번 기항 잔해
    /// 종류를 정한다" 는 파밍 순환이 끊긴다(자극이 없으면 뜰 잔해 종류가 없다). 그래서 랜덤인
    /// 것은 <b>시점·방·강도</b>이고 발생 자체는 보장한다.
    ///
    /// <b><see cref="MonoBehaviour"/> 가 아니다.</b> 시계를 검사가 직접 돌려 씬 없이 재고,
    /// 실제로 누가 이 시계를 돌리는지는 PlayMode 가 따로 본다 — <c>LastShiftEvaLift.Tick</c> 이
    /// 아무 데서도 안 불리고 있었는데 EditMode 는 전부 초록이었던 자리가 여기와 같은 모양이다.
    /// </summary>
    public static class LastShiftExternalStimulus
    {
        /// <summary>구간 하나의 길이. <c>voyage-run-structure-v1.md</c> §1 이 정한 값이다.</summary>
        public const float SegmentSeconds = 300f;

        /// <summary>
        /// 자극이 뜰 수 있는 가장 이른 구간 진행률. 이보다 앞이면 준비할 시간이 없다 —
        /// 구간 시작 직후는 승무원이 아직 자리를 잡는 중이다.
        /// </summary>
        public const float EarliestFraction = 0.20f;

        /// <summary>
        /// 가장 늦은 진행률. 이보다 뒤면 <see cref="DamageSeconds"/> 동안 값이 밀리는 중에
        /// 구간이 끝나 버려서, 대응할 시간이 없는 사고가 된다.
        /// </summary>
        public const float LatestFraction = 0.70f;

        /// <summary>
        /// 심각도 하한. 더 낮추면 "상황이 안 뜨는 자극" 이 되어 존재 이유가 없어진다(§2.1).
        /// </summary>
        public const float MinSeverity = 0.70f;

        /// <summary>
        /// 상한. 현행 고정값 <c>0.9924</c> 를 넘기지 않는다 — <c>RG-4</c> 전수검증이 그 값을
        /// 상한으로 짜여 있어서, 넘기면 검증 밖 강도가 된다.
        /// </summary>
        public const float MaxSeverity = 0.99f;

        /// <summary>
        /// 총량을 나눠 미는 시간. <b>이 상수가 "서서히" 의 전부다</b> — 0 으로 두면 즉발이
        /// 되고 그 순간 <c>RG-4</c> 재검증 대상이 된다.
        /// </summary>
        public const float DamageSeconds = 20f;

        // ── 방 고유 결과(B)의 총량. 심각도 1.0 기준이고 DamageSeconds 에 걸쳐 들어간다. ──
        // <b>이 숫자들은 game-balance 소관이다.</b> 설계 문서가 방-계통 대응만 정하고 크기는
        // 안 정해서(미결 2) 여기서 초안을 잡았다. 고칠 때 이 파일만 보면 된다.

        /// <summary>조종석 피격 — 추진 계통. 연료가 새고 자세가 틀어진다.</summary>
        public const float CockpitFuelLoss = 0.06f;

        public const float CockpitAttitudeDrift = 15f;

        /// <summary>전력실 피격 — 버스 전압 급락. 다섯 중 가장 큰 단일 계통 타격이다.</summary>
        public const float PowerBusLoss = 0.45f;

        /// <summary>냉각실 피격 — 열을 못 버린다. 값을 점프시키지 않고 올려 보낸다.</summary>
        public const float CoolingHeatGain = 0.30f;

        /// <summary>
        /// 산소실 피격 — 파공(A)에 더해 생성·저장이 직접 준다. 이 방만 (A)와 (B)가 같은
        /// 축에서 겹치므로 압력이 두 번 깎인다(§2.1-1 표의 "이중").
        /// </summary>
        public const float LifeSupportOxygenLoss = 0.20f;

        /// <summary>
        /// 모든 방에 공통으로 들어가는 파공(A). 맞은 자리가 뚫리는 것은 방을 안 가린다.
        /// </summary>
        public const float BreachPressureLoss = 0.30f;

        /// <summary>
        /// 파공으로 한 번에 뺄 수 있는 압력의 <b>구조적 상한</b>. 밸런스 값이 아니라 조항이다
        /// (PM 확정 2026-08-12).
        ///
        /// <b>이 선을 넘으면 자극이 그 방을 못 들어가게 만든다.</b> 압력이 한 번에 너무 많이
        /// 빠지면 그 방에 들어가는 것 자체가 불가능해지고, 그러면 그 방에서만 할 수 있는
        /// 복구가 통째로 막힌다 — <c>RG-3</c>(영구 잠금 금지)이 정확히 그것을 금지한다.
        /// <see cref="BreachPressureLoss"/> 를 올릴 때 이 값을 같이 올리지 않는다.
        /// </summary>
        public const float MaxBreachPressureLoss = 0.35f;

        /// <summary>
        /// 항해 <b>한 번</b> 동안 자극이 가져갈 수 있는 연료의 총합(PM 확정 2026-08-12).
        ///
        /// <b>구간마다가 아니라 항해마다다.</b> 연료는 보급 지점이 0개인 1회 예산이라
        /// (<c>LastShiftShipState.FuelReserve</c> 주석), 구간별 상한만 두면 조종석을 세 번
        /// 맞은 항해가 도킹에 필요한 추력적분을 못 채우는 판이 된다. 그 판은 복구 행동이
        /// 있어도 못 이기므로 <c>RG-3</c> 이 막으려는 상태와 같아진다.
        /// </summary>
        public const float VoyageFuelLossCap = 0.18f;

        /// <summary>방의 개수. 다섯이고, 균등이면 각 <c>20%</c> 다(PM 확정).</summary>
        public const int RoomCount = 5;

        /// <summary>
        /// 기항 하나가 지날 때마다 그 방의 가중치에 붙는 양. <c>w = 1 + 0.4k</c> 의 <c>0.4</c> 다.
        ///
        /// <b>확률을 균등에서 떼어내는 이유는 자재 결핍이다.</b> 완전 균등이면 한 계열이
        /// 연달아 안 나오는 구간이 생기고, 그 계열 자재가 필요한 확장이 그 동안 통째로 막힌다.
        /// </summary>
        public const float PityWeightPerPort = 0.4f;

        /// <summary>
        /// 이 기항 수만큼 안 맞은 방은 <b>확정으로</b> 맞는다.
        ///
        /// <b><c>8</c> 이 하한이다</b>(game-balance 확정). <c>6</c> 이하로 조이면 순서가 눈에
        /// 보여서 "다음은 저 방" 을 외우게 되고, 그러면 랜덤화가 만들려던 것이 사라진다.
        /// </summary>
        public const int HardCapPorts = 8;

        private static readonly int[] PortsSinceHit = new int[RoomCount];

        private static System.Random random;
        private static bool armed;
        private static bool fired;
        private static float elapsed;
        private static float damageElapsed;

        /// <summary>이번 항해가 자극으로 잃은 연료 누적. 항해가 바뀔 때만 지워진다.</summary>
        private static float voyageFuelLost;

        /// <summary>이번 구간에 자극이 예약돼 있는가.</summary>
        public static bool IsArmed => armed;

        /// <summary>이번 구간의 자극이 이미 터졌는가.</summary>
        public static bool HasFired => fired;

        /// <summary>지금 값을 밀고 있는 중인가. 터진 뒤 <see cref="DamageSeconds"/> 동안 참이다.</summary>
        public static bool IsDamaging => fired && damageElapsed < DamageSeconds;

        /// <summary>이번 구간이 때리는 방.</summary>
        public static LastShiftStimulusRoom Room { get; private set; }

        /// <summary>이번 구간의 강도.</summary>
        public static float Severity { get; private set; }

        /// <summary>구간 시작으로부터 몇 초에 터지는가.</summary>
        public static float FireAtSeconds { get; private set; }

        /// <summary>구간 시작으로부터 흐른 시간.</summary>
        public static float Elapsed => elapsed;

        /// <summary>
        /// 그 방이 뚫으면 어느 구역의 압력이 빠지는가.
        ///
        /// <b>숙소는 조종석과 같은 구역이다.</b> 방은 다섯인데 구역은 넷이라 하나가 겹치는데,
        /// 그 겹침이 설계 그대로다 — 숙소는 광장·조종석과 한 기압 구획이다.
        /// </summary>
        public static LastShiftZone BreachZoneOf(LastShiftStimulusRoom room) => room switch
        {
            LastShiftStimulusRoom.Power => LastShiftZone.Power,
            LastShiftStimulusRoom.Cooling => LastShiftZone.Cooling,
            LastShiftStimulusRoom.LifeSupport => LastShiftZone.LifeSupport,
            _ => LastShiftZone.Cockpit
        };

        /// <summary>
        /// 구간을 연다. <b>방·강도·시점을 여기서 미리 굴려 둔다</b> — 터지는 순간에 굴리면
        /// 호스트와 클라이언트가 다른 값을 뽑는다. 같은 <paramref name="seed"/> 는 같은 구간을
        /// 만든다.
        /// </summary>
        public static void BeginSegment(int seed)
        {
            random = new System.Random(seed);
            armed = true;
            fired = false;
            elapsed = 0f;
            damageElapsed = 0f;
            Room = RollRoom(random);
            for (var i = 0; i < RoomCount; i++)
                PortsSinceHit[i] = i == (int)Room ? 0 : PortsSinceHit[i] + 1;
            Severity = Mathf.Lerp(MinSeverity, MaxSeverity, (float)random.NextDouble());
            FireAtSeconds = Mathf.Lerp(
                SegmentSeconds * EarliestFraction,
                SegmentSeconds * LatestFraction,
                (float)random.NextDouble());
        }

        /// <summary>
        /// 그 방이 마지막으로 맞은 뒤 몇 기항이 지났는가. <c>w = 1 + 0.4k</c> 의 <c>k</c> 다.
        /// </summary>
        public static int PortsSince(LastShiftStimulusRoom room) => PortsSinceHit[(int)room];

        /// <summary>지금 그 방의 추첨 가중치.</summary>
        public static float WeightOf(LastShiftStimulusRoom room) =>
            1f + PityWeightPerPort * PortsSince(room);

        /// <summary>
        /// 이번에 뽑을 방. <b>하드캡이 먼저다</b> — <see cref="HardCapPorts"/> 기항을 안 맞은
        /// 방이 있으면 가중치를 안 보고 그 방으로 간다. 둘 이상이면 가장 오래 안 맞은 쪽이다.
        /// </summary>
        private static LastShiftStimulusRoom RollRoom(System.Random rng)
        {
            var starved = -1;
            for (var i = 0; i < RoomCount; i++)
            {
                if (PortsSinceHit[i] < HardCapPorts) continue;
                if (starved < 0 || PortsSinceHit[i] > PortsSinceHit[starved]) starved = i;
            }

            if (starved >= 0) return (LastShiftStimulusRoom)starved;

            var total = 0f;
            for (var i = 0; i < RoomCount; i++) total += WeightOf((LastShiftStimulusRoom)i);

            var pick = (float)rng.NextDouble() * total;
            for (var i = 0; i < RoomCount; i++)
            {
                pick -= WeightOf((LastShiftStimulusRoom)i);
                if (pick <= 0f) return (LastShiftStimulusRoom)i;
            }

            return (LastShiftStimulusRoom)(RoomCount - 1);
        }

        /// <summary>
        /// 검사가 발동 시점을 당긴다. 실제 창은 구간의 <c>20~70%</c>(<c>60~210</c>초)라
        /// PlayMode 가 그대로 기다릴 수 없다 — 방·강도는 그대로 두고 <b>시점만</b> 옮긴다.
        /// </summary>
        public static void FireAtForProbe(float seconds)
        {
            FireAtSeconds = Mathf.Max(0f, seconds);
        }

        /// <summary>구간 밖으로 나간다. 기항·로비·검사가 부른다.</summary>
        public static void Clear()
        {
            random = null;
            armed = false;
            fired = false;
            elapsed = 0f;
            damageElapsed = 0f;
            Room = LastShiftStimulusRoom.Cockpit;
            Severity = 0f;
            FireAtSeconds = 0f;
            // 결핍 기록도 같이 지운다. 남겨 두면 새 항해가 지난 항해의 빚을 지고 시작한다.
            for (var i = 0; i < RoomCount; i++) PortsSinceHit[i] = 0;
            // 연료 상한도 항해 단위라 여기서 함께 풀린다.
            voyageFuelLost = 0f;
        }

        /// <summary>
        /// 시계를 <paramref name="deltaTime"/> 만큼 돌리고, 이번 tick 에 계통에 밀 양을 준다.
        /// 아직 안 터졌거나 다 밀었으면 빈 값이다.
        ///
        /// <b>부르는 쪽이 멈출 수 있다.</b> 도입부 연출처럼 조작이 잠긴 동안에는 이 함수를
        /// 아예 안 불러야 자극이 그 시간을 안 먹는다 — 시간을 여기서 판단하면 잠금 규칙이
        /// 두 벌이 된다.
        /// </summary>
        public static LastShiftStimulusDelta Tick(float deltaTime)
        {
            if (!armed || deltaTime <= 0f) return default;

            if (!fired)
            {
                elapsed += deltaTime;
                if (elapsed < FireAtSeconds) return default;
                fired = true;
                // 터진 프레임부터 바로 밀기 시작한다. 넘어선 만큼만 쓰면 첫 tick 이 통째로
                // 사라지지 않는다.
                deltaTime = Mathf.Min(deltaTime, elapsed - FireAtSeconds + Mathf.Epsilon);
            }
            else
            {
                elapsed += deltaTime;
            }

            if (damageElapsed >= DamageSeconds) return default;

            var step = Mathf.Min(deltaTime, DamageSeconds - damageElapsed);
            damageElapsed += step;
            return CapFuel(DeltaFor(Room, Severity, step / DamageSeconds));
        }

        /// <summary>
        /// 이번 항해가 이미 잃은 연료. <see cref="VoyageFuelLossCap"/> 이 이 값을 상대로 걸린다.
        /// </summary>
        public static float VoyageFuelLost => voyageFuelLost;

        /// <summary>
        /// 연료 손실을 항해 상한 안으로 자른다. <b>구간이 아니라 항해를 넘어 누적한다</b> —
        /// 조종석을 여러 번 맞은 항해가 도킹을 못 채우는 판이 되지 않게 하는 것이 목적이다.
        /// </summary>
        private static LastShiftStimulusDelta CapFuel(LastShiftStimulusDelta delta)
        {
            if (delta.FuelReserve >= 0f) return delta;

            var room = Mathf.Max(0f, VoyageFuelLossCap - voyageFuelLost);
            var take = Mathf.Min(-delta.FuelReserve, room);
            voyageFuelLost += take;

            if (Mathf.Approximately(take, -delta.FuelReserve)) return delta;
            return new LastShiftStimulusDelta(delta.Zone, delta.ZonePressure, delta.BusPower,
                delta.EngineHeat, -take, delta.AttitudeDegrees);
        }

        /// <summary>
        /// 방 → 계통 대응표(§2.1-1)를 그대로 옮긴 것. <paramref name="portion"/> 은 총량 중
        /// 이번에 밀 비율이다.
        ///
        /// <b>순수 함수다</b> — 씬도 시계도 안 본다. 표가 맞는지는 이 함수 하나만 재면 된다.
        /// </summary>
        public static LastShiftStimulusDelta DeltaFor(
            LastShiftStimulusRoom room, float severity, float portion)
        {
            var scale = severity * portion;
            var breach = -BreachPressureLoss * scale;

            // 산소실은 (A)와 (B)가 같은 축이라 둘을 더하면 0.30 + 0.20 = 0.50 이 되어 조항
            // 상한을 넘는다. <b>조항이 이긴다</b> — 그 상한은 "그 방에 들어갈 수 있는가" 를
            // 지키는 구조적 선이고(RG-3), 넘기면 산소실 복구가 통째로 막힌다. 그래서 두 값을
            // 더한 뒤 상한에서 자른다. 이 자름이 걸리면 산소실의 실효 (B)가 줄어드는 것이라,
            // 0.20 을 온전히 쓰려면 상한을 올리거나 파공을 낮춰야 한다.
            float Capped(float loss) =>
                -Mathf.Min(-loss, MaxBreachPressureLoss * severity * portion);

            return room switch
            {
                LastShiftStimulusRoom.Cockpit => new LastShiftStimulusDelta(
                    LastShiftZone.Cockpit, breach, 0f, 0f,
                    -CockpitFuelLoss * scale, CockpitAttitudeDrift * scale),

                LastShiftStimulusRoom.Power => new LastShiftStimulusDelta(
                    LastShiftZone.Power, breach, -PowerBusLoss * scale, 0f, 0f, 0f),

                LastShiftStimulusRoom.Cooling => new LastShiftStimulusDelta(
                    LastShiftZone.Cooling, breach, 0f, CoolingHeatGain * scale, 0f, 0f),

                LastShiftStimulusRoom.LifeSupport => new LastShiftStimulusDelta(
                    LastShiftZone.LifeSupport, Capped(breach - LifeSupportOxygenLoss * scale),
                    0f, 0f, 0f, 0f),

                // 숙소는 (A) 파공만이다. 억지로 다섯 번째 계통을 만들면 그것이 곧 13번째
                // 상황이 되어 §3.4 동결선을 깬다 — "이 방에는 설비가 없다" 를 그대로 둔다.
                _ => new LastShiftStimulusDelta(LastShiftZone.Cockpit, breach, 0f, 0f, 0f, 0f)
            };
        }
    }
}
