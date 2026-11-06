// 彩纸粒子：不受光的顶点色小纸片，正反面明暗不同（翻转时一闪一闪）；带模板参数，PortalWorld 会把它限制在门洞里
Shader "Bang/Confetti"
{
    Properties
    {
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 0
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _StencilReadMask ("Stencil ReadMask（门后世界 = 7：门洞 64|n 和玩具化覆盖 128|n 都通过）", Float) = 255
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            Stencil { Ref [_StencilRef] ReadMask [_StencilReadMask] Comp [_StencilComp] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            CBUFFER_START(UnityPerMaterial) float _StencilRef, _StencilComp; CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.color = half4(SRGBToLinear(v.color.rgb), 1); return o; }
            half4 frag(V i, half facing : VFACE) : SV_Target { return half4(i.color.rgb * (facing > 0 ? 1.0 : 0.78), 1); }
            ENDHLSL
        }
    }
}
