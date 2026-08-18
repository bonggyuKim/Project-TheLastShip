// 승무원 몸의 국소 눌림. Built-in RP Surface Shader 라 Standard 조명을 그대로 쓴다.
//
// 커널 식(가중치·불룩함·이득 10)은 LastShiftBodyDeformKernel.cs 가 정본이고 여기는 사본이다.
// 두 벌이 갈리면 헤드리스 부피 검사가 먼저 깨지도록 테스트를 걸어 뒀다.
Shader "LastShift/BodyDeform"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.35
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        #define LS_DEFORM_SLOTS 8

        // xyz = 오브젝트 공간 접촉점, w = 접촉 반경
        float4 _LSDeformPosition[LS_DEFORM_SLOTS];
        // xyz = 오브젝트 공간 접촉 법선, w = 눌림 깊이
        float4 _LSDeformNormal[LS_DEFORM_SLOTS];
        float _LSDeformCount;

        sampler2D _MainTex;
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        struct Input { float2 uv_MainTex; };

        // W(t) = (1-t^2)^2 — 눌림
        float lsDeformWeight(float t)
        {
            float s = 1.0 - t * t;
            return s * s;
        }

        // B(t) = t^2 (1-t)^2 — 둘레 불룩함
        float lsDeformBulge(float t)
        {
            float s = t * (1.0 - t);
            return s * s;
        }

        // 부피 보존 이득. 평평한 판에서 적분을 0 으로 두면 (1/6) / (1/60) = 10 이 나온다.
        // 임의로 고른 값이 아니므로 눈으로 만지지 말 것 — 만지면 부피가 샌다.
        static const float LS_DEFORM_BULGE_GAIN = 10.0;

        float3 lsDeformOffset(float3 position, float3 normal)
        {
            float3 offset = float3(0, 0, 0);
            int count = (int)_LSDeformCount;
            for (int i = 0; i < LS_DEFORM_SLOTS; i++)
            {
                if (i >= count) break;

                float radius = _LSDeformPosition[i].w;
                float depth = _LSDeformNormal[i].w;
                if (radius <= 0.0001) continue;

                float t = length(position - _LSDeformPosition[i].xyz) / radius;
                if (t >= 1.0) continue;

                offset -= _LSDeformNormal[i].xyz * (depth * lsDeformWeight(t));
                offset += normal * (depth * LS_DEFORM_BULGE_GAIN * lsDeformBulge(t));
            }
            return offset;
        }

        void vert(inout appdata_full v)
        {
            float3 basePosition = v.vertex.xyz;
            float3 baseNormal = v.normal;
            float3 moved = basePosition + lsDeformOffset(basePosition, baseNormal);

            // 법선을 다시 만든다. 정점만 밀고 법선을 두면 눌린 자리의 음영이 안 따라와서
            // 실루엣만 들어가고 표면은 평평해 보인다. 접평면 위 두 점을 같은 커널로 밀어
            // 외적으로 새 법선을 뽑는다 — 해석적 기울기보다 짧고, 커널을 바꿔도 안 갈린다.
            float epsilon = 0.01;
            float3 tangent = v.tangent.xyz;
            float tangentLength = length(tangent);
            if (tangentLength > 0.0001)
            {
                tangent /= tangentLength;
                float3 bitangent = cross(baseNormal, tangent) * v.tangent.w;

                float3 alongTangent = basePosition + tangent * epsilon;
                float3 alongBitangent = basePosition + bitangent * epsilon;
                float3 movedTangent = alongTangent + lsDeformOffset(alongTangent, baseNormal);
                float3 movedBitangent = alongBitangent + lsDeformOffset(alongBitangent, baseNormal);

                float3 rebuilt = cross(movedTangent - moved, movedBitangent - moved);
                if (dot(rebuilt, rebuilt) > 0.000001)
                {
                    rebuilt = normalize(rebuilt);
                    // 외적 방향이 원래 법선과 반대로 나오는 접선 손잡이도 있다. 뒤집힌 쪽을
                    // 그대로 두면 그 면만 안팎이 바뀌어 검게 뜬다.
                    v.normal = dot(rebuilt, baseNormal) < 0.0 ? -rebuilt : rebuilt;
                }
            }

            v.vertex.xyz = moved;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
