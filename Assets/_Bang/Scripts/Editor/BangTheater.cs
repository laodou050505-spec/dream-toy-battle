// 封面“玩具小剧场”预制体（用户选定概念图 ArtSource/Concept/Cover/cover_A_theater.png）：
// 把 Tools/blender_theater.py 的部件按 Theater_layout.json 摆好，配木纹 / 丝绒（nano banana 材质图）、金色、灯泡；
// 台口一排脚灯、顶梁一排灯泡；拱顶上方 3D 标题字；台上一组小怪演员（游戏里真实的小怪模型）+ 小道具；
// 台口中央街机大按钮、前沿名牌、左右两块字母积木（选关 / 设置）。
// 运行时由 UiManager.ShowTitle 实例化，按名字找：BtnStart / Plaque/Label / Block_0、Block_1 / Sign/Logo / Bulbs / Curtain_L、Curtain_R / Cast。
// 预制体根节点 = 剧场视觉中心（大约在台面以上 0.55 m），封面锚点放在玩家眼前。
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Bang.Play;
using Bang.Play.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Bang.EditorTools
{
    public static class BangTheater
    {
        const string Src = "Assets/_Bang/Art/Models/Proc/UI";
        const string PrefabPath = "Assets/_Bang/Prefabs/UI/TitleTheater.prefab";
        const string Craft = "Assets/_Bang/Art/UI/Craft";

        static string _json;
        static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        static Vector3 V3(string key)
        {
            var m = Regex.Match(_json, "\"" + key + "\":\\s*\\[\\s*([-0-9.eE]+),\\s*([-0-9.eE]+),\\s*([-0-9.eE]+)");
            return m.Success ? new Vector3(F(m.Groups[1].Value), F(m.Groups[2].Value), F(m.Groups[3].Value)) : Vector3.zero;
        }
        static float Num(string key) { var m = Regex.Match(_json, "\"" + key + "\":\\s*([-0-9.eE]+)"); return m.Success ? F(m.Groups[1].Value) : 0; }
        static List<float[]> List(string key)
        {
            var r = new List<float[]>();
            var b = Regex.Match(_json, "\"" + key + "\":\\s*\\[(.*?)\\]\\s*\\]", RegexOptions.Singleline);
            if (!b.Success) return r;
            foreach (Match m in Regex.Matches(b.Groups[1].Value + "]", "\\[([^\\[\\]]+)\\]"))
                r.Add(m.Groups[1].Value.Split(',').Select(x => F(x.Trim())).ToArray());
            return r;
        }

        static Texture2D RepeatTex(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!imp) { AssetDatabase.ImportAsset(path); imp = AssetImporter.GetAtPath(path) as TextureImporter; }
            if (imp && (imp.wrapMode != TextureWrapMode.Repeat || !imp.mipmapEnabled || imp.maxTextureSize != 1024))
            { imp.textureType = TextureImporterType.Default; imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.maxTextureSize = 1024; imp.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // 高级感卡通（用户：欧美卡通 + 高级感）：两档硬明暗 + 小而锐的漆面高光，像上了清漆的木制玩具；不用照片细节贴图 / 绒布泛光 / 边缘光
        static Material Wood => BangArt.Mat("M_ThWood", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.32f); m.SetFloat("_ShadeSoft", 0.08f); m.SetColor("_ShadowColor", new Color(0.52f, 0.46f, 0.66f)); m.SetFloat("_Sheen", 0f); m.SetFloat("_RimStrength", 0f); m.SetFloat("_GlossSize", 96); m.SetFloat("_Ambient", 0.55f); m.SetTexture("_BaseMap", null); });
        static Material Velvet => BangArt.Mat("M_ThVelvet", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.08f); m.SetFloat("_ShadeSoft", 0.08f); m.SetColor("_ShadowColor", new Color(0.52f, 0.46f, 0.66f)); m.SetFloat("_Sheen", 0f); m.SetFloat("_RimStrength", 0f); m.SetFloat("_GlossSize", 96); m.SetFloat("_Ambient", 0.55f); m.SetTexture("_BaseMap", null); });   // 碎丝绒细节贴图（nano banana 微距照片 → craft_process 平铺灰度）+ 绒布边缘泛粉光
        static Material Floor => BangArt.Mat("M_ThFloor", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.35f); m.SetFloat("_ShadeSoft", 0.08f); m.SetColor("_ShadowColor", new Color(0.52f, 0.46f, 0.66f)); m.SetFloat("_Sheen", 0f); m.SetFloat("_RimStrength", 0f); m.SetFloat("_GlossSize", 96); m.SetFloat("_Ambient", 0.55f); m.SetTexture("_BaseMap", null); });   // 台面：窄木条地板（nano banana 微距照片 → 平铺灰度细节），清漆微光
        static Material Gold => BangArt.Mat("M_ThGold", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.6f); m.SetFloat("_ShadeSoft", 0.08f); m.SetColor("_ShadowColor", new Color(0.52f, 0.46f, 0.66f)); m.SetFloat("_Sheen", 0f); m.SetFloat("_RimStrength", 0f); m.SetFloat("_GlossSize", 96); m.SetFloat("_Ambient", 0.55f); m.SetTexture("_BaseMap", null); });
        static Material Sky => BangArt.Mat("M_ThBackdrop", m => { m.SetFloat("_UseVertexColor", 0); m.SetColor("_BaseColor", new Color(0.13f, 0.16f, 0.33f)); m.SetFloat("_Gloss", 0f); m.SetFloat("_ShadeSoft", 0.08f); m.SetColor("_ShadowColor", new Color(0.52f, 0.46f, 0.66f)); m.SetFloat("_Sheen", 0f); m.SetFloat("_RimStrength", 0f); m.SetFloat("_GlossSize", 96); m.SetFloat("_Ambient", 0.55f); m.SetTexture("_BaseMap", null); });   // 后幕：深藏青（衬托金色星月和红幕）
        static Material Bulb => BangArt.Mat("M_TitleBulb", m => { m.SetColor("_BaseColor", new Color(0.82f, 0.74f, 0.66f)); m.SetFloat("_Gloss", 0.9f); m.SetColor("_EmissionColor", Color.black); m.EnableKeyword("_EMISSION"); });

        static GameObject Part(Transform parent, string mesh, string name, Vector3 pos, Material mat, Quaternion? rot = null)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = pos; if (rot.HasValue) t.localRotation = rot.Value;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Src}/{mesh}.fbx");
            if (!src) { Debug.LogWarning("[Bang] 缺少剧场部件 " + mesh); return t.gameObject; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(src, t); g.transform.localPosition = Vector3.zero;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            return t.gameObject;
        }

        static void BulbAt(Transform parent, Vector3 p, float size)
        {
            var b = new GameObject("Bulb"); b.transform.SetParent(parent, false); b.transform.localPosition = p; b.transform.localScale = Vector3.one * size;
            b.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            var r = b.AddComponent<MeshRenderer>(); r.sharedMaterial = Bulb; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static TextMeshPro Print(Transform parent, string s, Vector3 pos, float size, Color col, float width)
        {
            var g = new GameObject("Label"); g.transform.SetParent(parent, false); g.transform.localPosition = pos;
            var t = g.AddComponent<TextMeshPro>();
            t.font = BangPlaySetup.Font(); t.fontSize = size * 10f; t.color = col; t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = false; t.rectTransform.sizeDelta = new Vector2(width, size * 2f); t.text = s;
            return t;
        }

        static Bounds BoundsOf(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        /// <summary>演员：放一只小怪，缩到目标高度，脚踩台面，面朝观众（-Z）；关掉它的玩法脚本（只留模型和骨架数据）。</summary>
        /// <summary>让一个独立物件能被枪打中：按渲染范围加碰撞体 + TitleToyHit。</summary>
        /// <summary>幕布中间吊着的月亮和星星：每个都是独立物件（自己的吊绳、以挂点为支点），能单独摆动、单独被打（用户：星星不要连在一起）。
        /// 原来 4 颗星是一整块网格，其中 2 颗藏在幕布后面看不见，不再保留；留下的和靶子、月亮拉开间距。</summary>
        static void HangParts(Transform th, Material gold)
        {
            var strings = List("strings");
            Mesh Source(string mesh) => AssetDatabase.LoadAllAssetsAtPath($"{Src}/{mesh}.fbx").OfType<Mesh>().First();
            void Hang(string name, Mesh src, int[] tris, float oldX, float newX)
            {
                var st = strings.OrderBy(q => Mathf.Abs(q[0] - oldX)).First();
                float top = Mathf.Max(st[1], st[2]), bottom = Mathf.Min(st[1], st[2]);
                var hook = new Vector3(newX, top, st[3]);
                var root = new GameObject(name).transform; root.SetParent(th, false); root.localPosition = hook;
                var v = src.vertices; var n = src.normals; var col = src.colors; var uv = src.uv; var map = new Dictionary<int, int>(); var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nc = new List<Color>(); var nuv = new List<Vector2>(); var nt = new List<int>();
                foreach (var i in tris)
                {
                    if (!map.TryGetValue(i, out var k)) { k = map[i] = nv.Count; nv.Add(v[i] + new Vector3(newX - oldX, 0, 0) - hook); nn.Add(n[i]); if (col.Length > 0) nc.Add(col[i]); if (uv.Length > 0) nuv.Add(uv[i]); }
                    nt.Add(k);
                }
                var m = new Mesh { name = "Th_" + name }; m.SetVertices(nv); m.SetNormals(nn); if (nc.Count > 0) m.SetColors(nc); if (nuv.Count > 0) m.SetUVs(0, nuv); m.SetTriangles(nt, 0); m.RecalculateBounds();
                var body = new GameObject(name + "_Mesh"); body.transform.SetParent(root, false);
                body.AddComponent<MeshFilter>().sharedMesh = SaveMesh(m, "Th_" + name);
                var br = body.AddComponent<MeshRenderer>(); br.sharedMaterial = gold; br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var line = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(line.GetComponent<Collider>());
                line.name = "String"; line.transform.SetParent(root, false); line.transform.localPosition = new Vector3(0, (bottom - top) * 0.5f, 0);
                line.transform.localScale = new Vector3(0.0018f, (top - bottom) * 0.5f, 0.0018f);
                var lr = line.GetComponent<MeshRenderer>(); lr.sharedMaterial = gold; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var bob = root.gameObject.AddComponent<Bang.Ambient.Bob>(); bob.amplitude = 0.003f; bob.period = 3.1f + newX * 2f; bob.swayDeg = 1.6f; bob.phase = (newX + 1f) * 1.7f;
            }
            var moon = Source("Th_Moon"); Hang("Moon", moon, moon.triangles, 0.04f, 0.07f);
            var stars = Source("Th_Stars"); var pieces = Islands(stars);
            int[] Nearest(float x) => pieces.OrderBy(t => Mathf.Abs(t.Select(i => stars.vertices[i].x).Average() - x)).First();
            Hang("Star_L", stars, Nearest(-0.12f), -0.12f, -0.2f);
            Hang("Star_R", stars, Nearest(0.2f), 0.2f, 0.2f);
        }

        /// <summary>按共用顶点位置把网格分成互不相连的几块，返回每块的三角形下标。</summary>
        static List<int[]> Islands(Mesh m)
        {
            var v = m.vertices; var t = m.triangles; var parent = Enumerable.Range(0, v.Length).ToArray();
            int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
            var weld = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < v.Length; i++)
            {
                var k = new Vector3Int(Mathf.RoundToInt(v[i].x * 10000), Mathf.RoundToInt(v[i].y * 10000), Mathf.RoundToInt(v[i].z * 10000));
                if (weld.TryGetValue(k, out var o)) parent[Find(i)] = Find(o); else weld[k] = i;
            }
            for (int i = 0; i < t.Length; i += 3) { parent[Find(t[i + 1])] = Find(t[i]); parent[Find(t[i + 2])] = Find(t[i]); }
            var groups = new Dictionary<int, List<int>>();
            for (int i = 0; i < t.Length; i += 3)
            {
                int r = Find(t[i]); if (!groups.TryGetValue(r, out var g)) groups[r] = g = new List<int>();
                g.Add(t[i]); g.Add(t[i + 1]); g.Add(t[i + 2]);
            }
            return groups.Values.Select(g => g.ToArray()).ToList();
        }

        static void Shootable(Transform t)
        {
            if (!t) return;
            var mfs = t.GetComponentsInChildren<MeshFilter>(true); if (mfs.Length == 0) return;
            bool has = false; var b = new Bounds();
            foreach (var mf in mfs)                                       // 按网格自身的包围盒换算（世界包围盒遇到倾斜的物件会虚胖，挡住旁边的东西）
            {
                if (!mf.sharedMesh || mf.name.StartsWith("LOD1") || mf.name.StartsWith("LOD2") || mf.name == "String") continue;
                var e = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var wp = mf.transform.TransformPoint(e.center + Vector3.Scale(e.extents, new Vector3((i & 1) * 2 - 1, (i >> 1 & 1) * 2 - 1, (i >> 2 & 1) * 2 - 1)));
                    var lp = t.InverseTransformPoint(wp);
                    if (!has) { b = new Bounds(lp, Vector3.zero); has = true; } else b.Encapsulate(lp);
                }
            }
            foreach (var c in t.GetComponents<BoxCollider>()) Object.DestroyImmediate(c);
            var box = t.gameObject.AddComponent<BoxCollider>(); box.center = b.center; box.size = b.size;
            if (!t.GetComponent<TitleToyHit>()) t.gameObject.AddComponent<TitleToyHit>();
        }

        static GameObject Actor(Transform cast, string prefab, string name, Vector2 xz, float height, float yaw, float topY)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Bang/Prefabs/Enemies/{prefab}.prefab");
            if (!pf) pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{BangArt.PropDir}/{prefab}.prefab");
            if (!pf) { Debug.LogWarning("[Bang] 缺少演员 " + prefab); return null; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(pf, cast); g.name = name;
            g.transform.localRotation = Quaternion.Euler(0, 180f + yaw, 0);       // 小怪预制体正面朝 +Z → 转成面朝观众
            var b = BoundsOf(g); float k = height / Mathf.Max(1e-3f, b.size.y); g.transform.localScale *= k;
            b = BoundsOf(g);
            var want = cast.TransformPoint(new Vector3(xz.x, topY, xz.y));
            // 落地按脚底（有 Foot 网格的小怪）：整体包围盒的最低点可能是下垂的尾巴，封面动画把尾巴甩起来后脚就悬空了（用户：老鼠没站在台上）
            float minY = b.min.y; bool feet = false;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || !mf.name.Contains("Foot")) continue;
                foreach (var v in mf.sharedMesh.vertices) { float y = mf.transform.TransformPoint(v).y; if (!feet || y < minY) { minY = y; feet = true; } }
            }
            g.transform.position += new Vector3(want.x - b.center.x, want.y - minY - 0.002f, want.z - b.center.z);
            var rig = g.GetComponent<Bang.Play.EnemyRig>();
            if (rig && rig.pack) rig.pack.gameObject.SetActive(false);         // 背后的小旗从正面看像“鸟嘴”：台上不背旗（摆位按含旗的包围盒算过，去掉只会更靠前，不碰幕布）
            return g;
        }

        public static GameObject Build(GameObject logoPrefab, Material logoMat, GameObject btnSource)
        {
            AssetDatabase.ImportAsset(Src, ImportAssetOptions.ImportRecursive);
            _json = File.ReadAllText($"{Src}/Theater_layout.json");
            float topY = Num("top_y"), halfW = Num("half_w"), frontB = Num("front_b");
            var root = new GameObject("TitleTheater");
            var th = new GameObject("Theater").transform; th.SetParent(root.transform, false); th.localPosition = new Vector3(0, -0.6f, 0.05f);

            Part(th, "Th_Stage", "Stage", Vector3.zero, Wood);
            Part(th, "Th_StageTop", "StageTop", Vector3.zero, Floor);
            Part(th, "Th_StageTrim", "StageTrim", Vector3.zero, Gold);
            Part(th, "Th_Crest", "Crest", Vector3.zero, Gold);
            foreach (var (c, i) in List("columns").Select((c, i) => (c, i))) Part(th, "Th_Column", i == 0 ? "Column_L" : "Column_R", new Vector3(c[0], c[1], c[2]), Wood);
            Part(th, "Th_Arch", "Arch", Vector3.zero, Wood);
            Part(th, "Th_ArchTrim", "ArchTrim", Vector3.zero, Gold);
            Part(th, "Th_Backdrop", "Backdrop", V3("backdrop"), Velvet);
            HangParts(th, Gold);
            Part(th, "Th_Valance", "Valance", Vector3.zero, Velvet);
            Part(th, "Th_Fringe", "Fringe", Vector3.zero, Gold);
            // 幕布：两片，原点在外侧上角向 +X 展开；右片镜像（缩放 X 取负）。拉开 = X 缩到 0.3
            var cl = Part(th, "Th_Curtain", "Curtain_L", V3("curtain_L"), Velvet);
            var cr = Part(th, "Th_Curtain", "Curtain_R", V3("curtain_R"), Velvet);
            cr.transform.localScale = new Vector3(-1, 1, 1);
            foreach (var r in cr.GetComponentsInChildren<Renderer>()) r.sharedMaterial = BangArt.Mat("M_ThVelvetFlip", m => { m.CopyPropertiesFromMaterial(Velvet); m.SetFloat("_Cull", 0); }, Velvet.shader);

            // 束幕金绳：幕布拉开后挂在两边幕布正前方（TitleStageFx 幕布开到位才弹出来）；挂高在演员举手 / 蹦跳够不到的地方
            foreach (var sx in new[] { -1f, 1f })
            {
                float cx = sx * (V3("curtain_R").x - Num("curtain_width") * 0.42f * 0.5f);
                float cz = Mathf.Min(V3("curtain_L").z, V3("curtain_R").z) - 0.054f;          // 幕布在这个高度最靠前约 z−0.04，金绳再往前 1.4 cm
                var tb = Part(th, "Th_TieBack", sx < 0 ? "TieBack_L" : "TieBack_R", new Vector3(cx, topY + 0.52f, cz), Gold);
                tb.transform.localScale = Vector3.one * 0.001f;
            }

            // 灯泡：顶梁一排 + 台口脚灯（灯座 + 灯泡）
            var bulbs = new GameObject("Bulbs").transform; bulbs.SetParent(th, false);
            foreach (var p in List("arch_bulbs")) BulbAt(bulbs, new Vector3(p[0], p[1], p[2]), 0.026f);
            var feet = new GameObject("FootBulbs").transform; feet.SetParent(th, false);
            foreach (var p in List("foot_lights"))
            {
                var rot = Quaternion.Euler(0, p[3] + 180f, 0);                  // 灯罩背朝观众、开口朝舞台里
                Part(feet, "Th_FootSocket", "Socket", new Vector3(p[0], p[1], p[2]), Gold, rot);
                BulbAt(bulbs, new Vector3(p[0], p[1] + 0.009f, p[2]), 0.016f);
            }
            // （光池 / 灯泡光晕 / 两道追光都去掉了：用户——AI 味重、要高级感；柔光特效是廉价感来源，明暗交给材质本身）

            // 标题字（错位设计）：前层字块 + 后层深紫影子层（同一张贴图，退后 3.5 cm）→ 正面看是套印错位，动头时前后两层真实错开。
            // 中文 Sign/Logo、英文 Sign/Logo_EN 两套都搭好，UiManager.LocalizeStage 按语言切换显示。
            var sign = new GameObject("Sign").transform; sign.SetParent(th, false); sign.localPosition = V3("logo") + new Vector3(0f, 0.075f, -0.12f);
            TitleChars(sign);                                 // 中文标题：五个手写字各自成块、错位排版（用户：注意文字错位，设计感要强）
            TitleCharsEn(sign);                               // 英文标题：同一套手绘玩具字，14 个字母各自成块，两行错落
            var mousePf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Bang/Prefabs/Enemies/Enemy_Mouse.prefab");
            if (mousePf)
            {
                var ms = Object.Instantiate(mousePf, sign); ms.name = "LogoMouse";
                ms.transform.localPosition = new Vector3(0f, 0.22f, 0.05f); ms.transform.localRotation = Quaternion.Euler(0, 200, 0); ms.transform.localScale = Vector3.one * 0.9f;
                for (int pass = 0; pass < 3; pass++)                                            // 有依赖的脚本（EnemyMotion 依赖 EnemyRig）：多删几轮，先删掉依赖方
                    foreach (var mb in ms.GetComponentsInChildren<MonoBehaviour>(true).Reverse()) { if (!mb) continue; try { Object.DestroyImmediate(mb); } catch { } }   // 连 Bob 一起去掉：只跟着标题整体缓缓漂，不自己上下蹦
            }
            // 放射彩色条纹：粉色为主（用户：游戏场景偏粉），立体微弧、慢慢转——XR 里是真实几何体，不是一张图
            // 紧贴幕布后方、整圈落在 2.7 米天花板以下（用户：上面不能被截）；更矮的房间由 UiManager.FitStage 运行时缩小
            var sun = new GameObject("Sunburst"); sun.transform.SetParent(root.transform, false); sun.transform.localPosition = new Vector3(0, -0.07f, 0.42f);
            sun.AddComponent<MeshFilter>().sharedMesh = SaveMesh(SunburstMesh(1.075f, 0.14f, 28), "Th_Sunburst");
            var sunR = sun.AddComponent<MeshRenderer>(); sunR.sharedMaterial = BangArt.Mat("M_UIToon", m => { }, Shader.Find("Bang/UIToon")); sunR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var spin = sun.AddComponent<Bang.Ambient.Spin>(); spin.axis = Vector3.forward; spin.degPerSec = 3.5f;
            // 漂浮物件 = 游戏里真正出现的东西（用户：封面必须和游戏内容匹配）：玩家的糖果手枪 + 枪口前一串泡沫软弹、
            // 小怪（积木插旗兵 / 胖墩）、它们插的大小旗、补给礼盒、投石车积木球、气球靶——倾斜着散在剧场四周不同深度，各自轻轻漂（统一深梅紫描边）；下排按钮周围留空，漂浮物都在按钮行以上、舞台两侧
            var floaters = new GameObject("Floaters").transform; floaters.SetParent(root.transform, false);
            const string E = "Assets/_Bang/Prefabs/Enemies";
            var props = new (string path, Vector3 p, float h, Vector3 rot)[]
            {
                ($"{E}/ToyEagle.prefab", new Vector3(0.82f, -0.2f, -0.42f), 0.26f, new Vector3(-8, -118, 14)),       // 手枪：右前方，枪口斜指舞台
                ($"{E}/Dart_Foam.prefab", new Vector3(0.6f, -0.1f, -0.38f), 0.05f, new Vector3(0, -105, 0)),
                ($"{E}/Dart_Foam.prefab", new Vector3(0.48f, -0.04f, -0.33f), 0.05f, new Vector3(4, -100, 0)),
                ($"{E}/Dart_Foam.prefab", new Vector3(0.37f, 0.01f, -0.28f), 0.05f, new Vector3(-3, -108, 0)),
                ($"{E}/ToyShield.prefab", new Vector3(-0.72f, -0.06f, -0.28f), 0.2f, new Vector3(6, 165, 10)),          // 玩具盾：兔子左上方，和右边的手枪呼应
                ($"{E}/Enemy_Planter.prefab", new Vector3(-0.9f, 0.3f, -0.2f), 0.24f, new Vector3(4, 168, -10)),
                ($"{E}/Flag_Small.prefab", new Vector3(1.12f, 0.46f, -0.05f), 0.2f, new Vector3(0, 195, -10)),
                ($"{E}/Flag_Big.prefab", new Vector3(-1.12f, -0.02f, 0.05f), 0.24f, new Vector3(0, 165, 9)),
                ($"{E}/GiftBox.prefab", new Vector3(1.32f, -0.42f, -0.1f), 0.13f, new Vector3(8, 200, -8)),
                ($"{E}/BlockBall.prefab", new Vector3(-1.02f, -0.34f, -0.2f), 0.1f, new Vector3(20, 15, 30)),
                ($"{E}/Target_Balloon.prefab", new Vector3(0.95f, 0.14f, 0.18f), 0.26f, new Vector3(0, 180, 5)),
            };
            int pi = 0;
            foreach (var (path, p, h, rot) in props)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!pf) { Debug.LogWarning("[Bang] 封面缺少 " + path); continue; }
                var g = Object.Instantiate(pf, floaters); g.name = "Float_" + pf.name;
                for (int pass = 0; pass < 3; pass++)                                            // 游戏里的行为脚本要游戏驱动，封面上只留漂动
                    foreach (var mb in g.GetComponentsInChildren<MonoBehaviour>(true).Reverse()) { if (!mb) continue; try { Object.DestroyImmediate(mb); } catch { } }
                foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                var b = BoundsOf(g); float k = h / Mathf.Max(0.01f, Mathf.Max(b.size.y, b.size.x * 0.6f)); g.transform.localScale *= k;
                g.transform.localPosition = p; g.transform.localRotation = Quaternion.Euler(rot);
                var bob = g.AddComponent<Bang.Ambient.Bob>(); bob.amplitude = 0.012f; bob.period = 3.4f + pi * 0.37f; bob.swayDeg = 4f; bob.phase = pi * 0.9f;
                foreach (var r in g.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (pf.name == "ToyEagle" || pf.name == "ToyShield") r.sharedMaterial = BangArt.ToyVC;   // 半透明材质会透出描边外壳；封面上用同样配色的不透明版
                }
                pi++;
            }

            // （去掉了 AI 生成的梦境远景大图——用户：封面 AI 味太重、XR 里不能是平面图；剧场直接立在真实房间里）
            // （漂浮金星去掉：卡通风不要闪光装饰）

            // 台口：街机大按钮 + 前沿名牌 + 左右字母积木（沿用旧封面的部件）
            var ui = "Assets/_Bang/Art/Models/Proc/UI";
            Material VC = BangArt.Mat("M_TitleVC", null), Candy = BangArt.Mat("M_TitleCandy", null);
            // 按钮（用户：按钮看不清）：舞台正下方一排实体立体按键，全部正对玩家、微微上仰，大字；节点名不变（BtnStart/Dome、Plaque/Label、Block_n/Label）
            Color ink = new Color(0.27f, 0.11f, 0.29f), ivory = new Color(1f, 0.97f, 0.91f), hot = new Color(0.93f, 0.28f, 0.42f);
            float keyY = -0.1f, keyZ = -frontB - 0.14f; var tilt = Quaternion.Euler(14, 0, 0);
            Transform Key(string name, Vector3 pos, Vector2 size, Color face, string keyChild)
            {
                var t = new GameObject(name).transform; t.SetParent(th, false); t.localPosition = pos; t.localRotation = tilt;
                float rad = size.y * 0.42f; var side = new Color(face.r * 0.76f, face.g * 0.68f, face.b * 0.74f);
                var well = new GameObject("Base"); well.transform.SetParent(t, false); well.transform.localPosition = new Vector3(0, -0.005f, 0.018f);
                well.AddComponent<MeshFilter>().sharedMesh = SaveMesh(Object.Instantiate(UiKit.Slab(size + new Vector2(0.02f, 0.026f), rad + 0.01f, 0.014f, ink, ink)), "Th_Key_" + name + "_Base");
                var wr = well.AddComponent<MeshRenderer>(); wr.sharedMaterial = BangArt.Mat("M_UIToon", m => { }, Shader.Find("Bang/UIToon")); wr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var key = new GameObject(keyChild); key.transform.SetParent(t, false);
                key.AddComponent<MeshFilter>().sharedMesh = SaveMesh(Object.Instantiate(UiKit.Slab(size, rad, 0.026f, face, side)), "Th_Key_" + name + "_Face");
                var kr = key.AddComponent<MeshRenderer>(); kr.sharedMaterial = wr.sharedMaterial; kr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var col = t.gameObject.AddComponent<BoxCollider>(); col.size = new Vector3(size.x + 0.01f, size.y + 0.01f, 0.05f); col.center = new Vector3(0, 0, 0.01f);
                return t;
            }
            var btn = Key("BtnStart", new Vector3(0, keyY, keyZ), new Vector2(0.48f, 0.14f), hot, "Dome");
            var plaque = new GameObject("Plaque").transform; plaque.SetParent(th, false); plaque.localPosition = btn.localPosition; plaque.localRotation = tilt;
            var pl = Print(plaque, "开始游戏", new Vector3(0, -0.004f, -0.012f), 0.066f, Color.white, 0.44f);
            string[] labels = { "选关", "设置", "玩法介绍", "退出游戏" };
            float[] xs = { -0.43f, 0.43f, -0.70f, 0.70f };
            for (int i = 0; i < 4; i++)
            {
                var blk = Key("Block_" + i, new Vector3(xs[i], keyY, keyZ), new Vector2(0.24f, 0.105f), ivory, "Key");
                var lab = Print(blk, labels[i], new Vector3(0, -0.003f, -0.006f), 0.046f, ink, 0.21f);
                lab.enableAutoSizing = true; lab.fontSizeMin = lab.fontSize * 0.6f; lab.fontSizeMax = lab.fontSize;
            }

            // 演员：小怪们站在幕布前的台口（幕布在 z≈0.05..0.13 开合，演员整个身子都在 z < 0.03：幕布怎么动都碰不到——用户：幕布有穿模）
            //   错落一排，不挡大按钮；练习用的气球靶挂在幕后正中高处（拉开幕布才露出来）
            var cast = new GameObject("Cast").transform; cast.SetParent(th, false);
            // 舞台内容（用户：积木小人丑、台上太空）：换成项目里质量最好的玩具，发条熊居中当主角，两侧发条兔、摇摇马，前面套圈 + 老鼠
            Actor(cast, "W5_WindupBear", "Toy_Bear", new Vector2(0.02f, -0.1f), 0.36f, 90f, topY);   // P2 模型正面朝 -X：+90° 才面向观众
            Actor(cast, "W5_WindupBunny", "Toy_Bunny", new Vector2(-0.38f, -0.1f), 0.28f, 104f, topY);
            Actor(cast, "W4_RockingHorse", "Toy_Horse", new Vector2(0.38f, -0.1f), 0.25f, 58f, topY);
            Actor(cast, "W6_StackRings", "Toy_Rings", new Vector2(0.2f, -0.2f), 0.12f, 0f, topY);
            Actor(cast, "Enemy_Mouse", "Mouse", new Vector2(-0.22f, -0.15f), 0.1f, 20f, topY);
            Actor(cast, "Target_Balloon", "Balloon", new Vector2(-0.085f, 0.21f), 0.26f, 0f, topY + 0.4f);   // 气球不藏到幕檐后面
            foreach (var mb in cast.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;           // 玩法脚本不跑，动作由 TitleStageFx 做
            foreach (var c in cast.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);        // 不挡视线 / 按钮射线

            // 飘落的彩纸（台上方一小片）
            var mascot = new GameObject("Mascot").transform; mascot.SetParent(th, false);                       // 兼容旧接口（不再放老鼠绕圈）

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            // 所有独立物件都能被枪打中：台上玩具、漂浮物、标题的每个字、字上的老鼠、吊着的月亮星星（气球靶有自己的打爆逻辑，不动）
            foreach (Transform c in cast) if (!c.name.Contains("Balloon")) Shootable(c);
            foreach (Transform c in floaters) Shootable(c);
            var logoNode = sign.Find("Logo"); if (logoNode) foreach (Transform c in logoNode) Shootable(c);
            var logoEn = sign.Find("Logo_EN"); if (logoEn) foreach (Transform c in logoEn) Shootable(c);
            Shootable(sign.Find("LogoMouse"));
            Shootable(th.Find("Moon")); Shootable(th.Find("Star_L")); Shootable(th.Find("Star_R"));
            var pfb = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return pfb;
        }


        static Material GlowMat(string name, Texture2D tex, Color c) => BangArt.Mat(name, m => { m.SetTexture("_BaseMap", tex); m.SetColor("_Color", c); m.renderQueue = 3050; }, Shader.Find("Bang/FxSprite"));

        /// <summary>追光光柱：沿 +Z 的开口圆锥（顶半径 r0、底半径 r1、长 len），UV v = 0 顶 → 1 底。</summary>
        /// <summary>放射光芒：内半径 r0 到外半径 r1 的扇形条，两色交替（芥末金两档）+ 外圈白边 + 深藏青细边；正面朝 -Z，烘顶点色（线性）。</summary>
        static Mesh SunburstMesh(float r1, float r0, int rays)
        {
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            Color a = new Color(0.96f, 0.50f, 0.64f).linear, b = new Color(1f, 0.77f, 0.83f).linear, butter = new Color(1f, 0.87f, 0.62f).linear, lilac = new Color(0.82f, 0.74f, 0.98f).linear, white = new Color(1f, 0.93f, 0.95f).linear, ink = new Color(0.27f, 0.11f, 0.29f).linear;
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color col)
            { int k = v.Count; v.AddRange(new[] { p0, p1, p2, p3 }); c.AddRange(new[] { col, col, col, col }); t.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 }); }
            Vector3 P(float r, float ang, float z) => new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, z);
            float step = Mathf.PI * 2 / rays;
            for (int i = 0; i < rays; i++)
            {
                for (int j = 0; j < 6; j++)                                    // 每条再分 6 小段，外缘是真正的圆弧
                {
                float a0 = (i + j / 6f) * step, a1 = (i + (j + 1) / 6f) * step;
                for (int s = 0; s < 3; s++)                                    // 每条光束分 3 小段，微微弧形（不是一块平板）
                {
                    float u0 = s / 3f, u1 = (s + 1) / 3f; float ra = Mathf.Lerp(r0, r1, u0), rb = Mathf.Lerp(r0, r1, u1);
                    float za = u0 * u0 * 0.18f, zb = u1 * u1 * 0.18f;
                    Quad(P(ra, a0, za), P(rb, a0, zb), P(rb, a1, zb), P(ra, a1, za), (i & 1) == 0 ? a : i % 6 == 1 ? butter : i % 6 == 3 ? lilac : b);
                }
                Quad(P(r1, a0, 0.18f), P(r1 + 0.05f, a0, 0.2f), P(r1 + 0.05f, a1, 0.2f), P(r1, a1, 0.18f), white);        // 外圈白边
                Quad(P(r1 + 0.05f, a0, 0.2f), P(r1 + 0.065f, a0, 0.205f), P(r1 + 0.065f, a1, 0.205f), P(r1 + 0.05f, a1, 0.2f), ink);
                }
            }
            var m = new Mesh { name = "Sunburst" }; m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            var nn = m.normals; if (nn[0].z > 0) { var tt = m.triangles; for (int i = 0; i < tt.Length; i += 3) (tt[i + 1], tt[i + 2]) = (tt[i + 2], tt[i + 1]); m.triangles = tt; m.RecalculateNormals(); }
            return m;
        }

        static Mesh BeamMesh(float r0, float r1, float len, int seg)
        {
            var v = new System.Collections.Generic.List<Vector3>(); var uv = new System.Collections.Generic.List<Vector2>(); var t = new System.Collections.Generic.List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f, c = Mathf.Cos(a), s2 = Mathf.Sin(a);
                v.Add(new Vector3(c * r0, s2 * r0, 0)); uv.Add(new Vector2(i / (float)seg, 0));
                v.Add(new Vector3(c * r1, s2 * r1, len)); uv.Add(new Vector2(i / (float)seg, 1));
            }
            for (int i = 0; i < seg; i++) { int a = i * 2; t.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 }); }
            var m = new Mesh { name = "Beam" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Mesh SaveMesh(Mesh m, string name)
        {
            var path = $"{Src}/{name}.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old) { old.Clear(); EditorUtility.CopySerialized(m, old); AssetDatabase.SaveAssets(); return old; }
            AssetDatabase.CreateAsset(m, path); return m;
        }

        /// <summary>光柱贴图：顶上亮、往下渐隐（底端淡到 0，落到台面时不出硬边）。</summary>
        static Texture2D BeamTex()
        {
            var path = "Assets/_Bang/Art/Textures/FX/FX_Beam.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(8, 128, TextureFormat.RGBA32, false);
                for (int y = 0; y < 128; y++)
                {
                    float v = y / 127f;                                         // 网格 UV v：0 = 灯口（顶），1 = 落到台面（底）；贴图第 y 行就是 v
                    float a = Mathf.Pow(1f - v, 1.3f) * 0.85f;
                    a *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.08f));             // 灯口处也收一下，不出硬边
                    for (int x = 0; x < 8; x++) tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti) { ti.wrapMode = TextureWrapMode.Clamp; ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport(); }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>微弯的大幕（圆柱面一段，凹面朝观众），宽 w、高 h、弯曲半径 radius；UV 0..1。</summary>
        static Mesh CurvedPanel(float w, float h, float radius, int seg)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            float ang = w / radius;
            for (int i = 0; i <= seg; i++)
            {
                float u = i / (float)seg, a = (u - 0.5f) * ang;
                float x = Mathf.Sin(a) * radius, z = radius - Mathf.Cos(a) * radius;      // 两边往前弯（凹面朝观众）
                v.Add(new Vector3(x, -h / 2, -z)); uv.Add(new Vector2(u, 0));
                v.Add(new Vector3(x, h / 2, -z)); uv.Add(new Vector2(u, 1));
            }
            for (int i = 0; i < seg; i++) { int a0 = i * 2; t.AddRange(new[] { a0, a0 + 1, a0 + 2, a0 + 2, a0 + 1, a0 + 3 }); }
            var m = new Mesh { name = "CoverBackdrop" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            Directory.CreateDirectory("Assets/_Bang/Art/Models/Generated");
            var path = "Assets/_Bang/Art/Models/Generated/CoverBackdrop.asset";
            var old = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (old) { old.Clear(); EditorUtility.CopySerialized(m, old); return old; }
            AssetDatabase.CreateAsset(m, path); return m;
        }

        /// <summary>错位标题：前层（字块）+ 后层（深紫影子，只留最大的一块剪影，退后 3.5 cm）；整体宽 width 米。</summary>
        /// <summary>中文标题：手写字逐字成块（Title_nf 字面 / Title_nb 底板），大小、高低、前后深度、转角都错开；每字轻轻漂动，转头能看出真实层次。</summary>
        static Material LogoFront() => BangArt.Mat("M_LogoToonFront", m => { m.SetColor("_BaseColor", new Color(1f, 0.97f, 0.91f)); m.SetColor("_EdgeColor", new Color(0.96f, 0.50f, 0.64f)); }, Shader.Find("Bang/TitleToon"));   // 不受灯光影响：头显和编辑器颜色一致

        /// <summary>英文标题的字母（字形图 title_en_v1.png 切出来）：位置按图上的相对位置，整体缩到和中文标题同宽。默认隐藏，英文模式由 UiManager 打开。</summary>
        static void TitleCharsEn(Transform sign)
        {
            var g = new GameObject("Logo_EN").transform; g.SetParent(sign, false); g.localPosition = new Vector3(0, 0.04f, 0); g.gameObject.SetActive(false);
            var matF = LogoFront(); var matB = BangArt.Mat("M_LogoToonBack", m => { m.SetColor("_BaseColor", new Color(0.27f, 0.11f, 0.29f)); }, Shader.Find("Universal Render Pipeline/Unlit"));
            const float K = 0.5f;                                // 下排不压拱顶和顶上的星星
            var pos = new[] { (-1.025f, 0.2f), (-0.737f, 0.288f), (-0.47f, 0.223f), (-0.201f, 0.317f), (0.121f, 0.232f), (0.531f, 0.263f), (0.762f, 0.203f), (1.025f, 0.225f),   // DREAM TOY
                              (-0.776f, -0.238f), (-0.405f, -0.267f), (-0.124f, -0.215f), (0.206f, -0.255f), (0.478f, -0.227f), (0.779f, -0.235f) };                                 // BATTLE
            for (int i = 0; i < pos.Length; i++)
            {
                var c = new GameObject("Char_" + i).transform; c.SetParent(g, false);
                c.localPosition = new Vector3(pos[i].Item1 * K, pos[i].Item2 * K, (i % 3 - 1) * 0.03f); c.localScale = Vector3.one * K;
                foreach (var (layer, mat, off) in new[] { ("f", matF, Vector3.zero), ("b", matB, new Vector3(0, -0.012f, 0.04f)) })
                {
                    var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Src}/Title_EN_{i}{layer}.fbx"); if (!src) { Debug.LogWarning("[Bang] 缺少标题字母 " + i + layer); continue; }
                    var lg = (GameObject)PrefabUtility.InstantiatePrefab(src, c); lg.name = layer == "f" ? "Front" : "Back"; lg.transform.localPosition = off;
                    foreach (var r in lg.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                }
                var bob = c.gameObject.AddComponent<Bang.Ambient.Bob>(); bob.amplitude = 0.004f; bob.period = 3.6f + i * 0.21f; bob.swayDeg = 1.2f; bob.phase = i * 0.9f;
            }
        }

        static void TitleChars(Transform sign)
        {
            var g = new GameObject("Logo").transform; g.SetParent(sign, false);
            var matF = LogoFront();
            var matB = BangArt.Mat("M_LogoToonBack", m => { m.SetColor("_BaseColor", new Color(0.27f, 0.11f, 0.29f)); }, Shader.Find("Universal Render Pipeline/Unlit"));
            var cfg = new (float x, float y, float z, float s, float rot)[]
            {
                (-0.52f, 0.05f, 0.00f, 0.54f, -5f),    // 梦：大
                (-0.25f, -0.07f, -0.05f, 0.44f, 3f),   // 境：压低、靠前
                (0.0f, 0.10f, 0.03f, 0.42f, -3f),     // 玩：抬高、靠后
                (0.24f, -0.06f, -0.06f, 0.44f, 4f),    // 具：压低、靠前
                (0.52f, 0.06f, -0.10f, 0.54f, -5f),    // 战：大、最靠前，尾笔射出去
            };
            for (int i = 0; i < 5; i++)
            {
                var c = new GameObject("Char_" + i).transform; c.SetParent(g, false);
                c.localPosition = new Vector3(cfg[i].x, cfg[i].y, cfg[i].z); c.localRotation = Quaternion.Euler(0, 0, cfg[i].rot); c.localScale = Vector3.one * cfg[i].s;
                foreach (var (layer, mat, off) in new[] { ("f", matF, Vector3.zero), ("b", matB, new Vector3(0, -0.012f, 0.04f)) })
                {
                    var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Src}/Title_{i}{layer}.fbx"); if (!src) { Debug.LogWarning("[Bang] 缺少标题字 " + i + layer); continue; }
                    var lg = (GameObject)PrefabUtility.InstantiatePrefab(src, c); lg.name = layer == "f" ? "Front" : "Back"; lg.transform.localPosition = off;
                    foreach (var r in lg.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                }
                var bob = c.gameObject.AddComponent<Bang.Ambient.Bob>(); bob.amplitude = 0.005f; bob.period = 3.8f + i * 0.43f; bob.swayDeg = 1.2f; bob.phase = i * 1.3f;
            }
        }

        static GameObject PartFrom(Transform parent, string fbx, string name, Vector3 pos, Material mat)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = pos;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (src) { var g = (GameObject)PrefabUtility.InstantiatePrefab(src, t); foreach (var r in g.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; } }
            return t.gameObject;
        }
    }
}
