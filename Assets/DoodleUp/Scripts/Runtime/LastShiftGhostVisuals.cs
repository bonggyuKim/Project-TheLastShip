using UnityEngine;
using UnityEngine.Rendering;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 유령 실루엣의 표현 정본(기획 §4.4 N11 구현물 4).
    ///
    /// <b>새 에셋을 만들지 않는다.</b> 기존 승무원 메시의 머티리얼을 반투명으로 바꾸는 것이
    /// 전부이며, 그래서 이 파일이 아는 것은 "어떤 셰이더 상태가 반투명인가" 하나다. 유령을
    /// 별도 프리팹으로 만들면 사망 순간에 오브젝트를 갈아끼워야 하고, 그러면 소유권·
    /// NetworkObject·카메라가 전부 다시 붙어야 한다 — 표현 하나를 위해 치를 값이 아니다.
    ///
    /// 색은 <see cref="LastShiftNetworkPlayer.PlayerColor"/> 를 그대로 쓴다. 유령이 되면서
    /// 색까지 회색으로 바꾸면 "누가 죽었는지" 를 알아보는 단서가 사라지는데, 산 사람이
    /// 유령을 보고 대화를 시작하는 것이 이 표현의 유일한 목적이다.
    /// </summary>
    public static class LastShiftGhostVisuals
    {
        /// <summary>
        /// 반투명 실루엣의 알파. 너무 낮으면 어두운 구역에서 유령이 안 보여 "동료가 어디를
        /// 보고 있는지" 라는 이 표현의 목적이 사라지고, 너무 높으면 산 사람과 구분되지 않는다.
        /// </summary>
        public const float GhostAlpha = 0.35f;

        /// <summary>반투명 큐. Built-in Standard 셰이더의 Transparent 모드 기본값이다.</summary>
        public const int TransparentRenderQueue = (int)RenderQueue.Transparent;

        /// <summary>불투명 큐. 유령을 되돌릴 때(프리셋 리셋) 원래 상태로 돌아갈 목적지다.</summary>
        public const int OpaqueRenderQueue = (int)RenderQueue.Geometry;

        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        /// <summary>
        /// 머티리얼을 유령/생존 상태로 맞춘다. 알파만 바꾸면 Standard 셰이더는 여전히 불투명
        /// 큐에서 그려서 화면상 아무 변화가 없다 — 블렌드 모드·ZWrite·렌더 큐가 함께 가야 한다.
        /// </summary>
        public static void Apply(Material material, bool isGhost, Color playerColor)
        {
            if (material == null) return;

            var color = playerColor;
            color.a = isGhost ? GhostAlpha : 1f;
            material.color = color;

            // 셰이더가 Standard 계열이 아니면 블렌드 상태를 건드릴 수 없다. 색만 넣고 나간다 —
            // 여기서 없는 프로퍼티를 쓰면 EditMode 테스트가 셰이더 경고로 시끄러워진다.
            if (!material.HasProperty(ModeId)) return;

            if (isGhost)
            {
                material.SetFloat(ModeId, 3f);
                material.SetInt(SrcBlendId, (int)BlendMode.SrcAlpha);
                material.SetInt(DstBlendId, (int)BlendMode.OneMinusSrcAlpha);
                material.SetInt(ZWriteId, 0);
                material.EnableKeyword("_ALPHABLEND_ON");
                material.renderQueue = TransparentRenderQueue;
                return;
            }

            material.SetFloat(ModeId, 0f);
            material.SetInt(SrcBlendId, (int)BlendMode.One);
            material.SetInt(DstBlendId, (int)BlendMode.Zero);
            material.SetInt(ZWriteId, 1);
            material.DisableKeyword("_ALPHABLEND_ON");
            material.renderQueue = OpaqueRenderQueue;
        }
    }
}
