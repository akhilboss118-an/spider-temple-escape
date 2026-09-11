Shader "Custom/AAA_RimGlow"
{
    Properties
    {
        [Header(Base)]
        _Color ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

        [Header(Rim Glow)]
        _RimColor ("Rim Color", Color) = (0.0, 0.8, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.5, 6.0)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0.0, 5.0)) = 2.0
        _RimPulseSpeed ("Rim Pulse Speed", Range(0.0, 4.0)) = 1.5

        [Header(Emissive Edge)]
        _EmissiveColor ("Emissive Edge Color", Color) = (1.0, 0.5, 0.0, 1.0)
        _EmissiveIntensity ("Emissive Intensity", Range(0.0, 4.0)) = 1.0
        _EmissivePower ("Emissive Power", Range(1.0, 8.0)) = 4.0

        [Header(SSS Approx)]
        _SSSColor ("Subsurface Color", Color) = (0.2, 0.8, 0.3, 1.0)
        _SSSPower ("SSS Power", Range(1.0, 8.0)) = 4.0
        _SSSStrength ("SSS Strength", Range(0.0, 1.0)) = 0.3

        [Header(Biome Tint)]
        _BiomeTint ("Biome Tint Color", Color) = (1,1,1,1)
        _BiomeTintStrength ("Biome Tint Strength", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        struct Input
        {
            float2 uv_MainTex;
            float3 worldNormal;
            float3 viewDir;
            float3 worldPos;
        };

        fixed4 _Color;
        half _Glossiness;
        half _Metallic;

        fixed4 _RimColor;
        half _RimPower;
        half _RimIntensity;
        half _RimPulseSpeed;

        fixed4 _EmissiveColor;
        half _EmissiveIntensity;
        half _EmissivePower;

        fixed4 _SSSColor;
        half _SSSPower;
        half _SSSStrength;

        fixed4 _BiomeTint;
        half _BiomeTintStrength;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            // Apply biome tint
            c.rgb = lerp(c.rgb, c.rgb * _BiomeTint.rgb, _BiomeTintStrength);

            o.Albedo = c.rgb;
            o.Smoothness = _Glossiness;
            o.Metallic = _Metallic;

            // Fresnel / Rim glow
            half NdotV = max(0, dot(IN.worldNormal, normalize(IN.viewDir)));
            half fresnel = pow(1.0 - NdotV, _RimPower);
            half pulse = 0.8f + 0.2f * sin(_Time.y * _RimPulseSpeed);
            fixed4 rimGlow = _RimColor * fresnel * _RimIntensity * pulse;

            // Emissive edge (edges facing perpendicular to view)
            half emissiveEdge = pow(fresnel, _EmissivePower);
            fixed4 emissive = _EmissiveColor * emissiveEdge * _EmissiveIntensity;

            // Subsurface scattering approximation (light wraps around geometry)
            half sss = pow(max(0, dot(IN.worldNormal, -normalize(IN.viewDir)) + 0.5), _SSSPower);
            fixed4 sssColor = _SSSColor * sss * _SSSStrength;

            // Combine: base + rim glow + emissive + SSS
            o.Emission = rimGlow.rgb + emissive.rgb + sssColor.rgb;
        }
        ENDCG
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _RimColor;
            float _RimPower;
            float _RimIntensity;
            float _RimPulseSpeed;
            float4 _EmissiveColor;
            float _EmissiveIntensity;
            float _EmissivePower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldViewDir : TEXCOORD2;
                float3 worldPos : TEXCOORD3;
                SHADOW_COORDS(4)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldViewDir = normalize(_WorldSpaceCameraPos - o.worldPos);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;

                // Basic diffuse
                half NdotL = max(0, dot(i.worldNormal, _WorldSpaceLightPos0.xyz));
                fixed shadow = SHADOW_ATTENUATION(i);
                fixed3 diffuse = albedo.rgb * _LightColor0.rgb * NdotL * shadow;
                fixed3 ambient = albedo.rgb * unity_AmbientEquator.rgb * 0.3;

                // Rim glow
                half NdotV = max(0, dot(i.worldNormal, i.worldViewDir));
                half fresnel = pow(1.0 - NdotV, _RimPower);
                half pulse = 0.8 + 0.2 * sin(_Time.y * _RimPulseSpeed);
                fixed3 rim = _RimColor.rgb * fresnel * _RimIntensity * pulse;

                // Emissive edge
                half emissiveEdge = pow(fresnel, _EmissivePower);
                fixed3 emission = _EmissiveColor.rgb * emissiveEdge * _EmissiveIntensity;

                fixed4 c;
                c.rgb = ambient + diffuse + rim + emission;
                c.a = albedo.a;
                return c;
            }
            ENDCG
        }

        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }

    FallBack "Mobile/VertexLit"
}
