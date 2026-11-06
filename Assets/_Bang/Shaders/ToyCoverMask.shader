// 房间玩具化覆盖的模板遮罩：覆盖度 > 0 的地方把模板写成 _CoverRef（= 128 | 章节值 n），不写颜色深度。
// 门后世界（模板 (值 & 7) == n）因此也在这些地方绘制——玩具化就是门后世界从门口漏进房间。
// 和 Bang/ToyCover 挂在同一个房间渲染器上（第二个材质），必须比门洞遮罩（Geometry-10）先画。
Shader "Bang/ToyCoverMask"
{
    Properties
    {
        _EdgeWidth ("过渡带宽（米）", Range(0.05, 1)) = 0.38
        _NoiseScale ("边缘噪声尺度", Range(0.2, 8)) = 2.4
        _NoiseAmp ("边缘不规则度（米）", Range(0, 1)) = 0.32
        [IntRange] _CoverRef ("覆盖处模板值（128 | 章节值）", Range(0, 255)) = 129
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry-20" }
        HLSLINCLUDE
        #include "ToyCover.hlsl"
        ENDHLSL
        Pass
        {
            Name "ToyMask"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ColorMask 0 ZWrite Off
            Stencil { Ref [_CoverRef] Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragMask
            #pragma multi_compile_instancing
            half4 fragMask(V i) : SV_Target { clip(Coverage(i.ws) - 0.003); return 0; }
            ENDHLSL
        }
    }
}
