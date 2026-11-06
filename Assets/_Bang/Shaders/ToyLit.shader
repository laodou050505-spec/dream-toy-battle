// 《砰！》玩具着色器（URP）：梦幻、柔和、糖果色
//  - 明暗：半兰伯特 + 柔和渐变（不做硬色带），暗部偏薰衣草紫，不死黑
//  - 塑料高光（_Gloss）/ 绒布光泽（_Sheen）/ 粉彩边缘光
//  - 顶点色可选（合并网格用），自发光
//  - Stencil：门后世界的材质用 Equal 只在门洞里画；门外用 Always
Shader "Bang/ToyLit"
{
    Properties
    {
        _BaseMap ("贴图", 2D) = "white" {}
        _BaseColor ("颜色", Color) = (1,1,1,1)
        [Toggle] _UseVertexColor ("使用顶点色", Float) = 0
        _ShadowColor ("暗部色", Color) = (0.56,0.5,0.76,1)
        _ShadeSoft ("明暗过渡", Range(0.05,1)) = 0.55
        _Gloss ("塑料高光", Range(0,1)) = 0.45
        _GlossSize ("高光锐度", Range(4,128)) = 36
        _Metal ("糖果金属（皇冠、金币：假环境反射 + 染色高光）", Range(0,1)) = 0
        _Sheen ("绒布光泽", Range(0,1)) = 0
        _SheenColor ("绒布光泽色", Color) = (1,0.92,0.97,1)
        _RimColor ("边缘光", Color) = (1,0.86,0.95,1)
        _RimPower ("边缘光宽度", Range(0.5,8)) = 3
        _RimStrength ("边缘光强度", Range(0,2)) = 0.25
        _Ambient ("环境光", Range(0,2)) = 0.3
        [HDR] _EmissionColor ("自发光", Color) = (0,0,0,1)
        _EmissionMap ("自发光遮罩", 2D) = "white" {}
        _Flash ("受击闪白", Range(0,1)) = 0
        _WorldTint ("世界色调（门后世界整体染色，如黄昏城堡）", Color) = (1,1,1,1)
        _Alpha ("透明度(配合 Fade)", Range(0,1)) = 1
        _HazeColor ("景深雾颜色", Color) = (1,0.93,0.9,1)
        _HazeStart ("景深雾起点(米)", Float) = 4
        _HazeEnd ("景深雾终点(米)", Float) = 16
        _HazeAmount ("景深雾强度", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("剔除", Float) = 2
        [IntRange] _StencilRef ("Stencil 值", Range(0,255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil 比较", Float) = 8
        [HideInInspector] _ClipPlane ("门面裁切平面", Vector) = (0,0,0,0)
        [HideInInspector] _ArmSwing ("摆臂", Vector) = (0,0,0,0)
        [HideInInspector] _StencilReadMask ("Stencil ReadMask（门后世界 = 7：门洞 64|n 和玩具化覆盖 128|n 都通过）", Float) = 255
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        half4 _ToyGrade;   // 全局，不进材质 CBUFFER
        half4 _ToyGrade2;  // 全局：x 高光倍率  y 细颗粒强度  z 大斑驳强度
        half4 _ToyMood;    // 全局氛围色（Boss 预警 / 战斗时整个玩具世界偏暗紫）：a > 0 才生效，rgb 乘在受光部分上
        TEXTURE2D(_ToyGrain); SAMPLER(sampler_ToyGrain);   // 全局：可平铺的表面细节（R 细颗粒，G 大斑驳）
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _ShadowColor, _SheenColor, _RimColor, _EmissionColor, _HazeColor, _WorldTint;
            float _HazeStart, _HazeEnd, _HazeAmount;
            half _UseVertexColor, _ShadeSoft, _Gloss, _GlossSize, _Sheen, _RimPower, _RimStrength, _Ambient, _Flash, _Alpha, _Metal;
            float4 _ClipPlane;
            float4 _ArmSwing;   // Boss 摆臂（模型是一整块、没有手臂骨骼）：x 左右两侧绕肩的摆角（弧度，左右反相）、y 肩高（模型局部）、z 身体半宽（从这里往外才算手臂）、w 前后摆的比例   // 跨门的敌人：dot(world, xyz) + w < 0 的一侧不画（房间那一半 / 门后那一半各画各的，经过门框时不缺一块）
        CBUFFER_END
        // 两侧摆臂：|x| 超过身体半宽的顶点算手臂，绕肩（x=±半宽、y=肩高）转一个小角度，越往外越满；左右反相（一只抬一只落）
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
        ENDHLSL

        Pass
        {
            Name "ToyForward"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]
            Stencil { Ref [_StencilRef] ReadMask [_StencilReadMask] Comp [_StencilComp] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3; half fog : TEXCOORD4; float3 grainP : TEXCOORD5; half3 grainN : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                v.positionOS.xyz = ArmSwing(v.positionOS.xyz);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                half4 vc = v.color;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                vc.rgb = SRGBToLinear(vc.rgb);      // 顶点色按 sRGB 写入，线性空间里要先转换，否则粉彩发白
                #endif
                o.color = lerp(half4(1,1,1,1), vc, _UseVertexColor);
                o.fog = ComputeFogFactor(p.positionCS.z);
                // 表面细节用物体空间（按缩放还原成米）：敌人走动、转身时颗粒贴在身上，不会游动
                float4x4 m = GetObjectToWorldMatrix();
                o.grainP = v.positionOS.xyz * float3(length(m._m00_m10_m20), length(m._m01_m11_m21), length(m._m02_m12_m22));
                o.grainN = v.normalOS;
                return o;
            }

            half4 frag(Varyings i, half facing : VFACE) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                if (any(_ClipPlane.xyz)) clip(dot(i.positionWS, _ClipPlane.xyz) + _ClipPlane.w);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor * i.color;
                half3 gn = abs(i.grainN);
                float2 guv = (gn.y > gn.x && gn.y > gn.z) ? i.grainP.xz : (gn.x > gn.z ? i.grainP.zy : i.grainP.xy);   // 按法线主轴投影
                half fine = SAMPLE_TEXTURE2D(_ToyGrain, sampler_ToyGrain, guv * 2.9).r;
                half broad = SAMPLE_TEXTURE2D(_ToyGrain, sampler_ToyGrain, guv * 0.41 + 0.31).g;
                tex.rgb *= 1.0 + (fine - 0.5) * _ToyGrade2.y + (broad - 0.5) * _ToyGrade2.z;
                float3 n = normalize(i.normalWS) * (facing > 0 ? 1 : -1);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light main = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half wrap = saturate(dot(n, main.direction) * 0.5 + 0.5);
                half lit = smoothstep(0.5 - _ShadeSoft * 0.5, 0.5 + _ShadeSoft * 0.5, wrap) * lerp(0.2, 1.0, main.shadowAttenuation);
                half3 tint = lerp(_ShadowColor.rgb, 1.0.xxx, lit);
                half3 col = tex.rgb * tint * main.color;

                // 塑料高光：柔和的 Blinn-Phong
                float3 h = normalize(main.direction + v);
                half spec = pow(saturate(dot(n, h)), _GlossSize) * _Gloss * _ToyGrade2.x * (0.55 + 0.9 * fine) * (0.4 + 0.6 * main.shadowAttenuation);
                col += main.color * spec;

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0; li < count; li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    half d = saturate(dot(n, l.direction) * 0.5 + 0.5);
                    col += tex.rgb * l.color * d * l.distanceAttenuation;
                }
                #endif

                half4 g = _ToyGrade.w > 0 ? _ToyGrade : half4(1, 1, 1, 1);   // 全局画面调色（ToyLook 组件设置）：x 边缘光 y 环境光 z 饱和度 w 亮度
                col += tex.rgb * SampleSH(n) * _Ambient * g.y;

                half fres = 1.0 - saturate(dot(n, v));
                // 糖果金属：反射方向上的“天光 → 地平线亮带 → 暖地面”渐变，乘本色；再加两层染色高光（宽软 + 窄锐）。不采样反射探针，移动端便宜
                if (_Metal > 0)
                {
                    float3 r = reflect(-v, n);
                    half ry = r.y;
                    half3 env = lerp(half3(0.42, 0.3, 0.26), half3(1.0, 0.95, 0.86), smoothstep(-0.35, 0.25, ry));   // 下暗上亮
                    env += half3(1.0, 0.96, 0.85) * pow(saturate(1.0 - abs(ry - 0.12) * 3.2), 3.0) * 0.9;              // 地平线一条亮带（金属感关键）
                    env += half3(1.0, 1.0, 1.0) * pow(saturate(ry), 6.0) * 0.6;                                        // 顶光
                    half3 metalCol = tex.rgb * env * lerp(0.55, 1.0, lit);
                    half s1 = pow(saturate(dot(n, h)), 18.0) * 0.5, s2 = pow(saturate(dot(n, h)), 160.0) * 2.2;
                    metalCol += main.color * (tex.rgb * s1 + lerp(tex.rgb, 1.0.xxx, 0.6) * s2) * (0.35 + 0.65 * main.shadowAttenuation);
                    metalCol += tex.rgb * pow(fres, 2.5) * 0.35;                                                         // 边缘回光
                    col = lerp(col, metalCol, _Metal);
                }
                // 绒布：掠射角泛起柔光
                col += _SheenColor.rgb * pow(fres, 3.0) * _Sheen * 0.25;
                // 边缘光
                col += _RimColor.rgb * pow(fres, _RimPower) * _RimStrength * g.x;
                half luma = dot(col, half3(0.3, 0.59, 0.11));
                col = max(0, lerp(luma.xxx, col, g.z)) * g.w;
                // 自发光
                col *= _WorldTint.rgb;                // 世界染色只压受光部分，自发光（窗灯、彩灯）不受影响
                col *= _ToyMood.a > 0 ? _ToyMood.rgb : half3(1, 1, 1);
                col += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                col = lerp(col, 1.0.xxx, _Flash);
                // 景深雾：门后世界越远越朦胧（雪景球般的空气感）
                float dist = distance(GetCameraPositionWS(), i.positionWS);
                col = lerp(col, _HazeColor.rgb, saturate((dist - _HazeStart) / max(_HazeEnd - _HazeStart, 0.01)) * _HazeAmount);
                col = MixFog(col, i.fog);
                return half4(col, tex.a * _Alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            float3 _LightDirection;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };
            V vert(A v)
            {
                V o; UNITY_SETUP_INSTANCE_ID(v);
                float3 ws = TransformObjectToWorld(ArmSwing(v.positionOS.xyz));
                float3 nw = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(ws, nw, _LightDirection));
                return o;
            }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]
            Stencil { Ref [_StencilRef] ReadMask [_StencilReadMask] Comp [_StencilComp] }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            V vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionCS = TransformObjectToHClip(ArmSwing(v.positionOS.xyz)); return o; }
            half4 frag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
