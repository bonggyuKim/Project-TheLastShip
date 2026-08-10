using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 압력 경계가 아닌 출입구의 문짝을 <b>열린 자세로 고정</b>한다.
    ///
    /// 조종석과 숙소는 광장과 같은 압력 구역이라 압력문이 없다 — 경계는 셋뿐이고
    /// (<see cref="LastShiftPlazaLayout.PressureBoundaryCount"/>) 그 셋은 전력실·냉각실·산소실이
    /// 가져간다. 그런데 정본 지도는 다섯 자리 <b>모두</b>에 문 킷을 세운다.
    ///
    /// 그래서 조종석·숙소 문은 <see cref="LastShiftZoneDoor"/> 가 안 붙고, 아무도 애니메이터를
    /// 건드리지 않아 <b>닫힌 기본 자세로 남는다</b>. 통행은 되므로 플레이어는 닫힌 문을 그대로
    /// 통과하게 되고, 그게 "문이 콜라이더 없이 뚫린다" 로 보고된 것이다. 실제로 없는 것은
    /// 콜라이더가 아니라 <b>여는 사람</b>이었다.
    ///
    /// 여기서 막지 않고 여는 쪽을 고르는 이유는 압력 모형이다. 두 방은 광장과 한 구역이라
    /// 막으면 갈 수가 없고, 열 주체를 새로 만들면 압력 경계 셋이라는 정본이 흔들린다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public sealed class LastShiftPassageDoor : MonoBehaviour
    {
        private static readonly int DoorClipState = Animator.StringToHash("LP_Door_OpenClose");

        private void Start() => ScrubOpen();
        private void OnEnable() => ScrubOpen();

        private void ScrubOpen()
        {
            var animator = GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            // speed = 0 이라 Play 만으로는 다음 프레임에야 반영된다. Update(0) 으로 그 자리에서
            // 평가시킨다 — LastShiftZoneDoor 가 같은 이유로 같은 일을 한다.
            animator.speed = 0f;
            animator.Play(DoorClipState, 0, 1f);
            animator.Update(0f);
        }
    }
}
