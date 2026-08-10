Shader "DoodleUp/Last Shift Space Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.004, 0.008, 0.028, 1)
        _HorizonColor ("Horizon", Color) = (0.035, 0.018, 0.065, 1)
        _NebulaColor ("Nebula", Color) = (0.08, 0.16, 0.28, 1)
        _StarColor ("Stars", Color) = (0.78, 0.9, 1, 1)
        _StarDensity ("Star Density", Range(0.97, 0.9995)) = 0.993
        _StarIntensity ("Star Intensity", Range(0, 4)) = 1.5
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };

            half4 _ZenithColor;
            half4 _HorizonColor;
            half4 _NebulaColor;
            half4 _StarColor;
            half _StarDensity;
            half _StarIntensity;

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = input.vertex.xyz;
                return output;
            }

            float Hash(float3 cell)
            {
                cell = frac(cell * 0.1031);
                cell += dot(cell, cell.yzx + 33.33);
                return frac((cell.x + cell.y) * cell.z);
            }

            half4 frag(v2f input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                half horizon = pow(saturate(1.0 - abs(direction.y)), 3.0);
                half3 sky = lerp(_ZenithColor.rgb, _HorizonColor.rgb, horizon);

                half band = pow(saturate(1.0 - abs(dot(direction, normalize(float3(0.18, 0.91, 0.37))))), 7.0);
                sky += _NebulaColor.rgb * band * 0.32;

                float3 starCell = floor(direction * 420.0);
                half star = smoothstep(_StarDensity, 1.0, Hash(starCell));
                half twinkle = 0.72 + 0.28 * Hash(starCell.yzx + 19.7);
                sky += _StarColor.rgb * star * twinkle * _StarIntensity;
                return half4(sky, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
