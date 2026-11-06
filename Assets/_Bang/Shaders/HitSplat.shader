// 视野遮挡贴片：不受光，始终画在最上面（贴在眼前，不和真实墙面、敌人抢深度），写入 alpha 让透视下也不透底。
Shader "Bang/HitSplat"
{
    Properties
    {
        _BaseMap ("贴图", 2D) = "white" {}
        _Color ("颜色", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }
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
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial) float4 _BaseMap_ST; half4 _Color; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half4 frag(V i) : SV_Target { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _Color; }
            ENDHLSL
        }
    }
}
