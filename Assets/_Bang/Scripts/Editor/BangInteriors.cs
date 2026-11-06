// 室内章节的“真房间”外壳（第 5 章发条工坊、第 6 章玩具箱），不再用一张画着墙的天空画幕：
//   W5 工坊：薄荷齿轮墙纸 + 桃色木墙裙 + 奶油腰线，后墙和两侧墙上开黄铜舷窗（窗里是第 1 章的粉彩天空），
//            天花板有木梁、垂下三盏吊灯；两侧墙上钉搁板，摆满小玩具和颜料罐（从门洞斜着看进去两侧也不空）。
//   W6 玩具箱：补上后墙（夜光星星贴纸），箱盖向后打开斜靠着，箱口上方是 360° 夜空布景；
//            两侧箱壁之间横一根木杆，月亮挂铃吊在杆上（单摆 + 扭摆，代替原来凭空匀速转）。
using System.Collections.Generic;
using Bang.Ambient;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bang.EditorTools
{
    public static partial class BangSceneBuilder
    {
        // 工坊房间范围（门后世界局部坐标）
        const float WsX0 = -7.0f, WsX1 = 7.4f, WsZ = -7.2f, WsZF = 7.6f, WsH = 4.2f, WsWain = 1.1f;   // 前墙在玩家房间身后：工坊把真实房间也包进去

        static Material WallpaperMat => BangArt.Mat("M_W5_Wallpaper", m =>
        {
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Bang/Art/Textures/W5_Wallpaper.png"));
            m.SetFloat("_Gloss", 0.05f); m.SetFloat("_RimStrength", 0.05f); m.SetFloat("_ShadeSoft", 0.85f); m.SetFloat("_Ambient", 0.45f);
        });
        static Material PaintedWood(string n, Color c) => BangArt.Mat(n, m =>
        {
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Bang/Art/Textures/T_WoodGrain.png"));
            m.SetColor("_BaseColor", c); m.SetFloat("_Gloss", 0.22f); m.SetFloat("_GlossSize", 24); m.SetFloat("_RimStrength", 0.12f); m.SetFloat("_ShadeSoft", 0.7f);
        });
        static Material Brass => BangArt.Mat("M_Brass", m => { m.SetColor("_BaseColor", new Color(0.98f, 0.8f, 0.5f)); m.SetFloat("_Gloss", 0.9f); m.SetFloat("_GlossSize", 70); m.SetFloat("_RimStrength", 0.45f); });

        static void BuildWorkshopShell(Transform w, Transform props)
        {
            var shell = new GameObject("WorkshopShell").transform; shell.SetParent(w, false);
            var wainMat = PaintedWood("M_W5_Wainscot", new Color(1f, 0.82f, 0.72f));
            var railMat = PaintedWood("M_W5_Rail", new Color(1f, 0.96f, 0.88f));
            // 三面墙：(起点, 终点) 沿墙走向，法线朝房间内
            var walls = new (Vector3 a, Vector3 b)[]
            {
                (new Vector3(WsX1, 0, WsZ), new Vector3(WsX0, 0, WsZ)),        // 后墙
                (new Vector3(WsX0, 0, WsZ), new Vector3(WsX0, 0, WsZF)),       // 左墙（x 小）
                (new Vector3(WsX1, 0, WsZF), new Vector3(WsX1, 0, WsZ)),       // 右墙（x 大）
                (new Vector3(WsX0, 0, WsZF), new Vector3(WsX1, 0, WsZF)),      // 前墙（玩家身后）
            };
            // 整个工坊铺同色棋盘格（程序地砖 W5_Floor 只覆盖中间一块，压在上面）
            MeshObj("W5_FloorExt", shell, WallQuad(new Vector3(WsX0, 0, WsZ), new Vector3(WsX1, 0, WsZ), 0, 1, 1, Vector3.up, true, WsZF - WsZ, -0.006f), BangArt.Mat("M_W5_FloorExt", m =>
            {
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Bang/Art/Textures/T_W5_Checker.png"));
                m.SetTextureScale("_BaseMap", new Vector2(WsX1 - WsX0, WsZF - WsZ)); m.SetFloat("_Gloss", 0.3f); m.SetFloat("_GlossSize", 30);
            }));
            int wi = 0;
            foreach (var (a, b) in walls)
            {
                var dir = (b - a).normalized; float len = Vector3.Distance(a, b);
                var nrm = Vector3.Cross(Vector3.up, dir);   // 指向房间内（左手系：up × 沿墙方向）
                if (Vector3.Dot(nrm, new Vector3(0.3f, 0, -3.5f) - a) < 0) nrm = -nrm;
                MeshObj($"W5_Wall_{wi}", shell, WallQuad(a, b, WsWain, WsH, 1f / 1.6f, nrm), WallpaperMat);
                MeshObj($"W5_Wain_{wi}", shell, PanelStrip(a + nrm * 0.03f, b + nrm * 0.03f, 0, WsWain, 0.55f, nrm), wainMat);
                MeshObj($"W5_Rail_{wi}", shell, RailMesh(a + nrm * 0.05f, b + nrm * 0.05f, WsWain, 0.07f, 0.05f, nrm), railMat);
                MeshObj($"W5_Base_{wi}", shell, RailMesh(a + nrm * 0.05f, b + nrm * 0.05f, 0.05f, 0.1f, 0.04f, nrm), railMat);
                // 舷窗：后墙 / 前墙 3 个、侧墙 5 个（避开搁板）
                int nWin = wi == 0 || wi == 3 ? 3 : 5;
                for (int k = 0; k < nWin; k++)
                {
                    float f = (k + 1f) / (nWin + 1f);
                    // 前墙（玩家身后，wi = 3）中段有两块搁板（1.6 / 2.0 m 高，上面还摆着玩具）：舷窗和侧墙一样开高一点，不压到搁板
                    bool high = wi == 1 || wi == 2 || wi == 3;
                    Porthole(shell, a + dir * len * f + Vector3.up * (high ? 3.15f : 2.45f) + nrm * 0.02f, nrm, high ? 0.42f : 0.5f);
                }
                wi++;
            }
            // 天花板 + 木梁 + 吊灯（室内无风：吊灯静止垂直）
            var ceil = MeshObj("W5_Ceiling", shell, WallQuad(new Vector3(WsX0, 0, WsZ), new Vector3(WsX1, 0, WsZ), 0, 1, 1, Vector3.down, true, WsZF - WsZ, WsH), PaintedWood("M_W5_Ceil", new Color(1f, 0.95f, 0.9f)));
            for (float z = WsZF - 0.6f; z > WsZ; z -= 1.6f)
                MeshObj($"W5_Beam_{z:F1}", shell, BoxMesh(new Vector3((WsX0 + WsX1) / 2, WsH - 0.12f, z), new Vector3(WsX1 - WsX0, 0.24f, 0.18f), new Color(0.95f, 0.78f, 0.62f)), BangArt.WoodVC);
            foreach (var p in new[] { new Vector3(-1.6f, 1.15f, -2.6f), new Vector3(1.8f, 1.15f, -3.4f), new Vector3(0.1f, 1.15f, -5.4f), new Vector3(-1.8f, 0.9f, 2.4f), new Vector3(2.2f, 0.9f, 4.8f) })
                PendantLamp(shell, new Vector3(p.x, WsH - 0.24f, p.z), p.y);          // 房间上方的两盏短一些（不垂进真实房间）
            // 身后墙（前墙）脚下一条矮长凳，排一排发条玩具；墙正中挂一把大发条钥匙（离真实房间不到 1 m，只能放薄的）
            var benchMat = PaintedWood("M_W5_Shelf", new Color(0.98f, 0.86f, 0.76f));
            MeshObj("W5_BackBench", shell, BoxMesh(new Vector3(0.2f, 0.42f, WsZF - 0.3f), new Vector3(9.6f, 0.06f, 0.42f), Color.white), benchMat);
            foreach (var lx in new[] { -4.3f, 4.7f }) MeshObj("W5_BackBenchLeg", shell, BoxMesh(new Vector3(lx, 0.2f, WsZF - 0.3f), new Vector3(0.08f, 0.4f, 0.36f), Color.white), benchMat);
            string[] row = { "W5_WindupBear", "W5_TinyRobots", "W5_ToyRocket", "W5_WindupBunny", "W5_PaintCans", "W5_TinyRobots", "W5_WindupBear", "W6_StackRings", "W5_WindupBunny", "W5_TinyRobots", "W5_ToyRocket", "W5_WindupBear" };
            for (int i = 0; i < row.Length; i++)
            {
                var g = Place(row[i], props, new Vector3(-4.2f + i * 0.78f, 0.45f, WsZF - 0.3f), 180 + (i % 3 - 1) * 15);
                if (!g) continue;
                var b = Bounds(g); float target = row[i] == "W5_ToyRocket" ? 0.8f : 0.5f;
                if (b.size.y > 1e-3f) g.transform.localScale *= target / b.size.y;
            }
            var bigKey = Place("W5_BigKey", props, new Vector3(0.2f, 1.6f, WsZF - 0.12f), 0);
            if (bigKey)
            {
                var b = Bounds(bigKey); if (b.size.y > 1e-3f) bigKey.transform.localScale *= 1.1f / b.size.y;
                bigKey.transform.localRotation = Quaternion.Euler(-90, 0, 0) * bigKey.transform.localRotation;  // 杆朝墙（+z）、钥匙柄朝房间
                var kb = Bounds(bigKey); var kc = props.InverseTransformPoint(kb.center);
                bigKey.transform.localPosition += new Vector3(0.2f - kc.x, 1.7f - kc.y, (WsZF - 0.06f - kb.extents.z) - kc.z);   // 整个钥匙在墙面前，居中挂在 1.7 m
            }
            // 两侧墙上的搁板：每侧两层，摆小玩具
            var shelfMat = PaintedWood("M_W5_Shelf", new Color(0.98f, 0.86f, 0.76f));
            string[] toys = { "W5_PaintCans", "W5_TinyRobots", "W5_WindupBear", "W6_StackRings", "W5_WindupBunny", "W2_Bucket", "W6_YarnBall", "W5_PaintCans", "W5_TinyRobots", "W6_GummyBear" };
            int ti = 0;
            // 前墙（玩家身后）：两块搁板摆玩具（墙上另有一组啮合齿轮，见 W5 构建）
            foreach (var (x0, x1, y) in new[] { (-4.6f, -1.6f, 1.6f), (1.8f, 5.0f, 2.0f) })
            {
                MeshObj($"W5_ShelfF_{x0:F1}", shell, BoxMesh(new Vector3((x0 + x1) / 2, y, WsZF - 0.2f), new Vector3(x1 - x0, 0.05f, 0.38f), Color.white), shelfMat);
                for (float x = x0 + 0.3f; x < x1 - 0.2f; x += 0.6f)
                {
                    var g = Place(toys[ti++ % toys.Length], props, new Vector3(x, y + 0.025f, WsZF - 0.2f), 180);
                    if (g && g.name.Contains("GummyBear")) g.transform.localScale *= 1.4f;
                }
            }
            foreach (var (x, side) in new[] { (WsX0 + 0.2f, 1f), (WsX1 - 0.2f, -1f) })
                foreach (var (y, z0, z1) in new[] { (1.55f, -1.0f, -3.6f), (2.05f, -4.2f, -6.6f), (1.75f, 6.8f, 3.6f), (2.25f, 2.8f, 0.4f) })
                {
                    float zc = (z0 + z1) / 2, L = Mathf.Abs(z1 - z0);
                    MeshObj($"W5_Shelf_{x:F1}_{y:F1}", shell, BoxMesh(new Vector3(x + side * 0.0f, y, zc), new Vector3(0.38f, 0.05f, L), Color.white), shelfMat);
                    for (float z = z0 - 0.35f; z > z1 + 0.2f; z -= 0.62f)
                    {
                        var g = Place(toys[ti++ % toys.Length], props, new Vector3(x, y + 0.025f, z), side > 0 ? -90 : 90);
                        if (g && g.name.Contains("GummyBear")) g.transform.localScale *= 1.4f;
                    }
                }
        }

        static void BuildToyBoxShell(Transform w, Transform props)
        {
            var shell = new GameObject("ToyBoxShell").transform; shell.SetParent(w, false);
            var wood = PaintedWood("M_W6_BoxWood", new Color(0.98f, 0.82f, 0.62f));
            var rim = PaintedWood("M_W6_Rim", new Color(1f, 0.9f, 0.76f));
            // 四面箱壁把真实房间也围在里面（高 2.75 与房间同高，箱口敞开 → 头顶是夜空）
            const float X = 5.4f, ZB = -6.7f, ZF = 8.2f, H = 2.75f, T = 0.14f;   // 比真实房间（x ±4、z 到 6.65）宽出一圈，四周还能摆玩具
            MeshObj("W6_BackWall", shell, BoxMesh(new Vector3(0, H / 2, ZB), new Vector3(2 * X + T, H, T), Color.white), wood);
            MeshObj("W6_FrontWall", shell, BoxMesh(new Vector3(0, H / 2, ZF), new Vector3(2 * X + T, H, T), Color.white), wood);
            MeshObj("W6_WallL", shell, BoxMesh(new Vector3(-X, H / 2, (ZB + ZF) / 2), new Vector3(T, H, ZF - ZB), Color.white), wood);
            MeshObj("W6_WallR", shell, BoxMesh(new Vector3(X, H / 2, (ZB + ZF) / 2), new Vector3(T, H, ZF - ZB), Color.white), wood);
            foreach (var (c, sz) in new[] { (new Vector3(0, H, ZB), new Vector3(2 * X + 0.3f, 0.07f, 0.22f)), (new Vector3(0, H, ZF), new Vector3(2 * X + 0.3f, 0.07f, 0.22f)), (new Vector3(-X, H, (ZB + ZF) / 2), new Vector3(0.22f, 0.07f, ZF - ZB)), (new Vector3(X, H, (ZB + ZF) / 2), new Vector3(0.22f, 0.07f, ZF - ZB)) })
                MeshObj("W6_Rim", shell, BoxMesh(c, sz, Color.white), rim);
            // 地板：粉彩木地板铺满整个箱底
            MeshObj("W6_FloorAll", shell, WallQuad(new Vector3(-X, 0, ZB), new Vector3(X, 0, ZB), 0, 1, 1, Vector3.up, true, ZF - ZB, -0.003f), BangArt.Mat("M_W6_FloorAll", m =>
            {
                m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Bang/Art/Textures/Playroom_Floor.png"));
                m.SetTextureScale("_BaseMap", new Vector2(2 * X / 3.2f, (ZF - ZB) / 3.2f)); m.SetColor("_BaseColor", new Color(0.97f, 0.9f, 0.86f)); m.SetFloat("_Gloss", 0.25f);
            }));
            // 后墙上的夜光星星贴纸
            var rnd = new System.Random(66);
            var starMat = BangArt.Mat("M_W6_StarDecal", m => { m.SetFloat("_UseVertexColor", 1); m.SetColor("_EmissionColor", new Color(0.5f, 0.55f, 0.42f)); m.SetFloat("_Gloss", 0.2f); });
            var verts = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            Color[] sc = { new(0.85f, 1f, 0.8f), new(1f, 0.97f, 0.7f), new(1f, 0.85f, 0.93f), new(0.8f, 0.9f, 1f) };
            for (int i = 0; i < 90; i++)
            {
                int wall = i % 4; float u = (float)rnd.NextDouble(), yy = 0.4f + (float)rnd.NextDouble() * 2.1f;
                var c = wall switch
                {
                    0 => new Vector3(-X + 0.3f + u * (2 * X - 0.6f), yy, ZB + T / 2 + 0.002f),
                    1 => new Vector3(-X + 0.3f + u * (2 * X - 0.6f), yy, ZF - T / 2 - 0.002f),
                    2 => new Vector3(-X + T / 2 + 0.002f, yy, ZB + 0.3f + u * (ZF - ZB - 0.6f)),
                    _ => new Vector3(X - T / 2 - 0.002f, yy, ZB + 0.3f + u * (ZF - ZB - 0.6f)),
                };
                var wn = wall switch { 0 => Vector3.forward, 1 => Vector3.back, 2 => Vector3.right, _ => Vector3.left };
                var wu = Vector3.up; var wr = Vector3.Cross(wn, wu);
                float r = 0.06f + (float)rnd.NextDouble() * 0.09f, rot = (float)rnd.NextDouble() * 6.28f;
                int b0 = verts.Count; verts.Add(c + wn * 0.004f); cols.Add(sc[i % 4]);
                for (int k = 0; k < 10; k++)
                {
                    float a = rot + k / 10f * Mathf.PI * 2, rr = k % 2 == 0 ? r : r * 0.45f;
                    verts.Add(c + wr * Mathf.Cos(a) * rr + wu * Mathf.Sin(a) * rr + wn * 0.004f); cols.Add(sc[i % 4]);
                }
                // 单面：绕序让法线朝房间里（双面共用顶点时法线会互相抵消 → 发黑）
                var e1 = verts[b0 + 1] - verts[b0]; var e2 = verts[b0 + 2] - verts[b0];
                bool flip = Vector3.Dot(Vector3.Cross(e1, e2), wn) < 0;
                for (int k = 0; k < 10; k++) { int a1 = b0 + 1 + k, a2 = b0 + 1 + (k + 1) % 10; tris.AddRange(flip ? new[] { b0, a2, a1 } : new[] { b0, a1, a2 }); }
            }
            var sm = new Mesh { name = "W6_StarDecals" }; sm.SetVertices(verts); sm.SetColors(cols); sm.SetTriangles(tris, 0); sm.RecalculateNormals();
            MeshObj("W6_StarDecals", shell, sm, starMat);
            // 箱盖：左右两扇对开，铰链在两侧箱壁顶，向外翻开约 150°（斜躺在箱外，挡不住头顶的夜空）
            foreach (var side in new[] { -1f, 1f })
            {
                var lid = new GameObject("W6_Lid").transform; lid.SetParent(shell, false);
                lid.localPosition = new Vector3(side * (X + 0.08f), H + 0.04f, (ZB + ZF) / 2); lid.localRotation = Quaternion.Euler(0, 0, side * -30f);
                MeshObj("W6_LidBoard", lid, BoxMesh(new Vector3(side * X / 2, 0, 0), new Vector3(X, 0.14f, ZF - ZB + 0.3f), Color.white), wood);
                MeshObj("W6_LidTrim", lid, BoxMesh(new Vector3(side * X / 2, 0.09f, 0), new Vector3(X - 0.7f, 0.04f, ZF - ZB - 0.6f), Color.white), PaintedWood("M_W6_LidTrim", new Color(0.8f, 0.78f, 1f)));
            }
            // 四面箱壁内侧一圈搁板（高 1.55）：摆字母积木和小玩具，箱壁不再空
            var shelfMat = PaintedWood("M_W6_Shelf", new Color(0.85f, 0.8f, 1f));
            string[] blocks = { "W6_Block_T", "W6_Block_O", "W6_Block_Y", "W6_Block_B", "W6_Block_O", "W6_Block_X" };
            string[] small = { "W6_StackRings", "W6_GummyBear", "W6_YarnBall", "W6_WrappedCandy", "W5_WindupBear", "W6_GummyBear", "W4_RubberDuck", "W5_TinyRobots" };
            int bi = 0, si = 0;
            foreach (var (a, b, nrm) in new[] { (new Vector3(-X, 0, ZB), new Vector3(X, 0, ZB), Vector3.forward), (new Vector3(X, 0, ZF), new Vector3(-X, 0, ZF), Vector3.back),
                                                (new Vector3(-X, 0, ZF), new Vector3(-X, 0, ZB), Vector3.right), (new Vector3(X, 0, ZB), new Vector3(X, 0, ZF), Vector3.left) })
            {
                var dir = (b - a).normalized; float len = Vector3.Distance(a, b);
                var c = (a + b) / 2 + nrm * (T / 2 + 0.17f) + Vector3.up * 1.55f;
                var size = new Vector3(Mathf.Abs(dir.x) * (len - 0.5f) + Mathf.Abs(nrm.x) * 0.34f, 0.05f, Mathf.Abs(dir.z) * (len - 0.5f) + Mathf.Abs(nrm.z) * 0.34f);
                MeshObj("W6_Shelf", shell, BoxMesh(c, size, Color.white), shelfMat);
                float yaw = Quaternion.LookRotation(nrm).eulerAngles.y;
                for (float t = 0.45f; t < len - 0.45f; t += 0.42f)
                {
                    var p = a + dir * t + nrm * (T / 2 + 0.17f) + Vector3.up * 1.575f;
                    bool letter = ((int)(t / 0.42f)) % 3 != 2;
                    if (letter)
                    {
                        var bl = Inst($"{Proc}/{blocks[bi++ % blocks.Length]}.fbx", props, BangArt.BlockWood);
                        if (bl) { bl.transform.localPosition = p + Vector3.up * 0.18f * 0.6f; bl.transform.localRotation = Quaternion.Euler(0, yaw + ((bi % 3) - 1) * 8f, 0); bl.transform.localScale = Vector3.one * 0.6f; }
                    }
                    else
                    {
                        var g = Place(small[si++ % small.Length], props, p, yaw - 90f);
                        if (g && (g.name.Contains("GummyBear") || g.name.Contains("WrappedCandy"))) g.transform.localScale *= 1.8f;
                    }
                }
            }
            // 两侧箱壁前：几捆立着的大蜡笔（略歪）+ 字母积木塔，从房间往左右看有高度、有体量
            var crayonMat = BangArt.Mat("M_CrayonVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.25f); m.SetFloat("_GlossSize", 18); });
            string[] cc = { "Yellow", "Blue", "Pink", "Mint" };
            int cn = 0;
            foreach (var sx in new[] { -1f, 1f })
                foreach (var (z, kind) in new[] { (-4.6f, 0), (-1.2f, 1), (2.4f, 0), (5.6f, 1), (7.4f, 0) })
                {
                    float x = sx * (X - 0.55f);
                    if (kind == 0)
                        for (int i = 0; i < 4; i++)
                        {
                            var g = Inst($"{Proc}/W6_Crayon_{cc[cn++ % 4]}.fbx", props, crayonMat);
                            if (!g) continue;
                            g.transform.localPosition = new Vector3(x + (i % 2) * 0.2f * -sx, 0, z + (i - 1.5f) * 0.18f);
                            g.transform.localRotation = Quaternion.Euler((i - 1.5f) * 3f, i * 41f, sx * (4f + i));
                            g.transform.localScale = Vector3.one * 1.05f;
                        }
                    else
                    {
                        string[] abc = { "W2_Block_A", "W2_Block_B", "W2_Block_C" };
                        var tower = new GameObject($"W6_BlockTower_{cn:00}").transform; tower.SetParent(props, false);
                        for (int lv = 0; lv < 3; lv++)
                        {
                            var b = Inst($"{Proc}/{abc[(lv + cn) % 3]}.fbx", tower, BangArt.BlockWood);
                            if (!b) continue;
                            b.transform.localPosition = new Vector3(x - sx * 0.05f, 0.18f + lv * 0.36f, z);
                            b.transform.localRotation = Quaternion.Euler(0, lv * 17f - 10f, 0);
                        }
                        cn++;
                    }
                }
            // 箱口上方横拉两串彩旗（从左壁顶到右壁顶）
            foreach (var z in new[] { 1.2f, 5.0f })
                BangFairyLights.Bunting("W6_TopBunting", shell, new[] { new Vector3(-X, H + 0.05f, z), new Vector3(0, H + 0.05f, z + 0.3f), new Vector3(X, H + 0.05f, z) }, 0.35f, 0.16f, 0.14f);
            // 横杆 + 月亮挂铃
            MeshObj("W6_Rod", shell, RodMesh(new Vector3(-X, 2.68f, -3.4f), new Vector3(X, 2.68f, -3.4f), 0.035f, new Color(0.96f, 0.8f, 0.62f)), BangArt.WoodVC);
            var pivot = new GameObject("MoonMobile").transform; pivot.SetParent(props, false); pivot.localPosition = new Vector3(0.9f, 2.65f, -3.4f);
            var pd = pivot.gameObject.AddComponent<Pendulum>(); pd.length = 0.9f; pd.amplitudeDeg = 2f; pd.twistDeg = 28f;     // 挂铃主要是扭摆
            BangFairyLights.Thread("Mobile_Thread", pivot, Vector3.zero, new Vector3(0, -0.32f, 0), 0.003f);
            MeshObj("Mobile_Arms", pivot, RodMesh(new Vector3(-0.5f, -0.33f, 0), new Vector3(0.5f, -0.33f, 0), 0.012f, new Color(1f, 0.9f, 0.75f)), BangArt.WoodVC);
            MeshObj("Mobile_Arms2", pivot, RodMesh(new Vector3(0, -0.34f, -0.5f), new Vector3(0, -0.34f, 0.5f), 0.012f, new Color(1f, 0.9f, 0.75f)), BangArt.WoodVC);
            void Charm(string n, Vector3 at, float drop, float s)
            {
                var g = Place(n, pivot, Vector3.zero, Random.Range(-40f, 40f));
                if (!g) return;
                g.transform.localScale *= s;
                var b = Bounds(g);
                var topW = pivot.TransformPoint(at + Vector3.down * drop);
                g.transform.position += topW - new Vector3(b.center.x, b.max.y, b.center.z);
                BangFairyLights.Thread(n + "_Str", pivot, at, at + Vector3.down * drop, 0.002f);
            }
            Charm("W3_SleepyMoon", new Vector3(0, -0.34f, 0), 0.22f, 0.5f);
            Charm("W3_FeltStar", new Vector3(0.48f, -0.33f, 0), 0.3f, 0.45f);
            Charm("W3_FeltStar", new Vector3(-0.48f, -0.33f, 0), 0.42f, 0.45f);
            Charm("W3_FeltStar", new Vector3(0, -0.34f, 0.48f), 0.36f, 0.4f);
            Charm("W3_FeltStar", new Vector3(0, -0.34f, -0.48f), 0.48f, 0.4f);
        }

        // ───────── 小网格工具 ─────────
        /// <summary>竖直墙面（a→b 沿墙，高度 y0..y1），平面 UV（米 × uvPerM）；ceiling=true 时改成水平天花板（宽 a→b、深 depth、高 y1）。</summary>
        static Mesh WallQuad(Vector3 a, Vector3 b, float y0, float y1, float uvPerM, Vector3 nrm, bool ceiling = false, float depth = 0, float ceilY = 0)
        {
            Vector3[] v; Vector2[] uv;
            if (!ceiling)
            {
                float L = Vector3.Distance(a, b);
                v = new[] { a + Vector3.up * y0, b + Vector3.up * y0, b + Vector3.up * y1, a + Vector3.up * y1 };
                uv = new[] { new Vector2(0, y0), new Vector2(L, y0), new Vector2(L, y1), new Vector2(0, y1) };
                for (int i = 0; i < 4; i++) uv[i] *= uvPerM;
            }
            else
            {
                var d = Vector3.forward * depth;
                v = new[] { a + Vector3.up * ceilY, b + Vector3.up * ceilY, b + d + Vector3.up * ceilY, a + d + Vector3.up * ceilY };
                uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            }
            var m = new Mesh { name = "Quad", vertices = v, uv = uv, triangles = new[] { 0, 1, 2, 0, 2, 3 } };
            m.RecalculateNormals();
            if (Vector3.Dot(m.normals[0], nrm) < 0) { m.triangles = new[] { 0, 2, 1, 0, 3, 2 }; m.RecalculateNormals(); }
            m.RecalculateBounds();
            return m;
        }

        /// <summary>墙裙：一排竖向木板（每块 panelW 宽，略带凹凸的板缝），沿 a→b，高度 y0..y1。</summary>
        static Mesh PanelStrip(Vector3 a, Vector3 b, float y0, float y1, float panelW, Vector3 nrm)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            var dir = (b - a).normalized; float L = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(L / panelW)); float pw = L / n;
            // 每个四边形按它该朝的方向定绕序（原来侧边绕序写死、最后又整体翻一次 → 一半板缝侧面朝里，头显里看到缺面）
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector2 u0, Vector2 u1, Vector2 u2, Vector2 u3, Vector3 want)
            {
                int k = v.Count; v.AddRange(new[] { p0, p1, p2, p3 }); uv.AddRange(new[] { u0, u1, u2, u3 });
                if (Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), want) >= 0) t.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
                else t.AddRange(new[] { k, k + 2, k + 1, k, k + 3, k + 2 });
            }
            for (int i = 0; i < n; i++)
            {
                var p0 = a + dir * (i * pw + 0.012f); var p1 = a + dir * ((i + 1) * pw - 0.012f);
                var o = nrm * 0.018f;
                // 每块板：正面 + 两条侧边（板缝）
                Quad(p0 + o + Vector3.up * y0, p1 + o + Vector3.up * y0, p1 + o + Vector3.up * y1, p0 + o + Vector3.up * y1,
                     new Vector2(i * 0.37f, 0), new Vector2(i * 0.37f + 0.3f, 0), new Vector2(i * 0.37f + 0.3f, 1), new Vector2(i * 0.37f, 1), nrm);
                Quad(p0 + Vector3.up * y0, p0 + o + Vector3.up * y0, p0 + o + Vector3.up * y1, p0 + Vector3.up * y1, Vector2.zero, Vector2.zero, Vector2.up, Vector2.up, -dir);
                Quad(p1 + Vector3.up * y0, p1 + o + Vector3.up * y0, p1 + o + Vector3.up * y1, p1 + Vector3.up * y1, Vector2.zero, Vector2.zero, Vector2.up, Vector2.up, dir);
            }
            // 板背后的底板（板缝里看到的深色）
            Quad(a + Vector3.up * y0, b + Vector3.up * y0, b + Vector3.up * y1, a + Vector3.up * y1, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, nrm);
            var m = new Mesh { name = "Panels" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>腰线 / 踢脚线：沿 a→b、高 y 处的圆角横条（高 h、凸出 d）。</summary>
        static Mesh RailMesh(Vector3 a, Vector3 b, float y, float h, float d, Vector3 nrm)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            var prof = new List<Vector2>();                                      // (凸出, 高度) 截面：四分之一圆角
            for (int i = 0; i <= 6; i++) { float k = i / 6f * Mathf.PI / 2; prof.Add(new Vector2(Mathf.Sin(k) * d, -h / 2 + (1 - Mathf.Cos(k)) * h * 0.25f)); }
            for (int i = 0; i <= 6; i++) { float k = i / 6f * Mathf.PI / 2; prof.Add(new Vector2(Mathf.Cos(k) * d, h / 4 + Mathf.Sin(k) * h * 0.25f)); }
            prof.Insert(0, new Vector2(0, -h / 2)); prof.Add(new Vector2(0, h / 2));
            foreach (var e in new[] { a, b })
                foreach (var p in prof) v.Add(e + nrm * p.x + Vector3.up * (y + p.y));
            int n = prof.Count;
            for (int i = 0; i < n - 1; i++) t.AddRange(new[] { i, i + 1, n + i + 1, i, n + i + 1, n + i });
            var m = new Mesh { name = "Rail" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals();
            if (Vector3.Dot(m.normals[n / 2], nrm) < 0) { var tt = m.triangles; for (int i = 0; i < tt.Length; i += 3) (tt[i + 1], tt[i + 2]) = (tt[i + 2], tt[i + 1]); m.triangles = tt; m.RecalculateNormals(); }
            m.RecalculateBounds();
            return m;
        }

        /// <summary>倒角小盒（木梁、搁板、箱壁）：顶点色 col。</summary>
        static Mesh BoxMesh(Vector3 c, Vector3 s, Color col)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var src = g.GetComponent<MeshFilter>().sharedMesh;
            var v = src.vertices; for (int i = 0; i < v.Length; i++) v[i] = c + Vector3.Scale(v[i], s);
            var uv = src.uv; for (int i = 0; i < uv.Length; i++) uv[i] = Vector2.Scale(uv[i], new Vector2(Mathf.Max(s.x, s.z), s.y) * 0.8f);
            var m = new Mesh { name = "Box", vertices = v, uv = uv, triangles = src.triangles, normals = src.normals };
            var cc = new Color[v.Length]; for (int i = 0; i < cc.Length; i++) cc[i] = col; m.colors = cc;
            m.RecalculateBounds();
            Object.DestroyImmediate(g);
            return m;
        }

        /// <summary>圆杆（横杆、挂铃支臂）。</summary>
        static Mesh RodMesh(Vector3 a, Vector3 b, float r, Color col, int seg = 12)
        {
            var v = new List<Vector3>(); var t = new List<int>(); var c = new List<Color>();
            var ax = (b - a).normalized; var u = Vector3.Cross(ax, Mathf.Abs(ax.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var w2 = Vector3.Cross(ax, u);
            for (int e = 0; e < 2; e++)
                for (int i = 0; i < seg; i++) { float k = i / (float)seg * Mathf.PI * 2; v.Add((e == 0 ? a : b) + (u * Mathf.Cos(k) + w2 * Mathf.Sin(k)) * r); c.Add(col); }
            for (int i = 0; i < seg; i++) { int j = (i + 1) % seg; t.AddRange(new[] { i, j, seg + i, j, seg + j, seg + i }); }   // 侧面朝外（原来绕序反了，法线朝里 → 头显里看着像破面）
            int ca = v.Count; v.Add(a); c.Add(col); int cb = v.Count; v.Add(b); c.Add(col);
            for (int i = 0; i < seg; i++) { int j = (i + 1) % seg; t.AddRange(new[] { ca, j, i }); t.AddRange(new[] { cb, seg + i, seg + j }); }
            var m = new Mesh { name = "Rod" }; m.SetVertices(v); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>黄铜舷窗：圆环（环管截面）+ 十字窗棂 + 一圈铆钉 + 窗里一片粉彩天空。</summary>
        static void Porthole(Transform parent, Vector3 center, Vector3 nrm, float r)
        {
            var root = new GameObject("Porthole").transform; root.SetParent(parent, false);
            root.localPosition = center; root.localRotation = Quaternion.LookRotation(nrm);
            // 窗里的天空（略凹进墙）
            var sky = MeshObj("Porthole_Sky", root, Disc(r * 0.92f, 40, 1f / (r * 2f), false), BangArt.Mat("M_PortholeSky", m =>
            {
                m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Bang/Art/Textures/Cyclo_W1.png")); m.SetFloat("_TexAmount", 1); m.SetFloat("_Cull", 0);
                m.SetTextureScale("_MainTex", new Vector2(0.18f, 0.7f)); m.SetTextureOffset("_MainTex", new Vector2(0.4f, 0.65f)); m.SetFloat("_Exposure", 1.05f);
            }, Shader.Find("Bang/PortalSky")));
            sky.transform.localRotation = Quaternion.Euler(-90, 0, 0); sky.transform.localPosition = new Vector3(0, 0, -0.01f);
            sky.GetComponent<MeshRenderer>().sharedMaterial.renderQueue = 2001;      // 窗里天空在墙之后画（墙不写进窗洞，靠先后顺序盖住）
            var ring = new List<Vector3>(); var tris = new List<int>(); int seg = 48, sides = 10; float tr = r * 0.13f;
            for (int i = 0; i < seg; i++)
                for (int k = 0; k < sides; k++)
                {
                    float a = i / (float)seg * Mathf.PI * 2, b = k / (float)sides * Mathf.PI * 2;
                    var cdir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                    ring.Add(cdir * (r + tr * Mathf.Cos(b)) + Vector3.forward * (tr * 0.8f * Mathf.Sin(b) + tr * 0.5f));
                }
            for (int i = 0; i < seg; i++) for (int k = 0; k < sides; k++)
                {
                    int a0 = i * sides + k, a1 = i * sides + (k + 1) % sides, b0 = ((i + 1) % seg) * sides + k, b1 = ((i + 1) % seg) * sides + (k + 1) % sides;
                    tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
            var rm = new Mesh { name = "PortholeRing" }; rm.SetVertices(ring); rm.SetTriangles(tris, 0); rm.RecalculateNormals(); rm.RecalculateBounds();
            MeshObj("Porthole_Ring", root, rm, Brass);
            MeshObj("Porthole_BarH", root, RodMesh(new Vector3(-r, 0, 0.02f), new Vector3(r, 0, 0.02f), 0.018f, Color.white), Brass);
            MeshObj("Porthole_BarV", root, RodMesh(new Vector3(0, -r, 0.02f), new Vector3(0, r, 0.02f), 0.018f, Color.white), Brass);
        }

        /// <summary>吊灯：吊线 + 奶油色灯罩（车削）+ 发光灯泡。</summary>
        static void PendantLamp(Transform parent, Vector3 top, float drop)
        {
            var root = new GameObject("PendantLamp").transform; root.SetParent(parent, false); root.localPosition = top;
            BangFairyLights.Thread("Lamp_Cord", root, Vector3.zero, Vector3.down * drop, 0.006f);
            var prof = new[] { new Vector2(0.03f, 0), new Vector2(0.06f, -0.05f), new Vector2(0.2f, -0.2f), new Vector2(0.26f, -0.28f), new Vector2(0.255f, -0.29f), new Vector2(0.19f, -0.21f), new Vector2(0.05f, -0.06f), new Vector2(0.025f, -0.01f) };
            var v = new List<Vector3>(); var t = new List<int>(); int seg = 32;
            foreach (var p in prof) for (int i = 0; i < seg; i++) { float a = i / (float)seg * Mathf.PI * 2; v.Add(new Vector3(Mathf.Cos(a) * p.x, p.y, Mathf.Sin(a) * p.x)); }
            for (int r = 0; r < prof.Length - 1; r++) for (int i = 0; i < seg; i++)
                {
                    int a0 = r * seg + i, a1 = r * seg + (i + 1) % seg, b0 = a0 + seg, b1 = a1 + seg;
                    t.AddRange(new[] { a0, a1, b0, a1, b1, b0 });
                }
            var m = new Mesh { name = "LampShade" }; m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            var shade = MeshObj("Lamp_Shade", root, m, BangArt.Mat("M_LampShade", mm => { mm.SetColor("_BaseColor", new Color(1f, 0.86f, 0.8f)); mm.SetFloat("_Gloss", 0.6f); mm.SetFloat("_RimStrength", 0.4f); }));
            shade.transform.localPosition = Vector3.down * drop;
            var bulb = MeshObj("Lamp_Bulb", root, RodMesh(new Vector3(0, -0.1f, 0), new Vector3(0, -0.2f, 0), 0.05f, new Color(1f, 0.95f, 0.8f), 16), BangArt.Mat("M_LampBulb", mm => { mm.SetFloat("_UseVertexColor", 1); mm.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.6f)); mm.SetFloat("_Ambient", 0.8f); }));
            bulb.transform.localPosition = Vector3.down * drop;
        }
    }
}
