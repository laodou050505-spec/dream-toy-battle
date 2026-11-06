// 第 4 章：气球拱门 + 门后世界「气球派对」（粉色黄昏的生日游园会；旋转木马是门后机关：转动时门里飘出气球兵，打摇柄可以提前停）。
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
        public static readonly Dictionary<string, (float h, float gloss, float sheen)> W4Sizes = new()
        {
            ["W4_Carousel_Base"] = (0.72f, 0.35f, 0), ["W4_Carousel_Canopy"] = (0.62f, 0.35f, 0), ["W4_CarouselHorse"] = (0.55f, 0.55f, 0),
            ["W4_Crank"] = (0.2f, 0.5f, 0), ["W4_CircusTent"] = (1.15f, 0.1f, 0.4f), ["W4_CakeTower"] = (0.95f, 0.3f, 0.1f),
            ["W4_BalloonBunch"] = (1.0f, 0.75f, 0), ["W4_GiftBox_A"] = (0.28f, 0.5f, 0), ["W4_GiftBox_B"] = (0.34f, 0.5f, 0),
            ["W4_RockingHorse"] = (0.55f, 0.3f, 0.2f), ["W4_HeartCloud"] = (0.8f, 0.02f, 0.8f),
            ["W4_RubberDuck"] = (0.16f, 0.7f, 0), ["W4_ToyPlane"] = (0.16f, 0.6f, 0),
        };

        public static GameObject BuildPortalW4(Transform root, Vector3 pos, int stencilRef)
        {
            var portal = new GameObject("Portal_W4");
            portal.transform.SetParent(root, false);
            portal.transform.localPosition = pos;
            var frame = new GameObject("Frame_Balloons").transform; frame.SetParent(portal.transform, false);
            Inst(Proc + "/W4_BalloonArch.fbx", frame, BangArt.Mat("M_BalloonVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.75f); m.SetFloat("_GlossSize", 60); m.SetFloat("_RimStrength", 0.45f); }));
            var duck = Place("W4_RubberDuck", frame, new Vector3(-0.55f, 0, 0.45f), -25);
            var (pp, _, _) = PathAt(4, "path16", 0.36f);
            var plane = Place("W4_ToyPlane", frame, new Vector3(pp.x, pp.y + 0.1f, 0.3f), -30);   // 停在门框右上的气球边
            Place("W4_GiftBox_A", frame, new Vector3(1.05f, 0, 0.4f), 20);
            PortalOpening(portal.transform, 4, stencilRef);   // 不规则虫洞门洞（遮罩 + 涡旋膜 + 发光内沿）

            var w = new GameObject("World_BalloonParty");
            w.transform.SetParent(portal.transform, false);
            SkyAndBackdrop(w.transform, "W4", new Color(1f, 0.7f, 0.82f), new Color(1f, 0.86f, 0.74f));
            var ground = Inst(Proc + "/W4_Ground.fbx", w.transform, BangArt.Mat("M_ConfettiFeltVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Bang/Art/Textures/T_ConfettiFelt.png")); m.SetFloat("_Gloss", 0.02f); m.SetFloat("_Sheen", 0.5f); m.SetFloat("_RimStrength", 0.06f); m.SetFloat("_ShadeSoft", 0.8f); }));
            FlattenNearRoom(ground, w.transform);   // 真实房间范围内压平地形（不然从房间里看会被山丘挡住门洞）
            var gcol = ground ? ground.GetComponentInChildren<MeshFilter>().gameObject.AddComponent<MeshCollider>() : null;
            Physics.SyncTransforms();
            _groundRoot = w.transform;
            Inst(Proc + "/W4_Flora.fbx", w.transform, BangArt.FeltVC);   // 绒布小花、草丛、贴片、小珠
            var props = new GameObject("Props").transform; props.SetParent(w.transform, false);
            GameObject P(string n, float x, float z, float yaw) { var g = Place(n, props, new Vector3(x, GroundY(x, z), z), yaw); SettleOnGround(g); return g; }

            // 旋转木马（中心）
            BuildCarousel(props, new Vector3(0.3f, GroundY(0.3f, -3.6f), -3.6f));
            var tent = P("W4_CircusTent", -1.55f, -3.4f, 20);
            var cake = P("W4_CakeTower", 1.95f, -3.45f, -20);
            foreach (var (n, x, z, yaw) in new[] { ("W4_GiftBox_A", -0.95f, -1.75f, 15f), ("W4_GiftBox_B", -1.25f, -2.05f, -10f), ("W4_GiftBox_B", 1.45f, -1.95f, 25f),
                                                  ("W4_GiftBox_A", 1.8f, -2.25f, -30f), ("W4_GiftBox_A", 2.45f, -2.75f, 5f), ("W4_GiftBox_B", -2.1f, -2.6f, 40f) })
                P(n, x, z, yaw);
            var rh = P("W4_RockingHorse", -0.45f, -2.25f, 30);
            // 摇摇马没人骑就停着（不为动而动）；玩法里被打中时才晃
            var b1 = P("W4_BalloonBunch", -2.45f, -3.0f, 0); var b2 = P("W4_BalloonBunch", 2.75f, -3.3f, 0); P("W4_BalloonBunch", -0.6f, -5.4f, 0);
            foreach (var b in new[] { b1, b2 }) if (b) { var pd = b.AddComponent<Pendulum>(); pd.length = 1.1f; pd.amplitudeDeg = 3.5f; pd.twistDeg = 6f; }   // 气球束被风吹着绕系绳点轻摆（浮力 = 倒立单摆）
            if (gcol) Object.DestroyImmediate(gcol);
            // 三角旗串：帐篷尖 → 木马顶 → 蛋糕顶
            var flags = new GameObject("Bunting").transform; flags.SetParent(w.transform, false);
            Vector3 Top(GameObject g, float k) { var b = Bounds(g); return flags.InverseTransformPoint(new Vector3(b.center.x, b.min.y + b.size.y * k, b.center.z)); }
            var canopy = props.Find("Carousel/Rotor/W4_Carousel_Canopy");
            if (tent && canopy && cake)
            {
                var ct = Top(canopy.gameObject, 1.0f) + Vector3.up * 0.02f;     // 系在顶棚尖上（转轴上的点不动；系在顶棚中间绳子会从顶棚里穿出来）
                BangFairyLights.Bunting("Bunting_L", flags, new[] { Top(tent, 0.92f), ct }, 0.22f);
                BangFairyLights.Bunting("Bunting_R", flags, new[] { ct, Top(cake, 0.9f) }, 0.22f);
            }
            // 天上的爱心云
            foreach (var (p, s) in new[] { (new Vector3(-2.4f, 2.6f, -8.2f), 1.3f), (new Vector3(1.6f, 3.3f, -9.0f), 1.6f), (new Vector3(3.8f, 2.2f, -8.0f), 1.0f) })
            FloatCloud("W4_HeartCloud", props, p, s);   // 爱心云随风慢慢漂
            Confetti(w.transform, new Vector3(0.3f, 2.2f, -3.6f), new Vector3(6f, 0.5f, 5f));
            Sparkles(w.transform, new Vector3(0.3f, 1.2f, -3.6f), new Vector3(6.5f, 2f, 5.5f), 8);
            Enrich(4, w.transform, props);   // 丰富度加密（远景 / 近景 / 中景 / 小生命）
            FloraRing(4, w.transform);   // 房间四周一整圈地面花草
            Vignettes(4, w.transform, props);   // 房间四周的主题小场景组团
            GiantRing(4, w.transform, props);   // 远处一圈巨型玩具（被玩具包围）
            Surround(4, w.transform, props);  // 房间四周和门洞两侧按主题摆满（玩具化露出哪个方向都不空）
            var pw = w.AddComponent<PortalWorld>();
            pw.stencilRef = stencilRef;
            pw.worldTint = new Color(1f, 0.95f, 0.96f);
            pw.hazeColor = new Color(1f, 0.88f, 0.92f); pw.hazeStart = 9.0f; pw.hazeEnd = 24.0f; pw.hazeAmount = 0.063f;
            pw.Rebind();
            ShadowPolicy(w.transform);                                                         // 只让有体量的物体投影
            w.AddComponent<PortalCuller>().arch = portal.GetComponentInChildren<PortalArch>();   // 只渲染透过门洞 / 已玩具化区域看得到的物体
            Spill(4, portal.transform);   // 门前真实房间里的溢出物（门后世界从洞口淌出来一截）
            return portal;
        }

        /// <summary>旋转木马：底座（含中心柱）+ 转子（顶棚 + 4 匹马，每匹自带金色立杆）+ 前侧摇柄。</summary>
        static void BuildCarousel(Transform props, Vector3 pos)
        {
            var root = new GameObject("Carousel").transform; root.SetParent(props, false); root.localPosition = pos;
            var bas = Place("W4_Carousel_Base", root, Vector3.zero, 0);
            if (!bas) return;
            var bb = Bounds(bas);
            // 底座图里自带的中心柱很短：平台面约在底座高度 45% 处，另接一根 Blender 做的糖果条纹长柱，顶棚架在马儿立杆顶端
            float floorY = bb.min.y + bb.size.y * 0.45f;
            float horseH = W4Sizes["W4_CarouselHorse"].h;
            float canopyY = floorY + horseH * 0.96f;
            var pole = Inst(Proc + "/W4_CarouselPole.fbx", root, BangArt.ToyVC);
            if (pole)
            {
                pole.transform.position = new Vector3(bb.center.x, floorY - 0.05f, bb.center.z);
                pole.transform.localScale = new Vector3(1.6f, canopyY - floorY + 0.2f, 1.6f);
            }
            var rotor = new GameObject("Rotor").transform; rotor.SetParent(root, false);
            var canopy = Place("W4_Carousel_Canopy", rotor, root.InverseTransformPoint(new Vector3(bb.center.x, canopyY, bb.center.z)), 0);
            float rad = Mathf.Min(bb.extents.x, bb.extents.z) * 0.62f;
            var horses = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var p = root.InverseTransformPoint(new Vector3(bb.center.x, floorY, bb.center.z)) + Quaternion.Euler(0, a, 0) * Vector3.forward * rad;
                var h = Place("W4_CarouselHorse", rotor, p, 0);
                if (!h) continue;
                h.transform.localRotation = Quaternion.Euler(0, a + 180f, 0) * Quaternion.Euler(0, 90, 0);   // 马头沿切线方向（同向绕圈跑）
                horses.Add(h.transform);
            }
            // 四匹马按 90° 摆一圈：半径太小时相邻马头马尾会插在一起。按马的俯视占地矩形逐步加大半径（不超出平台），还挤就整体缩小一点
            if (horses.Count > 1)
            {
                var mb = horses[0].GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
                for (int tries = 0; tries < 40; tries++)
                {
                    bool clash = false;
                    var rects = horses.Select(h => { var hs = h.lossyScale; var c = h.TransformPoint(mb.center);
                        return (c: new Vector2(c.x, c.z), half: new Vector2(mb.extents.x * hs.x, mb.extents.z * hs.z), ang: -h.eulerAngles.y); }).ToList();
                    for (int i = 0; i < rects.Count && !clash; i++)
                        for (int j = i + 1; j < rects.Count && !clash; j++)
                            clash = RectsOverlap(rects[i].c, rects[i].half, rects[i].ang, rects[j].c, rects[j].half, rects[j].ang, 0.02f);
                    if (!clash) break;
                    if (rad < Mathf.Min(bb.extents.x, bb.extents.z) * 0.8f) rad += 0.02f;
                    else foreach (var h in horses) h.localScale *= 0.95f;
                    for (int i = 0; i < horses.Count; i++)
                    {
                        float a = i * 90f + 45f;
                        horses[i].localPosition = root.InverseTransformPoint(new Vector3(bb.center.x, floorY, bb.center.z)) + Quaternion.Euler(0, a, 0) * Vector3.forward * rad;
                    }
                }
            }
            var crank = Place("W4_Crank", root, root.InverseTransformPoint(new Vector3(bb.center.x, bb.min.y, bb.max.z + 0.04f)), 0);
            if (crank) crank.name = "W4_Crank";
            var c = root.gameObject.AddComponent<Carousel>();
            c.rotor = rotor; c.horses = horses.ToArray();
        }

        /// <summary>缓缓飘落、翻转的彩纸粒子（Bang/Confetti）。</summary>
        static void Confetti(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("Confetti"); go.transform.SetParent(parent, false); go.transform.localPosition = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.prewarm = true; main.duration = 6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.012f, 0.02f); main.startSizeY = new ParticleSystem.MinMaxCurve(0.025f, 0.04f); main.startSizeZ = 1f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); main.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.62f, 0.78f), 0f), new GradientColorKey(new Color(0.55f, 0.78f, 1f), 0.25f), new GradientColorKey(new Color(1f, 0.88f, 0.45f), 0.5f), new GradientColorKey(new Color(0.74f, 0.62f, 1f), 0.75f), new GradientColorKey(new Color(0.55f, 0.9f, 0.75f), 1f) },
                      new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
            main.gravityModifier = 0.006f;
            main.maxParticles = 160;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.rateOverTime = 18;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = size;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true;
            rot.x = new ParticleSystem.MinMaxCurve(-3f, 3f); rot.y = new ParticleSystem.MinMaxCurve(-2f, 2f); rot.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
            var nz = ps.noise; nz.enabled = true; nz.strength = 0.08f; nz.frequency = 0.4f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = BangArt.Mat("M_Confetti", null, Shader.Find("Bang/Confetti"));
            r.renderMode = ParticleSystemRenderMode.Mesh;
            r.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            r.alignment = ParticleSystemRenderSpace.Local;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play(true);
            ps.Simulate(8f, true, true);
        }
    }
}
