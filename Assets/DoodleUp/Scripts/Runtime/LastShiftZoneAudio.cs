using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 구역별 사운드 감쇠·차폐(<c>A2</c>)의 정본. 판정 기준은 기획 정본 한 문장이다.
    ///
    ///   <b>HUD 에 전역으로 뜨는 것과 짝을 이루는 소리는 2D, 구역에 가야 읽히는 것과
    ///   짝을 이루는 소리는 3D.</b>
    ///
    /// A2 가 미관이 아니라 기능인 이유는 통로다. 통로에 서서 판단할 것이 없으면 금지 규칙
    /// 165(시간 때우기용 장거리 왕복)의 (다) 조건 — 이동 중 판단 재료가 0 — 이 참이 되고,
    /// 편도 3.5초가 순수 이동 시간이 된다. 국소 소리가 거리·방향을 주면 (다)가 거짓이 된다.
    ///
    /// <b>차폐(occlusion/lowpass)는 넣지 않는다.</b> 감쇠 커브만으로 간다. 근거 셋:
    /// 벌크헤드에 1.6m 개구부가 뚫려 있어 물리적으로도 소리는 새고, 통로에서 필요한 판단은
    /// "어느 쪽인가" 라서 패닝 + 거리로 이미 성립하며, 차폐는 <b>중요한 단서가 들리지 않는
    /// 실패 모드</b>를 새로 만든다. 이 스레드에서 조용한 실패를 세 번 봤으므로 감각 채널에
    /// 그 형태를 더 얹지 않는다. 통로에서 방향이 안 읽힌다는 플레이 판정이 나오면 그때
    /// <c>game-ta</c> 와 재검토한다.
    /// </summary>
    public static class LastShiftZoneAudio
    {
        /// <summary>구역에 가야 읽히는 소리. 거리·방향이 정보다.</summary>
        public const float LocalSpatialBlend = 1f;

        /// <summary>
        /// 배 전체에 걸리는 경보. <b>N9(전선 경보)의 구현 그 자체다.</b> 3D 로 바꾸면 먼 구역에서
        /// 감쇠되어 사이렌이 없는 것과 같아지고, 그러면 게이지가 2구역만 주는 구조에서
        /// 세 번째 구역을 특정하는 소거법의 전제("울렸다")가 사라진다. 165 회피가 그것에
        /// 걸려 있으므로 이 값은 연출이 아니라 전제 조건이다.
        /// </summary>
        public const float ShipWideSpatialBlend = 0f;

        /// <summary>
        /// 감쇠가 1 로 유지되는 반경. 음원은 승무원·설비 루트에 붙고 리스너는 눈높이에 있어서
        /// 자기 몸에서 나는 소리의 거리가 0 이 아니다. 이 값이 눈높이보다 작으면 <b>자기 호흡음이
        /// 자기 키 때문에 줄어든다.</b>
        /// </summary>
        public const float MinDistance = LastShiftShipPhysics.EyeHeight;

        /// <summary>
        /// 호흡음이 들리는 거리. 같은 방 안에서만 단서가 되어야 하므로 방 길이다.
        /// 통로 건너까지 들리면 "누가 근처에 있다" 가 아니라 "누군가 살아 있다" 가 되어
        /// 위치 정보가 사라진다.
        /// </summary>
        // 방 길이가 균등하지 않으므로 기준을 <b>가장 긴 방</b>으로 못박는다. 가운데 방
        // (전력실·냉각실 6m)을 쓰면 조종석·산소실(8m)에서 숨소리가 방 안에서도 안 닿는다.
        public static float BreathMaxDistance =>
            Mathf.Max(LastShiftShipDimensions.RoomLengthOf(LastShiftZone.Cockpit),
                LastShiftShipDimensions.RoomLengthOf(LastShiftZone.LifeSupport));

        /// <summary>
        /// 충격음이 들리는 거리. 선내 전 구간이다 — 충격은 상황의 시작이라 못 듣는 사람이
        /// 있으면 안 된다. 그래도 3D 인 이유는 <b>어느 구역에서 났는지가 정보</b>이기 때문이고,
        /// 선형 감쇠라 반대쪽 끝에서는 거리로 구역이 갈린다.
        /// </summary>
        // <b>광장 치수가 아니라 원반 지름이다.</b> InteriorLength 가 "배 전장" 에서
        // "허브 한 칸(12m)" 으로 뜻이 바뀌었으므로 그대로 두면 조종석에서 산소실 충격음이
        // 안 들린다 — 두 방 중심 사이가 20m 다.
        public static float ImpactMaxDistance => LastShiftHullShell.OverallLength;

        /// <summary>
        /// 국소 음원 설정. <see cref="AudioRolloffMode.Linear"/> 를 쓰는 이유는 로그 감쇠가
        /// 최대 거리에서 0 에 도달하지 않아 "선내 어디서든 들린다" 와 구분이 안 되기 때문이다.
        /// 선형은 <paramref name="maxDistance"/> 에서 정확히 0 이라 검사가 값을 고정할 수 있다.
        ///
        /// 도플러는 끈다. 승무원 이동 속도 4m/s 에 걸린 호흡 루프는 음정이 흔들리는 잡음으로
        /// 들리고, 그 흔들림은 거리 판단에 도움이 안 된다.
        /// </summary>
        public static void ConfigureLocal(AudioSource source, float maxDistance)
        {
            if (source == null) return;
            source.spatialBlend = LocalSpatialBlend;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = MinDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
        }

        /// <summary>전선 경보 설정. 값은 하나뿐이지만 이름이 붙어 있어야 3D 일괄 전환에서 살아남는다.</summary>
        public static void ConfigureShipWide(AudioSource source)
        {
            if (source == null) return;
            source.spatialBlend = ShipWideSpatialBlend;
        }

        /// <summary>
        /// 귀를 만든다. <b>씬에 AudioListener 가 없으면 2D 든 3D 든 아무 소리도 재생되지 않는다</b> —
        /// A2 가 0 인 1차 원인은 spatialBlend 가 아니라 이것이었다. 씬 빌더가 카메라를
        /// <c>new GameObject</c> + <c>AddComponent&lt;Camera&gt;</c> 로 만들기 때문에 Unity 가
        /// 기본 씬에 넣어 주는 리스너가 딸려 오지 않았다.
        ///
        /// 리스너는 배에 정확히 하나만 활성이어야 한다. 둘 이상이면 Unity 가 경고를 내고 그중
        /// 하나만 쓰므로, 어느 승무원의 귀가 쓰이는지가 스폰 순서에 좌우된다. 그래서 소유권을
        /// 아는 쪽(<see cref="LastShiftNetworkPlayer"/>)이 <paramref name="active"/> 를 정한다.
        /// </summary>
        public static AudioListener EnsureListener(Camera ear, bool active)
        {
            if (ear == null) return null;
            var listener = ear.GetComponent<AudioListener>();
            if (listener == null) listener = ear.gameObject.AddComponent<AudioListener>();
            listener.enabled = active;
            return listener;
        }
    }
}
