// 房间玩具化覆盖：贴在房间表面（MR 里是空间网格 / 房间平面，编辑器里是预览客厅）。
// 每个“覆盖种子”（插下的旗子）一个球形区域，半径慢慢长大；区域边界用多层 3D 噪声扰动成不规则的水彩晕染形状。
//
// 渲染顺序（模板）：
//   ToyCoverMask    （先画）：覆盖度 > 0 的地方写 128|n，不写颜色深度
//   门洞遮罩         （Geometry-10）：写 64|n（盖掉覆盖值，门洞里始终是门后世界）
//   门后世界         （模板 & 7 == n）：门洞里 + 房间被覆盖处都画——同一个世界从门口漏进房间
//   Pass 1 Surface  （Geometry-3：门后世界之后、小怪 / 枪 / 门框 / UI 之前；模板 64 位 == 0，即不在门洞里）：
//       用户要求“现实场景不要遮挡任何游戏内容”：MR 里真实表面不写深度、深度比较 Always，只负责把门后世界按覆盖度淡入，
//       其后画的游戏内容全部压在上面；编辑器预览客厅保留自己的深度（只是模拟现实）
//       编辑器预览（_RoomMode=1）：画客厅原色，alpha = 1-覆盖度 → 覆盖处淡出露出后面的 3D 玩具世界
//       MR（_RoomMode=0）：不改颜色，只把帧缓冲 alpha 乘上覆盖度 → 过渡带里玩具世界和真实房间（透视）柔和融合
//   Pass 2 Rim      ：过渡带中间一圈淡淡的粉彩光晕 + 稀疏闪点（加色）
// 遮罩在单独的 Bang/ToyCoverMask（Geometry-20）；房间渲染器同时挂两个材质：[表面, 遮罩]。
Shader "Bang/ToyCover"
{
    Properties
    {
        _BaseColor ("房间原色（编辑器预览）", Color) = (0.93, 0.88, 0.83, 1)
        _EdgeWidth ("过渡带宽（米）", Range(0.05, 1)) = 0.38
        _NoiseScale ("边缘噪声尺度", Range(0.2, 8)) = 2.4
        _NoiseAmp ("边缘不规则度（米）", Range(0, 1)) = 0.32
        _RimColor ("边缘闪光色", Color) = (1, 0.86, 0.96, 1)
        _RimStrength ("边缘闪光强度", Range(0, 1)) = 0.22
        _RoomMode ("编辑器预览模式", Float) = 1
        [Enum(Off,0,On,1)] _ZWrite ("写深度（编辑器预览客厅开；MR 真实表面关）", Float) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("深度比较（MR 用 Always）", Float) = 4
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendA ("SrcA", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendA ("DstA", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-3" }

        HLSLINCLUDE
        #include "ToyCover.hlsl"
        ENDHLSL

        // Pass 1：房间表面（编辑器：原色按覆盖度淡出；MR：帧缓冲 alpha 乘覆盖度）
        Pass
        {
            Name "Surface"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendA] [_DstBlendA]
            ZWrite [_ZWrite] ZTest [_ZTest]
            Stencil { Ref 0 ReadMask 64 Comp Equal }      // 跳过门洞（64 位），覆盖处（128|n）照画：原色按覆盖度淡出
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            half4 frag(V i) : SV_Target
            {
                float cov = Coverage(i.ws);
                if (_RoomMode > 0.5)
                {
                    Light L = GetMainLight(TransformWorldToShadowCoord(i.ws));
                    half lit = saturate(dot(normalize(i.n), L.direction) * 0.5 + 0.5) * lerp(0.5, 1, L.shadowAttenuation);
                    return half4(_BaseColor.rgb * (0.55 + 0.5 * lit), 1 - cov);
                }
                return half4(0, 0, 0, cov);      // MR：Blend Zero One, Zero SrcAlpha → 颜色不变、alpha *= 覆盖度
            }
            ENDHLSL
        }

        // Pass 2：过渡带的淡淡光晕和闪点
        Pass
        {
            Name "Rim"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One, Zero One
            ZWrite Off ZTest [_ZTest]
            Stencil { Ref 0 ReadMask 64 Comp Equal }      // 跳过门洞（64 位），覆盖处（128|n）照画：原色按覆盖度淡出
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragRim
            #pragma multi_compile_instancing
            half4 fragRim(V i) : SV_Target
            {
                float cov = Coverage(i.ws);
                float band = exp(-pow((cov - 0.5) / 0.2, 2));
                float spark = smoothstep(0.82, 1.0, vnoise(i.ws * 38 + _Time.y * 0.6));
                return half4(_RimColor.rgb * band * (_RimStrength + spark * 0.6), 0);
            }
            ENDHLSL
        }
    }
}
