// 门后世界“包住整个房间”之后的两件事：
//   Surround  房间四周（左右、身后）和门洞两侧按章节主题摆满：玩具化覆盖到房间哪面墙，那个方向的世界都不空；
//             近房间放小件、远处放大件，避开真实房间范围和门口走道，和已有摆件保持间距。
//   Spill     门前（真实房间里）的溢出物：正常渲染、一直看得见，像门后世界从洞口淌出来一截——
//             一块剪成不规则形状的毛毡地皮 / 沙子 / 地毯从门口铺出来，上面散着这一章的小东西。
using System.Collections.Generic;
using Bang.Ambient;
using UnityEditor;
using UnityEngine;

namespace Bang.EditorTools
{
    public static partial class BangSceneBuilder
    {
        // 真实房间在门后世界（门面局部）坐标里的范围：x ±4，z 0 .. 6.65
        static bool InRealRoom(Vector3 p, float m) => Mathf.Abs(p.x) < 4f + m && p.z > -m && p.z < PortalDist + 3.45f + m;

        static readonly Dictionary<int, (string[] small, string[] big)> SurroundSets = new()
        {
            [1] = (new[] { "W1_GumdropBushes", "W1_Mushroom_Lamp", "W1_ToyCar_A", "W1_ToyCar_B", "W1_Bench", "W1_LollipopTree_A", "W1_LollipopTree_B", "W1_GumdropBushes" },
                   new[] { "W1_House_Pink", "W1_House_Purple", "W1_House_Teal", "W1_House_Yellow", "W1_House_Blue", "W1_FeltTree", "W1_FeltTree", "W1_LollipopTree_A", "W1_HillMound" }),
            [2] = (new[] { "W2_BeachBall", "W2_SpadeRake", "W2_Bucket", "W2_SupplyCrates", "W2_SandbagWall", "W2_SandCastle_Small", "W2_Campfire" },
                   new[] { "W2_PalmTree", "W2_PalmTree", "W2_Tent_Blue", "W2_Tent_Pink", "W2_SandCastle_Big", "W2_DumpTruck", "W2_SandCastle_Small" }),
            [3] = (new[] { "W3_Hedge", "W3_TinSoldier_Deco", "W3_FeltPets", "W3_Hedge", "W3_TinSoldier_Deco" },
                   new[] { "W3_FeltPine", "W3_FeltPine", "W3_FeltTree_Lights", "W3_Cottage", "W3_BannerPole", "W3_Carriage", "W3_Fountain" }),
            [4] = (new[] { "W4_GiftBox_A", "W4_GiftBox_B", "W4_PartyHats", "W4_Teddy", "W4_PinataStar", "W4_GiftBox_A" },
                   new[] { "W4_BalloonBunch", "W4_BalloonBunch", "W4_CircusTent", "W4_CakeTower", "W4_IceCreamCart", "W4_GiftBox_B" }),
            [5] = (new[] { "W5_PaintCans", "W5_TinyRobots", "W2_SupplyCrates", "W5_WindupBear", "W5_PaintCans" },
                   new[] { "W5_Workbench", "W5_ToyRocket", "W5_ToyCrane", "W5_Lamp", "W5_ChimneyHouse_A", "W5_ChimneyHouse_B", "W2_SupplyCrates", "W5_GearMachine" }),
            [6] = (new[] { "W6_YarnBall", "W6_GummyBear", "W6_WrappedCandy", "W6_StackRings", "W6_ToyTrainCar" },
                   new[] { "W6_StackRings", "W6_DuckBeanie", "W4_RockingHorse", "W6_NightLight", "W4_Teddy", "W6_ToyTrainCar" }),
        };
        // 室内章节只能摆在墙里面：(x0, x1, z0, z1)
        static readonly Dictionary<int, Vector4> SurroundRect = new() { [5] = new Vector4(-6.6f, 7.0f, -6.8f, 7.2f), [6] = new Vector4(-5.0f, 5.0f, -6.3f, 7.8f) };

        static void Surround(int ch, Transform w, Transform props)
        {
            if (!SurroundSets.TryGetValue(ch, out var set)) return;
            MeshCollider col = null;
            foreach (var mf in w.GetComponentsInChildren<MeshFilter>())
                if (mf.name.Contains("_Ground")) { col = mf.gameObject.AddComponent<MeshCollider>(); break; }
            Physics.SyncTransforms();
            _groundRoot = col ? w : null;
            // 已有摆件的占位圆（地面、天幕、地皮这些大件不算）
            var occ = new List<(Vector2 c, float r)>();
            foreach (var r in w.GetComponentsInChildren<Renderer>())
            {
                var b = r.bounds;
                if (b.size.x > 4f || b.size.z > 4f || r is ParticleSystemRenderer) continue;
                var lc = w.InverseTransformPoint(b.center);
                occ.Add((new Vector2(lc.x, lc.z), Mathf.Max(b.extents.x, b.extents.z)));
            }
            var root = new GameObject("Surround").transform; root.SetParent(props, false);
            var rnd = new System.Random(9100 + ch);
            float Rf(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var roomC = new Vector3(0, 0, 3.3f);
            int placed = 0;
            bool indoor = SurroundRect.TryGetValue(ch, out var rect);
            int maxN = indoor ? 60 : 70;                                   // 组团之间的补缺
            for (int tries = 0; tries < 8000 && placed < maxN; tries++)
            {
                var p = indoor ? new Vector3(Rf(rect.x, rect.y), 0, Rf(rect.z, rect.w)) : new Vector3(Rf(-10.5f, 10.5f), 0, Rf(-6f, 10.4f));
                if (!indoor && Vector2.Distance(new Vector2(p.x, p.z), new Vector2(CycloCenter.x, CycloCenter.z)) > CycloR - 1.4f) continue;
                if (InRealRoom(p, 0.5f)) continue;
                if (Mathf.Abs(p.x) < 1.7f && p.z > -1.6f) continue;                       // 门口走道（小怪从这里出来）
                // 离房间边界的距离：近处放小件，远处放大件
                float dx = Mathf.Max(0, Mathf.Abs(p.x) - 4f), dz = Mathf.Max(0, Mathf.Max(-p.z, p.z - 6.65f));
                float dRoom = Mathf.Sqrt(dx * dx + dz * dz);
                bool big = indoor ? rnd.NextDouble() < 0.45 : dRoom > 1.3f;
                float need = big ? 0.7f : 0.38f;
                bool clash = false;
                foreach (var (c, r) in occ) if (Vector2.Distance(c, new Vector2(p.x, p.z)) < need + r * 0.85f) { clash = true; break; }
                if (clash) continue;
                var list = big ? set.big : set.small;
                string n = list[rnd.Next(list.Length)];
                var g = Place(n, root, new Vector3(p.x, GroundY(p.x, p.z), p.z), 0);
                if (!g) continue;
                var to = roomC - p; to.y = 0;
                g.transform.rotation = Quaternion.LookRotation(to.normalized) * Quaternion.Euler(0, 90 + Rf(-35, 35), 0);
                if (indoor) g.transform.localScale *= big ? 1.6f : 1.35f;
                else g.transform.localScale *= big ? Mathf.Lerp(1.3f, 1.9f, Mathf.InverseLerp(1.3f, 6.5f, dRoom)) : 1.25f;   // 房间四周放大：离得近也有体量
                if (n == "W6_GummyBear" || n == "W6_WrappedCandy") g.transform.localScale *= 2f;
                SettleOnGround(g);
                var gb = Bounds(g);
                occ.Add((new Vector2(p.x, p.z), Mathf.Max(gb.extents.x, gb.extents.z)));
                placed++;
            }
            LodPush(root, 1.7f);
            if (col) Object.DestroyImmediate(col);
            // 房间四周也有小生命绕着飞（室内章节不放鸟）
            if (!indoor) BangCritters.Spawn(w, ch != 2, 6, new Vector3(0.2f, 0, 3.2f), new Vector2(6.2f, 5.6f), new Vector2(1.2f, 2.6f), 9200 + ch);
        }

        /// <summary>真实房间范围内把门后世界的地形压平（向外 2.5 米平滑过渡回原来的起伏）：
        /// 门后世界也在房间被覆盖处绘制，房间所在位置的山丘会从房间里挡住门洞。改的是网格副本（存到 Generated）。</summary>
        static void FlattenNearRoom(GameObject ground, Transform w)
        {
            if (!ground) return;
            foreach (var mf in ground.GetComponentsInChildren<MeshFilter>())
            {
                var src = mf.sharedMesh; if (!src) continue;
                var m = Object.Instantiate(src); m.name = src.name + "_Flat";
                var v = m.vertices;
                // 地形外缘：最后 1.6 m 平滑降到接缝带高度（-0.03）。原来地形在外圈山丘上直接截断，从房间看过去是一道断崖、
                // 断崖外面是单面地形的背面（看着像破洞）——玩具化铺满后尤其明显（用户：“很多模型有破面”）
                var lb = new Bounds(w.InverseTransformPoint(mf.transform.TransformPoint(v[0])), Vector3.zero);
                for (int i = 1; i < v.Length; i++) lb.Encapsulate(w.InverseTransformPoint(mf.transform.TransformPoint(v[i])));
                var gc = new Vector2(lb.center.x, lb.center.z); float gr = Mathf.Min(lb.extents.x, lb.extents.z);
                for (int i = 0; i < v.Length; i++)
                {
                    var p = w.InverseTransformPoint(mf.transform.TransformPoint(v[i]));
                    float dx = Mathf.Max(0, Mathf.Abs(p.x) - 4.3f), dz = Mathf.Max(0, Mathf.Max(-0.3f - p.z, p.z - 7.0f));
                    float k = Mathf.SmoothStep(0, 1, Mathf.Sqrt(dx * dx + dz * dz) / 2.5f);
                    if (k < 1) p.y = Mathf.Lerp(Mathf.Min(p.y, 0f), p.y, k);
                    float e = Mathf.SmoothStep(0, 1, (gr - Vector2.Distance(new Vector2(p.x, p.z), gc)) / 1.6f);
                    p.y = Mathf.Lerp(-0.03f, p.y, e);
                    v[i] = mf.transform.InverseTransformPoint(w.TransformPoint(p));
                }
                m.vertices = v; m.RecalculateNormals(); m.RecalculateBounds();
                mf.sharedMesh = SaveMesh(m, m.name);
            }
        }

        // ───────── 门前溢出 ─────────
        static void Spill(int ch, Transform portal)
        {
            var root = new GameObject("Spill").transform; root.SetParent(portal, false);
            var rnd = new System.Random(9300 + ch);
            // 开门时门前地面要“漫开”（PortalOpen 逐帧改顶点）：门前的小路 / 花草模型要可读写，打包后才拿得到顶点
            foreach (var path in System.IO.Directory.GetFiles(Proc, "*Spill*.fbx"))
                if (AssetImporter.GetAtPath(path.Replace('\\', '/')) is ModelImporter mi && !mi.isReadable) { mi.isReadable = true; mi.SaveAndReimport(); }
            // 地皮：从门口（z≈0）往房间里铺开的不规则一块，前宽后窄
            (Material mat, Color a, Color b) patch = ch switch
            {
                1 => (BangArt.TurfGroundVC, new Color(0.62f, 0.86f, 0.6f), new Color(0.74f, 0.92f, 0.66f)),
                2 => (BangArt.SandVC, new Color(1f, 0.84f, 0.8f), new Color(1f, 0.94f, 0.72f)),
                3 => (BangArt.TurfGroundVC, new Color(0.7f, 0.88f, 0.68f), new Color(0.84f, 0.78f, 0.98f)),
                4 => (BangArt.Mat("M_ConfettiFeltVC", null), new Color(0.78f, 0.94f, 0.84f), new Color(1f, 0.88f, 0.9f)),
                5 => (BangArt.FeltVC, new Color(1f, 0.84f, 0.72f), new Color(0.78f, 0.94f, 0.86f)),
                6 => (BangArt.FeltVC, new Color(0.76f, 0.7f, 1f), new Color(0.64f, 0.84f, 1f)),
                _ => (null, Color.white, Color.white),
            };
            if (patch.mat) MeshObj($"SpillPatch_W{ch}", root, SpillPatchMesh(rnd, ch == 2 ? 1.9f : 1.6f, ch == 2 ? 2.1f : 1.7f, patch.a, patch.b), patch.mat);
            if (ch <= 4) Inst($"{Proc}/W{ch}_SpillFlora.fbx", root, BangArt.FeltVC);      // 地皮上的绒布花草（第 2 章：贝壳、海星、卵石、海滩草）
            // 用户：门前的东西尽量放在门框附近，不挡敌人走的路线、不卡脚。
            // 原来摆件散在门前 0.3～1.6 米、左右 ±1.4 米，正好是敌人出门后走的扇形路线上；现在一律收到门框两侧（|x| ≥ 0.95、离门面 ≤ 0.6 米），
            // 门洞正前方 ±0.95 米宽的通道保持空着
            GameObject P(string n, float x, float z, float yaw, float s = 1f)
            {
                float sx = x >= 0 ? 1f : -1f;
                x = sx * Mathf.Clamp(Mathf.Abs(x), 0.95f, 1.45f) ; z = Mathf.Clamp(0.15f + (z - 0.3f) * 0.3f, 0.12f, 0.6f);
                var g = Place(n, root, new Vector3(x, ch == 2 ? 0.02f : 0.012f, z), yaw);
                if (g && s != 1f) g.transform.localScale *= s;
                return g;
            }
            switch (ch)
            {
                case 1:
                    Inst(Proc + "/W1_BrickPathSpill.fbx", root, BangArt.ToyVC);       // 积木小路从门洞里一直铺到房间地上（尽头两块散砖还没铺好）
                    P("W1_GumdropBushes", 0.85f, 0.45f, 20); P("W1_GumdropBushes", -1.05f, 0.9f, -40, 0.8f);
                    P("W1_Mushroom_Lamp", -0.75f, 0.35f, 0); P("W1_ToyCar_A", 0.9f, 1.25f, -130); P("W1_LollipopTree_B", 1.35f, 0.25f, 0, 0.6f);
                    break;
                case 2:
                    P("W2_Bucket", -0.75f, 0.85f, 30, 0.8f); P("W2_SpadeRake", 0.6f, 1.05f, -50); P("W2_BeachBall", 1.15f, 1.6f, 0, 0.8f);
                    P("W2_SandCastle_Small", -1.15f, 1.5f, 20, 0.7f); P("W2_SupplyCrates", 1.4f, 0.4f, -15, 0.7f);
                    break;
                case 3:
                    P("W3_Hedge", -1.1f, 0.45f, 10, 0.9f); P("W3_TinSoldier_Deco", 0.55f, 0.85f, -20); P("W3_TinSoldier_Deco", 0.85f, 1.05f, -35);
                    P("W3_FeltPine", 1.35f, 0.35f, 0, 0.5f); P("W3_FeltPets", -0.7f, 1.2f, 30);
                    break;
                case 4:
                {
                    P("W4_GiftBox_B", -0.85f, 0.8f, 25, 0.9f); P("W4_PartyHats", 0.65f, 1.15f, 0); P("W4_PinataStar", -1.2f, 1.45f, -30, 0.6f);
                    // 系在礼物盒上的一束气球：浮力让它立着，吊点在底部（倒立的单摆，周期由绳长决定）
                    var pivot = new GameObject("BalloonTether").transform; pivot.SetParent(root, false); pivot.localPosition = new Vector3(1.25f, 0.2f, 0.3f);
                    P("W4_GiftBox_A", 1.25f, 0.45f, 10);
                    var bb = Place("W4_BalloonBunch", pivot, Vector3.zero, 0);
                    if (bb) { bb.transform.localScale *= 0.75f; var pd = pivot.gameObject.AddComponent<Pendulum>(); pd.length = 0.8f; pd.amplitudeDeg = 3f; pd.twistDeg = 10f; }
                    break;
                }
                case 5:
                {
                    // 滚出门的小齿轮、螺母（平躺在地上）+ 一只坐在门口的发条兔（不动）
                    foreach (var (n, x, z, s) in new[] { ("W5_Gear_S", 0.7f, 0.6f, 0.8f), ("W5_Gear_M", -0.95f, 0.85f, 0.55f), ("W5_Gear_S", 1.25f, 1.3f, 0.6f), ("W5_Gear_S", -0.4f, 1.55f, 0.5f) })
                    {
                        var g = Inst($"{Proc}/{n}.fbx", root, MetalVC);
                        if (!g) continue;
                        float gx = (x >= 0 ? 1f : -1f) * Mathf.Clamp(Mathf.Abs(x), 0.95f, 1.45f), gz = Mathf.Clamp(0.15f + (z - 0.3f) * 0.3f, 0.12f, 0.6f);   // 收到门框两侧
                        g.transform.localPosition = new Vector3(gx, 0.018f * s, gz); g.transform.localRotation = Quaternion.Euler(90, (float)rnd.NextDouble() * 360f, 0); g.transform.localScale = Vector3.one * s;
                    }
                    P("W5_PaintCans", -1.25f, 0.45f, 30, 0.8f);
                    // 发条兔静止摆在门口（用户：门外的兔子玩具会动，玩家以为是敌人）。门里的发条兔照常走
                    var bun = Place("W5_WindupBunny", root, new Vector3(1.1f, 0, 0.35f), 200);   // 坐在门框右侧（原来在门正前方 1 米，挡路）
                    if (bun) bun.transform.localScale *= 1.5f;
                    break;
                }
                case 6:
                    P("W6_YarnBall", -0.9f, 1.0f, 0, 0.9f); P("W6_WrappedCandy", 0.55f, 0.7f, 40, 2.2f); P("W6_WrappedCandy", 0.85f, 1.15f, -20, 2f);
                    P("W6_WrappedCandy", -0.4f, 1.5f, 70, 2.1f); P("W6_GummyBear", 1.2f, 0.5f, -30, 1.5f); P("W6_GummyBear", -1.25f, 0.45f, 20, 1.4f);
                    foreach (var (c, x, z, yaw) in new[] { ("Pink", 0.25f, 1.35f, 70f), ("Blue", 0.45f, 1.55f, 100f) })
                    {
                        var g = Inst($"{Proc}/W6_Crayon_{c}.fbx", root, BangArt.Mat("M_CrayonVC", null));
                        if (!g) continue;
                        g.transform.localPosition = new Vector3(x + 0.8f, 0.07f * 0.5f, Mathf.Clamp(0.15f + (z - 0.3f) * 0.3f, 0.12f, 0.6f)); g.transform.localRotation = Quaternion.Euler(0, yaw, 90); g.transform.localScale = Vector3.one * 0.5f;   // 收到门框右侧
                    }
                    break;
            }
        }

        /// <summary>从门口铺出来的不规则地皮：半椭圆（宽 2·hw、伸进房间 depth）+ 低频起伏的剪裁边，薄片带圆边，两色顶点色斑驳。</summary>
        static Mesh SpillPatchMesh(System.Random rnd, float hw, float depth, Color ca, Color cb, int seg = 96)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            const float th = 0.012f;
            float p1 = (float)rnd.NextDouble() * 6f, p2 = (float)rnd.NextDouble() * 6f, p3 = (float)rnd.NextDouble() * 6f;
            Vector3 Edge(float a)                                   // a: 0..π（从右到左绕到门口两侧），门口那边（z≈-0.05）是直边
            {
                float r = 1 + 0.12f * Mathf.Sin(a * 3 + p1) + 0.07f * Mathf.Sin(a * 7 + p2) + 0.035f * Mathf.Sin(a * 13 + p3);
                return new Vector3(Mathf.Cos(a) * hw * r, 0, -0.05f + Mathf.Sin(a) * depth * r);
            }
            Color Mot(Vector3 p) => Color.Lerp(ca, cb, Mathf.PerlinNoise(p.x * 1.7f + p1, p.z * 1.7f + p2));
            // 顶面：扇形三角化（中心在门口附近）
            var center = new Vector3(0, th, 0.35f);
            v.Add(center); c.Add(Mot(center));
            for (int i = 0; i <= seg; i++)
            {
                var e = Edge(i / (float)seg * Mathf.PI) + Vector3.up * th;
                v.Add(e); c.Add(Mot(e));
                if (i < seg) t.AddRange(new[] { 0, i + 2, i + 1 });
            }
            // 门口直边补到中心（右下 → 左下）
            t.AddRange(new[] { 0, 1, seg + 1 });
            // 侧边：一圈向下的圆边
            int top0 = 1, side0 = v.Count;
            for (int i = 0; i <= seg; i++) { var e = Edge(i / (float)seg * Mathf.PI); v.Add(e * 1.01f); c.Add(Mot(e) * 0.9f); }
            for (int i = 0; i < seg; i++)
            {
                int a0 = top0 + i, a1 = top0 + i + 1, b0 = side0 + i, b1 = side0 + i + 1;
                t.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
            }
            var m = new Mesh { name = "SpillPatch" }; m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
