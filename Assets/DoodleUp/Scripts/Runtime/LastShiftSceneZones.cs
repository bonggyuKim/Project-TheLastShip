namespace DoodleUp.Runtime
{
    /// <summary>
    /// 구역 이름 정본. 이전에는 LastShiftSceneBuilder(Editor 어셈블리)에만 있어서 런타임
    /// 연출이 구역을 찾을 수 없었다. 런타임과 빌더가 같은 문자열을 쓰도록 여기로 옮긴다.
    /// </summary>
    public static class LastShiftSceneZones
    {
        public const string CockpitZoneName = "Zone_Cockpit";
                /// <summary>전력실. 예전 "Zone_UtilityCorridor"(엔진실) 를 §2.1 이 둘로 쪼갠 앞쪽이다.</summary>
        public const string PowerZoneName = "Zone_Power";

        /// <summary>냉각실. 쪼갠 뒤쪽이다.</summary>
        public const string CoolingZoneName = "Zone_Cooling";
        public const string LifeSupportZoneName = "Zone_LifeSupport";
    }
}
