// 糖果卡片（程序化圆角矩形，任意尺寸都锐利）：上下渐变填充 + 描边 + 内发光 + 顶部高光带 + 外侧柔和投影。
// 四边形要比卡片大一圈（留投影的位置）：_Size = 卡片宽高（米），_Pad = 四周留白（米）。
Shader "Bang/UIPanel"
{
    Properties
    {
        _ColorA ("上部颜色", Color) = (1,0.94,0.97,0.96)
        _ColorB ("下部颜色", Color) = (1,0.84,0.9,0.96)
        _BorderColor ("描边颜色", Color) = (1,1,1,1)
        _Border ("描边宽度(米)", Float) = 0.006
        _Radius ("圆角(米)", Float) = 0.05
        _Size ("卡片宽高(米)", Vector) = (0.6,0.4,0,0)
        _Pad ("四周留白(米)", Float) = 0.05
        _Shadow ("投影浓度", Range(0,1)) = 0.35
        _Glow ("内发光", Range(0,1)) = 0.35
        _Gloss ("顶部高光", Range(0,1)) = 0.35
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
            CBUFFER_START(UnityPerMaterial) half4 _ColorA, _ColorB, _BorderColor; float _Border, _Radius, _Pad, _Shadow, _Glow, _Gloss, _Alpha; float4 _Size; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; return o; }
            float sdRound(float2 p, float2 b, float r) { float2 q = abs(p) - b + r; return length(max(q, 0)) + min(max(q.x, q.y), 0) - r; }
            half4 frag(V i) : SV_Target
            {
                float2 full = _Size.xy + 2 * _Pad;
                float2 p = (i.uv - 0.5) * full;                       // 以卡片中心为原点（米）
                float d = sdRound(p, _Size.xy * 0.5, _Radius);
                float aa = fwidth(d) * 1.2;
                float inside = 1 - smoothstep(-aa, aa, d);
                // 投影：往下偏一点、外侧柔和
                float ds = sdRound(p + float2(0, _Pad * 0.35), _Size.xy * 0.5, _Radius);
                float shadow = (1 - smoothstep(0, _Pad * 0.9, ds)) * _Shadow;
                float t = saturate(p.y / _Size.y + 0.5);              // 0 底 → 1 顶
                half4 c = lerp(_ColorB, _ColorA, t);
                float glow = smoothstep(-_Size.y * 0.25, 0, d) * _Glow;     // 边缘往里一圈亮
                c.rgb = lerp(c.rgb, 1, glow * 0.6);
                float gloss = smoothstep(0.62, 0.95, t) * smoothstep(-0.004, -0.02, d) * _Gloss; // 顶部高光带
                c.rgb += gloss * 0.5;
                float border = smoothstep(-_Border - aa, -_Border + aa, d) * inside;
                c = lerp(c, _BorderColor, border);
                half3 rgb = inside > 0.001 ? c.rgb : half3(0.22, 0.12, 0.3);    // 卡片外侧只剩淡紫色投影
                half4 o = half4(rgb, max(c.a * inside, shadow * (1 - inside)) * _Alpha);
                return o;
            }
            ENDHLSL
        }
    }
}
