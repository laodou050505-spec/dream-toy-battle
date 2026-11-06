// 玩具枪的果冻糖材质：半透明但颜色饱满（像参考图里的桃粉糖果玻璃），只画最外一层表面——
//   第一遍只写深度，第二遍 ZTest LEqual 上色：枪身内部的弹匣、背面的防滑纹不会透出来叠成杂线；
//   顶点色（sRGB）转线性再用，颜色不发白；
//   金色星星闪粉：按物体空间 4 毫米格子随机撒点，每颗只在特定视角反光（转动枪时一闪一闪）。
Shader "Bang/CandyGun"
{
    Properties
    {
        _Color ("颜色", Color) = (1,1,1,1)
        _Opacity ("不透明度", Range(0,1)) = 0.84
        _Fresnel ("边缘亮光", Range(0,2)) = 0.5
        _Glow ("内部透光", Range(0,1)) = 0.18
        _Glitter ("闪粉强度", Range(0,3)) = 1.4
        _GlitterDensity ("闪粉密度", Range(0,1)) = 0.06
        _GlitterScale ("闪粉格子（每米格数）", Float) = 240
        _Fade ("淡入淡出", Range(0,1)) = 1
        _Flash ("闪光", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" }
        Pass
        {
            Name "DepthPrepass"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ColorMask 0 ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Color; half _Opacity, _Fresnel, _Glow, _Glitter, _GlitterDensity, _Fade, _Flash; float _GlitterScale; CBUFFER_END
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "Color"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _Color; half _Opacity, _Fresnel, _Glow, _Glitter, _GlitterDensity, _Fade, _Flash; float _GlitterScale; CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 n : TEXCOORD0; float3 ws : TEXCOORD1; float3 os : TEXCOORD2; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.n = TransformObjectToWorldNormal(v.normalOS);
                o.ws = TransformObjectToWorld(v.positionOS.xyz); o.os = v.positionOS.xyz; o.color = half4(SRGBToLinear(v.color.rgb), v.color.a);
                return o;
            }
            float3 hash33(float3 p) { p = frac(p * float3(0.1031, 0.1030, 0.0973)); p += dot(p, p.yxz + 33.33); return frac((p.xxy + p.yxx) * p.zyx); }
            half4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.n); float3 vd = normalize(GetWorldSpaceViewDir(i.ws));
                Light ml = GetMainLight(); float3 L = ml.direction;
                half ndl = dot(n, L);
                half diff = 0.6 + 0.4 * saturate(ndl * 0.5 + 0.5);                      // 包裹光：糖果是透光的，暗面不死黑
                half spec = pow(saturate(dot(reflect(-L, n), vd)), 48.0) * 0.9;
                half f = pow(1.0 - saturate(dot(n, vd)), 2.5);
                half3 baseC = _Color.rgb * i.color.rgb;
                // 闪粉：物体空间格子，每格以 _GlitterDensity 的概率有一颗；随机朝向，只有朝向接近“反射光线”时才亮
                float3 g = i.os * _GlitterScale; float3 cell = floor(g); float3 h = hash33(cell);
                float3 c = cell + 0.2 + 0.6 * hash33(cell + 7.1);
                half star = step(1.0 - _GlitterDensity, h.x) * (1.0 - smoothstep(0.08, 0.2, length(g - c)));
                float3 fn = normalize(hash33(cell + 3.7) * 2.0 - 1.0 + n * 1.2);
                half glint = pow(saturate(dot(reflect(-vd, fn), L)), 6.0) * 0.8 + 0.25;
                half3 glitter = star * glint * _Glitter * half3(1.0, 0.78, 0.35);
                half3 col = baseC * diff * ml.color + baseC * _Glow + f * _Fresnel * half3(1, 0.95, 0.9) + spec + glitter + _Flash;
                half a = saturate(_Opacity + f * _Fresnel * 0.4 + spec + star * 0.6) * _Fade;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
}
