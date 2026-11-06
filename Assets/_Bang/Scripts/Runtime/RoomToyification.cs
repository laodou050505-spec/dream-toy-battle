// 房间玩具化：管理“覆盖种子”（小怪插下的旗子），把它们传给 Bang/ToyCover 的全局数组。
// 种子半径朝目标半径慢慢长（旗子越久覆盖越大）；旗子被打掉时目标归零、缩回去后删除。
// 编辑器里也运行（ExecuteAlways），previewSeeds 用来在不进玩法时预览覆盖效果。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bang
{
    [ExecuteAlways]
    public class RoomToyification : MonoBehaviour
    {
        public const int MaxSeeds = 16;
        public static RoomToyification Instance { get; private set; }

        [Serializable] public struct PreviewSeed { public Vector3 position; public float radius; }
        [Tooltip("编辑器预览用的种子（运行时玩法会清空并自己添加）")] public PreviewSeed[] previewSeeds;
        [Tooltip("半径增长速度（米/秒）")] public float growSpeed = 0.18f;
        [Tooltip("缩回速度（米/秒）")] public float shrinkSpeed = 0.6f;
        [Tooltip("房间可覆盖表面的总面积（平方米），用来估算玩具化度")] public float roomArea = 60f;
        [Header("从门口蔓延")]
        [Tooltip("门洞中心（贴墙）；玩具化从这里开始向四周长")] public Transform door;
        [Tooltip("门口蔓延半径（米）；玩法里随房间玩具化度增长")] public float doorRadius;
        [Tooltip("编辑器预览用的门口蔓延半径")] public float previewDoorRadius;

        class Seed { public int id; public Vector3 pos; public float r, target; }
        readonly List<Seed> _seeds = new();
        int _nextId = 1;
        static readonly Vector4[] Buf = new Vector4[MaxSeeds];
        [Tooltip("虚拟罩子的中心（玩家站位的眼睛高度，游戏空间）和半径：覆盖度在这个与房间无关的罩子上算")] public Vector3 eyePoint = new Vector3(0, 1.5f, 0);
        public float proxyRadius = 7f;
        static readonly int IdSeeds = Shader.PropertyToID("_ToySeeds"), IdCount = Shader.PropertyToID("_ToySeedCount"), IdDoor = Shader.PropertyToID("_ToyDoor"), IdEye = Shader.PropertyToID("_ToyEye");
        static readonly int IdBurstA = Shader.PropertyToID("_ToyBurstA"), IdBurstB = Shader.PropertyToID("_ToyBurstB"), IdBurstN = Shader.PropertyToID("_ToyBurstCount"), IdBurstMax = Shader.PropertyToID("_ToyBurstMax");

        // ───────── 不规则扩张（用户：后续扩张要不规则，不要平滑过渡）─────────
        // 每个覆盖源（门口那片 / 每个种子）每往外长 burstStep 米，就随机朝 1～2 个方向先“喷”出一条舌头（宽窄、长短不一），
        // 主体半径随后慢慢长上来、把舌头吞没；舌头的“伸到哪”是绝对位置，所以覆盖过的地方不会退回；源缩回时舌头一起缩。
        public const int MaxBursts = 12;
        [Header("不规则扩张")]
        [Tooltip("覆盖源每长这么多（米）就喷出新的舌头")] public float burstStep = 0.35f;
        [Tooltip("舌头伸出去的长度 = 这一步 × 倍数（随机）")] public Vector2 burstReach = new Vector2(1.6f, 3.2f);
        [Tooltip("舌头伸出所用时间（秒）")] public float burstTime = 0.9f;
        class Burst { public int src; public Vector3 dir; public float sharp, reach, spawnR, age; }
        readonly List<Burst> _bursts = new();
        readonly Dictionary<int, float> _lastSpawnR = new();          // 源 id（门口 = 0，种子 = 种子 id）→ 上次喷舌头时的半径
        static readonly Vector4[] BufA = new Vector4[MaxBursts], BufB = new Vector4[MaxBursts];
        float _burstMax;

        void OnEnable() { Instance = this; Push(); }
        void OnDisable() { if (Instance == this) Instance = null; Shader.SetGlobalFloat(IdCount, 0); Shader.SetGlobalVector(IdDoor, Vector4.zero); Shader.SetGlobalVector(IdEye, Vector4.zero); }
        void OnValidate() => Push();

        /// <summary>插旗：在 pos 处开始覆盖，最终半径 targetRadius。返回种子 id（满了返回 -1）。</summary>
        public int AddSeed(Vector3 pos, float targetRadius)
        {
            if (_seeds.Count >= MaxSeeds) return -1;
            var s = new Seed { id = _nextId++, pos = pos, r = 0.02f, target = targetRadius };
            _seeds.Add(s);
            return s.id;
        }

        public void SetTarget(int id, float targetRadius) { var s = _seeds.Find(x => x.id == id); if (s != null) s.target = targetRadius; }

        /// <summary>旗子被打掉：覆盖慢慢退回去。</summary>
        public void RemoveSeed(int id) => SetTarget(id, 0);

        public void ClearAll() { _seeds.Clear(); _bursts.Clear(); _lastSpawnR.Clear(); }

        /// <summary>估算玩具化度（0..1）：各种子圆面积之和 / 房间面积（重叠不扣，玩法上偏严一点也合理）。</summary>
        public float Coverage
        {
            get
            {
                float a = 0;
                foreach (var s in _seeds) a += Mathf.PI * s.r * s.r;
                a += Mathf.PI * doorRadius * doorRadius * 0.6f;          // 门口那片：墙 + 地面各占一部分
                return Mathf.Clamp01(a / Mathf.Max(roomArea, 1f));
            }
        }

        /// <summary>某个位置是否已经被覆盖（玩具化道具只在覆盖区里冒出来）。</summary>
        public bool IsCovered(Vector3 p, float margin = 0.2f)
        {
            foreach (var s in _seeds) if ((p - s.pos).sqrMagnitude < (s.r - margin) * (s.r - margin) && s.r > margin) return true;
            if (door && doorRadius > margin && (p - door.position).sqrMagnitude < (doorRadius - margin) * (doorRadius - margin)) return true;
            return false;
        }

        void Update()
        {
            if (Application.isPlaying)
            {
                float dt = Time.deltaTime;
                for (int i = _seeds.Count - 1; i >= 0; i--)
                {
                    var s = _seeds[i];
                    s.r = Mathf.MoveTowards(s.r, s.target, (s.target > s.r ? growSpeed : shrinkSpeed) * dt);
                    if (s.target <= 0 && s.r <= 0.01f) _seeds.RemoveAt(i);
                }
                UpdateBursts(dt);
            }
            Push();
        }

        float SrcRadius(int src, out Vector3 origin)
        {
            if (src == 0) { origin = door ? door.position : Vector3.zero; return door ? doorRadius : -1f; }
            foreach (var sd in _seeds) if (sd.id == src) { origin = sd.pos; return sd.r; }
            origin = Vector3.zero; return -1f;
        }

        void UpdateBursts(float dt)
        {
            if (door) CheckSource(0, door.position, doorRadius, true);
            foreach (var sd in _seeds) CheckSource(sd.id, sd.pos, sd.r, false);
            for (int i = _bursts.Count - 1; i >= 0; i--)
            {
                var b = _bursts[i]; b.age += dt;
                float r = SrcRadius(b.src, out _);
                // 源没了 / 缩回到喷出时以内（旗子被打掉）→ 舌头一起收；主体已经长过舌头尖 → 被吞没
                if (r < 0 || r < b.spawnR - 0.08f || (r >= b.reach && b.age > burstTime)) _bursts.RemoveAt(i);
            }
            var drop = new List<int>(); foreach (var k in _lastSpawnR.Keys) if (SrcRadius(k, out _) < 0) drop.Add(k);
            foreach (var k in drop) _lastSpawnR.Remove(k);
        }

        void CheckSource(int id, Vector3 o, float r, bool isDoor)
        {
            if (!_lastSpawnR.TryGetValue(id, out var last)) { _lastSpawnR[id] = 0f; last = 0f; }
            if (r < last) { _lastSpawnR[id] = r; return; }
            if (r - last < burstStep || r < 0.08f) return;
            _lastSpawnR[id] = r;
            int n = UnityEngine.Random.value < 0.45f ? 2 : 1;
            for (int k = 0; k < n; k++)
            {
                // 方向：门口的往房间里、沿墙面 / 地面散开（不往墙里钻）；旗子的四面八方、偏水平
                var d = UnityEngine.Random.onUnitSphere;
                if (isDoor)
                {
                    var e = transform.parent ? transform.parent.TransformPoint(eyePoint) : eyePoint;
                    var inward = e - o; inward.y = 0; inward = inward.sqrMagnitude > 1e-4f ? inward.normalized : Vector3.back;
                    float into = Vector3.Dot(d, inward); if (into < -0.15f) d -= inward * (into * 1.6f);
                }
                d.y *= isDoor ? 0.8f : 0.45f; d.Normalize();
                if (_bursts.Count >= MaxBursts)                                  // 满了：替换最老的（多半已经快被吞没）
                {
                    int oldest = 0; for (int j = 1; j < _bursts.Count; j++) if (_bursts[j].age > _bursts[oldest].age) oldest = j;
                    _bursts.RemoveAt(oldest);
                }
                float step = Mathf.Max(burstStep, r - last);
                _bursts.Add(new Burst
                {
                    src = id, dir = d, sharp = UnityEngine.Random.Range(5f, 16f),                    // 越大越尖细（指状），越小越宽（大片舌头）
                    spawnR = r, reach = r + step * UnityEngine.Random.Range(burstReach.x, burstReach.y) * (isDoor ? 1f : 0.7f), age = 0,
                });
            }
        }

        /// <summary>当前所有覆盖区（xyz 中心，w 半径，已含过渡带余量）：门口蔓延 + 旗子种子。给门后世界的视锥裁剪用。</summary>
        public void GetCoverSpheres(List<Vector4> outList, float margin = 0.6f)
        {
            outList.Clear();
            float dr = Application.isPlaying ? doorRadius : Mathf.Max(doorRadius, previewDoorRadius);
            if (door && dr > 0.02f) { var p = door.position; outList.Add(new Vector4(p.x, p.y, p.z, dr * 1.6f + _burstMax + margin)); }   // 门口那片边缘有舌头、触须，半径放宽
            if (Application.isPlaying || _seeds.Count > 0) { foreach (var sd in _seeds) if (sd.r > 0.02f) outList.Add(new Vector4(sd.pos.x, sd.pos.y, sd.pos.z, sd.r + _burstMax + margin)); }
            else if (previewSeeds != null) foreach (var sd in previewSeeds) if (sd.radius > 0.02f) outList.Add(new Vector4(sd.position.x, sd.position.y, sd.position.z, sd.radius + margin));
        }

        /// <summary>立刻把种子推送给 shader（编辑器里改了 previewSeeds 后调用）。</summary>
        public void Apply() => Push();

        void Push()
        {
            int n = 0;
            if (Application.isPlaying || _seeds.Count > 0)
            {
                foreach (var s in _seeds) { if (n >= MaxSeeds) break; Buf[n++] = new Vector4(s.pos.x, s.pos.y, s.pos.z, s.r); }
            }
            else if (previewSeeds != null)
            {
                foreach (var s in previewSeeds) { if (n >= MaxSeeds) break; Buf[n++] = new Vector4(s.position.x, s.position.y, s.position.z, s.radius); }
            }
            for (int i = n; i < MaxSeeds; i++) Buf[i] = Vector4.zero;
            Shader.SetGlobalVectorArray(IdSeeds, Buf);
            Shader.SetGlobalFloat(IdCount, n);
            float dr = Application.isPlaying ? doorRadius : Mathf.Max(doorRadius, previewDoorRadius);
            var dp = door ? door.position : Vector3.zero;
            Shader.SetGlobalVector(IdDoor, door ? new Vector4(dp.x, dp.y, dp.z, dr) : Vector4.zero);
            // 覆盖按“视线方向”算，不按真实表面的位置算：所有表面先投影到以玩家为中心的虚拟罩子（地面 + 半径 proxyRadius 的球）上，
            // 同一方向上的家具 / 墙 / 地板覆盖结果一样——玩具化的形状不受真实房间里有什么影响（用户要求）
            var e = transform.parent ? transform.parent.TransformPoint(eyePoint) : eyePoint;
            Shader.SetGlobalVector(IdEye, new Vector4(e.x, e.y, e.z, proxyRadius));
            int nb = 0; _burstMax = 0;
            if (Application.isPlaying)
                foreach (var b in _bursts)
                {
                    if (nb >= MaxBursts) break;
                    float r = SrcRadius(b.src, out var o); if (r < 0) continue;
                    float u = Mathf.Clamp01(b.age / burstTime); u = 1f - (1f - u) * (1f - u) * (1f - u);   // 先快后慢地伸出去
                    float ext = Mathf.Max(0, b.reach - r) * u;
                    _burstMax = Mathf.Max(_burstMax, ext);
                    BufA[nb] = new Vector4(o.x, o.y, o.z, ext); BufB[nb] = new Vector4(b.dir.x * b.sharp, b.dir.y * b.sharp, b.dir.z * b.sharp, 0); nb++;
                }
            for (int i = nb; i < MaxBursts; i++) { BufA[i] = Vector4.zero; BufB[i] = Vector4.zero; }
            Shader.SetGlobalVectorArray(IdBurstA, BufA); Shader.SetGlobalVectorArray(IdBurstB, BufB);
            Shader.SetGlobalFloat(IdBurstN, nb); Shader.SetGlobalFloat(IdBurstMax, _burstMax);
        }
    }
}
