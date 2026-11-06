// 半透明糖果玻璃（玩具枪、盾牌）：菲涅尔亮边 + 柔和高光 + 内部微光，不依赖场景光（MR 下稳定）
Shader "Bang/CandyGlass"
{
    Properties
    {
        _Color ("颜色", Color) = (1,0.6,0.45,1)
        _Opacity ("基础不透明度", Range(0,1)) = 0.42
        _Fresnel ("菲涅尔亮边", Range(0,2)) = 0.6
        _Glow ("内部微光", Range(0,1)) = 0.25
        _Fade ("淡入淡出", Range(0,1)) = 1
        _Flash ("闪光", Range(0,1)) = 0
        _BaseMap ("贴图(可选)", 2D) = "white" {}
        [Toggle] _UseVertexColor ("使用顶点色（零件色块）", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha ZWrite Off Cull Back      // 透明度单独混合：MR 透视下帧缓冲 alpha 才是正确的覆盖度
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial) half4 _Color; half _Opacity, _Fresnel, _Glow, _Fade, _Flash, _UseVertexColor; float4 _BaseMap_ST; CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 n : TEXCOORD0; float3 ws : TEXCOORD1; float2 uv : TEXCOORD2; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.n = TransformObjectToWorldNormal(v.normalOS); o.ws = TransformObjectToWorld(v.positionOS.xyz); o.uv = TRANSFORM_TEX(v.uv, _BaseMap); o.color = v.color; return o; }
            half4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.n); float3 vd = normalize(GetWorldSpaceViewDir(i.ws));
                float3 L = normalize(float3(0.4, 0.85, 0.35));
                half f = pow(1.0 - saturate(dot(n, vd)), 2.2);
                half diff = 0.62 + 0.38 * saturate(dot(n, L));
                half spec = pow(saturate(dot(reflect(-L, n), vd)), 32.0) * 0.8;
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                half3 baseC = _Color.rgb * (_UseVertexColor > 0.5 ? i.color.rgb : half3(1, 1, 1));
                half3 col = baseC * tex * diff + f * _Fresnel * 0.9 + spec + baseC * _Glow + _Flash;
                half a = saturate(_Opacity + f * _Fresnel * 0.6 + spec) * _Fade;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
