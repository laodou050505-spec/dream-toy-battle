// 手作卡片（菜单板）：厚牛皮纸板模切成圆角矩形，上面贴一张带毛边的奶油色纸。纹理是真实材质照片（nano banana 生成的纸 / 牛皮纸板微距，一张铺满整张卡片，不平铺），
// 形状全部程序化（任意尺寸都锐利）：纸板切边略深、纸压在纸板上有一圈细影、毛边按噪声不规则起伏、整张卡片下方柔和落影。
// 四边形要比卡片大一圈（留投影）：_Size = 卡片宽高（米），_Pad = 四周留白（米）。
Shader "Bang/UICraft"
{
    Properties
    {
        _KraftTex ("牛皮纸板纹理", 2D) = "white" {}
        _PaperTex ("纸纹理", 2D) = "white" {}
        _KraftTint ("纸板色", Color) = (1,1,1,1)
        _PaperTint ("纸色", Color) = (1,1,1,1)
        _TexScale ("纹理密度(每米重复)", Float) = 1
        _PaperContrast ("纸纹对比度", Range(0,1)) = 0.4
        _KraftContrast ("纸板纹对比度", Range(0,1)) = 0.75
        _Radius ("圆角(米)", Float) = 0.035
        _Inset ("纸离纸板边(米)", Float) = 0.022
        _Deckle ("毛边起伏(米)", Float) = 0.0025
        _Size ("卡片宽高(米)", Vector) = (0.6,0.4,0,0)
        _Pad ("四周留白(米)", Float) = 0.05
        _Shadow ("投影浓度", Range(0,1)) = 0.32
        _Alpha ("整体透明度", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" }
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
            TEXTURE2D(_KraftTex); SAMPLER(sampler_KraftTex); TEXTURE2D(_PaperTex); SAMPLER(sampler_PaperTex);
            CBUFFER_START(UnityPerMaterial) half4 _KraftTint, _PaperTint; float _TexScale, _PaperContrast, _KraftContrast, _Radius, _Inset, _Deckle, _Pad, _Shadow, _Alpha; float4 _Size, _KraftTex_ST, _PaperTex_ST; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }
            float sdRound(float2 p, float2 b, float r) { float2 q = abs(p) - b + r; return length(max(q, 0)) + min(max(q.x, q.y), 0) - r; }
            float h12(float2 p) { p = frac(p * float2(0.1031, 0.103)); p += dot(p, p.yx + 33.33); return frac((p.x + p.y) * p.x); }
            float vn(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(h12(i), h12(i + float2(1, 0)), f.x), lerp(h12(i + float2(0, 1)), h12(i + 1), f.x), f.y); }
            half4 frag(V i) : SV_Target
            {
                float2 full = _Size.xy + 2 * _Pad;
                float2 p = (i.uv - 0.5) * full;
                float d = sdRound(p, _Size.xy * 0.5, _Radius);
                float aa = fwidth(d) * 1.2;
                float inside = 1 - smoothstep(-aa, aa, d);
                float ds = sdRound(p + float2(0, _Pad * 0.3), _Size.xy * 0.5, _Radius);
                float shadow = (1 - smoothstep(0, _Pad * 0.85, ds)) * _Shadow;
                // 牛皮纸板：真实纹理 + 切边一圈略深（模切的纤维断面）+ 左上受光的一道细亮边
                half3 kraft = SAMPLE_TEXTURE2D(_KraftTex, sampler_KraftTex, p * _TexScale + 0.5).rgb;
                kraft = lerp(half3(0.78, 0.6, 0.4), kraft, _KraftContrast) * _KraftTint.rgb;          // 纹理只留纤维质感，底色统一成暖蜂蜜色
                kraft *= lerp(0.8, 1.0, smoothstep(0.0, -0.005, d));
                kraft += 0.06 * smoothstep(-0.004, -0.0015, d) * saturate(dot(normalize(p + 1e-5), float2(-0.5, 0.85)));
                // 贴上去的纸：比纸板小一圈，边缘按噪声起伏（手撕 / 冷压纸的毛边）
                float dn = (vn(p * 70) - 0.5) * _Deckle * 2 + (vn(p * 230 + 7) - 0.5) * _Deckle * 0.8;
                float dp = sdRound(p, _Size.xy * 0.5 - _Inset, max(0.004, _Radius - _Inset * 0.7)) + dn;
                float aap = fwidth(dp) * 1.2;
                float paper = 1 - smoothstep(-aap, aap, dp);
                kraft *= 1 - 0.28 * (1 - smoothstep(0.0, 0.007, dp - 0.0015)) * (1 - paper);   // 纸压在纸板上的细影（偏下）
                half3 pt = SAMPLE_TEXTURE2D(_PaperTex, sampler_PaperTex, p * _TexScale + 0.5).rgb;
                half3 pc = (half3(1.0, 0.965, 0.9) + (pt - 0.86) * _PaperContrast) * _PaperTint.rgb;   // 暖奶油纸：只叠一层淡淡的纤维
                pc *= lerp(0.94, 1.0, smoothstep(0.0, -0.01, dp));                                 // 毛边一圈纸纤维略透、略暗
                half3 rgb = lerp(kraft, pc, paper);
                rgb = inside > 0.001 ? rgb : half3(0.25, 0.16, 0.12);                            // 卡片外只剩暖褐色投影
                return half4(rgb, max(inside, shadow * (1 - inside)) * _Alpha);
            }
            ENDHLSL
        }
    }
}
