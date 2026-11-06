// 第 5 章：齿轮拱门 + 门后世界「发条工厂」（室内工坊；传送带 + 大发条钥匙是门后机关：传送带转时出怪加快，打中钥匙传送带反转）。
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
        public static readonly Dictionary<string, (float h, float gloss, float sheen)> W5Sizes = new()
        {
            ["W5_GearMachine"] = (0.85f, 0.45f, 0), ["W5_BigKey"] = (0.42f, 0.85f, 0),
            ["W5_WindupBear"] = (0.22f, 0.1f, 0.5f), ["W5_WindupBunny"] = (0.22f, 0.1f, 0.5f),
            ["W5_ChimneyHouse_A"] = (0.85f, 0.35f, 0.1f), ["W5_ChimneyHouse_B"] = (0.7f, 0.35f, 0.1f),
            ["W5_Lamp"] = (1.1f, 0.7f, 0), ["W5_TinyRobots"] = (0.26f, 0.6f, 0), ["W5_Workbench"] = (0.55f, 0.3f, 0),
        };

        static Material MetalVC => BangArt.Mat("M_MetalVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.85f); m.SetFloat("_GlossSize", 70); m.SetFloat("_RimStrength", 0.4f); });

        public static GameObject BuildPortalW5(Transform root, Vector3 pos, int stencilRef)
        {
            var portal = new GameObject("Portal_W5");
            portal.transform.SetParent(root, false);
            portal.transform.localPosition = pos;
            var frame = new GameObject("Frame_Gears").transform; frame.SetParent(portal.transform, false);
            Inst(Proc + "/W5_GearFrame.fbx", frame, MetalVC);       // 铜安装板 + 轴销 + 铆钉（静止）
            BuildGearTrain(frame, Proc + "/W5_GearTrain.json", "W5_PG_", true);
            Place("W5_TinyRobots", frame, new Vector3(-0.95f, 0, 0.42f), 20);
            PortalOpening(portal.transform, 5, stencilRef);   // 不规则虫洞门洞（遮罩 + 涡旋膜 + 发光内沿）

            var w = new GameObject("World_WindupWorkshop");
            w.transform.SetParent(portal.transform, false);
            Inst(Proc + "/W5_Floor.fbx", w.transform, BangArt.ToyVC);
            _groundRoot = null;                                                                                     // 地面是平的
            var props = new GameObject("Props").transform; props.SetParent(w.transform, false);
            GameObject P(string n, float x, float z, float yaw) => Place(n, props, new Vector3(x, 0, z), yaw);
            Inst(Proc + "/W5_Pipes.fbx", props, MetalVC);
            Inst(Proc + "/W5_Clutter.fbx", props, MetalVC);   // 地上散落的小齿轮、螺母、螺栓、弹簧、珠子
            BuildWorkshopShell(w.transform, props);           // 真房间：墙纸 + 墙裙 + 舷窗 + 木梁天花板 + 吊灯 + 两侧搁板

            // 传送带：中间横放，皮带滚动，两只发条玩具在上面走
            var conv = new GameObject("Conveyor").transform; conv.SetParent(props, false); conv.localPosition = new Vector3(0.3f, 0, -3.0f); conv.localScale = Vector3.one * 1.3f;
            Inst(Proc + "/W5_ConveyorFrame.fbx", conv, MetalVC);
            var belt = Inst(Proc + "/W5_ConveyorBelt.fbx", conv, BangArt.Mat("M_Belt", m => { m.SetFloat("_UseVertexColor", 1); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Bang/Art/Textures/T_BeltSlats.png")); m.SetFloat("_Gloss", 0.3f); }));
            if (belt) belt.GetComponentInChildren<MeshRenderer>().gameObject.AddComponent<BeltScroll>();
            var riders = new List<Transform>();
            foreach (var (n, x) in new[] { ("W5_WindupBear", -0.45f), ("W5_WindupBunny", 0.3f), ("W5_WindupBear", 0.72f) })
            {
                var r = Place(n, conv, new Vector3(x, 0.4f, 0), 0);
                if (!r) continue;
                // 脚底贴在皮带面上（原来按固定高度 0.4 摆，离皮带 4 cm 飘着）：从上往下找这一点的皮带面
                if (belt)
                {
                    var rb = Bounds(r); float top = float.NegativeInfinity;
                    var bmr = belt.GetComponentInChildren<MeshRenderer>(); var mc = bmr.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = bmr.GetComponent<MeshFilter>().sharedMesh; Physics.SyncTransforms();
                    if (mc.Raycast(new Ray(new Vector3(rb.center.x, rb.max.y + 1f, rb.center.z), Vector3.down), out var hit, 5f)) top = hit.point.y;
                    Object.DestroyImmediate(mc);
                    if (!float.IsNegativeInfinity(top)) r.transform.position += Vector3.up * (top - rb.min.y - 0.003f);
                }
                riders.Add(r.transform);
            }
            var br = conv.gameObject.AddComponent<BeltRider>(); br.riders = riders.ToArray(); br.halfLength = 0.8f;
            // 齿轮机器 + 前面的齿轮组 + 顶上转动的大发条钥匙
            var mach = P("W5_GearMachine", -1.45f, -3.6f, 25);
            if (mach)
            {
                var mb = Bounds(mach);
                var key = Place("W5_BigKey", props, props.InverseTransformPoint(new Vector3(mb.center.x, mb.max.y - 0.02f, mb.center.z)), 0);
                if (key) { key.name = "W5_BigKey"; var ks = key.AddComponent<Spin>(); ks.axis = Vector3.up; ks.degPerSec = 12f; }   // 发条松开：钥匙慢慢倒转
                var spring = Inst(Proc + "/W5_Spring.fbx", props, MetalVC);
                if (spring) spring.transform.position = new Vector3(mb.max.x + 0.08f, mb.min.y, mb.center.z);
            }
            WallGears(props, new Vector3(2.2f, 2.35f, WsZ + 0.1f), 2.6f, new[] { 34, 18, 28, 14, 22, 34 }, new[] { 15f, -35f, 30f, 70f, -20f });   // 后墙上一组真正啮合的大齿轮（工坊的传动轴）
            WallGears(props, new Vector3(-2.8f, 2.9f, WsZ + 0.1f), 2.2f, new[] { 28, 14, 22, 18 }, new[] { -10f, -60f, 20f });
            var gf = WallGears(props, new Vector3(-0.6f, 3.0f, WsZF - 0.1f), 2.4f, new[] { 22, 34, 14, 28, 18 }, new[] { -20f, 30f, -40f, 15f });   // 前墙（玩家身后）也有一组
            if (gf) gf.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var h1 = P("W5_ChimneyHouse_A", 2.35f, -2.7f, -25); var h2 = P("W5_ChimneyHouse_B", -2.5f, -4.7f, 30);
            P("W2_SupplyCrates", 1.4f, -1.7f, -25); P("W2_SupplyCrates", -1.0f, -1.5f, 30); P("W5_TinyRobots", -0.6f, -2.1f, 10);
            P("W5_Lamp", -1.9f, -1.9f, 0); P("W5_Lamp", 1.95f, -1.7f, 0); P("W5_Workbench", -2.45f, -2.45f, 40);
            foreach (var h in new[] { h1, h2 }) if (h) { var hb = Bounds(h); Smoke(w.transform, w.transform.InverseTransformPoint(new Vector3(hb.center.x + hb.extents.x * 0.35f, hb.max.y, hb.center.z))); }
            Sparkles(w.transform, new Vector3(0.3f, 1.3f, -3.4f), new Vector3(6f, 2f, 5f), 8);
            Enrich(5, w.transform, props);   // 丰富度加密（远景 / 近景 / 中景 / 小生命）
            GiantRing(5, w.transform, props);   // 远处一圈巨型玩具（被玩具包围）
            Surround(5, w.transform, props);  // 房间四周和门洞两侧按主题摆满（玩具化露出哪个方向都不空）
            var pw = w.AddComponent<PortalWorld>();
            pw.stencilRef = stencilRef;
            pw.worldTint = new Color(1f, 0.96f, 0.92f);
            pw.hazeColor = new Color(1f, 0.92f, 0.86f); pw.hazeStart = 7.0f; pw.hazeEnd = 16.0f; pw.hazeAmount = 0.054f;
            pw.Rebind();
            ShadowPolicy(w.transform);                                                         // 只让有体量的物体投影
            w.AddComponent<PortalCuller>().arch = portal.GetComponentInChildren<PortalArch>();   // 只渲染透过门洞 / 已玩具化区域看得到的物体
            Spill(5, portal.transform);   // 门前真实房间里的溢出物（门后世界从洞口淌出来一截）
            return portal;
        }

        /// <summary>按 Blender 导出的齿轮表（位置、齿数、啮合父齿轮）摆出整组齿轮，挂 GearTrain：
        /// 每个齿轮一个轴心枢轴（只绕本地 Z 转），主动轮上装发条钥匙（withKey）。</summary>
        static GearTrain BuildGearTrain(Transform parent, string jsonPath, string prefix, bool withKey)
        {
            if (!System.IO.File.Exists(jsonPath)) { Debug.LogWarning("[Bang] 缺少齿轮表 " + jsonPath); return null; }
            var txt = System.IO.File.ReadAllText(jsonPath);
            float gz = 0.062f;
            var mz = System.Text.RegularExpressions.Regex.Match(txt, "\"gearZ\":\\s*([-0-9.eE]+)");
            if (mz.Success) gz = float.Parse(mz.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var ms = System.Text.RegularExpressions.Regex.Matches(txt, "\\{[^{}]*\"x\":\\s*([-0-9.eE]+),\\s*\"y\":\\s*([-0-9.eE]+),\\s*\"n\":\\s*(\\d+),\\s*\"parent\":\\s*(-?\\d+)[^{}]*\\}");
            var root = new GameObject("GearTrain").transform; root.SetParent(parent, false);
            var train = root.gameObject.AddComponent<GearTrain>();
            var list = new List<GearTrain.Gear>();
            float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
            for (int i = 0; i < ms.Count; i++)
            {
                var m = ms[i];
                var pivot = new GameObject("Gear_" + i).transform; pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(F(m.Groups[1].Value), F(m.Groups[2].Value), gz);
                var g = Inst($"{Proc}/{prefix}{i:00}.fbx", pivot, MetalVC);
                if (g) g.transform.localPosition = Vector3.zero;
                list.Add(new GearTrain.Gear { pivot = pivot, teeth = int.Parse(m.Groups[3].Value), parent = int.Parse(m.Groups[4].Value) });
            }
            train.gears = list.ToArray();
            if (withKey && list.Count > 0)
            {
                var key = Inst(Proc + "/W5_WindKey.fbx", root, BangArt.Mat("M_KeyGold", mm => { mm.SetFloat("_UseVertexColor", 1); mm.SetFloat("_Gloss", 0.9f); mm.SetFloat("_GlossSize", 80); mm.SetFloat("_RimStrength", 0.5f); }));
                if (key) { key.name = "WindKey"; key.transform.localPosition = list[0].pivot.localPosition + new Vector3(0, 0, 0.02f); train.driverExtra = key.transform; }
            }
            train.Solve();
            return train;
        }

        /// <summary>墙上的一串啮合齿轮：复用门框齿轮模型（同模数），整体放大 scale 倍（放大后仍然啮合）；
        /// 第 i 个和第 i+1 个的连线方向 = dirs[i]（度，0 = 本地 +X），中心距 = (N₁+N₂)·模数/2·scale。</summary>
        static GearTrain WallGears(Transform parent, Vector3 origin, float scale, int[] teeth, float[] dirs)
        {
            var txt = System.IO.File.ReadAllText(Proc + "/W5_GearTrain.json");
            var ms = System.Text.RegularExpressions.Regex.Matches(txt, "\"n\":\\s*(\\d+)");
            var byTeeth = new Dictionary<int, int>();
            for (int i = 0; i < ms.Count; i++) { int n = int.Parse(ms[i].Groups[1].Value); if (!byTeeth.ContainsKey(n)) byTeeth[n] = i; }
            const float mod = 0.0125f;
            var root = new GameObject("WallGears").transform; root.SetParent(parent, false); root.localPosition = origin; root.localScale = Vector3.one * scale;
            var train = root.gameObject.AddComponent<GearTrain>();
            var list = new List<GearTrain.Gear>(); var pos = Vector2.zero;
            for (int i = 0; i < teeth.Length; i++)
            {
                if (i > 0) { float a = dirs[i - 1] * Mathf.Deg2Rad; pos += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ((teeth[i - 1] + teeth[i]) * mod / 2 + 0.003f); }
                if (!byTeeth.TryGetValue(teeth[i], out var src)) continue;
                var pivot = new GameObject("Gear_" + i).transform; pivot.SetParent(root, false); pivot.localPosition = new Vector3(pos.x, pos.y, 0.06f + (i % 2) * 0.004f);
                var g = Inst($"{Proc}/W5_PG_{src:00}.fbx", pivot, MetalVC);
                if (g) g.transform.localPosition = Vector3.zero;
                // 轴销：钉在墙上
                MeshObj("Axle", root, RodMesh(new Vector3(pos.x, pos.y, 0), new Vector3(pos.x, pos.y, 0.1f), 0.012f, new Color(0.98f, 0.8f, 0.5f)), Brass);
                list.Add(new GearTrain.Gear { pivot = pivot, teeth = teeth[i], parent = list.Count - 1 });
            }
            train.gears = list.ToArray(); train.driverDegPerSec = 9f; train.ticksPerSec = 1.5f;
            train.Solve();
            return train;
        }

        /// <summary>烟囱里冒出的棉花团（Bang/Puff）：慢慢上升、变大、变淡。</summary>
        static void Smoke(Transform parent, Vector3 pos)
        {
            var go = new GameObject("Smoke"); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.prewarm = true; main.startLifetime = 3.2f; main.startSpeed = 0.12f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.1f); main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            main.maxParticles = 24; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = 3f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8; sh.radius = 0.02f; sh.rotation = new Vector3(-90, 0, 0);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.2f));
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.15f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = BangArt.Mat("M_Puff", null, Shader.Find("Bang/Puff"));
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play(true); ps.Simulate(4f, true, true);
        }
    }
}
