// 贴纸按钮：厚贴纸的圆角形状 + 外圈白色模切描边 + 底色上叠一层淡淡的纸纹 + 顶部一条光泽 + 下方柔和落影。
// 颜色接口和 Bang/UIPanel 一样（_ColorA 上 / _ColorB 下），悬停变色、透明度由 UiKit / UiButton 统一设置。
Shader "Bang/UISticker"
{
    Properties
    {
        _ColorA ("上部颜色", Color) = (1,0.6,0.62,1)
        _ColorB ("下部颜色", Color) = (0.95,0.5,0.56,1)
        _BorderColor ("描边颜色", Color) = (1,0.99,0.97,1)
        _Border ("白边宽度(米)", Float) = 0.006
        _Radius ("圆角(米)", Float) = 0.03
        _Size ("宽高(米)", Vector) = (0.3,0.08,0,0)
        _Pad ("四周留白(米)", Float) = 0.02
        _Shadow ("投影浓度", Range(0,1)) = 0.3
        _Glow ("（兼容）", Range(0,1)) = 0
        _Gloss ("顶部光泽", Range(0,1)) = 0.35
        _Alpha ("整体透明度", Range(0,1)) = 1
        _PaperTex ("纸纹理", 2D) = "white" {}
        _Grain ("纸纹强度", Range(0,1)) = 0.35
        _TexScale ("纹理密度(每米重复)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+104" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_PaperTex); SAMPLER(sampler_PaperTex);
            CBUFFER_START(UnityPerMaterial) half4 _ColorA, _ColorB, _BorderColor; float _Border, _Radius, _Pad, _Shadow, _Glow, _Gloss, _Alpha, _Grain, _TexScale; float4 _Size, _PaperTex_ST; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }
            float sdRound(float2 p, float2 b, float r) { float2 q = abs(p) - b + r; return length(max(q, 0)) + min(max(q.x, q.y), 0) - r; }
            half4 frag(V i) : SV_Target
            {
                float2 full = _Size.xy + 2 * _Pad;
                float2 p = (i.uv - 0.5) * full;
                float d = sdRound(p, _Size.xy * 0.5, _Radius);
                float aa = fwidth(d) * 1.2;
                float inside = 1 - smoothstep(-aa, aa, d);
                float ds = sdRound(p + float2(0, _Pad * 0.35), _Size.xy * 0.5, _Radius);
                float shadow = (1 - smoothstep(0, _Pad * 0.9, ds)) * _Shadow;
                float t = saturate(p.y / _Size.y + 0.5);
                half4 c = lerp(_ColorB, _ColorA, t);
                half g = SAMPLE_TEXTURE2D(_PaperTex, sampler_PaperTex, p * _TexScale + 0.5).r;
                c.rgb *= lerp(1, 0.86 + g * 0.22, _Grain);                                       // 印在纸上的颜色：带一点纸纹
                c.rgb += smoothstep(0.6, 0.95, t) * smoothstep(-_Border - 0.002, -_Border - 0.012, d) * _Gloss * 0.35;   // 覆膜光泽
                float edge = smoothstep(-_Border - aa, -_Border + aa, d);                       // 白色模切边
                half3 white = _BorderColor.rgb * lerp(1, 0.93 + g * 0.08, _Grain);
                c.rgb = lerp(c.rgb, white, edge);
                c.a = lerp(c.a, _BorderColor.a * c.a, edge);
                half3 rgb = inside > 0.001 ? c.rgb : half3(0.22, 0.14, 0.12);
                return half4(rgb, max(c.a * inside, shadow * (1 - inside)) * _Alpha);
            }
            ENDHLSL
        }
    }
}
