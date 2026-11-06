// 门后世界的天空：贴图（nano banana 画的天空）+ 竖直渐变兜底，只在门洞模板里画，不写深度（先画，世界物体盖在上面）。
Shader "Bang/PortalSky"
{
    Properties
    {
        _MainTex ("天空画片", 2D) = "white" {}
        _Top ("顶部色", Color) = (0.66,0.82,1,1)
        _Horizon ("地平线色", Color) = (1,0.9,0.78,1)
        _TexAmount ("画片强度", Range(0,1)) = 1
        _Exposure ("亮度", Range(0.5,2)) = 1
        _DirMode ("UV 方式：0 网格 UV / 1 经纬度全景 / 2 环带全景天球", Float) = 0
        _BandMin ("环带下缘仰角（度）", Float) = -6
        _BandMax ("环带上缘仰角（度）", Float) = 62
        _ZenithBlend ("上缘向天顶过渡（度）", Float) = 16
        _Zenith ("天顶色", Color) = (0.62,0.78,1,1)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("剔除", Float) = 1
        [IntRange] _StencilRef ("Stencil 值", Range(0,255)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil 比较", Float) = 3
        [HideInInspector] _StencilReadMask ("Stencil ReadMask（门后世界 = 7：门洞 64|n 和玩具化覆盖 128|n 都通过）", Float) = 255
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-5" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off Cull [_Cull]
            Stencil { Ref [_StencilRef] ReadMask [_StencilReadMask] Comp [_StencilComp] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial) half4 _Top, _Horizon, _Zenith; half _TexAmount, _Exposure, _DirMode; float _BandMin, _BandMax, _ZenithBlend; float4 _MainTex_ST; CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 dirOS : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv; o.dirOS = v.positionOS.xyz; return o; }
            half4 _ToyMood;   // 全局氛围色（Boss 预警 / 战斗）：a > 0 才生效
            half4 frag(V i) : SV_Target
            {
                half3 mood = _ToyMood.a > 0 ? _ToyMood.rgb : half3(1, 1, 1);
                half t = saturate(i.dirOS.y * 1.4 + 0.1);
                half3 grad = lerp(_Horizon.rgb, _Top.rgb, t);
                float3 d = normalize(i.dirOS);
                float2 uvDir = float2(atan2(d.x, d.z) / (2 * PI) + 0.5, asin(clamp(d.y, -1, 1)) / PI + 0.5);   // 经纬度（等距柱状）
                if (_DirMode > 1.5)
                {
                    // 环带全景天球：整张横幅全景绕一圈（不重复），按仰角铺在 _BandMin.._BandMax 之间；
                    // 上缘以上平滑过渡到天顶色，整个天是一个整体，没有顶盖接缝
                    float el = degrees(asin(clamp(d.y, -1, 1)));
                    float v = saturate((el - _BandMin) / (_BandMax - _BandMin)) * 0.996 + 0.002;
                    half4 bt = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, float2(uvDir.x + _MainTex_ST.z, v), 0);
                    half k = smoothstep(_BandMax - _ZenithBlend, _BandMax + 6, el);
                    return half4(lerp(bt.rgb, _Zenith.rgb, k) * _Exposure * mood, 1);
                }
                float2 uv = _DirMode > 0.5 ? uvDir : i.uv;
                half4 tex = _DirMode > 0.5 ? SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, TRANSFORM_TEX(uv, _MainTex), 0)   // 经度接缝处不取 mip，避免一条细缝
                                           : SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(uv, _MainTex));
                return half4(lerp(grad, tex.rgb, _TexAmount * tex.a) * _Exposure * mood, 1);
            }
            ENDHLSL
        }
    }
}
