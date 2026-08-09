using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 선외 잔해 덩어리의 씬 표현 — <b>상태를 안 든다.</b> 정본은
    /// <see cref="LastShiftSalvage"/> 이고 여기서는 "지금 떠 있는가 · 어느 계열인가 · 몇 덩이
    /// 남았는가" 를 보이는 것으로만 옮긴다. <see cref="LastShiftAirlockHatch"/> 와 같은 규약이다.
    ///
    /// <b>덩이를 물체로 안 만드는 결정</b>(<see cref="LastShiftSalvage"/> 주석)이 여기서
    /// 그대로 드러난다: 덩이 하나가 게임오브젝트 하나가 아니라 <b>덩어리 하나에 붙은 조각
    /// <c>N</c>개</b>이고, 뜯을 때마다 뒤에서부터 꺼진다. 회수량이 눈으로 읽히면서도
    /// 저중력에서 떠다니는 물체가 하나도 안 생긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastShiftSalvageField : MonoBehaviour
    {
        [SerializeField] private Transform[] chunks = System.Array.Empty<Transform>();
        [SerializeField] private Renderer[] tinted = System.Array.Empty<Renderer>();

        private int shownChunks = -1;
        private LastShiftSalvageKind shownKind = (LastShiftSalvageKind)(-1);
        private MaterialPropertyBlock tintBlock;

        // URP/Lit 과 Built-in/Standard 가 서로 다른 이름을 쓴다. 씬 빌더가 어느 파이프라인에서
        // 돌든 같은 색이 나와야 해서 둘 다 넣는다 — 없는 프로퍼티는 조용히 무시된다.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        /// <summary>
        /// 계열별 색. <b>구간 자극과 같은 축으로 읽혀야 한다</b> — 냉각은 배 안 냉각 계통과
        /// 같은 청록, 전력은 경고 황색, 선체는 외피 회색이다. 새 팔레트를 안 만드는 것이
        /// 요점이고, 그래서 "직전 구간이 남긴 것" 이 색으로 먼저 읽힌다(§4.2).
        /// </summary>
        public static Color ColorOf(LastShiftSalvageKind kind) => kind switch
        {
            LastShiftSalvageKind.Cooling => new Color(0.30f, 0.66f, 0.70f),
            LastShiftSalvageKind.Power => new Color(0.78f, 0.62f, 0.22f),
            _ => new Color(0.55f, 0.54f, 0.52f)
        };

        public void Configure(Transform[] chunkTransforms, Renderer[] tintedRenderers)
        {
            chunks = chunkTransforms ?? System.Array.Empty<Transform>();
            tinted = tintedRenderers ?? System.Array.Empty<Renderer>();
            Apply();
        }

        private void Awake() => Apply();

        private void Update() => Apply();

        /// <summary>
        /// 지금 상태를 씬에 반영한다. <b>바뀐 프레임에만 실제로 만진다</b> — 잔해는 기항마다
        /// 한 번 뜨고 뜯을 때만 줄어드는데, 매 프레임 <c>SetActive</c> 와 재질 색을 다시 쓰면
        /// 아무 일도 안 일어나는 항해 내내 그 비용을 문다.
        /// </summary>
        private void Apply()
        {
            var visible = LastShiftSalvage.HasField;
            var remaining = visible ? LastShiftSalvage.Remaining : 0;
            var kind = LastShiftSalvage.Kind;
            if (remaining == shownChunks && kind == shownKind) return;

            shownChunks = remaining;
            shownKind = kind;

            // 좌표는 씬 빌더가 이미 <see cref="LastShiftSalvage.FieldCenter"/> 로 놓았다.
            // 여기서 매번 다시 쓰면 배 루트가 원점이 아닌 구성에서 월드/로컬이 어긋난다.
            for (var index = 0; index < chunks.Length; index++)
            {
                if (chunks[index] == null) continue;
                chunks[index].gameObject.SetActive(index < remaining);
            }

            // 색은 프로퍼티 블록으로 넣는다. <c>sharedMaterial</c> 을 직접 쓰면 에디터에서
            // 재질 자산이 더럽혀지고(플레이를 멈춰도 색이 남는다), <c>material</c> 은 사본을
            // 만들어 씬 빌드 산출물에 이름 없는 재질이 늘어난다.
            tintBlock ??= new MaterialPropertyBlock();
            tintBlock.SetColor(BaseColorId, ColorOf(kind));
            tintBlock.SetColor(LegacyColorId, ColorOf(kind));
            foreach (var chunkRenderer in tinted)
            {
                if (chunkRenderer == null) continue;
                chunkRenderer.SetPropertyBlock(tintBlock);
            }
        }
    }
}
