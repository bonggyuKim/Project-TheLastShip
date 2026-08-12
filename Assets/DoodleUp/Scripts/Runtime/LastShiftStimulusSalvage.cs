using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 잔해 성분. <b><see cref="LastShiftSalvageKind"/> 와 다른 타입이다</b> — 그쪽은 세 갈래
    /// (냉각/전력/선체)이고 네트워크 상태로 오가는 값이라, 갈래를 늘리면 통신 의미가 바뀐다.
    /// 방-피격이 만든 다섯 갈래는 여기서 따로 센다.
    /// </summary>
    public enum LastShiftSalvageComponent
    {
        /// <summary>선체분. 맞으면 뚫리므로 다섯 방 전부에서 나온다.</summary>
        Hull = 0,

        /// <summary>추진 계열 — 조종석 피격.</summary>
        Propulsion = 1,

        /// <summary>전력 계열 — 전력실 피격.</summary>
        Power = 2,

        /// <summary>열 계열 — 냉각실 피격.</summary>
        Heat = 3,

        /// <summary>산소 계열 — 산소실 피격.</summary>
        Oxygen = 4,

        /// <summary>없음 — 숙소 피격. 그 방에는 계통이 없다.</summary>
        None = 5
    }

    /// <summary>한 번의 피격이 남기는 잔해. 성분 둘로 갈라 센다.</summary>
    public readonly struct LastShiftSalvageYield
    {
        public LastShiftSalvageYield(int hull, int system, LastShiftSalvageComponent component)
        {
            Hull = hull;
            System = system;
            Component = component;
        }

        /// <summary>선체분. 항상 있다.</summary>
        public int Hull { get; }

        /// <summary>계통분. 계통이 없는 방(숙소)에서는 <c>0</c>.</summary>
        public int System { get; }

        /// <summary>계통분의 계열.</summary>
        public LastShiftSalvageComponent Component { get; }

        public int Total => Hull + System;

        public bool HasSystem => System > 0;
    }

    /// <summary>
    /// 피격 방 → 잔해 산출(<c>game-balance</c> 확정 · PM 핸드오프 2026-08-12).
    ///
    /// <b>두 성분으로 가른다.</b> <b>선체분</b>은 다섯 방 전부에서 나온다 — 맞으면 뚫리는 것은
    /// 방을 안 가린다. <b>계통분</b>은 그 방에 계통이 있을 때만 나온다. 그래서 숙소는 선체분만
    /// 남기고, 대신 그 선체분이 크다(<c>60</c>) — 계통이 없는 방을 맞은 것이 "아무것도 못
    /// 건지는 판" 이 되지 않게 하는 것이 이 배분의 요점이다. 조종석도 같은 이유로 선체분이
    /// 큰데, 그쪽은 구역이 배의 절반이라 파공 자체가 크기 때문이다.
    ///
    /// <b>총량은 심각도에 비례한다.</b> 약하게 맞으면 덜 나온다 — 안 그러면 강도 랜덤화가
    /// 파밍에는 아무 의미가 없어진다.
    ///
    /// <b>기존 <see cref="LastShiftSalvage"/> 경제와 아직 안 이어져 있다.</b> 그쪽은 덩이
    /// <b>개수</b>(<see cref="LastShiftSalvage.FieldChunks"/>, 인원 비례)로 도는데 이 표는
    /// 자재 <b>총량</b>이라 단위가 다르고, 튜토리얼 기항의 "끝나면 잔액 0" 불변식이 지금
    /// 덩이 계산에 걸려 있다. 단위를 맞추는 것은 그 불변식을 다시 푸는 일이라 별도 카드다.
    /// </summary>
    public static class LastShiftStimulusSalvage
    {
        /// <summary>구역이 배의 절반이라 파공이 크다. 숙소도 같은 값을 쓴다.</summary>
        public const int LargeHullBase = 60;

        /// <summary>단독 구역 방의 선체분.</summary>
        public const int SmallHullBase = 30;

        /// <summary>계통이 있는 방의 계통분. 네 방이 같은 값이다.</summary>
        public const int SystemBase = 60;

        /// <summary>그 방을 맞혔을 때 나오는 잔해.</summary>
        public static LastShiftSalvageYield Of(LastShiftStimulusRoom room, float severity)
        {
            var scale = Mathf.Clamp(severity, 0f, 1f);
            int Scaled(int baseValue) => Mathf.RoundToInt(baseValue * scale);

            return room switch
            {
                LastShiftStimulusRoom.Cockpit => new LastShiftSalvageYield(
                    Scaled(LargeHullBase), Scaled(SystemBase), LastShiftSalvageComponent.Propulsion),

                LastShiftStimulusRoom.Power => new LastShiftSalvageYield(
                    Scaled(SmallHullBase), Scaled(SystemBase), LastShiftSalvageComponent.Power),

                LastShiftStimulusRoom.Cooling => new LastShiftSalvageYield(
                    Scaled(SmallHullBase), Scaled(SystemBase), LastShiftSalvageComponent.Heat),

                LastShiftStimulusRoom.LifeSupport => new LastShiftSalvageYield(
                    Scaled(SmallHullBase), Scaled(SystemBase), LastShiftSalvageComponent.Oxygen),

                // 숙소 — 계통분이 없는 대신 선체분이 크다. 그래야 "계통 없는 방을 맞으면
                // 그 기항은 빈손" 이 안 된다.
                _ => new LastShiftSalvageYield(
                    Scaled(LargeHullBase), 0, LastShiftSalvageComponent.None)
            };
        }
    }
}
