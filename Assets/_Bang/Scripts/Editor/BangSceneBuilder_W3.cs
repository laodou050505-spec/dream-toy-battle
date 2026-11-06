// 第 3 章：毛毡月亮星星门 + 门后世界「锡兵城堡」（黄昏，护城河小岛上的纸板城堡；钟楼大钟是友方门后机关）。
using System.Collections.Generic;
using Bang.Ambient;
using Bang.Portal;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bang.EditorTools
{
    public static partial class BangSceneBuilder
    {
        public static readonly Dictionary<string, (float h, float gloss, float sheen)> W3Sizes = new()
        {
            ["W3_Castle_Gate"] = (0.78f, 0.15f, 0), ["W3_Drawbridge"] = (0.12f, 0.25f, 0),
            ["W3_Tower_Round"] = (1.3f, 0.15f, 0), ["W3_Tower_Stripe"] = (1.55f, 0.25f, 0),
            ["W3_BellTower_Body"] = (1.8f, 0.2f, 0), ["W3_Bell"] = (0.2f, 0.85f, 0),
            ["W3_Keep"] = (1.0f, 0.12f, 0.2f), ["W3_Wall_Segment"] = (0.5f, 0.12f, 0),
            ["W3_FeltTree_Lights"] = (1.0f, 0.04f, 0.7f), ["W3_FeltPine"] = (0.95f, 0.04f, 0.7f),
            ["W3_Cottage"] = (0.6f, 0.1f, 0.3f), ["W3_SleepyMoon"] = (1.2f, 0.04f, 0.7f),
            ["W3_FeltStar"] = (0.35f, 0.04f, 0.7f), ["W3_FeltCloud"] = (0.4f, 0.03f, 0.8f),
            ["W3_FeltPets"] = (0.24f, 0.03f, 0.8f), ["W3_TinSoldier_Deco"] = (0.24f, 0.55f, 0),
        };

        public static GameObject BuildPortalW3(Transform root, Vector3 pos, int stencilRef)
        {
            var portal = new GameObject("Portal_W3");
            portal.transform.SetParent(root, false);
            portal.transform.localPosition = pos;
            BuildFrameW3(portal.transform);
            PortalOpening(portal.transform, 3, stencilRef);   // 不规则虫洞门洞（遮罩 + 涡旋膜 + 发光内沿）

            var w = new GameObject("World_TinCastle");
            w.transform.SetParent(portal.transform, false);
            SkyAndBackdrop(w.transform, "W3", new Color(0.55f, 0.52f, 0.9f), new Color(1f, 0.74f, 0.8f));
            var ground = Inst(Proc + "/W3_Ground.fbx", w.transform, BangArt.TurfGroundVC);
            FlattenNearRoom(ground, w.transform);   // 真实房间范围内压平地形（不然从房间里看会被山丘挡住门洞）
            var gcol = ground ? ground.GetComponentInChildren<MeshFilter>().gameObject.AddComponent<MeshCollider>() : null;
            Physics.SyncTransforms();
            _groundRoot = w.transform;
            Inst(Proc + "/W3_Flora.fbx", w.transform, BangArt.FeltVC);   // 绒布小花、草丛、贴片、小珠（Tools/blender_w1_flora.py W3）

            var props = new GameObject("Props").transform; props.SetParent(w.transform, false);
            GameObject P(string n, float x, float z, float yaw) { var g = Place(n, props, new Vector3(x, GroundY(x, z), z), yaw); SettleOnGround(g); return g; }
            // 城堡：前排 城门 + 两段城墙 + 两座圆塔；后排 主楼、钟楼、彩条塔
            var gate = P("W3_Castle_Gate", 0.3f, -3.3f, 0);
            P("W3_Wall_Segment", -0.42f, -3.38f, 0); P("W3_Wall_Segment", 1.02f, -3.38f, 0);
            var tl = P("W3_Tower_Round", -0.92f, -3.45f, 15); var tr = P("W3_Tower_Round", 1.52f, -3.45f, -15);
            P("W3_Keep", 0.75f, -4.55f, 0);
            var bell = P("W3_BellTower_Body", -0.3f, -4.65f, -40);   // 转 40°：钟面和钟窗都朝向门洞
            var ts = P("W3_Tower_Stripe", 1.62f, -5.12f, 0);          // 离主楼远一点（原来插进主楼 12 cm）
            P("W3_Tower_Round", -1.0f, -5.25f, 30);
            // 吊桥：城门前横跨护城河，平放
            if (gate)
            {
                var gb = Bounds(gate);
                var db = Place("W3_Drawbridge", props, props.InverseTransformPoint(new Vector3(gb.center.x, gb.min.y - 0.01f, gb.max.z + 0.16f)), 90);
                if (db) db.name = "W3_Drawbridge";
            }
            // 大钟：挂在钟楼钟窗里（玩法：打一枪“当——”，全场小怪定住 3 秒）
            if (bell)
            {
                var bb = Bounds(bell);
                var b = Place("W3_Bell", props, props.InverseTransformPoint(new Vector3(bb.center.x, bb.min.y + bb.size.y * 0.5f, bb.center.z)), 0);
                if (b) { b.name = "W3_Bell"; b.transform.SetParent(bell.transform, true); }   // 挂在钟楼下面，和钟楼是一个整体；钟只在玩法里被敲响时摆
            }
            // 城墙上的锡兵（装饰；玩法里的锡兵是敌人另做）
            foreach (var (x, z) in new[] { (-0.42f, -3.38f), (1.02f, -3.38f), (0.3f, -3.3f) })
            {
                var y = GroundY(x, z) + (x == 0.3f ? 0.78f : 0.5f) - 0.02f;
                Place("W3_TinSoldier_Deco", props, new Vector3(x + 0.12f, y, z), 0);
            }
            // 树、小屋
            P("W3_FeltTree_Lights", -2.2f, -3.0f, 0); P("W3_FeltTree_Lights", 2.65f, -3.2f, 0);
            foreach (var (x, z) in new[] { (-2.8f, -4.6f), (3.1f, -4.9f), (-1.9f, -6.3f), (2.2f, -6.4f), (-3.4f, -2.2f), (0.3f, -6.9f) }) P("W3_FeltPine", x, z, 0);
            P("W3_Cottage", -2.45f, -5.3f, 25); P("W3_Cottage", 3.0f, -2.2f, -30);
            if (gcol) Object.DestroyImmediate(gcol);

            // 彩灯：两座前塔顶之间、城墙上、钟楼到彩条塔
            var lights = new GameObject("FairyLights").transform; lights.SetParent(w.transform, false);
            // 灯串挂在各座塔朝门洞的正面（原来锚在塔的中心，灯线从塔肚子里穿出来）：锚点在塔身最前面再往前 2 cm
            Vector3 Top(GameObject g, float k) { var b = Bounds(g); return lights.InverseTransformPoint(new Vector3(b.center.x, b.min.y + b.size.y * k, b.max.z + 0.02f)); }
            if (tl && tr) BangFairyLights.Build("Lights_Front", lights, new[] { Top(tl, 0.62f), Top(gate ? gate : tl, 0.95f), Top(tr, 0.62f) }, 0.14f);
            if (bell && ts) BangFairyLights.Build("Lights_Back", lights, new[] { Top(bell, 0.7f), Top(ts, 0.62f) }, 0.18f);
            // 天上挂着的毛毡月亮和星星（细线吊着，轻轻摆）
            var sky = new GameObject("Hanging").transform; sky.SetParent(w.transform, false);
            Hang("W3_SleepyMoon", sky, new Vector3(-1.5f, 2.6f, -8.2f), 1f);
            foreach (var (p, s) in new[] { (new Vector3(-3.2f, 3.1f, -8.6f), 1.2f), (new Vector3(0.9f, 3.4f, -8.8f), 1.4f), (new Vector3(2.8f, 2.5f, -8.0f), 1.1f), (new Vector3(-0.4f, 3.9f, -9.2f), 0.9f), (new Vector3(4.2f, 3.6f, -9.0f), 1.3f) })
                Hang("W3_FeltStar", sky, p, s);
            Sparkles(w.transform, new Vector3(0.3f, 1.4f, -4f), new Vector3(7f, 2.4f, 6f), 14);
            Enrich(3, w.transform, props);   // 丰富度加密（远景 / 近景 / 中景 / 小生命）
            FloraRing(3, w.transform);   // 房间四周一整圈地面花草
            Vignettes(3, w.transform, props);   // 房间四周的主题小场景组团
            GiantRing(3, w.transform, props);   // 远处一圈巨型玩具（被玩具包围）
            Surround(3, w.transform, props);  // 房间四周和门洞两侧按主题摆满（玩具化露出哪个方向都不空）
            var pw = w.AddComponent<PortalWorld>();
            pw.stencilRef = stencilRef;
            pw.worldTint = new Color(0.92f, 0.88f, 1.0f);      // 黄昏：受光偏紫、略暗，窗灯和彩灯是自发光不受影响
            pw.hazeColor = new Color(0.9f, 0.78f, 0.92f); pw.hazeStart = 8.0f; pw.hazeEnd = 22.0f; pw.hazeAmount = 0.09f;
            pw.Rebind();
            ShadowPolicy(w.transform);                                                         // 只让有体量的物体投影
            w.AddComponent<PortalCuller>().arch = portal.GetComponentInChildren<PortalArch>();   // 只渲染透过门洞 / 已玩具化区域看得到的物体
            Spill(3, portal.transform);   // 门前真实房间里的溢出物（门后世界从洞口淌出来一截）
            return portal;
        }

        /// <summary>天上的毛毡月亮 / 星星：随风极慢地漂（幅度比云小）。</summary>
        static void Hang(string name, Transform parent, Vector3 pos, float scale) { var g = FloatCloud(name, parent, pos, scale); if (g) g.GetComponent<Drift>().amplitude = 0.12f; }

        /// <summary>毛毡拱门（Blender 程序化 W3_FeltArch）+ 月亮、星星、云朵挂饰 + 门脚的毛毡小动物。</summary>
        static void BuildFrameW3(Transform portal)
        {
            var frame = new GameObject("Frame_Felt").transform;
            frame.SetParent(portal, false);
            Inst(Proc + "/W3_FeltArch.fbx", frame, BangArt.FeltGroundVC);   // 绒布纤维贴图
            // 挂饰的中心落在绒布边的中线上；Place 的位置是模型底部，所以按包围盒中心对齐
            void Deco(string n, float angDeg, float s, float roll = 0)
            {
                var g = Place(n, frame, Vector3.zero, 0);
                if (!g) return;
                g.transform.localScale *= s;
                g.transform.localRotation = Quaternion.Euler(0, 0, roll) * g.transform.localRotation;
                var b = Bounds(g);
                // 角度换成沿不规则门框中心线的位置：0° = 右侧中段，180° = 左侧中段
                var (p, _, _) = PathAt(3, "path12", 0.2f + angDeg / 180f * 0.6f);
                var want = frame.TransformPoint(new Vector3(p.x, p.y, 0.16f));
                g.transform.position += want - b.center;
            }
            void DecoAt(string n, Vector3 center, float s, float roll = 0)
            {
                var g = Place(n, frame, Vector3.zero, 0);
                if (!g) return;
                g.transform.localScale *= s;
                g.transform.localRotation = Quaternion.Euler(0, 0, roll) * g.transform.localRotation;
                g.transform.position += frame.TransformPoint(center) - Bounds(g).center;
            }
            Deco("W3_SleepyMoon", 138f, 0.34f, 16);
            Deco("W3_FeltStar", 72f, 0.75f, -8);
            Deco("W3_FeltStar", 28f, 0.55f, 14);
            Deco("W3_FeltStar", -12f, 0.5f, -20);                                   // 右侧中下段
            DecoAt("W3_FeltCloud", new Vector3(-0.82f, 0.22f, 0.2f), 1.1f);
            DecoAt("W3_FeltCloud", new Vector3(0.86f, 0.2f, 0.18f), 0.9f);
            var pets = Place("W3_FeltPets", frame, new Vector3(-0.35f, 0, 0.42f), 0);
        }
    }
}
