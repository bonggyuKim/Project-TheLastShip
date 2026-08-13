namespace DoodleUp.Runtime
{
    /// <summary>
    /// 광장 둘레 방 여섯의 <b>사람이 읽는 이름</b>과 그 방에서 무엇을 하는지 한 마디.
    ///
    /// <b>이 자리가 없어서 온보딩이 막혔다</b>(2026-08-13 플레이테스트: "처음 하는 사람이 어느
    /// 방이 어딘지 모름"). 배는 광장 하나에 문이 다섯 뚫린 방사형이라, 광장에 서면 갈 수 있는
    /// 데가 한꺼번에 다 보인다 — 그런데 <b>어느 것이 무엇인지를 말하는 자리가 화면에 하나도
    /// 없었다</b>. 벽 이름표는 씬 빌더에서 걷혔고(<c>LastShiftSceneBuilder.CreateCompartmentLabel</c>
    /// 은 지금 빈 함수다), 지도(<c>M</c>)은 방 테두리만 그렸고, 압력문 프롬프트는 문 사거리에
    /// 들어가야 뜬다. 그래서 처음 하는 사람은 문 다섯을 하나씩 열어 보는 것으로 배를 배웠다.
    ///
    /// <b>이름을 월드에 다시 붙이지 않는다.</b> 상시 월드 텍스트는 이미 한 번 걷어낸 결정이고
    /// (구획 이름표와 같이 걷혔다), 되돌리면 아트 정본과 프리팹을 다시 구워야 한다. 대신
    /// <b>이름이 뜨는 자리를 UI 로 모은다</b> — 지도(<c>M</c>)이 그 정본이고, 조작줄이 그 지도의
    /// 존재를 알리고, 튜토리얼 재촉이 헤매는 사람을 그리로 보낸다.
    ///
    /// <b>이름이 두 벌이 되지 않는 것이 이 클래스의 전부다.</b> 기능실 넷은 HUD 구역 칸이 이미
    /// 이름을 갖고 있어(<see cref="LastShiftZoneAtlas.ShortLabelOf"/>) 여기서 그것을 그대로
    /// 끌어온다 — 표를 따로 적으면 지도에서 읽은 이름과 HUD 에서 읽은 이름이 갈리고, 그건
    /// "어느 방이 어딘지 모름" 을 고치려는 화면이 새로 만드는 같은 종류의 혼동이다. 새 문자열은
    /// 광장·숙소 둘뿐이고, 그 둘은 구역이 조종석이라 구역 이름으로 부를 수 없다.
    /// </summary>
    public static class LastShiftRoomLabels
    {
        /// <summary>
        /// 광장 한가운데 코어의 이름. <b>이것이 지도에서 가장 중요한 한 줄이다</b> — 튜토리얼
        /// <c>2</c>·<c>3</c>단계가 사람을 보내는 곳이 여기이고(선외로 나가는 유일한 길),
        /// 지도에서는 광장 한복판의 다른 색 사각형으로만 서 있어서 "지나갈 수 없는 기둥" 으로
        /// 읽히기 쉽다. 이름이 붙으면 그 사각형이 <b>목적지</b>가 된다.
        ///
        /// 문구는 튜토리얼 <c>3</c>단계 제목과 같은 말이다(<c>LastShiftTutorialCopy</c>) —
        /// 띠에서 읽은 말과 지도에서 찾는 말이 같아야 그 둘이 이어진다.
        /// </summary>
        public static string ShaftName => LastShiftText.Get("term.core.name");

        /// <summary>승강구가 하는 일. 방 부제와 같은 규약으로 짧다.</summary>
        public static string ShaftPurpose => LastShiftText.Get("term.core.purpose");

        /// <summary>
        /// 이 방의 이름. <b>기능실 넷은 구역 이름을 그대로 쓴다</b> — 파생이라 HUD 와 갈릴 수가
        /// 없고, 구역 이름이 바뀌면 지도가 따라온다.
        ///
        /// 광장·숙소만 자기 문자열이다. 둘은 조종석 구역에 속해 있어(조항 <c>S-1</c>) 구역
        /// 이름으로 부르면 셋이 전부 "조종석" 이 된다.
        /// </summary>
        public static string NameOf(LastShiftPlazaSpace space) => space switch
        {
            LastShiftPlazaSpace.Plaza => LastShiftText.Get("term.room.plaza"),
            LastShiftPlazaSpace.Quarters => LastShiftText.Get("term.room.quarters"),
            _ => LastShiftZoneAtlas.ShortLabelOf(LastShiftPlazaLayout.Of(space).Zone)
        };

        /// <summary>
        /// 그 방에 <b>왜 가는가</b> 한 마디. 이름만으로는 "전력실" 이 무엇을 하는 방인지 모르는
        /// 사람에게 아무것도 아니므로, 이름 아래 한 줄로 그 방에서 걸리는 동사나 계기를 적는다.
        ///
        /// <b>계통 이름이 아니라 그 방에서 할 일을 적는다.</b> 냉각실이 "열 배출" 인 것보다
        /// 밸브가 거기 있다는 것이 처음 하는 사람에게 쓸모 있고(<see cref="LastShiftCoolingValve"/>),
        /// 광장이 "허브" 인 것보다 들고 온 자재가 거기서 들어간다는 것이 쓸모 있다.
        ///
        /// <b>길이가 규약이다.</b> 방 하나가 지도에서 <c>110~220</c>px 이고 부제 글자가
        /// <c>11</c>px 라 여덯 자를 넘기면 방 밖으로 삐져나간다 —
        /// <c>LastShiftRoomLabelTests</c> 가 그 상한을 잰다.
        /// </summary>
        public static string PurposeOf(LastShiftPlazaSpace space) => LastShiftText.Get(space switch
        {
            LastShiftPlazaSpace.Plaza => "term.room.purpose.plaza",
            LastShiftPlazaSpace.CockpitRoom => "term.room.purpose.cockpit",
            LastShiftPlazaSpace.LifeSupportRoom => "term.room.purpose.lifeSupport",
            LastShiftPlazaSpace.PowerRoom => "term.room.purpose.power",
            LastShiftPlazaSpace.CoolingRoom => "term.room.purpose.cooling",
            _ => "term.room.purpose.quarters"
        });
    }
}
