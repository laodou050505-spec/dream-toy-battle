// 搭场景的最后一道：去穿模（用户硬要求“任何场景都不能有穿模”）。
// 各套摆放逻辑（手摆布局、组团、巨型玩具环、四周散件、门前溢出）各自只用粗略的圆半径避让，互相并不知道对方，
// 所以最后按真实网格统一检查一遍（检测方法同 BangClipAudit：顶点落在另一件封闭网格里面、深过 1 cm）：
//   ① 摆件互相插进去：优先级低 / 体积小的那件沿离开对方的方向一点点挪开、重新贴地，挪 1.7 m 还不行就删掉；
//   ② 合并成一个网格的散布物（花草、碎积木）：按颗粒（焊接后的连通块）拆开，长进别的东西里的那几颗剔掉；
//   ③ 会动的东西按整条路线扫：小火车跑一整圈，挡路的摆件挪开；小鸟 / 蝴蝶航线穿过东西就整体抬高。
using System.Collections.Generic;
using System.Linq;
using Bang.Ambient;
using Bang.Portal;
using UnityEditor;
using UnityEngine;

namespace Bang.EditorTools
{
    public static partial class BangSceneBuilder
    {
        class Unit
        {
            public Transform root; public List<Renderer> rs = new(); public int prio; public bool movable, strip, spill, frameDeco, onPath;   // onPath：沿固定路线 / 转轴运动的装置（小火车、传送带、旋转木马…），高度是对齐好的
            public Bounds B { get { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; } }
        }

        static readonly System.Type[] Movers = { typeof(TrainLoop), typeof(Carousel), typeof(Pendulum), typeof(Critter), typeof(BeltRider), typeof(Drift), typeof(GearTrain), typeof(Spin), typeof(Precess), typeof(Bob), typeof(Rock), typeof(CatapultArm) };

        // 沿固定路线走 / 绕轴转一大圈的：整组不能挪（会和轨道、转台分家）；原地摆动的（单摆、浮动、摇晃、自转）可以连同支点一起挪
        static readonly System.Type[] PathMovers = { typeof(TrainLoop), typeof(Carousel), typeof(Critter), typeof(BeltRider), typeof(GearTrain), typeof(CatapultArm) };
        static readonly string[] Groups = { "Props", "Rich", "Surround", "GiantToys", "Spill" };
        // 结构件：轨道、路面、地形、传送带、墙壳、门框… 摆在那里是和别的东西对齐的，不能挪
        static readonly string[] Structural = { "Track", "Path", "Ground", "Rail", "Belt", "Shell", "Wall", "Frame", "Arch", "Bricks", "Fence", "Rainbow", "Hill", "Mound", "Pole", "Cord" };

        /// <summary>摆件的“整体”：往上找到直接挂在分组（Props / 组团 Vig_* / 巨型玩具 / 四周 / 溢出）下面的那一层。</summary>
        static Transform MoveRoot(Transform o, Transform stop)
        {
            for (var t = o; t && t != stop; t = t.parent)
            {
                var p = t.parent; if (!p) break;
                if (System.Array.IndexOf(Groups, p.name) >= 0 || p.name.StartsWith("Vig_") || p.name.StartsWith("Frame_")) return t;
            }
            return null;
        }

        static bool HasMover(Transform t, Transform stop)
        {
            for (; t && t != stop; t = t.parent)
                foreach (var m in Movers) if (t.GetComponent(m)) return true;
            return false;
        }

        // 门的进出通道（门面局部坐标：门后为 -z）；和规则层 Config.portal（inside 0.7 + spawnDepth 0.9 = 门后 1.6 m 出生）对应
        const float LaneHalf = 0.5f, LaneBack = 1.95f, LaneFront = 0.5f;
        const float ClashDepth = 0.006f;                 // 比巡检（8 mm）更严：去穿模做完，巡检一定是 0

        static float ClashOf(Unit x, Unit y, Dictionary<Renderer, MeshCollider> cols)
        {
            float worst = 0;
            foreach (var a in x.rs)
                foreach (var b in y.rs)
                {
                    if (!a.bounds.Intersects(b.bounds) || BangClipAudit.Attached(a, b)) continue;
                    worst = Mathf.Max(worst, Pierce(a, b, cols), Pierce(b, a, cols));
                    if (worst >= ClashDepth) return worst;
                }
            return worst;
        }

        static float Pierce(Renderer a, Renderer b, Dictionary<Renderer, MeshCollider> cols)
        {
            if (!cols.TryGetValue(b, out var c)) return 0;
            var bb = b.bounds; bb.Expand(0.001f);
            var v = a.GetComponent<MeshFilter>().sharedMesh.vertices;
            int step = 1;                                   // 全部顶点都查（抽样会漏掉边角的浅穿插）
            float worst = 0;
            for (int i = 0; i < v.Length; i += step)
            {
                var p = a.transform.TransformPoint(v[i]);
                if (!bb.Contains(p)) continue;
                worst = Mathf.Max(worst, BangClipAudit.Depth(c, p));
                if (worst >= ClashDepth) break;
            }
            return worst;
        }

        /// <summary>该章所有门的角度（度）。Unity 里第 i 扇门 = 0° 门绕房间中心转 yaw = angles[i]（逻辑层 x 取反后正好相等）。</summary>
        /// 世界的门通道 = 这个世界两关的门 ∪ 无尽模式的四扇门（无尽在每个世界都开四扇门，通道都要清出来）。
        public static float[] ChapterDoorAngles(int ch)
        {
            // ch 是世界号（1..6，BuildWorldScenes 传进来的），不是关卡号
            var set = new System.Collections.Generic.SortedSet<float>();
            for (int c = 1; c <= Bang.Logic.CHAPTERS.Count; c++) if (Bang.Logic.CHAPTERS.World(c) == ch) foreach (var a in Bang.Logic.CHAPTERS.Get(c).portals) set.Add((float)a);
            foreach (var a in Bang.Logic.CHAPTERS.Get(Bang.Logic.CHAPTERS.Count).portals) set.Add((float)a);
            return set.ToArray();
        }

        static void Declutter(Transform portal, int ch)
        {
            var w = portal.childCount > 0 ? portal.GetComponentsInChildren<PortalWorld>(true).Select(x => x.transform).FirstOrDefault() : null;
            var local = w ? w : portal;
            bool indoor = SurroundRect.TryGetValue(ch, out var rect);
            // 地面碰撞体（只用来贴地，不参与穿模判断）
            var ground = new List<MeshCollider>();
            foreach (var mf in portal.GetComponentsInChildren<MeshFilter>())
                if (mf.name.Contains("Ground") && mf.GetComponent<MeshRenderer>() && mf.GetComponent<MeshRenderer>().bounds.size.x > 4f) ground.Add(mf.gameObject.AddComponent<MeshCollider>());
            float GroundAt(Vector3 wp, float fallback)
            {
                float best = float.NegativeInfinity;
                foreach (var g in ground)
                    if (g.Raycast(new Ray(new Vector3(wp.x, wp.y + 30, wp.z), Vector3.down), out var h, 80f)) best = Mathf.Max(best, h.point.y);
                return float.IsNegativeInfinity(best) ? fallback : best;
            }

            // 拆成“件”
            var units = new Dictionary<Transform, Unit>();
            foreach (var r in portal.GetComponentsInChildren<Renderer>())
            {
                if (BangClipAudit.Skip(r)) continue;
                var o = BangClipAudit.Owner(r);
                var mr = MoveRoot(o, portal);
                bool strip0 = o.name.Contains("Flora") || o.name.Contains("Debris");
                // 可挪的摆件按“整体”算一件（气球束连同系绳支点、礼物盒连同上面的气球一起挪）
                bool pathMover = mr && PathMovers.Any(m => mr.GetComponentInChildren(m, true));
                var asset = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(o.gameObject) ?? "";
                bool structural = !asset.StartsWith(BangArt.PropDir) && Structural.Any(n => o.name.Contains(n));   // P2 摆件（沙袋墙之类）不按名字算结构件
                bool isProp = !structural && (asset.StartsWith(BangArt.PropDir) || asset.EndsWith(".fbx") || BangClipAudit.Path(o).Contains("/GiantToys/"));
                var key = !strip0 && mr && !pathMover && isProp ? mr : o;
                if (!units.TryGetValue(key, out var u))
                {
                    u = new Unit { root = key };
                    string path = BangClipAudit.Path(key);
                    u.strip = strip0;
                    u.onPath = pathMover;
                    u.spill = path.Contains("/Spill/");
                    u.frameDeco = key.parent && key.parent.name.StartsWith("Frame_") && asset.StartsWith(BangArt.PropDir);   // 挂在门框上的装饰（月亮、星星、玩具飞机、软糖熊…）
                    if (u.frameDeco) { u.movable = true; u.prio = 0; }
                    u.movable = !u.strip && isProp && key == mr && !Pinned.Any(n => BangClipAudit.Path(o).Contains(n));   // 组团里的轨道、路面这类结构件不挪（会和它的小火车分家）
                    u.prio = path.Contains("/Surround/") ? 0 : (path.Contains("/Vignettes/") || path.Contains("/GiantToys/")) ? 1 : 2;
                    units[key] = u;                    // 按“整体”记（原来按零件记：一座塔、一组摆件被拆成好几件，各自挪 / 落地，整体被来回推）
                }
                u.rs.Add(r);
            }
            var list = units.Values.ToList();
            var cols = new Dictionary<Renderer, MeshCollider>();
            var added = new List<MeshCollider>();
            foreach (var u in list)
                foreach (var r in u.rs)
                {
                    var mc = r.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                    cols[r] = mc; added.Add(mc);
                }
            bool back = Physics.queriesHitBackfaces; Physics.queriesHitBackfaces = true;
            Physics.SyncTransforms();
            int moved = 0, removed = 0, stripped = 0, raised = 0, settled = 0, settledIslands = 0; var stuck = new List<string>();
            try
            {
                bool Allowed(Unit u, Vector3 wp)
                {
                    var p = local.InverseTransformPoint(wp);
                    if (u.spill) return Mathf.Abs(p.x) > 0.55f && p.z > 0.15f && p.z < 2.2f && Mathf.Abs(p.x) < 2.0f;
                    if (InRealRoom(p, 0.3f)) return false;
                    if (Mathf.Abs(p.x) < 1.2f && p.z > -1.4f) return false;                         // 门口走道
                    if (indoor) return p.x > rect.x + 0.3f && p.x < rect.y - 0.3f && p.z > rect.z + 0.3f && p.z < rect.w - 0.3f;
                    return Vector2.Distance(new Vector2(p.x, p.z), new Vector2(CycloCenter.x, CycloCenter.z)) < CycloR - 1.2f;
                }
                List<Unit> Near(Unit u) { var b = u.B; b.Expand(0.02f); return list.Where(o => o != u && o.root && !o.strip && o.B.Intersects(b)).ToList(); }
                bool Clear(Unit u) => Near(u).All(o => ClashOf(u, o, cols) < ClashDepth);
                System.Func<Unit, bool> settleOne = null;            // 挪到新位置后当场落到新位置的支撑面上（下面定义）
                bool Relocate(Unit u, Vector3 away)
                {
                    var t = u.root; var p0 = t.position; var r0 = t.rotation;
                    if (u.frameDeco)
                    {
                        // 门框装饰：沿门面法线往房间那边推出来（挂在门框前面，而不是嵌进门框里），最多 15 cm
                        for (int k = 1; k <= 15; k++)
                        {
                            t.position = p0 + portal.forward * (0.01f * k); Physics.SyncTransforms();
                            if (Clear(u)) return true;
                        }
                        t.position = p0; Physics.SyncTransforms();
                        return false;
                    }
                    away.y = 0; if (away.sqrMagnitude < 1e-4f) away = Quaternion.Euler(0, Random.Range(0, 360f), 0) * Vector3.forward;
                    away.Normalize();
                    float g0 = GroundAt(p0, p0.y);
                    foreach (float turn in new[] { 0f, 35f, -35f, 70f, -70f })
                    {
                        var dir = Quaternion.Euler(0, turn, 0) * away;
                        for (int k = 1; k <= 14; k++)
                        {
                            var p = p0 + dir * (0.12f * k);
                            p.y = p0.y + (GroundAt(p, g0) - g0);
                            if (!Allowed(u, p)) continue;
                            t.position = p; t.rotation = r0; Physics.SyncTransforms();
                            if (settleOne != null && Settleable(u)) settleOne(u);
                            if (Clear(u)) return true;
                        }
                    }
                    t.position = p0; t.rotation = r0; Physics.SyncTransforms();
                    return false;
                }
                void Remove(Unit u)
                {
                    foreach (var r in u.rs) { cols.Remove(r); }
                    Object.DestroyImmediate(u.root.gameObject); list.Remove(u); removed++;
                }

                // ⓪ 贴地（用户：“有些模型悬空摆放”）——在互相推开之前做：从物体上方往下打射线，取物体腰线以下最高的支撑面
                //    （地面或底下的实体：桥、桌子、搁板…，不算自己）；底部离支撑面 > 1.5 cm 就落下去（再陷 6 mm），
                //    底部被地形埋进去 > 3 cm（摆的时候按高度 0 摆在山坡上）就抬上来；云、气球、飞鸟、吊饰这类本来就浮着 / 挂着的不动
                var support = new HashSet<Collider>(ground);
                foreach (var su in list) if (su.root && !su.strip) foreach (var r in su.rs) if (r && cols.TryGetValue(r, out var sc)) support.Add(sc);
                // 巡检不查的大件 / 外壳（搁板、箱底、城墙顶、地台…）也能托住东西：临时加碰撞体，只用来找支撑面
                foreach (var mr in portal.GetComponentsInChildren<MeshRenderer>())
                {
                    if (!mr.enabled || !mr.gameObject.activeInHierarchy || cols.ContainsKey(mr) || mr.GetComponent<Collider>()) continue;
                    var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                    if (mr.name.Contains("Flora") || (mr.sharedMaterial && mr.sharedMaterial.renderQueue >= 3000)) continue;
                    if (mr.bounds.size.x > 30f || new[] { "Sky", "Cyclo", "Dome", "Apron", "Backdrop", "Mountain", "Hills_Far" }.Any(n => BangClipAudit.Path(mr.transform).Contains(n))) continue;   // 天幕、远山、地裙不算
                    var mc = mr.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; added.Add(mc); support.Add(mc);
                }
                Physics.SyncTransforms();
                settleOne = u =>
                {
                        var b = u.B; float hi = float.NegativeInfinity, lo = float.PositiveInfinity; int n = 0;
                        foreach (var off in new[] { Vector2.zero, new Vector2(0.3f, 0.3f), new Vector2(-0.3f, 0.3f), new Vector2(0.3f, -0.3f), new Vector2(-0.3f, -0.3f) })
                        {
                            var o = new Vector3(b.center.x + b.extents.x * off.x, b.max.y + 0.5f, b.center.z + b.extents.z * off.y);
                            float sY = float.NegativeInfinity, gY = GroundAt(o, float.NegativeInfinity);
                            foreach (var h in Physics.RaycastAll(o, Vector3.down, 60f))
                                if (h.point.y <= b.center.y && h.point.y > gY - 0.05f && !h.collider.transform.IsChildOf(u.root) && support.Contains(h.collider)) sY = Mathf.Max(sY, h.point.y);
                            if (gY > sY) sY = gY;                                // 地形表面不论多高都算（挪到山坡上整个埋进去的，要抬上来，不能当成悬空往下压）
                            if (float.IsNegativeInfinity(sY)) continue;
                            hi = Mathf.Max(hi, sY); lo = Mathf.Min(lo, sY); n++;
                        }
                        if (n == 0) return false;
                        if (b.min.y - hi > 0.015f) { u.root.position += Vector3.down * (b.min.y - hi + 0.006f); Physics.SyncTransforms(); return true; }
                        else if (lo - b.min.y > 0.03f) { u.root.position += Vector3.up * (lo - b.min.y - 0.006f); Physics.SyncTransforms(); return true; }
                        return false;
                };
                void SettleAll()
                {
                    foreach (var u in list.Where(u => u.root && !u.strip && !u.frameDeco && Settleable(u)).OrderBy(u => u.B.min.y).ToList())
                        if (settleOne(u)) settled++;
                }
                SettleAll();

                // ⓪b 清出门的进出通道（门后出生点 → 门槛外 0.5 m、宽 1 m）：小怪只走这条路，路上不能有摆件（用户：小怪卡在门里）
                //     门框脚下的装饰（毛毡宠物、橡皮鸭…）往两侧让；其他摆件往离通道近的那一侧挪，挪不开就删
                // 多门章节：每扇门各一条通道（0° 门绕房间中心转到该章每个门的角度；用户：小怪从别处出来就给它开一扇门）
                var lanes = new List<(Vector3 pos, Quaternion rot)>();
                foreach (var a in ChapterDoorAngles(ch)) { var q = Quaternion.Euler(0, a, 0); lanes.Add((q * portal.position, q * portal.rotation)); }
                Vector3 ToLane(int k, Vector3 wp) => Quaternion.Inverse(lanes[k].rot) * (wp - lanes[k].pos);
                int LaneOf(Unit u)
                {
                    var b = u.B; if (b.min.y > 0.7f) return -1;
                    for (int k = 0; k < lanes.Count; k++)
                    {
                        float x0 = 1e9f, x1 = -1e9f, z0 = 1e9f, z1 = -1e9f;
                        for (int i = 0; i < 4; i++) { var lp = ToLane(k, new Vector3((i & 1) == 0 ? b.min.x : b.max.x, b.center.y, (i & 2) == 0 ? b.min.z : b.max.z)); x0 = Mathf.Min(x0, lp.x); x1 = Mathf.Max(x1, lp.x); z0 = Mathf.Min(z0, lp.z); z1 = Mathf.Max(z1, lp.z); }
                        if (x1 > -LaneHalf && x0 < LaneHalf && z1 > -LaneBack && z0 < LaneFront) return k;
                    }
                    return -1;
                }
                bool InLane(Unit u) => LaneOf(u) >= 0;
                foreach (var u in list.Where(u => u.root && !u.strip && !u.onPath && u.movable && InLane(u)).ToList())
                {
                    int k0 = LaneOf(u);
                    float cx = ToLane(k0, u.B.center).x; float sgn = cx >= 0 ? 1f : -1f;
                    var right = lanes[k0].rot * Vector3.right;
                    var t = u.root; var p0 = t.position; bool ok = false;
                    for (int k = 1; k <= 16 && !ok; k++)
                    {
                        t.position = p0 + right * (sgn * 0.06f * k); Physics.SyncTransforms();
                        if (InLane(u)) continue;
                        if (!u.frameDeco && settleOne != null) settleOne(u);
                        ok = Clear(u);
                    }
                    if (ok) moved++; else { t.position = p0; Physics.SyncTransforms(); Remove(u); }
                }

                // ① 摆件互相插入
                void Unclash(int passes)
                {
                    for (int pass = 0; pass < passes; pass++)
                    {
                        bool any = false;
                        var order = list.Where(u => !u.strip).ToList();
                        for (int i = 0; i < order.Count; i++)
                        {
                            var x = order[i]; if (!x.root) continue;
                            foreach (var y in Near(x))
                            {
                                if (!x.root || !y.root || y.strip) continue;
                                if (ClashOf(x, y, cols) < ClashDepth) continue;
                                any = true;
                                Unit lose, win;
                                if (x.movable && y.movable)
                                {
                                    if (x.prio != y.prio) { lose = x.prio < y.prio ? x : y; }
                                    else { var bx = x.B.size; var by = y.B.size; lose = bx.x * bx.y * bx.z <= by.x * by.y * by.z ? x : y; }
                                }
                                else if (x.movable) lose = x;
                                else if (y.movable) lose = y;
                                else { stuck.Add($"{BangClipAudit.Path(x.root)} ↔ {BangClipAudit.Path(y.root)} 深 {ClashOf(x, y, cols) * 100:0.0} cm @ {x.B.center}"); continue; }
                                win = lose == x ? y : x;
                                if (Relocate(lose, lose.B.center - win.B.center)) moved++;
                                else Remove(lose);
                                if (!x.root) break;
                            }
                        }
                        if (!any) break;
                    }
                }
                Unclash(3);
                // ①b 挪开的东西可能挪到了坡上 / 底下的东西被挪走（叠放的）——再落一次地，落完再查一遍插入
                SettleAll();
                Unclash(2);


                // ③a 小火车：跑一整圈，每一步检查车厢有没有撞上别的东西
                foreach (var train in portal.GetComponentsInChildren<TrainLoop>())
                {
                    if (train.cars == null || train.cars.Length == 0) continue;
                    var carUnits = list.Where(u => u.root && train.cars.Any(c => c && u.root.IsChildOf(c))).ToList();
                    float perim = Mathf.PI * (3 * (train.a + train.b) - Mathf.Sqrt((3 * train.a + train.b) * (train.a + 3 * train.b)));
                    const int N = 90;
                    for (int k = 0; k < N; k++)
                    {
                        train.Step(perim / N / Mathf.Max(0.01f, train.speed));
                        Physics.SyncTransforms();
                        foreach (var cu in carUnits)
                            foreach (var o in Near(cu))
                            {
                                if (carUnits.Contains(o) || ClashOf(cu, o, cols) < ClashDepth) continue;
                                // 离开轨道：椭圆外的往外推、里面的往里推
                                var lp = train.transform.InverseTransformPoint(o.B.center);
                                var q = new Vector2((lp.x - train.center.x) / train.a, (lp.z - train.center.y) / train.b);
                                var outDir = train.transform.TransformDirection(new Vector3(q.x / train.a, 0, q.y / train.b)).normalized * (q.magnitude >= 1 ? 1 : -1);
                                if (o.movable && Relocate(o, outDir)) { moved++; continue; }
                                if (o.movable) { Remove(o); continue; }
                                stuck.Add($"火车 ↔ {BangClipAudit.Path(o.root)}");
                            }
                    }
                    train.Step(0);
                }

                // 合并成一个网格的花草：按颗粒落到地面上
                foreach (var u in list.Where(u => u.strip && u.root && u.root.name.Contains("Flora")).ToList())
                    foreach (var r in u.rs)
                    {
                        if (!r) continue;
                        var mf = r.GetComponent<MeshFilter>();
                        var m = SettleIslands(mf, ground, out int n);
                        if (n == 0) continue;
                        settledIslands += n;
                        mf.sharedMesh = SaveMesh(m, m.name);
                        if (cols.TryGetValue(r, out var mc)) mc.sharedMesh = mf.sharedMesh;
                    }

                // ② 合并散布物：剔除长进别的东西里的颗粒
                var solids = list.Where(u => !u.strip && u.root).SelectMany(u => u.rs).Where(r => r && cols.ContainsKey(r)).ToList();
                foreach (var u in list.Where(u => u.strip && u.root).ToList())
                    foreach (var r in u.rs)
                    {
                        var mf = r.GetComponent<MeshFilter>();
                        int before = mf.sharedMesh.triangles.Length / 3;
                        var m = StripIslands(mf, solids, cols, out int cut);
                        if (cut == 0) continue;
                        stripped += cut;
                        if (m.triangles.Length == 0) { cols.Remove(r); Object.DestroyImmediate(r.gameObject); continue; }   // 整块都剔空了
                        mf.sharedMesh = SaveMesh(m, m.name);
                        if (cols.TryGetValue(r, out var mc)) mc.sharedMesh = mf.sharedMesh;
                    }

                // ③b 小鸟 / 蝴蝶：航线穿过东西就整体抬高
                foreach (var cr in portal.GetComponentsInChildren<Critter>())
                {
                    var par = cr.transform.parent;
                    for (int lift = 0; lift < 10; lift++)
                    {
                        bool hit = false;
                        for (int k = 0; k < 96 && !hit; k++)
                        {
                            var wp = par ? par.TransformPoint(cr.PathPoint(k, 96)) : cr.PathPoint(k, 96);
                            foreach (var s in solids)
                            {
                                if (!s) continue;
                                var b = s.bounds; b.Expand(0.3f);
                                if (!b.Contains(wp)) continue;
                                if (BangClipAudit.Depth(cols[s], wp) > 0 || Physics.OverlapSphere(wp, 0.14f).Any(c => c == cols[s])) { hit = true; break; }
                            }
                        }
                        if (!hit) break;
                        cr.height += 0.3f; raised++;
                    }
                    cr.Snap();
                }
            }
            finally
            {
                Physics.queriesHitBackfaces = back;
                foreach (var c in added) if (c) Object.DestroyImmediate(c);
                foreach (var g in ground) if (g) Object.DestroyImmediate(g);
            }
            Debug.Log($"[Bang] 去穿模 W{ch}：挪开 {moved} 件、删掉 {removed} 件、剔除散布颗粒 {stripped} 颗、抬高航线 {raised} 次、悬空落地 {settled} 件 + 花草 {settledIslands} 颗" + (stuck.Count > 0 ? "\n  无法自动处理：\n  " + string.Join("\n  ", stuck.Distinct()) : ""));
        }

        static readonly string[] Air = { "Cloud", "Balloon", "Bird", "Butterfl", "Critter", "Plane", "Bunting", "Light", "Lamp", "Mobile", "Kite", "Sparkle", "Confetti", "Sky", "Rainbow", "Moon", "Sun", "Heart", "Star", "Bulb", "Wire", "Cord", "Thread", "Gear", "Shelf", "Porthole", "Train", "Arch", "Frame", "Shell", "Wall", "Ceiling", "Beam", "Clock", "Key", "Windmill_Blades", "Rotor", "Canopy", "Catapult_Arm", "Pendulum", "Tether", "W3_Bell" };
        // 钉死不动的（玩法依赖位置：投石车的弹道按它在门洞正对面的位置算）
        static readonly string[] Pinned = { "Props/W3_Wall_Segment", "Props/W3_Castle_Gate", "Props/W3_Tower_", "Props/W3_Keep", "Props/W3_BellTower", "Props/W3_Drawbridge",   /* 只钉城堡本身（巨型玩具里的彩条塔照常落地） */ "Catapult" };

        static bool Settleable(Unit u)
        {
            string path = BangClipAudit.Path(u.root);
            if (u.onPath || Pinned.Any(n => path.Contains(n))) return false;
            if (Air.Any(a => path.Contains(a))) return false;
            return u.movable || path.Contains("/Props/");
        }

        /// <summary>按颗粒落地：每颗（焊接后的连通块）往下找地面，悬空超过 1.5 cm 就整颗往下挪（最低点陷进 4 mm）。</summary>
        /// <summary>把包围盒互相重叠（外扩 3 mm）的封闭小块并成“一株”：一朵花 = 茎 + 叶 + 每片花瓣 + 花心，各自是独立封闭块，
        /// 按小块单独落地时花瓣、叶子全掉到茎根（游戏里只剩一根光杆），按小块剔除时会剪掉半朵花。parent 是顶点并查集，原地合并。</summary>
        static void JoinPlants(Vector3[] v, int[] parent)
        {
            int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
            var lo = new Dictionary<int, Vector3>(); var hi = new Dictionary<int, Vector3>();
            for (int i = 0; i < v.Length; i++)
            {
                int r = Find(i);
                if (lo.TryGetValue(r, out var l)) { lo[r] = Vector3.Min(l, v[i]); hi[r] = Vector3.Max(hi[r], v[i]); }
                else { lo[r] = v[i]; hi[r] = v[i]; }
            }
            const float pad = 0.003f;
            var roots = lo.Keys.OrderBy(r => lo[r].x).ToList();
            for (int a = 0; a < roots.Count; a++)
            {
                var la = lo[roots[a]]; var ha = hi[roots[a]];
                for (int b = a + 1; b < roots.Count; b++)
                {
                    var lb = lo[roots[b]]; if (lb.x > ha.x + pad) break;
                    var hb = hi[roots[b]];
                    if (lb.y > ha.y + pad || la.y > hb.y + pad || lb.z > ha.z + pad || la.z > hb.z + pad || la.x > hb.x + pad) continue;
                    int x = Find(roots[a]), y = Find(roots[b]); if (x != y) parent[x] = y;
                }
            }
        }

        static Mesh SettleIslands(MeshFilter mf, List<MeshCollider> ground, out int moved)
        {
            moved = 0;
            var src = mf.sharedMesh; var v = src.vertices; var tr = src.triangles;
            var parent = new int[v.Length]; for (int i = 0; i < v.Length; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
            var weld = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 5000), Mathf.RoundToInt(v[i].y * 5000), Mathf.RoundToInt(v[i].z * 5000));
                if (weld.TryGetValue(key, out var j)) { int a = Find(i), b = Find(j); if (a != b) parent[a] = b; } else weld[key] = i;
            }
            for (int t = 0; t < tr.Length; t += 3) { int a = Find(tr[t]), b = Find(tr[t + 1]); if (a != b) parent[a] = b; a = Find(tr[t]); b = Find(tr[t + 2]); if (a != b) parent[a] = b; }
            JoinPlants(v, parent);
            var wv = new Vector3[v.Length]; for (int i = 0; i < v.Length; i++) wv[i] = mf.transform.TransformPoint(v[i]);
            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < v.Length; i++) { int r = Find(i); if (!groups.TryGetValue(r, out var l)) groups[r] = l = new List<int>(); l.Add(i); }
            var nv = (Vector3[])v.Clone();
            foreach (var g in groups.Values)
            {
                float minY = float.MaxValue; Vector3 c = Vector3.zero;
                foreach (var i in g) { minY = Mathf.Min(minY, wv[i].y); c += wv[i]; }
                c /= g.Count;
                float best = float.NegativeInfinity;
                foreach (var gc in ground) if (gc.Raycast(new Ray(new Vector3(c.x, minY + 30f, c.z), Vector3.down), out var h, 80f)) best = Mathf.Max(best, h.point.y);
                if (float.IsNegativeInfinity(best)) continue;
                float gap = minY - best;
                if (gap <= 0.015f) continue;
                var d = mf.transform.InverseTransformVector(Vector3.down * (gap + 0.004f));
                foreach (var i in g) nv[i] = v[i] + d;
                moved++;
            }
            if (moved == 0) return src;
            var m = Object.Instantiate(src); m.name = src.name.Replace("(Clone)", "").Replace("_Clean", "") + "_Clean";
            m.vertices = nv; m.RecalculateBounds();
            return m;
        }

        /// <summary>按颗粒（位置焊接后的三角形连通块）剔除：颗粒里有顶点落进任一实体内部（深 > 4 mm）就整颗去掉。</summary>
        static Mesh StripIslands(MeshFilter mf, List<Renderer> solids, Dictionary<Renderer, MeshCollider> cols, out int cut)
        {
            var src = mf.sharedMesh; var v = src.vertices; var tr = src.triangles;
            var parent = new int[v.Length]; for (int i = 0; i < v.Length; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
            void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
            var weld = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 5000), Mathf.RoundToInt(v[i].y * 5000), Mathf.RoundToInt(v[i].z * 5000));
                if (weld.TryGetValue(key, out var j)) Union(i, j); else weld[key] = i;
            }
            for (int t = 0; t < tr.Length; t += 3) { Union(tr[t], tr[t + 1]); Union(tr[t], tr[t + 2]); }
            JoinPlants(v, parent);
            var bad = new HashSet<int>(); var checkedRoot = new Dictionary<int, bool>();
            var wv = new Vector3[v.Length]; for (int i = 0; i < v.Length; i++) wv[i] = mf.transform.TransformPoint(v[i]);
            for (int i = 0; i < v.Length; i++)
            {
                int root = Find(i); if (bad.Contains(root)) continue;
                foreach (var s in solids)
                {
                    if (!s || s.transform.IsChildOf(mf.transform)) continue;
                    if (!s.bounds.Contains(wv[i])) continue;
                    if (BangClipAudit.Depth(cols[s], wv[i]) > 0.004f) { bad.Add(root); break; }
                }
            }
            // 反方向：实体的顶点插进某颗散布物里（扁的路砖角戳进碎砖，而碎砖自己没有顶点在路砖里）→ 这颗也去掉
            var self = mf.GetComponent<MeshCollider>();
            if (self)
            {
                var islandB = new Dictionary<int, Bounds>();
                for (int i = 0; i < v.Length; i++) { int r0 = Find(i); if (islandB.TryGetValue(r0, out var ib)) { ib.Encapsulate(wv[i]); islandB[r0] = ib; } else islandB[r0] = new Bounds(wv[i], Vector3.zero); }
                var mb = mf.GetComponent<Renderer>().bounds;
                foreach (var s in solids)
                {
                    if (!s || !s.bounds.Intersects(mb)) continue;
                    var sv = s.GetComponent<MeshFilter>().sharedMesh.vertices;
                    foreach (var lv in sv)
                    {
                        var p = s.transform.TransformPoint(lv);
                        if (!mb.Contains(p) || BangClipAudit.Depth(self, p) <= 0.004f) continue;
                        foreach (var kv in islandB) { var b = kv.Value; b.Expand(0.002f); if (b.Contains(p)) bad.Add(kv.Key); }
                    }
                }
            }
            cut = bad.Count;
            if (cut == 0) return src;
            var keep = new List<int>();
            for (int t = 0; t < tr.Length; t += 3) if (!bad.Contains(Find(tr[t]))) { keep.Add(tr[t]); keep.Add(tr[t + 1]); keep.Add(tr[t + 2]); }
            // 只留用到的顶点（各属性一起重排），剔掉的颗粒不留悬空顶点
            var map = new Dictionary<int, int>(); var order = new List<int>();
            for (int i = 0; i < keep.Count; i++) { if (!map.TryGetValue(keep[i], out var ni)) { ni = map[keep[i]] = order.Count; order.Add(keep[i]); } keep[i] = ni; }
            T[] Pick<T>(T[] a) => a == null || a.Length != v.Length ? null : order.Select(i => a[i]).ToArray();
            var m = new Mesh { name = src.name.Replace("(Clone)", "") + "_Clean" };
            if (order.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = Pick(v);
            var nrm = Pick(src.normals); if (nrm != null) m.normals = nrm;
            var tan = Pick(src.tangents); if (tan != null && tan.Length > 0) m.tangents = tan;
            var c32 = Pick(src.colors32); if (c32 != null && c32.Length > 0) m.colors32 = c32;
            for (int ch = 0; ch < 4; ch++) { var uv = new List<Vector2>(); src.GetUVs(ch, uv); if (uv.Count == v.Length) m.SetUVs(ch, Pick(uv.ToArray())); }
            m.SetTriangles(keep, 0); m.RecalculateBounds();
            return m;
        }
    }
}
