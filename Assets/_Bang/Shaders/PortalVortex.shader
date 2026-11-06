// 门洞涡旋膜（虫洞）：贴着洞口边缘一圈旋向中心的粉彩流光，越往中心越淡、中心完全透明（门后世界看得清）；
// 再叠一层极淡的泡泡干涉色。网格来自 PortalArch.FilmMesh：uv.x = 绕洞口一圈的比例，uv.y = 0 边缘 → 1 中心。
Shader "Bang/PortalVortex"
{
    Properties
    {
        _Strength ("流光强度", Range(0, 2)) = 0.6
        _Width ("流光带宽（uv.y）", Range(0.05, 1)) = 0.13
        _Arms ("旋臂数", Float) = 9
        _Twist ("扭转", Float) = 2.2
        _Speed ("旋转速度", Range(0, 2)) = 0.18
        _EdgeColor ("边缘光色", Color) = (1, 0.92, 0.98, 1)
        _Film ("泡泡膜强度", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha, Zero One
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half _Strength, _Width, _Arms, _Twist, _Speed, _Film; half4 _EdgeColor; CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 ws : TEXCOORD2; float oy : TEXCOORD3; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.uv = v.uv;
                o.n = TransformObjectToWorldNormal(v.normalOS); o.ws = TransformObjectToWorld(v.positionOS.xyz); o.oy = v.positionOS.y;
                return o;
            }
            half3 hue(half h) { return saturate(abs(frac(h + half3(0, 0.333, 0.667)) * 6 - 3) - 1); }
            float h11(float x) { return frac(sin(x * 127.1) * 43758.5453); }
            half4 frag(V i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float r = i.uv.y;                                     // 0 边缘 → 1 中心
                float band = saturate(1 - r / _Width);
                band *= band;
                // 旋臂：沿一圈的相位 + 向内扭转 + 时间（顺时针流向中心）
                float ph = i.uv.x * _Arms + r * _Twist * _Arms * 0.35 - t * _Arms * 0.5;
                float arm = pow(saturate(sin(ph * 6.2831853) * 0.5 + 0.5), 3);
                float arm2 = pow(saturate(sin((i.uv.x * (_Arms + 4) - r * _Twist * 3 + t * 1.7) * 6.2831853) * 0.5 + 0.5), 6) * 0.55;
                // 稀疏亮点沿旋臂流进去
                float cell = floor(i.uv.x * 64);
                float spark = step(0.86, h11(cell)) * saturate(1 - abs(frac(r * 3 - t * 2.4 + h11(cell + 3)) - 0.5) * 9);
                half3 col = lerp(_EdgeColor.rgb, hue(i.uv.x * 2 + t * 0.15) * 0.55 + 0.45, 0.5);
                half3 c = col * (arm * 0.8 + arm2) * band * _Strength + _EdgeColor.rgb * spark * band * 1.2;
                // 紧贴边缘一圈柔光
                c += _EdgeColor.rgb * pow(saturate(1 - r / 0.045), 2) * 0.45;
                // 泡泡膜：极淡的干涉色
                float3 vd = normalize(GetWorldSpaceViewDir(i.ws));
                half ndv = abs(dot(normalize(i.n), vd));
                c += hue(ndv * 0.9 + i.uv.x * 0.3 + t * 0.2) * _Film * (0.4 + 0.6 * (1 - ndv)) * band;   // 只在边缘带里，中间完全透明
                // 贴地的门槛一段淡出：小路、地皮从门洞底边穿过，流光不能像一道发光的门槛横在路面上
                c *= smoothstep(0.02, 0.16, i.oy);
                half a = saturate(max(max(c.r, c.g), c.b) * 0.75);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
