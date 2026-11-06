// 第 1 章小怪 / 旗子 / 道具预制体：按 Tools/blender_enemies_w1.py 导出的部件和 W1E_parts.json 的安装位置，
// 组装成带关节层级的预制体（EnemyRig 指好各零件），零件动作由 EnemyMotion 程序驱动，不用骨骼。
// 菜单：Bang/5 生成第1章小怪预制体
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Bang.Play;
using Bang.Play.Motion;
using UnityEditor;
using System.Linq;
using UnityEngine;

namespace Bang.EditorTools
{
    public static class BangEnemyPrefabs
    {
        const string Src = "Assets/_Bang/Art/Models/Proc/Enemies";
        const string Dst = "Assets/_Bang/Prefabs/Enemies";
        const string BitDst = "Assets/_Bang/Resources/ToyBits";

        static Dictionary<string, Vector3> _parts;
        static Vector3 Pos(string key)
        {
            if (_parts == null)
            {
                _parts = new Dictionary<string, Vector3>();
                foreach (var file in new[] { "W1E_parts.json", "W2E_parts.json" })
                {
                    if (!File.Exists($"{Src}/{file}")) continue;
                    var txt = File.ReadAllText($"{Src}/{file}");
                    foreach (Match m in Regex.Matches(txt, "\"([A-Za-z0-9_]+)\":\\s*\\[\\s*([-0-9.eE]+),\\s*([-0-9.eE]+),\\s*([-0-9.eE]+)\\s*\\]"))
                        _parts[m.Groups[1].Value] = new Vector3(F(m.Groups[2].Value), F(m.Groups[3].Value), F(m.Groups[4].Value));
                }
            }
            return _parts.TryGetValue(key, out var v) ? v : Vector3.zero;
        }
        static float F(string s) => float.Parse(s, CultureInfo.InvariantCulture);

        static Material Toy => BangArt.ToyVC;

        /// <summary>在 parent 下建一个枢轴（局部位置 pos），把部件网格挂上去（原点就是关节）。</summary>
        static Transform Pivot(Transform parent, string name, Vector3 pos, string mesh, Material mat = null)
        {
            var p = new GameObject(name).transform; p.SetParent(parent, false); p.localPosition = pos;
            if (mesh != null)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Src}/{mesh}.fbx");
                if (!src) { Debug.LogWarning("[Bang] 缺少部件 " + mesh); return p; }
                var g = (GameObject)PrefabUtility.InstantiatePrefab(src, p);
                g.transform.localPosition = Vector3.zero;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat ? mat : Toy; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            }
            return p;
        }

        [MenuItem("Bang/5 生成第1章小怪预制体")]
        public static void BuildAll()
        {
            _parts = null;
            foreach (var n in new[] { "Flag_Cloth_S", "Flag_Cloth_L" })                 // 旗布要做顶点动画：网格必须可读写
            {
                var imp = (ModelImporter)AssetImporter.GetAtPath($"{Src}/{n}.fbx");
                if (imp && !imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }
            }
            Directory.CreateDirectory(Dst);
            // P2 素材还没进来时自动退回程序模型（Figure 返回 null）
            Save(Figure("Enemy_Planter", "En_Planter", "Arm_Planter", 0.5f, 0.13f, 0.515f, 90f, MoveStyle.Hop, true) ?? Planter(), "Enemy_Planter");
            var brute = Figure("Enemy_Brute", "En_Brute", "Arm_Brute", 0.72f, 0.31f, 0.55f, 90f, MoveStyle.Stomp, true);
            if (brute) { var bp = brute.GetComponent<EnemyRig>().pack; bp.localPosition = new Vector3(0, 0.44f, -0.17f); bp.localScale = Vector3.one * 1.5f; }   // 胖墩背的是大旗
            Save(brute ? brute : Brute(), "Enemy_Brute");
            var m = Mouse(); if (m) Save(m, "Enemy_Mouse");
            Save(Figure("Enemy_Trooper", "En_Trooper", "Arm_Trooper", 0.52f, 0.145f, 0.555f, 90f, MoveStyle.Hop, false) ?? Trooper(), "Enemy_Trooper");
            Save(Figure("Enemy_Tin", "En_Tin", "Arm_Tin", 0.62f, 0.145f, 0.5f, 90f, MoveStyle.Slide, true, 0.15f, -0.012f) ?? TinFamily("Enemy_Tin", "Tin", 1f, true), "Enemy_Tin");
            Save(Figure("Enemy_Minion", "En_Minion", "Arm_Minion", 0.38f, 0.126f, 0.515f, 90f, MoveStyle.Slide, true) ?? TinFamily("Enemy_Minion", "Minion", 0.6f, true), "Enemy_Minion");
            Save(Figure("Enemy_Commander", "En_Commander", null, 0.92f, 0f, 0f, 90f, MoveStyle.Slide, false) ?? TinFamily("Enemy_Commander", "Cmd", 1.5f, false), "Enemy_Commander");
            Save(Figure("Enemy_Ninja", "En_Ninja", "Arm_Ninja", 0.56f, 0.215f, 0.55f, 90f, MoveStyle.Hop, true, 0.15f, 0.048f) ?? Ninja(), "Enemy_Ninja");
            Save(JackP2() ?? Jack(), "Enemy_Jack");
            Save(Whole("Enemy_Plane", "En_Plane", 0.13f, 90f, MoveStyle.Fly) ?? PaperPlane(), "Enemy_Plane");
            Save(BalloonP2() ?? BalloonBuddy(), "Enemy_Balloon");
            Save(CarP2() ?? RcCar(), "Enemy_Car");
            Save(BotP2() ?? RepairBot(), "Enemy_Bot");
            string[] bossBody = { "B_Giant", "B_Tank", "B_General", "B_BalloonKing", "B_Windup", "B_ToyboxKing" };
            float[] bossH = { 1.25f, 0.85f, 1.35f, 1.05f, 1.3f, 1.05f };
            MoveStyle[] bossMove = { MoveStyle.Stomp, MoveStyle.Drive, MoveStyle.Slide, MoveStyle.Float, MoveStyle.Slide, MoveStyle.Drive };
            for (int i = 0; i < 6; i++) { var bg = BossFig("Enemy_Boss" + (i + 1), bossBody[i], bossH[i], bossMove[i]); if (bg) Save(bg, "Enemy_Boss" + (i + 1)); }
            string[] mechBody = { "M_Mortar", "M_Gate", "M_Popper", "M_Crane", "M_Spring" };
            float[] mechH = { 0.7f, 1.0f, 0.75f, 0.95f, 0.7f };
            for (int i = 0; i < mechBody.Length; i++) { var mg = PropP2("Mech_" + mechBody[i].Substring(2), mechBody[i], mechH[i]); if (mg) Save(mg, "Mech_" + mechBody[i].Substring(2)); }
            Save(Flag("S"), "Flag_Small");
            Save(Flag("L"), "Flag_Big");
            Save(Gift(), "GiftBox");                                                // 补给礼盒改用 Blender 程序模型（Tools/blender_props_fix.py --gift-parts）：P2 版有破面，且是一整块、开盒时盒盖飞不起来
            Save(Ball(), "BlockBall");                                              // 积木球改用 Blender 程序版（blender_blockball.py）：P2 白模碎成几百块
            Save(GunPart("Gun_Shell"), "Gun_Shell");
            Save(GunPart("Gun_Mag"), "Gun_MagDrop");
            Save(Gun(), "ToyEagle");
            Save(Rifle(), "ToyRifle");
            Save(Target(), "Target_Balloon");
            Save(Dart(), "Dart_Foam");
            Save(Shield(), "ToyShield");
            Directory.CreateDirectory(BitDst);
            foreach (var n in new[] { "Bit_Gear", "Bit_Screw", "Bit_Spring", "Bit_Star" })   // 运行时 PlayFx.ToyBits 从 Resources 取
            {
                var g = new GameObject(n); Pivot(g.transform, "Mesh", Vector3.zero, n);
                PrefabUtility.SaveAsPrefabAsset(g, $"{BitDst}/{n}.prefab"); Object.DestroyImmediate(g);
            }
            const string splat = "Assets/_Bang/Resources/Splat/M_HitSplat.mat";            // 视野遮挡贴片的材质放 Resources，运行时 HitSplatView 取
            if (!AssetDatabase.LoadAssetAtPath<Material>(splat)) AssetDatabase.CreateAsset(new Material(Shader.Find("Bang/HitSplat")), splat);
            AssetDatabase.SaveAssets();
            Debug.Log("[Bang] 第 1 章小怪预制体已生成 → " + Dst);
        }

        static void Save(GameObject g, string name)
        {
            foreach (var lg in g.GetComponentsInChildren<LODGroup>(true))                       // 敌人是视觉主角：离玩家 2～3 米时还要是高模（原来 2 米外就掉到 35% 面数）
            {
                var l = lg.GetLODs(); if (l.Length < 3) continue;
                l[0].screenRelativeTransitionHeight = 0.075f; l[1].screenRelativeTransitionHeight = 0.03f; l[2].screenRelativeTransitionHeight = 0.004f;
                lg.SetLODs(l);
            }
            PrefabUtility.SaveAsPrefabAsset(g, $"{Dst}/{name}.prefab");
            Object.DestroyImmediate(g);
        }

        static (GameObject root, EnemyRig rig, Transform body) Root(string name, MoveStyle style)
        {
            var root = new GameObject(name);
            var rig = root.AddComponent<EnemyRig>(); rig.style = style;
            root.AddComponent<EnemyMotion>();
            var body = new GameObject("Body").transform; body.SetParent(root.transform, false);
            rig.body = body;
            return (root, rig, body);
        }

        static GameObject Planter()
        {
            var (root, rig, body) = Root("Enemy_Planter", MoveStyle.Hop);
            rig.footL = Pivot(body, "FootL", Pos("Planter_FootL"), "Planter_Foot");
            rig.footR = Pivot(body, "FootR", Pos("Planter_FootR"), "Planter_Foot");
            var pieces = new GameObject("Pieces").transform; pieces.SetParent(body, false);
            var list = new List<Transform>();
            for (int i = 0; i < 3; i++) list.Add(Pivot(pieces, "Piece_" + i, Pos("Planter_Brick_" + i), "Planter_Brick_" + i));
            rig.pieces = list.ToArray(); rig.pieceHeight = 0.087f;
            // Upper 的原点在最上面一块积木的顶面：头、手臂、背旗都挂在它下面，掉积木时整体往下落
            float top = Pos("Planter_Brick_2").y + 0.085f;
            rig.upper = Pivot(body, "Upper", new Vector3(0, top, 0), null);
            rig.head = Pivot(rig.upper, "Head", Pos("Planter_Head") - new Vector3(0, top, 0), "Planter_Head");
            rig.armL = Pivot(rig.upper, "ArmL", Pos("Planter_ArmL") - new Vector3(0, top, 0), "Planter_Arm");
            rig.armR = Pivot(rig.upper, "ArmR", Pos("Planter_ArmR") - new Vector3(0, top, 0), "Planter_Arm");
            rig.pack = Pivot(rig.upper, "Pack", Pos("Planter_Pack") - new Vector3(0, top, 0), "Planter_Pack");
            rig.hopLength = 0.16f; rig.hopHeight = 0.045f;
            return root;
        }

        // ───────── P2 人物：先出设计图 → 白模 → 贴图，身体 + 单独的手臂（手臂能摆、能举枪）─────────
        /// <summary>body / arm = P2 素材名；h = 总高；sx、sy = 肩关节相对身高的位置；armLen = 手臂长（肩球到手）；dx = 身体中线相对包围盒中心的偏移（比例，长着发条钥匙的模型包围盒不对称）。</summary>
        static GameObject Figure(string name, string body, string arm, float h, float sx, float sy, float yaw, MoveStyle style, bool flag, float gloss = 0.15f, float dx = 0f)
        {
            var bodyPf = P2Prop(body, h, gloss); if (!bodyPf) { Debug.LogWarning("[Bang] P2 素材还没好：" + body); return null; }
            var (root, rig, bodyT) = Root(name, style);
            rig.pieces = new Transform[0];
            var b = (GameObject)PrefabUtility.InstantiatePrefab(bodyPf, bodyT); b.name = "Figure";
            b.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            b.transform.localPosition = new Vector3(dx * h, 0, 0);
            foreach (var r in b.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (arm != null)                                            // 手臂是程序模型（Tools/blender_en_arms.py）：原点在肩关节球心，左右各一份
            {
                // 肩位按身体网格自动对齐：P2 身体两侧的肩桩不一定对称（机器人一边的肩桩露在肩球外面），左右分别量
                float ballR = ArmBallRadius(arm + "_L");
                float xl = ShoulderX(b, bodyT, sy * h, 1f, h), xr = ShoulderX(b, bodyT, sy * h, -1f, h);
                float pl = float.IsNaN(xl) ? sx * h : Mathf.Lerp(sx * h, xl - ballR * 0.6f, 0.75f);
                float pr = float.IsNaN(xr) ? -sx * h : Mathf.Lerp(-sx * h, xr + ballR * 0.6f, 0.75f);
                rig.armL = Pivot(bodyT, "ArmL", new Vector3(pl, sy * h, 0), arm + "_L");
                rig.armR = Pivot(bodyT, "ArmR", new Vector3(pr, sy * h, 0), arm + "_R");
            }
            if (flag) rig.pack = Pivot(bodyT, "Pack", new Vector3(0, sy * h - 0.04f, -0.11f * h / 0.6f), "Planter_Pack");
            return root;
        }

        /// <summary>身体在肩高附近（±3.5% 身高、前后 ±6%）最靠外的顶点 x（side = +1 左 / -1 右）；用最低一级 LOD（只有它可读）。</summary>
        static float ShoulderX(GameObject body, Transform bodyT, float y, float side, float h)
        {
            var mf = body.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m => m.name == "LOD2" || m.transform.name == "LOD2");
            if (!mf || !mf.sharedMesh || !mf.sharedMesh.isReadable) return float.NaN;
            float best = float.NaN;
            foreach (var v in mf.sharedMesh.vertices)
            {
                var w = bodyT.InverseTransformPoint(mf.transform.TransformPoint(v));
                if (Mathf.Abs(w.y - y) > 0.035f * h || Mathf.Abs(w.z) > 0.06f * h) continue;
                if (float.IsNaN(best) || w.x * side > best * side) best = w.x;
            }
            return best;
        }

        /// <summary>手臂顶上肩球的半径（原点就是球心：取原点附近一圈顶点的最大水平距离）。</summary>
        static float ArmBallRadius(string mesh)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Src}/{mesh}.fbx");
            var mf = src ? src.GetComponentInChildren<MeshFilter>() : null; if (!mf || !mf.sharedMesh) return 0.02f;
            float r = 0f; foreach (var v in mf.sharedMesh.vertices) if (Mathf.Abs(v.y) < 0.004f) r = Mathf.Max(r, new Vector2(v.x, v.z).magnitude);
            return r > 0 ? r : 0.02f;
        }

        static GameObject BossFig(string name, string body, float h, MoveStyle style)
        {
            var root = Whole(name, body, h, 90f, style); if (!root) return null;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return root;
        }

        /// <summary>P2 道具（礼盒、积木球）：整体一个模型；礼盒没有可以飞走的盖子，开箱时整个炸成彩纸。</summary>
        static GameObject PropP2(string name, string body, float h)
        {
            var pf = P2Prop(body, h, 0.15f); if (!pf) return null;
            var root = new GameObject(name);
            var g = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform); g.name = "Mesh"; g.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            if (name == "BlockBall")                                                  // P2 把积木球做成了蛋形：三个方向缩成一样大，变回圆球
            {
                var e0 = BoundsOf(g).extents; float m = Mathf.Min(e0.x, Mathf.Min(e0.y, e0.z));
                g.transform.localScale = Vector3.Scale(g.transform.localScale, new Vector3(m / e0.x, m / e0.y, m / e0.z));
            }
            var b = BoundsOf(g); g.transform.localPosition -= b.center - root.transform.position;   // 原点放到中心（飘着转、翻滚都绕中心）
            return root;
        }

        /// <summary>整块的 P2 模型（飞机、气球这类没有活动关节的）。</summary>
        static GameObject Whole(string name, string body, float h, float yaw, MoveStyle style)
        {
            var pf = P2Prop(body, h, 0.15f); if (!pf) return null;
            var (root, rig, bodyT) = Root(name, style);
            rig.pieces = new Transform[0];
            var b = (GameObject)PrefabUtility.InstantiatePrefab(pf, bodyT); b.name = "Figure";
            b.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return root;
        }

        static GameObject BalloonP2()
        {
            var root = Whole("Enemy_Balloon", "En_Balloon", 0.6f, 90f, MoveStyle.Float); if (!root) return null;
            var rig = root.GetComponent<EnemyRig>();
            rig.pack = Pivot(rig.body, "Pack", new Vector3(0, 0.07f, 0), "Planter_Pack"); rig.pack.localScale = Vector3.one * 0.55f;   // 矮旗插在吊篮里，不戳进气球
            return root;
        }

        /// <summary>遥控车：P2 车身，四个程序车轮盖在原来的轮子上，继续按实际速度转。</summary>
        static GameObject CarP2()
        {
            var root = Whole("Enemy_Car", "En_Car", 0.3f, 90f, MoveStyle.Drive); if (!root) return null;
            var rig = root.GetComponent<EnemyRig>();
            var wheels = new List<Transform>();
            for (int i = 0; i < 4; i++) wheels.Add(Pivot(rig.body, "Wheel_" + i, new Vector3((i % 2 == 0 ? 1f : -1f) * (i < 2 ? 0.106f : 0.112f), 0.049f, i < 2 ? 0.117f : -0.072f), "Car_Wheel"));   // 轮心按 P2 车身实测，前后轴不对称
            rig.feet = wheels.ToArray(); rig.hopLength = 0.1f;
            return root;
        }

        static GameObject BotP2()
        {
            var root = Figure("Enemy_Bot", "En_Bot", "Arm_Bot", 0.45f, 0.262f, 0.52f, 90f, MoveStyle.Slide, false, 0.15f, -0.02f); if (!root) return null;
            var rig = root.GetComponent<EnemyRig>(); rig.hopLength = 0.1f;
            Pivot(rig.body, "Antenna", new Vector3(0, 0.447f, 0), "Bot_Antenna");
            return root;
        }

        /// <summary>弹簧小丑盒：P2 盒身 + 深色盒口 + 能翻开的程序盖板 + 弹簧 + P2 小丑（合着时藏在盒身里）。</summary>
        static GameObject JackP2()
        {
            var clownPf = P2Prop("En_JackClown", 0.22f, 0.15f);
            if (!clownPf) return null;
            var (root, rig, body) = Root("Enemy_Jack", MoveStyle.Pop);
            rig.pieces = new Transform[0];
            const float top = 0.285f, w = 0.275f, d = 0.205f;
            Pivot(body, "Box", Vector3.zero, "Jack_BoxP");                                       // 盒身是程序模型（Tools/blender_en_arms.py）
            rig.key = Pivot(body, "Crank", new Vector3(-0.135f, 0.14f, 0), "Jack_CrankP");
            var hole = Pivot(body, "Hole", new Vector3(0, top - 0.002f, 0), "Jack_HoleP"); hole.localScale = new Vector3(w, 0.2f, d);
            rig.pack = Pivot(body, "Lid", new Vector3(0, top, -d * 0.5f), "Jack_LidP"); rig.pack.localScale = new Vector3(w, 0.2f, d);
            rig.spring = Pivot(body, "Spring", new Vector3(0, top - 0.17f, 0), "Jack_Spring");
            var pop = new GameObject("Clown").transform; pop.SetParent(body, false); pop.localPosition = new Vector3(0, 0.015f, 0);
            var c = (GameObject)PrefabUtility.InstantiatePrefab(clownPf, pop); c.name = "Figure"; c.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            rig.pop = pop;
            return root;
        }

        // ───────── 第 2～6 章小怪（Tools/blender_enemies_more.py）─────────
        static GameObject Trooper()
        {
            var (root, rig, body) = Root("Enemy_Trooper", MoveStyle.Hop);
            rig.footL = Pivot(body, "FootL", Pos("Trooper_FootL"), "Trooper_Foot");
            rig.footR = Pivot(body, "FootR", Pos("Trooper_FootR"), "Trooper_Foot");
            var pieces = new GameObject("Pieces").transform; pieces.SetParent(body, false);
            var list = new List<Transform>();
            for (int i = 0; i < 3; i++) list.Add(Pivot(pieces, "Piece_" + i, Pos("Trooper_Brick_" + i), "Trooper_Brick_" + i));
            rig.pieces = list.ToArray(); rig.pieceHeight = 0.087f;
            float top = Pos("Trooper_Brick_2").y + 0.085f;
            rig.upper = Pivot(body, "Upper", new Vector3(0, top, 0), null);
            rig.head = Pivot(rig.upper, "Head", Pos("Trooper_Head") - new Vector3(0, top, 0), "Trooper_Head");
            rig.armL = Pivot(rig.upper, "ArmL", Pos("Trooper_ArmL") - new Vector3(0, top, 0), "Trooper_Arm");
            rig.armR = Pivot(rig.upper, "ArmR", Pos("Trooper_ArmR") - new Vector3(0, top, 0), "Trooper_Arm");
            rig.hopLength = 0.16f; rig.hopHeight = 0.045f;
            return root;
        }

        /// <summary>铁皮兵家族：铁皮兵 / 跟班小锡兵 / 指挥官。背后都有发条；插旗的背小旗。</summary>
        static GameObject TinFamily(string name, string pre, float s, bool flag)
        {
            var (root, rig, body) = Root(name, MoveStyle.Slide);
            rig.footL = Pivot(body, "FootL", Pos(pre + "_FootL"), pre + "_Foot");
            rig.footR = Pivot(body, "FootR", Pos(pre + "_FootR"), pre + "_Foot");
            var pieces = new GameObject("Pieces").transform; pieces.SetParent(body, false);
            var list = new List<Transform>();
            for (int i = 0; i < 3; i++) list.Add(Pivot(pieces, "Piece_" + i, Pos($"{pre}_Ring_{i}"), $"{pre}_Ring_{i}"));
            rig.pieces = list.ToArray(); rig.pieceHeight = 0.075f * s;
            float top = Pos(pre + "_Ring_2").y + 0.075f * s;
            rig.upper = Pivot(body, "Upper", new Vector3(0, top, 0), null);
            rig.head = Pivot(rig.upper, "Head", Pos(pre + "_Head") - new Vector3(0, top, 0), pre + "_Head");
            rig.armL = Pivot(rig.upper, "ArmL", Pos(pre + "_ArmL") - new Vector3(0, top, 0), pre + "_Arm");
            rig.armR = Pivot(rig.upper, "ArmR", Pos(pre + "_ArmR") - new Vector3(0, top, 0), pre + "_Arm");
            if (flag) rig.pack = Pivot(rig.upper, "Pack", new Vector3(0, -0.06f * s, -0.095f * s), "Planter_Pack");
            rig.key = Pivot(rig.upper, "Key", new Vector3(0, -0.13f * s, -0.092f * s), "Mouse_Key");
            rig.keyBase = Quaternion.Euler(-90f, 0, 0); rig.key.localRotation = rig.keyBase; rig.key.localScale = Vector3.one * (1.1f * s);
            rig.hopLength = 0.09f * s;                                   // 底座半宽：摇摆时身体抬高的量
            return root;
        }

        static GameObject Ninja()
        {
            var (root, rig, body) = Root("Enemy_Ninja", MoveStyle.Hop);
            rig.footL = Pivot(body, "FootL", Pos("Ninja_FootL"), "Ninja_Foot");
            rig.footR = Pivot(body, "FootR", Pos("Ninja_FootR"), "Ninja_Foot");
            var pieces = new GameObject("Pieces").transform; pieces.SetParent(body, false);
            rig.pieces = new[] { Pivot(pieces, "Piece_0", Pos("Ninja_Plate_0"), "Ninja_Plate_0"), Pivot(pieces, "Piece_1", Pos("Ninja_Plate_1"), "Ninja_Plate_1") };
            rig.pieceHeight = 0.09f;
            float top = Pos("Ninja_Plate_1").y + 0.1f;
            rig.upper = Pivot(body, "Upper", new Vector3(0, top, 0), null);
            rig.head = Pivot(rig.upper, "Head", Pos("Ninja_Head") - new Vector3(0, top, 0), "Ninja_Head");
            rig.armL = Pivot(rig.upper, "ArmL", Pos("Ninja_ArmL") - new Vector3(0, top, 0), "Ninja_Arm");
            rig.armR = Pivot(rig.upper, "ArmR", Pos("Ninja_ArmR") - new Vector3(0, top, 0), "Ninja_Arm");
            rig.pack = Pivot(rig.upper, "Pack", new Vector3(0, -0.06f, -0.03f), "Planter_Pack");
            rig.hopLength = 0.2f; rig.hopHeight = 0.05f; rig.footSwing = 0.2f;
            return root;
        }

        static GameObject Jack()
        {
            var (root, rig, body) = Root("Enemy_Jack", MoveStyle.Pop);
            Pivot(body, "Box", Vector3.zero, "Jack_Box");
            rig.pack = Pivot(body, "Lid", Pos("Jack_Lid"), "Jack_Lid");
            rig.key = Pivot(body, "Crank", Pos("Jack_Crank"), "Jack_Crank");
            rig.spring = Pivot(body, "Spring", Pos("Jack_Spring"), "Jack_Spring");
            rig.pop = Pivot(body, "Clown", Pos("Jack_Clown") - Vector3.up * 0.2f, "Jack_Clown");
            return root;
        }

        static GameObject PaperPlane()
        {
            var (root, rig, body) = Root("Enemy_Plane", MoveStyle.Fly);
            Pivot(body, "Keel", Vector3.zero, "Plane_Keel");
            rig.armL = Pivot(body, "WingL", Pos("Plane_WingL"), "Plane_WingL");
            rig.armR = Pivot(body, "WingR", Pos("Plane_WingR"), "Plane_WingR");
            return root;
        }

        static GameObject BalloonBuddy()
        {
            var (root, rig, body) = Root("Enemy_Balloon", MoveStyle.Float);
            Pivot(body, "Balloon", Pos("Balloon_Body"), "Balloon_Body");
            Pivot(body, "String", Pos("Balloon_String"), "Balloon_String");
            Pivot(body, "Basket", Pos("Balloon_Basket"), "Balloon_Basket");
            rig.pack = Pivot(body, "Pack", new Vector3(0, 0.09f, -0.05f), "Planter_Pack");
            return root;
        }

        static GameObject RcCar()
        {
            var (root, rig, body) = Root("Enemy_Car", MoveStyle.Drive);
            Pivot(body, "Chassis", Vector3.zero, "Car_Body");
            var wheels = new List<Transform>();
            for (int i = 0; i < 4; i++) wheels.Add(Pivot(body, "Wheel_" + i, Pos("Car_Wheel_" + i), "Car_Wheel"));
            rig.feet = wheels.ToArray(); rig.hopLength = 0.1f;
            return root;
        }

        static GameObject RepairBot()
        {
            var (root, rig, body) = Root("Enemy_Bot", MoveStyle.Slide);
            rig.footL = Pivot(body, "FootL", Pos("Bot_TreadL"), "Bot_Tread");
            rig.footR = Pivot(body, "FootR", Pos("Bot_TreadR"), "Bot_Tread");
            var pieces = new GameObject("Pieces").transform; pieces.SetParent(body, false);
            rig.pieces = new[] { Pivot(pieces, "Piece_0", Pos("Bot_Plate_0"), "Bot_Plate_0"), Pivot(pieces, "Piece_1", Pos("Bot_Plate_1"), "Bot_Plate_1") };
            rig.pieceHeight = 0.092f;
            float top = Pos("Bot_Plate_1").y + 0.09f;
            rig.upper = Pivot(body, "Upper", new Vector3(0, top, 0), null);
            rig.head = Pivot(rig.upper, "Head", Pos("Bot_Head") - new Vector3(0, top, 0), "Bot_Head");
            rig.armL = Pivot(rig.upper, "ArmL", Pos("Bot_ArmL") - new Vector3(0, top, 0), "Bot_ArmOil");
            rig.armR = Pivot(rig.upper, "ArmR", Pos("Bot_ArmR") - new Vector3(0, top, 0), "Bot_ArmWrench");
            rig.hopLength = 0.1f;                                         // 履带底座半宽
            return root;
        }

        static GameObject Brute()
        {
            var (root, rig, body) = Root("Enemy_Brute", MoveStyle.Stomp);
            rig.footL = Pivot(body, "FootL", Pos("Brute_FootL"), "Brute_Foot");
            rig.footR = Pivot(body, "FootR", Pos("Brute_FootR"), "Brute_Foot");
            var pieces = new GameObject("Pieces").transform; pieces.SetParent(body, false);
            var list = new List<Transform>();
            for (int i = 0; i < 8; i++) list.Add(Pivot(pieces, "Piece_" + i, Pos("Brute_Brick_" + i), "Brute_Brick_" + i));
            rig.pieces = list.ToArray(); rig.pieceHeight = 0.0625f;      // 两列一层：每掉一块，上半身落半层
            float top = Pos("Brute_Brick_7").y + 0.12f;
            rig.upper = Pivot(body, "Upper", new Vector3(0, top, 0), null);
            rig.head = Pivot(rig.upper, "Head", Pos("Brute_Head") - new Vector3(0, top, 0), "Brute_Head");
            rig.armL = Pivot(rig.upper, "ArmL", Pos("Brute_ArmL") - new Vector3(0, top, 0), "Brute_Arm");
            rig.armR = Pivot(rig.upper, "ArmR", Pos("Brute_ArmR") - new Vector3(0, top, 0), "Brute_Arm");
            rig.pack = Pivot(rig.upper, "Pack", Pos("Brute_Pack") - new Vector3(0, top, 0), "Brute_Pack");
            rig.hopLength = 0.22f; rig.hopHeight = 0.04f;
            return root;
        }

        static GameObject Mouse()
        {
            var bodyPf = AssetDatabase.LoadAssetAtPath<GameObject>($"{BangArt.PropDir}/W1E_MouseBody.prefab");
            if (!bodyPf)
            {
                var imp = BangArt.ImportP2("W1E_MouseBody", 0.11f, 0.05f, 0.6f);     // 身高 11 cm（不含钥匙），绒布
                if (!imp) { Debug.LogWarning("[Bang] 老鼠身体还没生成完，跳过 Enemy_Mouse"); return null; }
                bodyPf = imp;
            }
            var (root, rig, body) = Root("Enemy_Mouse", MoveStyle.Scurry);
            var b = (GameObject)PrefabUtility.InstantiatePrefab(bodyPf, body);
            b.transform.localRotation = Quaternion.Euler(0, 180, 0);                     // Tripo 模型正面朝 -Z → 转成朝 +Z（截图核对过：90° 时头朝侧面）
            var bb = BoundsOf(b);
            b.transform.localPosition += new Vector3(-bb.center.x, 0.012f - bb.min.y, -bb.center.z);
            bb = BoundsOf(b);
            // 发条钥匙：从后背斜着往后伸出（杆向后、略上翘 20°），转动时绕杆本身转——发条玩具的真实结构（用户指出原来竖在背上像竹蜻蜓）
            float keyZ = -bb.extents.z * 0.62f, keyY = SurfaceY(b, keyZ, bb.max.y);           // 插进后背实际表面
            rig.key = Pivot(body, "Key", new Vector3(0, keyY - 0.008f, keyZ), "Mouse_Key");
            rig.keyBase = Quaternion.Euler(-70f, 0, 0);                                         // 钥匙模型的杆沿本地 +Y → 转到朝后上方
            rig.key.localRotation = rig.keyBase;
            // 四只小脚
            var feet = new List<Transform>();
            foreach (var (x, z) in new[] { (0.03f, 0.04f), (-0.03f, 0.04f), (0.03f, -0.04f), (-0.03f, -0.04f) })
                feet.Add(Pivot(body, "Foot", new Vector3(x, 0.012f, z * bb.extents.z / 0.07f), "Mouse_Foot"));
            rig.feet = feet.ToArray();
            // 尾巴：7 节毛线珠串成链，每节挂在上一节的末端，逐节缩小、略向上翘
            var tail = new List<Transform>();
            Transform parent = body; var pos = new Vector3(0, bb.min.y + bb.size.y * 0.3f, bb.min.z + 0.004f);
            for (int i = 0; i < 4; i++)                                                   // 性能：狂欢时满场老鼠，尾巴从 7 节减到 4 节（每节拉长，总长不变）
            {
                var seg = Pivot(parent, "Tail_" + i, pos, "Mouse_TailSeg");
                seg.localScale = i == 0 ? new Vector3(0.62f, 0.62f, 1.1f) : new Vector3(0.9f, 0.9f, 1.55f);                        // 尾巴细一点：第一节 0.62，之后逐节再缩 10%
                seg.localRotation = Quaternion.Euler(i < 2 ? -16f : 10f, 0, 0);
                tail.Add(seg); parent = seg; pos = new Vector3(0, 0, -0.028f * 1.55f);
            }
            rig.tail = tail.ToArray();
            rig.pieces = new Transform[0];
            AddBeadEyes(b.transform, $"{BangArt.P2Dst}/W1E_MouseBody/albedo.png");   // 用户：老鼠眼睛糊（封面上很明显）→ 换成立体珠子眼
            return root;
        }

        /// <summary>贴图里画的眼睛只占 2K 贴图很小一块，近看发糊：按贴图找出眼睛所在的两团深色三角形，在原位放上立体珠子眼
        /// （扁圆黑色亮面珠 + 一颗小白高光），任何距离都清楚。坐标直接取网格本地坐标，和身体严格对齐。</summary>
        public static void AddBeadEyes(Transform model, string albedoPath, float darkSum = 0.25f)
        {
            var mf = model.GetComponentInChildren<MeshFilter>(); if (!mf || !mf.sharedMesh) return;
            var bytes = System.IO.File.Exists(albedoPath) ? System.IO.File.ReadAllBytes(albedoPath) : null; if (bytes == null) { Debug.LogWarning("[Bang] 找不到贴图 " + albedoPath); return; }
            var tex = new Texture2D(2, 2); tex.LoadImage(bytes);
            var m = mf.sharedMesh; var v = m.vertices; var uv = m.uv; var tr = m.triangles;
            float ext = m.bounds.extents.magnitude;
            var dark = new List<(Vector3 p, Vector3 n, float a)>();
            for (int t = 0; t < tr.Length; t += 3)
            {
                var u = (uv[tr[t]] + uv[tr[t + 1]] + uv[tr[t + 2]]) / 3f;
                var c = tex.GetPixelBilinear(u.x, u.y);
                if (c.r + c.g + c.b >= darkSum) continue;
                var a = v[tr[t]]; var e1 = v[tr[t + 1]] - a; var e2 = v[tr[t + 2]] - a; var cr = Vector3.Cross(e1, e2);
                dark.Add(((a + v[tr[t + 1]] + v[tr[t + 2]]) / 3f, cr.normalized, cr.magnitude * 0.5f));
            }
            Object.DestroyImmediate(tex);
            // 按距离连通聚类，取最大的两团（眼睛）；太散的（缝线、鼻头）不算
            var cl = new List<List<(Vector3 p, Vector3 n, float a)>>();
            float link = ext * 0.08f;
            foreach (var d in dark)
            {
                var hit = cl.Where(c => c.Any(q => (q.p - d.p).sqrMagnitude < link * link)).ToList();
                if (hit.Count == 0) { cl.Add(new List<(Vector3, Vector3, float)> { d }); continue; }
                hit[0].Add(d); for (int i = 1; i < hit.Count; i++) { hit[0].AddRange(hit[i]); cl.Remove(hit[i]); }
            }
            var eyes = cl.Where(c => c.Count >= 20).OrderByDescending(c => c.Count).Take(2).ToList();
            if (eyes.Count < 2) { Debug.LogWarning($"[Bang] {model.name} 只找到 {eyes.Count} 只眼睛，跳过珠子眼"); return; }
            var bead = BangArt.Mat("M_EyeBead", mt => { mt.SetColor("_BaseColor", new Color(0.05f, 0.04f, 0.06f)); if (mt.HasProperty("_Gloss")) mt.SetFloat("_Gloss", 0.9f); if (mt.HasProperty("_Sheen")) mt.SetFloat("_Sheen", 0f); if (mt.HasProperty("_RimStrength")) mt.SetFloat("_RimStrength", 0.15f); });
            var glint = BangArt.Mat("M_EyeGlint", mt => { mt.SetColor("_BaseColor", Color.white); if (mt.HasProperty("_EmissionColor")) mt.SetColor("_EmissionColor", new Color(0.6f, 0.6f, 0.6f)); if (mt.HasProperty("_Gloss")) mt.SetFloat("_Gloss", 0.2f); });
            var sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            int k = 0;
            foreach (var c in eyes)
            {
                Vector3 cen = Vector3.zero, n = Vector3.zero; float area = 0;
                foreach (var d in c) { cen += d.p * d.a; n += d.n * d.a; area += d.a; }
                cen /= Mathf.Max(area, 1e-8f); n.Normalize();
                float r = c.Max(d => (d.p - cen).magnitude);
                var eye = new GameObject(k == 0 ? "Eye_A" : "Eye_B").transform; eye.SetParent(mf.transform, false);
                eye.localPosition = cen + n * r * 0.22f; eye.localRotation = Quaternion.LookRotation(n);
                eye.localScale = new Vector3(r * 2.2f, r * 2.2f, r * 1.1f);                       // 扁圆珠：盖住贴图里的眼睛，略微鼓出表面
                eye.gameObject.AddComponent<MeshFilter>().sharedMesh = sphere;
                var er = eye.gameObject.AddComponent<MeshRenderer>(); er.sharedMaterial = bead; er.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var g = new GameObject("Glint").transform; g.SetParent(eye, false);
                g.localPosition = new Vector3(-0.18f, 0.22f, 0.42f); g.localScale = new Vector3(0.26f, 0.26f, 0.2f);   // 左上方一颗小高光
                g.gameObject.AddComponent<MeshFilter>().sharedMesh = sphere;
                var gr = g.gameObject.AddComponent<MeshRenderer>(); gr.sharedMaterial = glint; gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                k++;
            }
        }

        static GameObject Flag(string tag)
        {
            var root = new GameObject("Flag_" + tag);
            Pivot(root.transform, "Base", Vector3.zero, "Flag_Base_" + tag);
            var cloth = Pivot(root.transform, "Cloth", Pos("Flag_Cloth_" + tag), "Flag_Cloth_" + tag, BangArt.FeltVC);
            var fl = cloth.GetComponentInChildren<MeshFilter>();
            if (fl) fl.gameObject.AddComponent<ClothWave>();
            return root;
        }

        static GameObject Gift()
        {
            var root = new GameObject("GiftBox");
            Pivot(root.transform, "Box", Vector3.zero, "GiftBox");
            Pivot(root.transform, "Lid", Pos("GiftLid"), "GiftLid");
            return root;
        }

        static GameObject Ball()
        {
            var root = new GameObject("BlockBall");
            Pivot(root.transform, "Ball", Vector3.zero, "BlockBall").localScale = Vector3.one * 1.44f;   // 模型直径 0.18 → 0.26（和之前一样大）
            return root;
        }

        // ───────── 玩具沙鹰（半透明糖果玻璃，按 Gun_parts.json 组装）─────────
        const string GunSrc = "Assets/_Bang/Art/Models/Proc/Gun";
        static Material CandyGun => BangArt.Mat("M_CandyGun", m => { m.SetColor("_Color", Color.white); m.SetFloat("_Opacity", 0.62f); m.SetFloat("_Fresnel", 0.62f); m.SetFloat("_Glow", 0.16f); m.SetFloat("_Glitter", 2.2f); m.SetFloat("_GlitterDensity", 0.1f); }, Shader.Find("Bang/CandyGun"));
        static Dictionary<string, Vector3> _gunParts;
        static Vector3 GPos(string k)
        {
            if (_gunParts == null)
            {
                _gunParts = new Dictionary<string, Vector3>();
                foreach (Match m in Regex.Matches(File.ReadAllText($"{GunSrc}/Gun_parts.json"), "\"([A-Za-z0-9_]+)\":\\s*\\[\\s*([-0-9.eE]+),\\s*([-0-9.eE]+),\\s*([-0-9.eE]+)\\s*\\]"))
                    _gunParts[m.Groups[1].Value] = new Vector3(F(m.Groups[2].Value), F(m.Groups[3].Value), F(m.Groups[4].Value));
            }
            return _gunParts.TryGetValue(k, out var v) ? v : Vector3.zero;
        }
        static Transform GunPivot(Transform parent, string name, Vector3 pos, string mesh)
        {
            var p = new GameObject(name).transform; p.SetParent(parent, false); p.localPosition = pos;
            if (mesh == null) return p;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{GunSrc}/{mesh}.fbx");
            if (!src) { Debug.LogWarning("[Bang] 缺少枪部件 " + mesh); return p; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(src, p); g.transform.localPosition = Vector3.zero;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = CandyGun; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            return p;
        }
        static GameObject GunPart(string mesh) { _gunParts = null; var root = new GameObject(mesh); GunPivot(root.transform, "Mesh", Vector3.zero, mesh); return root; }
        static GameObject Gun()
        {
            _gunParts = null;
            var root = new GameObject("ToyEagle");
            var gm = root.AddComponent<GunMotion>();
            var model = new GameObject("Model").transform; model.SetParent(root.transform, false);
            gm.model = model;
            GunPivot(model, "Frame", Vector3.zero, "Gun_Frame");
            gm.slide = GunPivot(model, "Slide", Vector3.zero, "Gun_Slide");
            gm.mag = GunPivot(model, "Mag", GPos("Gun_Mag"), "Gun_Mag");
            gm.hammer = GunPivot(model, "Hammer", GPos("Gun_Hammer"), "Gun_Hammer");
            gm.trigger = GunPivot(model, "Trigger", GPos("Gun_Trigger"), "Gun_Trigger");
            gm.muzzle = GunPivot(gm.slide, "Muzzle", GPos("Muzzle"), null);
            gm.shellPoint = GunPivot(gm.slide, "ShellPoint", GPos("Gun_Shell"), null);
            gm.shellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dst}/Gun_Shell.prefab");
            gm.magPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dst}/Gun_MagDrop.prefab");
            return root;
        }

        /// <summary>无限火力的长枪（Tools/blender_rifle.py 逐部件建模）：握把和手枪同位，枪管沿 +Z，子物体 Muzzle 是枪口。</summary>
        static GameObject Rifle()
        {
            var root = new GameObject("ToyRifle");
            var mesh = GunPivot(root.transform, "Mesh", Vector3.zero, "Gun_Rifle");
            foreach (var r in mesh.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // 材质和手枪同款糖果半透明（GunPivot 已设）
            GunPivot(root.transform, "Muzzle", new Vector3(0, 0.036f, 0.415f), null);
            return root;
        }

        static GameObject P2Prop(string name, float height, float gloss, float sheen = 0f)
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{BangArt.PropDir}/{name}.prefab");
            if (!pf) pf = BangArt.ImportP2(name, height, gloss, sheen);
            if (pf && (name.StartsWith("En_") || name.StartsWith("B_") || name.StartsWith("M_") || name.StartsWith("P_") || name == "En_Minion" || name == "En_JackBox"))
                foreach (var r in pf.GetComponentsInChildren<Renderer>(true))       // P2 贴图自带烘焙暗影：暗部抬一点、暗部色偏粉紫，和粉彩世界统一
                    if (r.sharedMaterial && r.sharedMaterial.HasProperty("_Ambient")) { r.sharedMaterial.SetFloat("_Ambient", 0.42f); r.sharedMaterial.SetColor("_ShadowColor", new Color(0.7f, 0.64f, 0.84f, 1f)); }
            if (pf && name.StartsWith("B_"))                                       // Boss：P2 模型零件互相穿插、嘴 / 箱口这类内腔有开口，单面渲染从开口看进去是透明窟窿（用户：不要有破面漏面）
                foreach (var r in pf.GetComponentsInChildren<Renderer>(true))       // 双面渲染（ToyLit 背面自动翻转法线），开口里看到的是实心的内壁
                    if (r.sharedMaterial && r.sharedMaterial.HasProperty("_Cull")) { r.sharedMaterial.SetFloat("_Cull", 0); EditorUtility.SetDirty(r.sharedMaterial); }
            return pf;
        }

        /// <summary>物体在 (x≈0, z) 处的表面最高点（世界 y），用顶点采样：取 x、z 都在 6 毫米内的顶点里最高的。</summary>
        static float SurfaceY(GameObject g, float z, float fallback)
        {
            float best = float.MinValue;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
            {
                var m = mf.sharedMesh; if (!m) continue;
                var tr = mf.transform;
                foreach (var v in m.vertices) { var w = tr.TransformPoint(v); if (Mathf.Abs(w.x) < 0.006f && Mathf.Abs(w.z - z) < 0.006f && w.y > best) best = w.y; }
                break;                                   // 只看 LOD0
            }
            return best > float.MinValue ? best : fallback;
        }

        // ───────── 教学气球靶、敌方泡沫弹（Tools/blender_props_play.py）─────────
        const string PlaySrc = "Assets/_Bang/Art/Models/Proc/Play";
        static Transform PlayPivot(Transform parent, string name, Vector3 pos, string mesh, Material mat)
        {
            var p = new GameObject(name).transform; p.SetParent(parent, false); p.localPosition = pos;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{PlaySrc}/{mesh}.fbx");
            if (!src) { Debug.LogWarning("[Bang] 缺少道具 " + mesh); return p; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(src, p); g.transform.localPosition = Vector3.zero;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            return p;
        }

        static GameObject Target()
        {
            var root = new GameObject("Target_Balloon");
            var hook = Vector3.up * 0.17f;
            var m = Regex.Match(File.ReadAllText($"{PlaySrc}/Play_parts.json"), "\"Target_Hook\":\\s*\\[\\s*([-0-9.eE]+),\\s*([-0-9.eE]+),\\s*([-0-9.eE]+)");
            if (m.Success) hook = new Vector3(F(m.Groups[1].Value), F(m.Groups[2].Value), F(m.Groups[3].Value));
            var sway = root.AddComponent<TargetSway>();
            sway.board = PlayPivot(root.transform, "Board", Vector3.zero, "Target_Board", Toy);
            sway.balloon = PlayPivot(root.transform, "Balloon", hook, "Target_Balloon", BangArt.Mat("M_BalloonVC", mm => { mm.SetFloat("_UseVertexColor", 1); mm.SetFloat("_Gloss", 0.8f); mm.SetFloat("_GlossSize", 60); mm.SetFloat("_RimStrength", 0.45f); }));
            return root;
        }

        static GameObject Dart()
        {
            var root = new GameObject("Dart_Foam");
            PlayPivot(root.transform, "Mesh", Vector3.zero, "Dart_Foam", Toy);
            return root;
        }

        // 玩具手盾（Tools/blender_shield.py）：盘面更通透（举在眼前也不挡视线），护边 / 星星 / 握带用常规糖果材质
        static GameObject Shield()
        {
            var root = new GameObject("ToyShield");
            var model = new GameObject("Model").transform; model.SetParent(root.transform, false);
            var disc = BangArt.Mat("M_CandyShield", m => { m.SetColor("_Color", Color.white); m.SetFloat("_Opacity", 0.72f); m.SetFloat("_Fresnel", 0.55f); m.SetFloat("_Glow", 0.2f); m.SetFloat("_Glitter", 1.6f); m.SetFloat("_GlitterDensity", 0.08f); }, Shader.Find("Bang/CandyGun"));
            foreach (var (n, mat) in new[] { ("Disc", disc), ("Rim", CandyGun), ("Star", CandyGun), ("Handle", CandyGun) })
            {
                var t = PlayPivot(model, n, Vector3.zero, "Shield_" + n, mat);
                foreach (var r in t.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return root;
        }

        static Bounds BoundsOf(GameObject g)
        {
            var rs = g.GetComponentsInChildren<Renderer>(); var b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
