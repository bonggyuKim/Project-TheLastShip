namespace DoodleUp.Runtime
{
    /// <summary>
    /// 내레이션 신호음 태그(대본 §2).
    ///
    /// <b>소리 자체가 아니라 태그다.</b> 음원은 game-art 소관이고 여기서는 "어떤 종류의
    /// 사건인가" 만 정한다 — 그래야 음원이 바뀌어도 문안 표가 안 흔들린다.
    ///
    /// 조항 <c>N-1</c>: 재촉 라인에는 붙지 않는다. 같은 사건을 다시 말하는 것이라 소리를
    /// 또 내면 플레이어가 새 사건으로 읽는다.
    /// 조항 <c>N-2</c>: <see cref="ChimeLong"/> 이 도는 동안에도 이동 입력을 막지 않는다.
    /// 유일한 예외가 기상 첫 줄(암전 구간)이고, 그것은 신호음이 아니라 연출이 막는 것이다.
    /// </summary>
    public enum LastShiftNarrationSfx
    {
        /// <summary>소리 없음. 앞줄에 이어 붙는 줄과 재촉이 여기 해당한다.</summary>
        None = 0,

        /// <summary>짧은 알림. 상태가 하나 바뀌었다는 신호다.</summary>
        ChimeShort = 1,

        /// <summary>긴 알림. 구간이 바뀌었다는 신호라 블록 첫 줄에만 붙는다.</summary>
        ChimeLong = 2,

        /// <summary>경고. 산소 임계 두 줄이 쓴다.</summary>
        ChimeAlert = 3
    }
}
