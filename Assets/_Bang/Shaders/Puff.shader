// 棉花团烟雾粒子：柔边圆团，上亮下暗的体积感，透明混合；带模板参数，PortalWorld 会把它限制在门洞里
Shader "Bang/Puff"
{
    Properties
    {
        _Top ("亮面", Color) = (1,1,1,1)
        _Bottom ("暗面", Color) = (0.86,0.84,0.95,1)
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 0
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _StencilReadMask ("Stencil ReadMask（门后世界 = 7：门洞 64|n 和玩具化覆盖 128|n 都通过）", Float) = 255
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            Stencil { Ref [_StencilRef] ReadMask [_StencilReadMask] Comp [_StencilComp] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Top, _Bottom; float _StencilRef, _StencilComp; CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.color = v.color; o.uv = v.uv * 2 - 1; return o; }
            half4 frag(V i) : SV_Target
            {
                float r = length(i.uv);
                // 三个小圆叠成的棉花团轮廓
                float b = min(min(length(i.uv - float2(-0.3, -0.1)) / 0.62, length(i.uv - float2(0.32, -0.05)) / 0.6), length(i.uv - float2(0.0, 0.25)) / 0.66);
                half a = saturate((1 - b) * 4) * i.color.a;
                half3 c = lerp(_Bottom.rgb, _Top.rgb, saturate(i.uv.y * 0.6 + 0.6));
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
