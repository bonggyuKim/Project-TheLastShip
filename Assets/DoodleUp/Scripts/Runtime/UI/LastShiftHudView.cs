using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 상시 HUD 한 장. <b>자리는 프리팹이 정한다.</b>
    ///
    /// <b>이 컴포넌트에는 좌표 API 가 없다</b> — 그것이 이 클래스의 존재 이유다. 예전에는
    /// 아이콘 셋의 크기·여백·간격이 코드 상수였고 매 프레임 <c>Rect</c> 를 계산해 얹었다.
    /// 그러면 아이콘을 조금 옮기는 일이 <b>코드 수정과 재컴파일</b>이 되고, 에디터에서
    /// 드래그로 맞출 수가 없다 — 사용자가 "이러면 에디터에서 수정을 못 하잖아" 로 지적한
    /// 그 상태다.
    ///
    /// 지금은 프리팹 <c>Resources/LastShiftHud</c> 의 <see cref="RectTransform"/> 앵커와
    /// 오프셋이 정본이고, 런타임은 <b>값만</b> 갱신한다(채움 비율과 색). 아이콘을 옮기려면
    /// 프리팹을 열어 끌면 되고 코드는 안 건드린다.
    ///
    /// <b>런타임에서 자리를 다시 잡지 않는다.</b> 여기서 한 줄이라도 앵커를 만지면 프리팹에서
    /// 끈 위치가 첫 프레임에 덮여서, 에디터 수정이 "저장은 되는데 게임에서는 안 보이는"
    /// 상태가 된다. 그 조용한 실패가 좌표를 코드에 두는 것보다 나쁘다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftHudView : MonoBehaviour
    {
        /// <summary>프리팹을 찾는 이름. <c>Resources</c> 밑의 경로다.</summary>
        public const string ResourcePath = "LastShiftHud";

        [SerializeField] private LastShiftGaugeView oxygen;
        [SerializeField] private LastShiftGaugeView power;
        [SerializeField] private LastShiftGaugeView heat;

        public LastShiftGaugeView Oxygen => oxygen;
        public LastShiftGaugeView Power => power;
        public LastShiftGaugeView Heat => heat;

        /// <summary>프리팹을 굽는 에디터 도구가 참조를 꽂는다. 런타임은 안 부른다.</summary>
        public void Configure(LastShiftGaugeView oxygenGauge, LastShiftGaugeView powerGauge,
            LastShiftGaugeView heatGauge)
        {
            oxygen = oxygenGauge;
            power = powerGauge;
            heat = heatGauge;
        }

        /// <summary>세 아이콘이 다 꽂혀 있는가. 하나라도 비면 그 계통이 화면에서 사라진다.</summary>
        public bool IsWired => oxygen != null && power != null && heat != null;

        /// <summary>
        /// 값과 색만 갱신한다. <b>자리는 안 건드린다</b> — 프리팹이 정본이다.
        /// </summary>
        public void Set(LastShiftUiIcon icon, float value01, Color tone)
        {
            var gauge = GaugeOf(icon);
            if (gauge == null) return;
            gauge.SetValue(Mathf.Clamp01(value01));
            gauge.SetTone(tone);
        }

        public LastShiftGaugeView GaugeOf(LastShiftUiIcon icon) => icon switch
        {
            LastShiftUiIcon.Oxygen => oxygen,
            LastShiftUiIcon.Power => power,
            LastShiftUiIcon.Heat => heat,
            _ => null
        };

        /// <summary>화면에서 통째로 감춘다. 판정 화면이 뜨면 상시 HUD 는 물러난다.</summary>
        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
        }
    }
}
