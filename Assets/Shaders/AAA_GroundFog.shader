Shader "Custom/AAA_GroundFog"
{
    Properties
    {
        [Header(Color)]
        _FogColor ("Fog Color", Color) = (0.5, 0.6, 0.5, 0.4)
        _FogColor2 ("Fog Color 2 (Depth tint)", Color) = (0.3, 0.5, 0.4, 0.3)

        [Header(Density)]
        _FogDensity ("Fog Density", Range(0.0, 1.0)) = 0.5
        _FogSpeed ("Fog Movement Speed", Range(0.0, 2.0)) = 0.3
        _FogScale ("Fog Scale", Range(0.01, 5.0)) = 1.0

        [Header(Depth Fade)]
        _FadeStart ("Distance Fade Start", Range(0.0, 50.0)) = 10.0
        _FadeEnd ("Distance Fade End", Range(10.0, 100.0)) = 40.0
    }

    SubShader
    {
        Tags { "Queue"="Transparent-100" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        CGPROGRAM
        #pragma surface surf NoLighting alpha:fade
        #pragma target 3.0

        struct Input
        {
            float3 worldPos;
            float4 screenPos;
            float3 viewDir;
            float depth;
        };

        fixed4 _FogColor;
        fixed4 _FogColor2;
        half _FogDensity;
        half _FogSpeed;
        half _FogScale;
        half _FadeStart;
        half _FadeEnd;

        // Value noise
        float hash(float n) { return frac(sin(n) * 753.5453123); }
        float noise(float3 x)
        {
            float3 p = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            float n = p.x + p.y * 157.0 + 113.0 * p.z;
            return lerp(
                lerp(
                    lerp(hash(n + 0.0), hash(n + 1.0), f.x),
                    lerp(hash(n + 157.0), hash(n + 158.0), f.x),
                    f.y),
                lerp(
                    lerp(hash(n + 113.0), hash(n + 114.0), f.x),
                    lerp(hash(n + 270.0), hash(n + 271.0), f.x),
                    f.y),
                f.z);
        }

        float fbm(float3 p)
        {
            float f = 0.0;
            f += 0.5000 * noise(p); p *= 2.02;
            f += 0.2500 * noise(p); p *= 2.03;
            f += 0.1250 * noise(p); p *= 2.01;
            f += 0.0625 * noise(p);
            return f;
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            float2 uv = IN.worldPos.xz * _FogScale + float2(_Time.y * _FogSpeed, _Time.y * _FogSpeed * 0.7);

            float3 noiseCoord = float3(uv.x, _Time.y * _FogSpeed * 0.5, uv.y);
            float n = fbm(noiseCoord);
            float n2 = fbm(noiseCoord * 1.5 + float3(50.0, 30.0, 10.0));

            float density = (n * 0.6 + n2 * 0.4) * _FogDensity;
            density = smoothstep(0.2, 0.8, density);

            // Depth fade based on distance from camera
            float depth = length(IN.viewDir);
            float depthFade = 1.0 - smoothstep(_FadeStart, _FadeEnd, depth);

            // Fade based on Y position (fog hugs the ground)
            float heightFade = smoothstep(3.0, 0.0, IN.worldPos.y);

            // Blend two fog colors based on density
            fixed4 fogCol = lerp(_FogColor, _FogColor2, density * 0.5);

            float alpha = density * depthFade * heightFade * 0.6;

            o.Albedo = fogCol.rgb;
            o.Alpha = clamp(alpha, 0.0, 0.55);
            o.Emission = fogCol.rgb * 0.2;
        }
        ENDCG
    }

    FallBack "Particles/AlphaBlend"
}
