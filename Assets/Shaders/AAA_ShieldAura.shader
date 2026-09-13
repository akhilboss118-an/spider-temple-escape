Shader "Custom/AAA_ShieldAura"
{
    Properties
    {
        _AuraColor ("Aura Base Color (Center)", Color) = (0.04, 0.40, 1.0, 0.05)
        _RimColor ("Rim Glow Color (Edge)", Color) = (0.25, 0.85, 1.0, 0.95)
        _RimPower ("Rim Sharpness", Range(1.0, 6.0)) = 3.2
        _RimIntensity ("Rim Brightness", Range(0.5, 4.0)) = 2.4
        _PulseSpeed ("Pulse Frequency", Range(0.0, 6.0)) = 2.2
        _HexScale ("Hex Grid Scale", Range(4.0, 30.0)) = 14.0
        _HexIntensity ("Hex Grid Visibility", Range(0.0, 1.0)) = 0.25
        _ScanSpeed ("Wave Scan Speed", Range(0.0, 6.0)) = 1.8
    }

    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200

        // Crystal blue transparent forcefield: standard alpha blending reveals the player inside
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                half3 viewDir : TEXCOORD2;
            };

            fixed4 _AuraColor;
            fixed4 _RimColor;
            half _RimPower;
            half _RimIntensity;
            half _PulseSpeed;
            half _HexScale;
            half _HexIntensity;
            half _ScanSpeed;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                return o;
            }

            // Mobile-optimized procedural hex lattice
            half hexLattice(float2 p)
            {
                float2 q = float2(p.x * 0.866025, p.y + p.x * 0.5);
                float2 pi = floor(q);
                float2 pf = frac(q);
                if (pf.x + pf.y > 1.0)
                {
                    pf -= float2(1.0, 1.0);
                }
                float d = min(min(abs(pf.x), abs(pf.y)), abs(1.0 - pf.x - pf.y));
                return smoothstep(0.0, 0.08, d);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half3 n = normalize(i.worldNormal);
                half3 v = normalize(i.viewDir);
                half NdotV = saturate(dot(n, v));

                // Sharp Fresnel rim: high on outer contours, near 0 in center
                half fresnel = pow(1.0 - NdotV, _RimPower) * _RimIntensity;

                // Gentle rhythmic pulse
                half pulse = 0.88 + 0.12 * sin(_Time.y * _PulseSpeed);

                // Subtle hexagonal forcefield shimmer (active mainly on edges so center stays crystal clear)
                float2 hexUV = i.worldPos.xz * _HexScale + float2(_Time.y * 0.3, _Time.y * 0.4);
                half hex = (1.0 - hexLattice(hexUV)) * _HexIntensity;

                // Gentle vertical energy ripple wave
                half wave = 0.5 + 0.5 * sin(i.worldPos.y * 5.0 - _Time.y * _ScanSpeed);
                wave = pow(wave, 4.0) * 0.3;

                // Color interpolation: deep transparent blue in center, neon cyan on rim
                fixed4 col;
                col.rgb = lerp(_AuraColor.rgb, _RimColor.rgb, saturate(fresnel));

                // Add energy highlights
                half edgeEnergy = (fresnel + (hex + wave) * saturate(fresnel * 1.5)) * pulse;
                col.rgb += _RimColor.rgb * edgeEnergy * 0.4;

                // Alpha: very faint in the center (~0.08) so the player is 100% visible, glowing bright on edges
                col.a = saturate(_AuraColor.a + edgeEnergy * _RimColor.a);

                return col;
            }
            ENDCG
        }
    }
    FallBack "Mobile/Particles/Alpha Blended"
}
