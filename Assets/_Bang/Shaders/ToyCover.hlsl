// ToyCover / ToyCoverMask 共用：覆盖种子与覆盖度计算（不规则的水彩晕染边缘）
#ifndef BANG_TOYCOVER_INCLUDED
#define BANG_TOYCOVER_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor, _RimColor;
    float _EdgeWidth, _NoiseScale, _NoiseAmp, _RimStrength, _RoomMode, _SrcBlend, _DstBlend, _SrcBlendA, _DstBlendA;
CBUFFER_END
float4 _ToySeeds[16];
float _ToySeedCount;
float4 _ToyDoor;          // xyz = 门洞中心（贴墙），w = 从门口蔓延出去的半径（米）；w<=0 不启用
float4 _ToyEye;           // xyz = 玩家眼睛（游戏空间固定站位），w = 虚拟罩子半径；w<=0 不投影
float4 _ToyBurstA[12];    // 不规则扩张的“舌头”：xyz = 源（门口 / 种子）中心，w = 伸出长度（米）
float4 _ToyBurstB[12];    //                     xyz = 方向 × 尖细度（模长越大越细），w = 来源当前半径
float _ToyBurstCount, _ToyBurstMax;

// 把一个点沿“从眼睛看过去的方向”投到与真实房间无关的虚拟罩子上：往下看落到地面（y=0），否则落到半径 w 的球面。
// 这样同一视线方向上的家具、墙、地板覆盖度一样，玩具化的边界不会顺着真实家具的轮廓走。
float3 ToyProj(float3 p)
{
    if (_ToyEye.w <= 0.001) return p;
    float3 d = p - _ToyEye.xyz; float l = length(d);
    if (l < 1e-4) return p;
    d /= l;
    float t = _ToyEye.w;
    if (d.y < -0.02) t = min(t, -_ToyEye.y / d.y);
    return _ToyEye.xyz + d * t;
}

struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
struct V { float4 positionCS : SV_POSITION; float3 ws : TEXCOORD0; float3 n : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
V vert(A v)
{
    V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.ws = TransformObjectToWorld(v.positionOS.xyz);
    o.positionCS = TransformWorldToHClip(o.ws);
    o.n = TransformObjectToWorldNormal(v.normalOS);
    return o;
}
float hash13(float3 p) { p = frac(p * 0.1031); p += dot(p, p.zyx + 31.32); return frac((p.x + p.y) * p.z); }
// 值噪声。性能（用户：终极决战小怪一多就卡；真机实测覆盖层是最大的 GPU 开销——整圈覆盖壳铺满视野，每个像素要算 8 层噪声 × 3 遍）：
// 晶格值预先烘进一张 64³ 的 3D 贴图（_ToyNoise3D，RoomToyification 启动时生成），一层噪声 = 1 次采样，代替 8 次哈希 + 7 次插值。
// 采样点放在“晶格 + 平滑后的小数 + 半个像素”上，硬件三线性插值算出的正好是原来 f*f*(3-2f) 平滑插值的结果（晶格每 64 格重复一次，看不出来）。
TEXTURE3D(_ToyNoise3D); SAMPLER(sampler_ToyNoise3D);
float _ToyNoiseN;
float vnoise(float3 p)
{
    float3 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
    float r;
    [branch] if (_ToyNoiseN > 0.5) r = SAMPLE_TEXTURE3D_LOD(_ToyNoise3D, sampler_ToyNoise3D, (i + f + 0.5) / _ToyNoiseN, 0).r;
    else   // 查找表还没建（编辑器刚打开等）：退回原来的逐点哈希
    {
        float n000 = hash13(i), n100 = hash13(i + float3(1, 0, 0)), n010 = hash13(i + float3(0, 1, 0)), n110 = hash13(i + float3(1, 1, 0));
        float n001 = hash13(i + float3(0, 0, 1)), n101 = hash13(i + float3(1, 0, 1)), n011 = hash13(i + float3(0, 1, 1)), n111 = hash13(i + float3(1, 1, 1));
        r = lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y), lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
    }
    return r;
}
float fbm(float3 p) { return vnoise(p) * 0.55 + vnoise(p * 2.07 + 7.1) * 0.28 + vnoise(p * 4.13 + 3.7) * 0.17; }

// 覆盖度 0..1：
//   门口蔓延：以门洞为源头向外长，半径越大轮廓越不规则——低频噪声把边缘推出大片的“舌头”，
//            再按离门方向叠一层角向噪声，长出一根根向外伸的指状触须；
//   旗子种子：各自一小片（玩法里小怪插旗），和门口那片自然连成一片；
//   过渡带里再叠细碎斑驳（像颜料慢慢渗开）。
float Coverage(float3 wsReal)
{
    float3 ws = ToyProj(wsReal);
    // 先用便宜的距离判断提前退出（一体机：覆盖壳 / 空间网格铺满整个视野，每个像素都跑三层噪声太贵）：
    //   噪声能把门口边缘往外推的最大量 ≈ 0.25 + 0.28r（舌头）+ 0.45r（触须）+ 振幅 + 半个过渡带；
    //   离门口和所有种子都比这还远 → 一定没被覆盖；门口半径已经过 12.5 米 → 一定全覆盖
    float rD = _ToyDoor.w;
    if (rD >= 12.5) return 1;
    float3 doorP = ToyProj(_ToyDoor.xyz);
    bool maybe = rD > 0.001 && distance(ws, doorP) < rD * 1.75 + 0.3 + _NoiseAmp + _EdgeWidth + _ToyBurstMax;
    int cnt0 = (int)_ToySeedCount;
    [loop] for (int j = 0; j < 16 && !maybe; j++)
    {
        if (j >= cnt0) break;
        float4 s0 = _ToySeeds[j];
        maybe = s0.w > 0.001 && distance(ws, ToyProj(s0.xyz)) < s0.w + _NoiseAmp + _EdgeWidth + _ToyBurstMax;
    }
    if (!maybe) return 0;
    float t = _Time.y;
    float n = fbm(ws * _NoiseScale + float3(0, t * 0.03, 0));
    float nf = vnoise(ws * _NoiseScale * 5.3 + float3(t * 0.05, 0, 0));
    float cov = 0;
    if (_ToyDoor.w > 0.001)
    {
        float r = _ToyDoor.w;
        float3 d = ws - doorP;
        float dist = length(d);
        float3 dir = d / max(dist, 1e-3);
        float lobes = fbm(ws * 0.55 + 11.3) - 0.5;                                   // 大片舌头
        float fingers = vnoise(dir * 3.4 + float3(0, 0, t * 0.01)) - 0.5;          // 指状触须（按方向）
        fingers = sign(fingers) * pow(abs(fingers) * 2, 1.6) * 0.5;
        float edge = r - dist + lobes * 2 * (0.25 + 0.28 * r) + fingers * 2 * 0.45 * r * saturate(r - 0.4)
                   + (n - 0.5) * 2 * _NoiseAmp;
        cov = smoothstep(-_EdgeWidth * 0.5, _EdgeWidth * 0.5, edge);
        // 快长满时（半径 > 10 米）把舌头 / 触须之间残留的小洞一起填上：100% 时任何方向都没有破洞
        cov = max(cov, smoothstep(10.0, 12.5, r));
    }
    int cnt = (int)_ToySeedCount;
    [loop] for (int k = 0; k < 16; k++)
    {
        if (k >= cnt) break;
        float4 s = _ToySeeds[k];
        if (s.w <= 0.001) continue;
        float edge = s.w - distance(ws, ToyProj(s.xyz)) + (n - 0.5) * 2 * _NoiseAmp * saturate(s.w * 1.5);
        cov = max(cov, smoothstep(-_EdgeWidth * 0.5, _EdgeWidth * 0.5, edge));
    }
    // 不规则扩张的舌头：从源中心朝某个方向伸出去的一条（按方向的锥形 + 噪声让边缘不整齐），和所属的那片连在一起
    int nb = (int)_ToyBurstCount;
    [loop] for (int b = 0; b < 12; b++)
    {
        if (b >= nb) break;
        float4 A = _ToyBurstA[b];
        if (A.w <= 0.001) continue;
        float3 B = _ToyBurstB[b].xyz; float sharp = length(B);
        float3 o = ToyProj(A.xyz); float3 d = ws - o; float dist = length(d);
        float c = dot(d / max(dist, 1e-3), B / max(sharp, 1e-3));
        if (c <= 0) continue;
        float lobe = pow(c, sharp) * (0.65 + 0.7 * n);
        float sr = _ToyBurstB[b].w;     // 来源半径由 CPU 算好（原来每个像素 × 每条舌头都去 16 个种子里找一遍，最多 192 次比较）
        float edge = sr + A.w * lobe - dist + (n - 0.5) * 2 * _NoiseAmp;
        cov = max(cov, smoothstep(-_EdgeWidth * 0.5, _EdgeWidth * 0.5, edge));
    }
    return saturate(cov + (nf - 0.5) * 0.6 * cov * (1 - cov) * 4);
}
#endif
