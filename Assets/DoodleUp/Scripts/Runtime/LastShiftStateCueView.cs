using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 상태 단서(서리·그을음)를 <b>그 구역이 실제로 나쁠 때만</b> 보이게 한다.
    ///
    /// <b>이 컴포넌트가 생기기 전에는 조건이 아예 없었다.</b> 판은 씬에 정적으로 깔려 있었고
    /// 켜고 끄는 코드가 한 줄도 없어서, 냉각이 멀쩡해도 벽에 서리가 껴 있었다 — 사용자가
    /// "상시 바닥에 서 보인다" 로 지적한 것이 그 상태다.
    ///
    /// <b>보간을 끊지 않는다.</b> 임계 근처에서 값이 떨려도 목표만 바뀔 뿐 지금 진하기는
    /// 이어서 움직인다. 켜고 끄는 것을 즉시로 두면 그 자리에서 깜빡인다.
    ///
    /// <b>게임플레이에 손대지 않는다.</b> 여기서 만지는 것은 렌더러와 색뿐이고, 콜라이더도
    /// 상태도 안 건드린다 — 이 판은 원래 콜라이더가 없다(드레싱 소품 경로가 지운다).
    /// </summary>
    /// <remarks>
    /// <b>편집 모드에서도 돈다.</b> 플레이 중에만 끄면 씬을 여는 사람에게는 여전히 서리가
    /// 껴 있고, 아트·사용자가 보는 화면이 바로 그 편집 모드다. 안 돌 때 목표가 <c>0</c> 이라
    /// (샌드박스가 없으면 <see cref="ShouldShow"/> 가 거짓) 저절로 걷힌다.
    /// </remarks>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class LastShiftStateCueView : MonoBehaviour
    {
        /// <summary>켜지는 데 걸리는 시간. 서리는 순간에 끼지 않는다(game-art 확정).</summary>
        public const float FadeInSeconds = 0.8f;

        /// <summary>걷히는 데 걸리는 시간. <b>끼는 것보다 느리다</b> — 녹는 쪽이 더 오래 간다.</summary>
        public const float FadeOutSeconds = 1.2f;

        /// <summary>
        /// 이 등급부터 단서가 뜬다. <b>위기 하나만 본다</b> — 불안정에서도 뜨면 "나쁘다" 가
        /// 두 단계가 되어, 판이 떠 있는 것 자체가 정보를 잃는다.
        /// </summary>
        public const LastShiftSituationGrade ShowFrom = LastShiftSituationGrade.Crisis;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private LastShiftZone zone;

        private MeshRenderer[] renderers;
        private MaterialPropertyBlock block;
        private float amount;
        private LastShiftSandboxController sandbox;

        /// <summary>지금 진하기. <c>0</c> 이면 완전히 안 보인다.</summary>
        public float Amount => amount;

        /// <summary>어느 구역의 상태를 보는가.</summary>
        public LastShiftZone Zone => zone;

        public void Configure(LastShiftZone room)
        {
            zone = room;
        }

        /// <summary>그 구역이 지금 단서를 띄울 만한 상태인가.</summary>
        public bool ShouldShow
        {
            get
            {
                var controller = Sandbox;
                if (controller == null) return false;
                return LastShiftSituationTable.GradeOf(controller.DominantSituationOf(zone)) >= ShowFrom;
            }
        }

        private LastShiftSandboxController Sandbox =>
            sandbox != null ? sandbox : sandbox = FindFirstObjectByType<LastShiftSandboxController>();

        private void OnEnable()
        {
            renderers = GetComponentsInChildren<MeshRenderer>(true);
            block = new MaterialPropertyBlock();
            // 첫 프레임부터 안 보인다. 켜진 채로 시작하면 씬을 여는 순간이 곧 "고장난 배" 다.
            amount = 0f;
            Apply();
        }

        private void Update()
        {
            var target = ShouldShow ? 1f : 0f;
            if (!Mathf.Approximately(amount, target))
            {
                var seconds = target > amount ? FadeInSeconds : FadeOutSeconds;
                amount = Mathf.MoveTowards(amount, target, Time.deltaTime / seconds);
                Apply();
            }
        }

        private void Apply()
        {
            if (renderers == null) return;
            var visible = amount > 0.001f;

            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                // 완전히 안 보일 때는 렌더러를 끈다 — 알파 0 인 판을 계속 그리면 그리는 값이
                // 없는데도 투명 정렬 비용을 낸다.
                renderer.enabled = visible;
                if (!visible) continue;

                renderer.GetPropertyBlock(block);
                var color = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(ColorId)
                    ? renderer.sharedMaterial.GetColor(ColorId)
                    : Color.white;
                block.SetColor(ColorId, new Color(color.r, color.g, color.b, color.a * amount));
                renderer.SetPropertyBlock(block);
            }
        }

        /// <summary>검사가 시계를 직접 돌린다. 씬 없이 보간만 재려는 자리다.</summary>
        public void TickForProbe(bool show, float deltaTime)
        {
            var target = show ? 1f : 0f;
            if (Mathf.Approximately(amount, target)) return;
            var seconds = target > amount ? FadeInSeconds : FadeOutSeconds;
            amount = Mathf.MoveTowards(amount, target, deltaTime / seconds);
            Apply();
        }
    }
}
