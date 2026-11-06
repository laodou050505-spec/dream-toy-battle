// 立体卡通 UI（按钮 / 菜单板）：不受场景光照影响的平涂顶点色——正面主色、侧壁暗一档、描边深藏青都烘在顶点色里。
// 不透明、写深度（不会被别的半透明 UI 叠在上面），排在门后世界和真实房间表面之后。
Shader "Bang/UIToon"
{
    Properties
    {
        _BaseColor ("整体染色", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+20" }
        Pass
        {
            Cull Back ZWrite On ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color * _BaseColor;
                return o;
            }
            half4 frag(V i) : SV_Target { return half4(i.color.rgb, 1); }
            ENDHLSL
        }
    }
}
