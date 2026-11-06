// 特效贴片：不受光，贴图 × 顶点色 × 颜色，半透明叠加（“砰！”“啵！”字、弹道蜡笔线、蜡笔弹痕）。
Shader "Bang/FxSprite"
{
    Properties
    {
        _BaseMap ("贴图", 2D) = "white" {}
        _Color ("颜色", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("深度测试", Float) = 4
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 0
        [HideInInspector] _StencilComp ("Stencil Comp", Float) = 8
        [HideInInspector] _StencilReadMask ("Stencil ReadMask", Float) = 255
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off Cull Off ZTest [_ZTest]
            Stencil { Ref [_StencilRef] ReadMask [_StencilReadMask] Comp [_StencilComp] }     // 门后小怪的脚影也只透过门洞显示（PortalWorld.VariantOf）
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial) float4 _BaseMap_ST; half4 _Color; float _ZTest, _StencilRef, _StencilComp, _StencilReadMask; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = TRANSFORM_TEX(v.uv, _BaseMap); o.color = v.color;
                return o;
            }
            half4 frag(V i) : SV_Target { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * i.color * _Color; }
            ENDHLSL
        }
    }
}
