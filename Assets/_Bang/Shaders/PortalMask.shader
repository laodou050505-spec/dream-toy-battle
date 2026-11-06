// 门洞遮罩：不出颜色、不写深度，只把门洞像素的模板值写成 _StencilRef（= 64 | 章节值 n；64 位表示“门洞”，房间表面据此跳过门洞）。
// 门后世界的材质用 Stencil (值 & 7) == n 绘制：门洞（64|n）和房间被玩具化覆盖处（128|n）都通过。队列在不透明物体之前。
Shader "Bang/PortalMask"
{
    Properties { [IntRange] _StencilRef ("Stencil 值", Range(1,255)) = 1 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-10" }
        Pass
        {
            Name "Mask"
            Tags { "LightMode"="UniversalForward" }
            ColorMask 0 ZWrite Off Cull Off
            Stencil { Ref [_StencilRef] Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
