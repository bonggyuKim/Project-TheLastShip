using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>에어록 해치 두 짝의 구분. 같은 <c>xz</c> 에 위아래로 있다(§23.5).</summary>
    public enum LastShiftAirlockSide
    {
        /// <summary>덕트 바닥에 붙은 안쪽 해치. 배 안으로 통한다.</summary>
        Inner,

        /// <summary>배 밑면의 바깥 해치. 선외로 통한다.</summary>
        Outer
    }

    /// <summary>
    /// 에어록 해치 판 — <b>상태를 안 든다.</b> 정본은 <see cref="LastShiftAirlock"/> 이고 이
    /// 컴포넌트는 그 값을 향해 따라가기만 한다. <see cref="LastShiftDeckHatch"/> 가 sandbox 를
    /// 되묻는 것과 같은 구조·같은 이유이고(클라이언트에서 서버와 다른 해치가 열리면 안 된다),
    /// 여기서는 그 정본이 sandbox 가 아니라 정적 상태라서 조회조차 필요 없다.
    ///
    /// <b>조작 진입점이 여기 없다.</b> 승강구 해치는 <c>TryOperate</c> 를 자기가 갖는데,
    /// 에어록은 두 짝이 하나의 시퀀스로 묶여 있어(감압 한 번에 안쪽이 닫히고 바깥이 열린다)
    /// 판 하나가 자기 짝만 보고 결정할 수 있는 것이 없다 — 그 판단은
    /// <see cref="LastShiftAirlock.TryOperate"/> 한 자리에 모인다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftAirlockHatch : MonoBehaviour
    {
        /// <summary>
        /// 판 두께. 승강구 해치와 같은 값이다 — 열렸을 때 판이 바닥에 얹혀 있으므로
        /// <c>CharacterController.stepOffset</c>(<c>0.3</c>)보다 한참 낮아야 걸림돌이 안 된다.
        /// </summary>
        public const float PanelThickness = LastShiftDeckHatch.PanelThickness;

        /// <summary>
        /// 해치 한 짝의 한 변. <b>두 짝이 다르다.</b>
        ///
        /// 안쪽은 덕트 바닥에 뚫린 칸을 메우므로 통로 단면(<see cref="LastShiftBypassDuct.Section"/>)
        /// 이어야 한다 — 더 넓게 잡으면 문짝이 관 밖으로 나가고, 좁게 잡으면 닫아도 발밑에 틈이
        /// 남는다. 바깥쪽은 <c>3m</c> 짜리 에어록 바닥이라 그런 제약이 없고 문 개구 치수를 쓴다.
        /// </summary>
        public static float SpanOf(LastShiftAirlockSide side) =>
            side == LastShiftAirlockSide.Inner
                ? LastShiftBypassDuct.Section
                : LastShiftZoneDoor.OpeningWidth;

        [SerializeField] private LastShiftAirlockSide side;
        [SerializeField] private Transform panel;
        [SerializeField] private BoxCollider blocker;

        private float openAmount;

        public LastShiftAirlockSide Side => side;

        /// <summary>이 짝이 열려 있는가. 상태기가 그리는 단계에서 곧바로 나온다.</summary>
        public bool IsOpen => side == LastShiftAirlockSide.Inner
            ? LastShiftAirlock.IsInnerHatchOpen
            : LastShiftAirlock.IsOuterHatchOpen;

        public float OpenAmount => openAmount;

        /// <summary>판이 아직 목표에 닿지 않았는가. 연출 확인용이며 판정에는 쓰지 않는다.</summary>
        public bool IsMoving => !Mathf.Approximately(openAmount, IsOpen ? 1f : 0f);

        public void Configure(LastShiftAirlockSide hatchSide, Transform hatchPanel, BoxCollider hatchBlocker)
        {
            side = hatchSide;
            panel = hatchPanel;
            blocker = hatchBlocker;
            // 승강구 해치와 같은 이유로 조립 직후 바로 맞춘다 — AddComponent 가 Awake 를 먼저
            // 돌리므로 그때는 판도 차단면도 아직 null 이다.
            SnapToState();
        }

        private void Awake() => SnapToState();

        /// <summary>판과 차단 콜라이더를 지금 상태에 즉시 맞춘다. 씬 빌드·EditMode 조립용이다.</summary>
        public void SnapToState()
        {
            openAmount = IsOpen ? 1f : 0f;
            ApplyPanelPose();
        }

        private void Update()
        {
            var target = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(openAmount, target)) return;

            var step = Time.deltaTime / LastShiftRecoveryTuning.ZoneDoorTransitionSeconds;
            openAmount = Mathf.MoveTowards(openAmount, target, step);
            ApplyPanelPose();
        }

        private void ApplyPanelPose()
        {
            // 판은 x 로 미끄러진다. 승강구와 같은 방향이라 두 판이 같은 동작으로 읽힌다.
            // 물러나는 거리는 자기 한 변이다 — 짝마다 폭이 다르므로 상수 하나로 두면 안쪽 판이
            // 구멍을 덜 비우거나 더 멀리 나간다.
            if (panel != null)
                panel.localPosition = new Vector3(SpanOf(side) * openAmount, 0f, 0f);

            // 문·승강구와 같은 규칙 — 완전히 닫혔을 때만 막는다.
            if (blocker != null) blocker.enabled = openAmount <= 0.001f;
        }
    }
}
