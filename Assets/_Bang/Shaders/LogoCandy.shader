// 3D 糖果 Logo：正面贴 nano banana 的 Logo 图，侧面用边缘像素拉伸成实心糖果厚边；
// 包裹光 + 强镜面高光（硬糖的亮点）+ 边缘光，每隔几秒一道斜向光泽从左扫到右（只扫正面）。
Shader "Bang/LogoCandy"
{
    Properties
    {
        _BaseMap ("Logo 贴图", 2D) = "white" {}
        _Tint ("颜色", Color) = (1,1,1,1)
        _Spec ("镜面高光", Range(0,2)) = 0.9
        _Rim ("边缘光", Range(0,2)) = 0.45
        _Sweep ("光泽扫过强度", Range(0,2)) = 0.7
        _SweepPeriod ("扫过周期(秒)", Float) = 4.5
        _Alpha ("透明度", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+90" }
        Pass
        {
            Name "Depth" Tags { "LightMode"="SRPDefaultUnlit" } ColorMask 0 ZWrite On
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
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial) float4 _BaseMap_ST; half4 _Tint; half _Spec, _Rim, _Sweep, _SweepPeriod, _Alpha; CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 ws : TEXCOORD2; float3 nos : TEXCOORD3; float3 os : TEXCOORD4; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.n = TransformObjectToWorldNormal(v.normalOS); o.ws = TransformObjectToWorld(v.positionOS.xyz); o.nos = v.normalOS; o.os = v.positionOS.xyz;
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                float3 n = normalize(i.n); float3 vd = normalize(GetWorldSpaceViewDir(i.ws));
                Light L = GetMainLight();
                half diff = 0.72 + 0.28 * saturate(dot(n, L.direction) * 0.5 + 0.5);
                float3 h = normalize(L.direction + vd);
                half spec = pow(saturate(dot(n, h)), 64) * _Spec + pow(saturate(dot(n, normalize(vd + float3(0.3, 0.6, 0)))), 24) * 0.25 * _Spec;
                half rim = pow(1 - saturate(dot(n, vd)), 3) * _Rim;
                half front = saturate(-i.nos.z * 1.5);                  // 正面（朝 -Z 的面，即朝玩家）
                // 光泽扫过：沿 (x + y) 方向的一条窄亮带，周期性从左扫到右
                float ph = frac(_Time.y / _SweepPeriod);
                float band = i.os.x * 0.9 + i.os.y * 0.5;
                float s = 1 - smoothstep(0, 0.06, abs(band - lerp(-0.9, 0.9, ph * 1.6)));
                half3 c = tex.rgb * _Tint.rgb * diff + spec + rim * half3(1, 0.92, 0.97) + s * _Sweep * front * 0.6;
                return half4(c, _Alpha);
            }
            ENDHLSL
        }
    }
}
