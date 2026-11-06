// 小怪描边（反向外壳）：沿“平滑后的法线”（存在 UV3，由 EnemyView 运行时算好，积木的硬边转角不会裂开）往外推一圈，
// 只画背面，颜色是深莓紫——把小怪从同样糖果色的场景里“勾”出来（真机试玩：小怪会融进背景）。
// 宽度按世界尺寸算（约 3.5 毫米），离得远时稍微加粗，保证 3 米外也看得见；门后时和身体一样只在门洞里画（模板）。
Shader "Bang/ToyOutline"
{
    Properties
    {
        _OutlineColor ("描边颜色", Color) = (0.33, 0.2, 0.42, 1)
        _Width ("宽度（米）", Range(0, 0.02)) = 0.0035
        _FarBoost ("远处加粗（每米）", Range(0, 1)) = 0.35
        _DepthPush ("沿视线后推（米）：藏掉网格内部凹凸处的线，只留外轮廓", Range(0, 0.05)) = 0
        [IntRange] _StencilRef ("Stencil 值", Range(0,255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil 比较", Float) = 8
        [HideInInspector] _StencilReadMask ("Stencil ReadMask", Float) = 255
        [HideInInspector] _ClipPlane ("门面裁切平面", Vector) = (0,0,0,0)
        [HideInInspector] _ArmSwing ("摆臂", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front ZWrite On
            Stencil { Ref [_StencilRef] ReadMask [_StencilReadMask] Comp [_StencilComp] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial) half4 _OutlineColor; float _Width, _FarBoost, _DepthPush, _StencilRef, _StencilComp, _StencilReadMask; float4 _ClipPlane; float4 _ArmSwing; CBUFFER_END
        float3 ArmSwing(float3 p)
        {
            // Boss 模型导入时转了 90°：模型局部 Z = 左右（手臂在 ±Z 两侧）、Y = 上、X = 前后
            if (_ArmSwing.z <= 0) return p;
            float side = sign(p.z), ax = abs(p.z) - _ArmSwing.z;
            if (ax <= 0 || p.y > _ArmSwing.y + 0.05) return p;
            float w = saturate(ax / max(0.02, _ArmSwing.z * 0.6));
            float a = _ArmSwing.x * side * w;
            float2 pivot = float2(side * _ArmSwing.z, _ArmSwing.y);
            float2 d = float2(p.z, p.y) - pivot; float c = cos(a), s2 = sin(a);
            float2 r = float2(d.x * c - d.y * s2, d.x * s2 + d.y * c) + pivot;
            float fx = p.x + _ArmSwing.w * (p.y - _ArmSwing.y) * sin(a) * side;   // 前后也带一点
            return float3(fx, r.y, r.x);
        }
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float3 smoothN : TEXCOORD3; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 ws : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 n = dot(v.smoothN, v.smoothN) > 0.01 ? v.smoothN : v.normalOS;
                float3 ws = TransformObjectToWorld(ArmSwing(v.positionOS.xyz));
                float3 nw = normalize(TransformObjectToWorldNormal(n));
                float d = distance(ws, GetCameraPositionWS());
                ws += nw * _Width * (1.0 + max(0, d - 1.0) * _FarBoost);
                ws += normalize(ws - GetCameraPositionWS()) * _DepthPush;
                o.positionCS = TransformWorldToHClip(ws); o.ws = ws;
                return o;
            }
            half4 frag(V i) : SV_Target { if (any(_ClipPlane.xyz)) clip(dot(i.ws, _ClipPlane.xyz) + _ClipPlane.w); return half4(_OutlineColor.rgb, 1); }
            ENDHLSL
        }
    }
}
