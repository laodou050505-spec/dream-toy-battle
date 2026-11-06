// 圆环进度（程序生成，不用贴图）：血量环、换弹环。_Fill 0..1 从 12 点钟顺时针填充；_GoldA/_GoldB 是金区（换弹的快速区间，0..1）。
// 底环半透明，填充部分用 _Color，金区用 _Gold 并带一圈亮边；整体有柔和描边，糖果感。
Shader "Bang/FxRing"
{
    Properties
    {
        _Color ("填充色", Color) = (1,0.45,0.55,1)
        _Back ("底环色", Color) = (1,1,1,0.35)
        _Gold ("金区色", Color) = (1,0.82,0.25,1)
        _Fill ("进度", Range(0,1)) = 1
        _GoldA ("金区起", Range(0,1)) = 0
        _GoldB ("金区止", Range(0,1)) = 0
        _Inner ("内径", Range(0,1)) = 0.62
        _Alpha ("整体透明度", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+30" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off Cull Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Color, _Back, _Gold; float _Fill, _GoldA, _GoldB, _Inner, _Alpha; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv * 2 - 1; return o; }
            half4 frag(V i) : SV_Target
            {
                float r = length(i.uv);
                float aa = fwidth(r) * 1.5;
                float band = smoothstep(_Inner - aa, _Inner + aa, r) * (1 - smoothstep(1 - aa * 2, 1, r));
                float a = atan2(i.uv.x, i.uv.y) / (2 * PI); a = frac(a + 1);            // 12 点钟 = 0，顺时针增加
                float fill = step(a, _Fill);
                float gold = step(_GoldA, a) * step(a, _GoldB) * step(0.0001, _GoldB - _GoldA);
                half4 c = lerp(_Back, _Color, fill);
                c = lerp(c, half4(lerp(_Gold.rgb, _Color.rgb, fill * 0.35), max(_Gold.a, c.a)), gold);
                float edge = 1 - smoothstep(0, aa * 3, abs(r - (_Inner + 1) * 0.5) - (1 - _Inner) * 0.5 + aa * 3);   // 外沿和内沿一圈白描边
                c.rgb = lerp(c.rgb, 1, edge * 0.55);
                c.a *= band * _Alpha;
                return c;
            }
            ENDHLSL
        }
    }
}
