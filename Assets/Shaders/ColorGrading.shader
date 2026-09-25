Shader "Hidden/SpiderTempleEscape_ColorGrading"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Saturation ("Saturation", Float) = 1.15
        _Contrast ("Contrast", Float) = 1.10
        _Temperature ("Temperature", Float) = 0.05
        _VignetteIntensity ("Vignette Intensity", Float) = 0.35
        _VignetteSmoothness ("Vignette Smoothness", Float) = 0.8
        _BiomeTint ("Biome Tint", Color) = (1, 1, 1, 1)
        _SpeedBlur ("Speed Blur", Float) = 0
        _ChromaticAberration ("Chromatic Aberration", Float) = 0
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float _Saturation;
            float _Contrast;
            float _Temperature;
            float _VignetteIntensity;
            float _VignetteSmoothness;
            float _SpeedBlur;
            float _ChromaticAberration;
            float4 _BiomeTint;

            static const float LUMA_R = 0.2126;
            static const float LUMA_G = 0.7152;
            static const float LUMA_B = 0.0722;

            fixed4 frag(v2f i) : SV_Target
            {
                float2 center = i.uv - 0.5;
                float dist = length(center);

                float3 col;

                if (_SpeedBlur > 0.0005 || _ChromaticAberration > 0.00005)
                {
                    // Radial speed blur + chromatic aberration (fast path only at speed)
                    float blur = _SpeedBlur * dist;
                    float ca = _ChromaticAberration * dist;
                    float3 acc = 0;
                    [unroll]
                    for (int s = 0; s < 5; s++)
                    {
                        float t = (s / 4.0) - 0.5;
                        float2 uv = i.uv - center * blur * t;
                        acc.r += tex2D(_MainTex, uv + center * ca).r;
                        acc.g += tex2D(_MainTex, uv).g;
                        acc.b += tex2D(_MainTex, uv - center * ca).b;
                    }
                    col = acc / 5.0;
                }
                else
                {
                    col = tex2D(_MainTex, i.uv).rgb;
                }

                // Warm / cool white balance
                col.r *= 1.0 + _Temperature;
                col.b *= 1.0 - _Temperature;

                // Contrast around mid grey
                col = (col - 0.5) * _Contrast + 0.5;

                // Saturation
                float luma = dot(col, float3(LUMA_R, LUMA_G, LUMA_B));
                col = lerp(luma, col, _Saturation);

                // Per-biome colour tint
                col *= _BiomeTint.rgb;

                // Vignette (softens edges of the frame)
                float edge0 = 0.34;
                float edge1 = edge0 + max(0.05, 0.50 * _VignetteSmoothness);
                float vig = smoothstep(edge0, edge1, dist);
                col *= 1.0 - saturate(_VignetteIntensity) * vig;

                return fixed4(saturate(col), 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
