// 门后世界里飘浮的小星光（粒子用）：程序化四角星 + 柔光晕，加色混合；带模板参数，PortalWorld 会把它限制在门洞里
Shader "Bang/Sparkle"
{
    Properties
    {
        _Intensity ("亮度", Range(0,4)) = 1.4
        _Core ("星芒锐度", Range(2,40)) = 14
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 0
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _StencilReadMask ("Stencil ReadMask（门后世界 = 7：门洞 64|n 和玩具化覆盖 128|n 都通过）", Float) = 255
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            Stencil { Ref [_StencilRef] ReadMask [_StencilReadMask] Comp [_StencilComp] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            CBUFFER_START(UnityPerMaterial) half _Intensity, _Core; float _StencilRef, _StencilComp; CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = half4(SRGBToLinear(v.color.rgb), v.color.a); o.uv = v.uv * 2 - 1;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float2 p = i.uv;
                float r = length(p);
                half glow = saturate(1 - r) ; glow *= glow * 0.55;
                half star = saturate(1 - abs(p.x) * _Core) * saturate(1 - abs(p.y) * 1.1) + saturate(1 - abs(p.y) * _Core) * saturate(1 - abs(p.x) * 1.1);
                half core = saturate(1 - r * 5);
                half a = saturate(glow + star * 0.9 + core) * i.color.a;
                half3 c = lerp(i.color.rgb, half3(1, 1, 1), saturate(core + star * 0.4));
                return half4(c * _Intensity, a);
            }
            ENDHLSL
        }
    }
}
