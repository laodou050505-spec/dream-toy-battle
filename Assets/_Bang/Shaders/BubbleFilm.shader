// 门洞上的彩虹泡泡膜：极淡的薄膜干涉色 + 缓慢流动的涟漪 + 边缘柔光，让门洞像一层魔法泡泡
Shader "Bang/BubbleFilm"
{
    Properties
    {
        _Strength ("强度", Range(0,1)) = 0.12
        _EdgeGlow ("边缘光", Range(0,2)) = 0.45
        _EdgeColor ("边缘光色", Color) = (1,0.95,0.85,1)
        _Speed ("流动速度", Range(0,2)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half _Strength, _EdgeGlow, _Speed; half4 _EdgeColor; CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 ws : TEXCOORD2; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; o.n = TransformObjectToWorldNormal(v.normalOS); o.ws = TransformObjectToWorld(v.positionOS.xyz); return o; }
            half3 hue(half h) { return saturate(abs(frac(h + half3(0, 0.333, 0.667)) * 6 - 3) - 1); }
            half4 frag(V i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float3 vd = normalize(GetWorldSpaceViewDir(i.ws));
                half ndv = abs(dot(normalize(i.n), vd));
                float swirl = sin(i.uv.x * 7 + t * 3 + sin(i.uv.y * 5 - t * 2) * 1.5) * 0.5 + sin(i.uv.y * 9 + t * 2.3) * 0.5;
                half3 film = hue(ndv * 0.9 + swirl * 0.18 + t * 0.05) * _Strength * (0.35 + 0.65 * (1 - ndv));
                // 门洞边缘（uv 0..1）柔光
                float2 e = min(i.uv, 1 - i.uv);
                half edge = pow(saturate(1 - min(e.x, e.y) * 8), 3) * _EdgeGlow;
                half3 col = film + _EdgeColor.rgb * edge;
                half a = saturate(max(max(film.r, film.g), film.b) * 0.6 + edge * 0.6);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
