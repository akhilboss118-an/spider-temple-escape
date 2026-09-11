Shader "Custom/AAA_ShieldAura"
{
    Properties
    {
        [Header(Aura Settings)]
        _AuraColor ("Shield Color", Color) = (0.2, 0.85, 1.0, 0.15)
        _RimColor ("Rim Color", Color) = (0.4, 0.95, 1.0, 1.0)
        _RimPower ("Rim Power", Range(1.0, 6.0)) = 2.5
        _RimIntensity ("Rim Intensity", Range(0.0, 4.0)) = 2.5
        _PulseSpeed ("Pulse Speed", Range(0.0, 5.0)) = 2.0
        _NoiseScale ("Hex/Noise Scale", Range(0.1, 10.0)) = 3.0
        _NoiseSpeed ("Noise Flow Speed", Range(0.0, 2.0)) = 0.5
        _ScanSpeed ("Scan Line Speed", Range(0.0, 4.0)) = 1.5
        _FresnelPower ("Fresnel Glow Power", Range(0.5, 5.0)) = 2.0
        _FresnelIntensity ("Fresnel Glow Intensity", Range(0.0, 3.0)) = 1.5
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        CGPROGRAM
        #pragma surface surf NoLighting alpha:fade
        #pragma target 3.0

        sampler2D _MainTex;
        struct Input
        {
            float3 worldNormal;
            float3 viewDir;
            float3 worldPos;
            float4 screenPos;
            float depth;
        };

        fixed4 _AuraColor;
        fixed4 _RimColor;
        half _RimPower;
        half _RimIntensity;
        half _PulseSpeed;
        half _NoiseScale;
        half _NoiseSpeed;
        half _ScanSpeed;
        half _FresnelPower;
        half _FresnelIntensity;

        // Procedural hex-like noise
        float hexNoise(float2 p)
        {
            float2 h = float2(0.0, 1.0);
            float2 a = floor(p);
            float2 d = frac(p);
            float v = a.x + a.y * 5.0;
            vec4 r = vec4(a.xy, a.xy + 1.0);
            r.xy = r.xy - float2(floor(r.x + r.y * 0.5), 0.0);
            r.zw = r.zw - float2(floor(r.z + r.w * 0.5), 0.0);
            float2 v1 = float2(floor(0.5 + d.x), floor(d.y));
            float2 v2 = float2(floor(d.x), floor(0.5 + d.y));
            float c1 = abs(dot(r.xy - d, r.xy - d));
            float c2 = abs(dot(r.zw - d, r.zw - d));
            float c3 = abs(dot(float2(v1.x - d.x, v1.y - d.y), float2(v1.x - d.x, v1.y - d.y)));
            float c4 = abs(dot(float2(v2.x - d.x, v2.y - d.y), float2(v2.x - d.x, v2.y - d.y)));
            float n = min(min(c1, c2), min(c3, c4));
            return 1.0 - smoothstep(0.0, 0.25, n);
        }

        float simpleNoise(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            // Fresnel / rim glow
            half NdotV = max(0, dot(IN.worldNormal, normalize(IN.viewDir)));
            half fresnel = pow(1.0 - NdotV, _RimPower);
            half pulse = 0.75 + 0.25 * sin(_Time.y * _PulseSpeed);

            // Animated hex grid pattern
            float2 uvNoise = IN.worldPos.xz * _NoiseScale + float2(_Time.y * _NoiseSpeed, _Time.y * _NoiseSpeed * 0.7);
            float hex = hexNoise(uvNoise);
            float hex2 = hexNoise(uvNoise * 1.5 + float2(10.0, 5.0));

            // Flowing energy pattern
            float flow = hex * 0.6 + hex2 * 0.4;
            flow = pow(flow, 0.8);

            // Scan line effect (energy flowing around the sphere)
            float scanY = IN.worldPos.y + _Time.y * _ScanSpeed;
            float scanLine = pow(0.5 + 0.5 * sin(scanY * 8.0), 4.0);

            // Combine
            float aura = fresnel * flow + scanLine * fresnel * 0.4;
            aura *= pulse;

            fixed4 rimCol = _RimColor * fresnel * _RimIntensity * pulse;
            fixed4 glowCol = _AuraColor * (aura + scanLine * 0.3) * pulse;

            o.Albedo = glowCol.rgb + rimCol.rgb;
            o.Alpha = clamp((fresnel * _FresnelIntensity + aura * 0.5) * pulse, 0.0, 0.85);
            o.Emission = o.Albedo * 1.5;
            o.Alpha = clamp((fresnel * _FresnelIntensity + aura * 0.5) * pulse * 1.2, 0.0, 0.9);
        }
        ENDCG
    }

    FallBack "Particles/Additive"
}
