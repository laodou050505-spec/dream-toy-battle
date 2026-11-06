// 第 2 章：ABC 木头积木门 + 门后世界「沙盒兵营」（粉彩沙丘上的玩具军营，纸板大炮是门后机关）。
using System.Collections.Generic;
using Bang.Ambient;
using Bang.Portal;
using UnityEditor;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bang.EditorTools
{
    public static partial class BangSceneBuilder
    {
        public static readonly Dictionary<string, (float h, float gloss, float sheen)> W2Sizes = new()
        {
            ["W2_WoodAnimals"] = (0.24f, 0.2f, 0),
            ["W2_Tent_Blue"] = (0.58f, 0.08f, 0.5f), ["W2_Tent_Pink"] = (0.64f, 0.08f, 0.5f),
            ["W2_Watchtower"] = (1.45f, 0.18f, 0), ["W2_Jeep"] = (0.42f, 0.5f, 0),
            ["W2_SandCastle_Big"] = (0.9f, 0.06f, 0.25f), ["W2_SandCastle_Small"] = (0.5f, 0.06f, 0.25f),
            ["W2_Bucket"] = (0.42f, 0.55f, 0), ["W2_Campfire"] = (0.26f, 0.05f, 0.6f),
            ["W2_Cannon_Carriage"] = (0.5f, 0.12f, 0), ["W2_Cannon_Barrel"] = (0.24f, 0.3f, 0),
            ["W2_FeltSun"] = (1.3f, 0.05f, 0.7f), ["W2_CottonCloud"] = (0.75f, 0.02f, 0.8f),
            ["W2_SandbagWall"] = (0.3f, 0.05f, 0.6f), ["W2_SupplyCrates"] = (0.46f, 0.2f, 0),
        };

        public static GameObject BuildPortalW2(Transform root, Vector3 pos, int stencilRef)
        {
            var portal = new GameObject("Portal_W2");
            portal.transform.SetParent(root, false);
            portal.transform.localPosition = pos;
            BuildFrameW2(portal.transform);
            PortalOpening(portal.transform, 2, stencilRef);   // 不规则虫洞门洞（遮罩 + 涡旋膜 + 发光内沿）

            var w = new GameObject("World_SandboxCamp");
            w.transform.SetParent(portal.transform, false);
            SkyAndBackdrop(w.transform, "W2", new Color(0.62f, 0.8f, 1f), new Color(1f, 0.92f, 0.86f));
            var ground = Inst(Proc + "/W2_Ground.fbx", w.transform, BangArt.SandVC);
            FlattenNearRoom(ground, w.transform);   // 真实房间范围内压平地形（不然从房间里看会被山丘挡住门洞）
            var gcol = ground ? ground.GetComponentInChildren<MeshFilter>().gameObject.AddComponent<MeshCollider>() : null;
            Physics.SyncTransforms();
            _groundRoot = w.transform;
            Inst(Proc + "/W2_Border.fbx", w.transform, BangArt.WoodVC);
            Inst(Proc + "/W2_Decor.fbx", w.transform, BangArt.ToyVC);

            var props = new GameObject("Props").transform; props.SetParent(w.transform, false);
            var layout = new (string n, float x, float z, float yaw)[]
            {
                ("W2_Tent_Blue", -0.9f, -2.3f, 22), ("W2_Tent_Pink", 0.45f, -2.0f, -16), ("W2_Campfire", -0.25f, -1.55f, 0),
                ("W2_Watchtower", -1.6f, -3.6f, 15),
                ("W2_SandCastle_Big", 1.7f, -2.9f, -20), ("W2_SandCastle_Small", 2.3f, -2.15f, 12),
                ("W2_SandbagWall", -0.15f, -3.08f, 0), ("W2_SandCastle_Small", -1.45f, -1.05f, -25), ("W2_Bucket", 0.95f, -0.95f, 40), ("W2_SupplyCrates", -2.0f, -2.15f, 30),
            };
            foreach (var (n, x, z, yaw) in layout) SettleOnGround(Place(n, props, new Vector3(x, GroundY(x, z), z), yaw));
            // 纸板大炮：营地正中、两顶帐篷后面的沙袋墙后（门洞里一眼能看到，玩法要瞄准它），炮管架在炮车的凹槽上（玩法里朝房间地面打颜料弹）
            var cx = -0.15f; var cz = -3.5f;
            var carriage = Place("W2_Cannon_Carriage", props, new Vector3(cx, GroundY(cx, cz), cz), 0);
            if (carriage)
            {
                var cb = Bounds(carriage);
                var barrel = Place("W2_Cannon_Barrel", props, props.InverseTransformPoint(new Vector3(cb.center.x, cb.min.y + cb.size.y * 0.62f, cb.center.z)), 0);
                if (barrel) { barrel.name = "W2_Cannon_Barrel"; barrel.transform.SetParent(carriage.transform, true); }   // 挂在炮车下面：去穿模 / 落地时整门炮一起动，炮管不会和炮车分家
            }
            // 玩具吉普：绕着柠檬色沙丘兜圈（高度沿路线烘焙，上下坡会自然俯仰）
            var jeepLoop = new GameObject("JeepLoop").AddComponent<TrainLoop>();
            jeepLoop.transform.SetParent(w.transform, false);
            jeepLoop.center = new Vector2(0.75f, -3.95f); jeepLoop.a = 0.6f; jeepLoop.b = 0.4f; jeepLoop.y = 0f;
            jeepLoop.speed = 0.22f; jeepLoop.startPhase = 0.2f; jeepLoop.yawOffset = 90; jeepLoop.spacing = 0;
            var hc = new AnimationCurve();
            for (int i = 0; i <= 48; i++)
            {
                float u = i / 48f, ang = u * Mathf.PI * 2;
                hc.AddKey(new Keyframe(u, GroundY(jeepLoop.center.x + Mathf.Cos(ang) * jeepLoop.a, jeepLoop.center.y + Mathf.Sin(ang) * jeepLoop.b)));
            }
            for (int i = 0; i < hc.length; i++) hc.SmoothTangents(i, 0);
            jeepLoop.heightCurve = hc;
            var jeep = Place("W2_Jeep", jeepLoop.transform, Vector3.zero, 0);
            if (jeep) { jeepLoop.cars = new[] { jeep.transform }; jeepLoop.Step(0); FitLoopToGround(jeepLoop); }
            if (gcol) Object.DestroyImmediate(gcol);
            // 天上：棉花云（天空画幕里已经画了毛毡太阳，3D 太阳不再重复放）
            var clouds = new (Vector3 p, float s)[] { (new Vector3(-2.6f, 2.4f, -7.8f), 1.5f), (new Vector3(0.4f, 3.3f, -9.4f), 1.9f), (new Vector3(3.9f, 1.6f, -8.2f), 1.2f), (new Vector3(-0.9f, 1.9f, -6.0f), 0.8f) };
            foreach (var (p, s) in clouds) FloatCloud("W2_CottonCloud", props, p, s);   // 棉花云随风慢慢漂
            Sparkles(w.transform, new Vector3(0.3f, 1.1f, -3.4f), new Vector3(6.5f, 1.8f, 5.5f), 10);
            Enrich(2, w.transform, props);   // 丰富度加密（远景 / 近景 / 中景 / 小生命）
            FloraRing(2, w.transform);   // 房间四周一整圈地面花草
            Vignettes(2, w.transform, props);   // 房间四周的主题小场景组团
            GiantRing(2, w.transform, props);   // 远处一圈巨型玩具（被玩具包围）
            Surround(2, w.transform, props);  // 房间四周和门洞两侧按主题摆满（玩具化露出哪个方向都不空）
            var pw = w.AddComponent<PortalWorld>();
            pw.stencilRef = stencilRef;
            pw.hazeColor = new Color(0.98f, 0.94f, 0.95f); pw.hazeStart = 9.0f; pw.hazeEnd = 24.0f; pw.hazeAmount = 0.054f;
            pw.Rebind();
            ShadowPolicy(w.transform);                                                         // 只让有体量的物体投影
            w.AddComponent<PortalCuller>().arch = portal.GetComponentInChildren<PortalArch>();   // 只渲染透过门洞 / 已玩具化区域看得到的物体
            Spill(2, portal.transform);   // 门前真实房间里的溢出物（门后世界从洞口淌出来一截）
            return portal;
        }

        /// <summary>ABC 木头积木搭的拱门：两侧各 3 块（略微歪扭，像小孩搭的），拱顶 7 块沿半圆排开，门脚放木头小动物。</summary>
        /// <summary>平面上两个旋转正方形（中心、半边长、角度°）是否相交（分离轴），gap = 额外留的缝。</summary>
        static bool SquaresOverlap(Vector2 ca, float ha, float aa, Vector2 cb, float hb, float ab, float gap)
            => RectsOverlap(ca, new Vector2(ha, ha), aa, cb, new Vector2(hb, hb), ab, gap);

        /// <summary>平面上两个旋转矩形（中心、半边长 x/y、角度°）是否相交（分离轴），gap = 额外留的缝。</summary>
        public static bool RectsOverlap(Vector2 ca, Vector2 ha, float aa, Vector2 cb, Vector2 hb, float ab, float gap)
        {
            Vector2[] Axes(float a) { float r = a * Mathf.Deg2Rad; return new[] { new Vector2(Mathf.Cos(r), Mathf.Sin(r)), new Vector2(-Mathf.Sin(r), Mathf.Cos(r)) }; }
            Vector2[] Corners(Vector2 c, Vector2 h, float a) { var ax = Axes(a); return new[] { c + ax[0] * h.x + ax[1] * h.y, c + ax[0] * h.x - ax[1] * h.y, c - ax[0] * h.x + ax[1] * h.y, c - ax[0] * h.x - ax[1] * h.y }; }
            var g2 = Vector2.one * gap / 2; var A = Corners(ca, ha + g2, aa); var B = Corners(cb, hb + g2, ab);
            foreach (var axis in Axes(aa).Concat(Axes(ab)))
            {
                float a0 = float.MaxValue, a1 = float.MinValue, b0 = float.MaxValue, b1 = float.MinValue;
                foreach (var v in A) { float d = Vector2.Dot(v, axis); a0 = Mathf.Min(a0, d); a1 = Mathf.Max(a1, d); }
                foreach (var v in B) { float d = Vector2.Dot(v, axis); b0 = Mathf.Min(b0, d); b1 = Mathf.Max(b1, d); }
                if (a1 < b0 || b1 < a0) return false;
            }
            return true;
        }

        static void BuildFrameW2(Transform portal)
        {
            var frame = new GameObject("Frame_ABC").transform;
            frame.SetParent(portal, false);
            var rnd = new System.Random(2026102802);
            float J(float a) => (float)(rnd.NextDouble() * 2 - 1) * a;
            string[] kinds = { "W2_Block_A", "W2_Block_B", "W2_Block_C" };
            int k = 0;
            const float S = 0.36f;
            void Block(Vector3 center, float roll, float yaw)
            {
                // Blender 程序化字母积木（Tools/blender_w2_blocks.py）：原点在方块中心、正面朝 +Z
                var b = Inst($"{Proc}/{kinds[k++ % kinds.Length]}.fbx", frame, BangArt.BlockWood);
                if (!b) return;
                b.name = "Block";
                b.transform.localPosition = center;
                b.transform.localRotation = Quaternion.Euler(0, 0, roll) * Quaternion.Euler(0, yaw, 0);
            }
            // 沿不规则洞口外沿（中心线外扩 0.2 m）一块挨一块摆，方块“上面”朝洞口外侧，像小孩沿着洞口垒的一圈。
            // 弯道内侧和贴地处按弧长等分会让相邻方块互相插进去：逐块往前找，和已摆的方块（门面内的旋转正方形，含偏航放大）不相交才放
            float total = PathLength(2, "path20");
            var placed = new List<(Vector2 c, float half, float ang)>();
            bool Hits(Vector2 c, float half, float ang)
            {
                foreach (var q in placed) if (SquaresOverlap(c, half, ang, q.c, q.half, q.ang, 0.006f)) return true;
                return false;
            }
            float u = 0.5f * S / total;
            while (u < 1f - 0.4f * S / total)
            {
                var (p, _, nrm) = PathAt(2, "path20", u);
                float roll = Mathf.Atan2(nrm.y, nrm.x) * Mathf.Rad2Deg - 90f;
                if (p.y < S * 0.75f) roll = Mathf.Round(roll / 90f) * 90f;          // 贴地的几块放平
                float yaw = J(14f), rj = J(5f), dz = J(0.03f);
                float rr = rj * Mathf.Deg2Rad;
                var c = new Vector2(p.x, p.y) + nrm * (S / 2 * (Mathf.Cos(rr) + Mathf.Abs(Mathf.Sin(rr)) - 1f) + 0.004f);   // 墙面内歪一点时角会伸进洞口：整块往外让开
                c.y = Mathf.Max(c.y, S / 2);
                float half = S / 2 * (Mathf.Cos(Mathf.Abs(yaw) * Mathf.Deg2Rad) + Mathf.Sin(Mathf.Abs(yaw) * Mathf.Deg2Rad));   // 偏航后在门面上的投影变宽
                if (Hits(c, half, roll + rj)) { u += 0.01f * S / total * 4f; continue; }
                placed.Add((c, half, roll + rj));
                Block(new Vector3(c.x, c.y, S / 2 - 0.02f + dz), roll + rj, yaw);
                u += (S + 0.012f) / total;
            }
            // 门脚两侧再放两块倒在地上的积木 + 木头小动物
            Block(new Vector3(-1.3f, S / 2, 0.28f), 0, 28);
            Block(new Vector3(1.32f, S / 2, 0.22f), 0, -22);
            Place("W2_WoodAnimals", frame, new Vector3(-0.95f, 0, 0.42f), 25);
            Place("W2_WoodAnimals", frame, new Vector3(1.0f, 0, 0.45f), -40);
        }
    }
}
