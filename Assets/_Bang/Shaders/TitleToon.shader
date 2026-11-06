// 标题字：颜色只由表面朝向决定，不受任何灯光影响——正面象牙白、倒角和侧面粉色。
// 原来用受光材质：编辑器里正面受光是象牙白，头显里灯光不同，整个正面落进暗面色（粉），和粉色放射底贴在一起认不清字。
Shader "Bang/TitleToon"
{
    Properties
    {
        _BaseColor ("正面", Color) = (1, 0.97, 0.91, 1)
        _EdgeColor ("倒角 / 侧面", Color) = (0.96, 0.5, 0.64, 1)
        _FaceDir ("正面朝向（模型空间）", Vector) = (0, 0, -1, 0)
        _Edge ("正面范围", Range(0, 1)) = 0.72
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _EdgeColor; float4 _FaceDir; float _Edge;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 n : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.n = v.normalOS;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float f = dot(normalize(i.n), _FaceDir.xyz);
                return half4(lerp(_EdgeColor.rgb, _BaseColor.rgb, smoothstep(_Edge - 0.03, _Edge + 0.03, f)), 1);
            }
            ENDHLSL
        }
    }
}
