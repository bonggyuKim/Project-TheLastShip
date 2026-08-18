using UnityEngine;

namespace DoodleUp.Runtime
{
    /// <summary>
    /// 접촉 하나가 살을 어떻게 밀어내는가 — <b>눌림과 그 둘레의 불룩함</b>의 정본.
    ///
    /// <b>왜 두 항인가.</b> 누르기만 하면 부피가 사라진다. Blender Soft Body 에 부피 보존이
    /// 없어 배 표면이 액체처럼 흐른 것이 그 증상이었고, 강성을 올려 막으면 변형 자체가
    /// 사라졌다. 여기서는 누른 만큼을 <b>둘레로 밀어내</b> 부피를 근사 보존한다.
    ///
    /// <b>이 파일이 정본이고 셰이더는 사본이다.</b> 같은 식을 HLSL 과 C# 에 두 벌 적으면
    /// 반드시 갈린다. <c>LastShiftBodyDeform.shader</c> 안의 <c>lsDeformWeight</c> /
    /// <c>lsDeformBulge</c> 는 아래 함수와 <b>같은 식이어야 하고</b>, 그것을
    /// <see cref="DoodleUp.Tests.EditMode"/> 의 부피 검사가 이 쪽으로 고정한다.
    /// </summary>
    public static class LastShiftBodyDeformKernel
    {
        /// <summary>
        /// 둘레 불룩함의 이득. <b>임의로 고른 값이 아니라 풀어서 나온 값이다.</b>
        ///
        /// 평평한 판에서 변위를 반경 방향으로 적분해 0 이 되게 두면
        /// <c>∫₀¹ W(t)·t dt = k·∫₀¹ B(t)·t dt</c> 이고,
        /// <c>∫ (1-t²)²·t dt = 1/6</c>, <c>∫ t²(1-t)²·t dt = 1/60</c> 이므로 <c>k = 10</c> 이다.
        ///
        /// 곡면에서는 근사다 — 접촉 반경이 부위 굵기보다 훨씬 크면 오차가 커진다.
        /// 그래서 반경은 부위 굵기의 절반 언저리로 잡는 것을 기본으로 한다.
        /// </summary>
        public const float BulgeGain = 10f;

        /// <summary>눌림 가중치 <c>W(t) = (1-t²)²</c>. <c>t</c> 는 접촉 반경으로 정규화한 거리.</summary>
        public static float Weight(float t)
        {
            if (t >= 1f || t < 0f) return 0f;
            var s = 1f - t * t;
            return s * s;
        }

        /// <summary>
        /// 둘레 불룩함 <c>B(t) = t²(1-t)²</c>. 접촉점(<c>t=0</c>)과 경계(<c>t=1</c>)에서 0 이라
        /// 눌린 한복판이 부풀지 않고 커널 밖으로 이음매가 안 생긴다.
        /// </summary>
        public static float Bulge(float t)
        {
            if (t >= 1f || t < 0f) return 0f;
            var s = t * (1f - t);
            return s * s;
        }

        /// <summary>
        /// 정점 하나에 접촉 하나를 먹인 변위. 오브젝트 공간에서 계산한다.
        /// </summary>
        /// <param name="vertex">정점 위치(스키닝 후).</param>
        /// <param name="vertexNormal">정점 법선. 불룩함이 밀려나는 방향이다.</param>
        /// <param name="contact">접촉점.</param>
        /// <param name="contactNormal">접촉 법선. 눌림은 이 반대로 들어간다.</param>
        /// <param name="radius">접촉 반경. 0 이하면 변위 없음.</param>
        /// <param name="depth">눌림 깊이. 스프링이 흔드는 값이 이것이다.</param>
        public static Vector3 Displace(
            Vector3 vertex,
            Vector3 vertexNormal,
            Vector3 contact,
            Vector3 contactNormal,
            float radius,
            float depth)
        {
            if (radius <= 0.0001f || depth == 0f) return Vector3.zero;
            var t = (vertex - contact).magnitude / radius;
            if (t >= 1f) return Vector3.zero;
            return -contactNormal * (depth * Weight(t))
                   + vertexNormal * (depth * BulgeGain * Bulge(t));
        }
    }
}
