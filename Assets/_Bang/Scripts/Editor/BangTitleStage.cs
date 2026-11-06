// 封面“玩具小剧场”预制体：把 Tools/blender_title_stage.py 生成的部件按 Title_layout.json 摆好，配材质、灯泡、印字、Logo。
// 运行时由 UiManager.ShowTitle 实例化，按名字找到：BtnStart（开始大按钮）、Block_0 / Block_1（选关 / 设置积木）、Bulbs（跑马灯）、Mascot（小老鼠绕台）。
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Bang.Play.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Bang.EditorTools
{
    public static class BangTitleStage
    {
        const string Src = "Assets/_Bang/Art/Models/Proc/UI";
        const string PrefabPath = "Assets/_Bang/Prefabs/UI/TitleStage.prefab";

        static Dictionary<string, Vector3> _lay;
        static List<Vector3> _bulbs;
        static float F(string s) => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture);

        static void LoadLayout()
        {
            _lay = new Dictionary<string, Vector3>(); _bulbs = new List<Vector3>();
            var txt = File.ReadAllText($"{Src}/Title_layout.json");
            foreach (Match m in Regex.Matches(txt, "\"(Title_[A-Za-z0-9_]+)\":\\s*\\[\\s*([-0-9.eE]+),\\s*([-0-9.eE]+),\\s*([-0-9.eE]+)\\s*\\]"))
                _lay[m.Groups[1].Value] = new Vector3(F(m.Groups[2].Value), F(m.Groups[3].Value), F(m.Groups[4].Value));
            var b = Regex.Match(txt, "\"bulbs\":\\s*\\[(.*?)\\]\\s*\\]", RegexOptions.Singleline);
            if (b.Success)
                foreach (Match m in Regex.Matches(b.Groups[1].Value + "]", "\\[\\s*([-0-9.eE]+),\\s*([-0-9.eE]+),\\s*([-0-9.eE]+)\\s*\\]"))
                    _bulbs.Add(new Vector3(F(m.Groups[1].Value), F(m.Groups[2].Value), F(m.Groups[3].Value)));
        }

        static Material VC => BangArt.Mat("M_TitleVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.55f); m.SetFloat("_GlossSize", 48); m.SetFloat("_RimStrength", 0.3f); m.SetFloat("_Ambient", 0.45f); });
        static Material Felt => BangArt.Mat("M_TitleFelt", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.02f); m.SetFloat("_Sheen", 0.65f); m.SetFloat("_RimStrength", 0.15f); m.SetFloat("_Ambient", 0.5f); });
        static Material Candy => BangArt.Mat("M_TitleCandy", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.95f); m.SetFloat("_GlossSize", 90); m.SetFloat("_RimStrength", 0.6f); m.SetFloat("_Ambient", 0.5f); });
        static Material Bulb => BangArt.Mat("M_TitleBulb", m => { m.SetColor("_BaseColor", new Color(0.82f, 0.74f, 0.66f)); m.SetFloat("_Gloss", 0.9f); m.SetColor("_EmissionColor", Color.black); m.EnableKeyword("_EMISSION"); });

        static Transform Part(Transform parent, string mesh, string name, Vector3 pos, Material mat)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = pos;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Src}/{mesh}.fbx");
            if (!src) { Debug.LogWarning("[Bang] 缺少封面部件 " + mesh); return t; }
            var g = (GameObject)PrefabUtility.InstantiatePrefab(src, t); g.transform.localPosition = Vector3.zero;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; }
            return t;
        }

        static TextMeshPro Print(Transform parent, string s, Vector3 pos, float size, Color col, float width, float spacing = 0)
        {
            var g = new GameObject("Text_" + s); g.transform.SetParent(parent, false); g.transform.localPosition = pos;
            var t = g.AddComponent<TextMeshPro>();
            t.font = BangPlaySetup.Font(); t.fontSize = size * 10f; t.color = col; t.alignment = TextAlignmentOptions.Center;
            t.characterSpacing = spacing; t.enableWordWrapping = false; t.rectTransform.sizeDelta = new Vector2(width, size * 2f);
            t.text = s;
            return t;
        }

        public static GameObject Build(GameObject logoPrefab, Material logoMat)
        {
            AssetDatabase.ImportAsset(Src, ImportAssetOptions.ImportRecursive);
            LoadLayout();
            var root = new GameObject("TitleStage");
            Vector3 L(string k) => _lay.TryGetValue(k, out var v) ? v : Vector3.zero;

            Part(root.transform, "Title_Stage", "Stage", L("Title_Stage"), VC);
            Part(root.transform, "Title_StageTop", "StageTop", L("Title_StageTop"), Felt);
            Part(root.transform, "Title_Pole", "Pole_L", L("Title_Pole_L"), VC);
            Part(root.transform, "Title_Pole", "Pole_R", L("Title_Pole_R"), VC);

            // 招牌：深莓绒布底板 + 珊瑚粉圆管框 + 一圈灯泡 + 3D Logo + 英文名
            var sign = new GameObject("Sign").transform; sign.SetParent(root.transform, false); sign.localPosition = L("Title_SignBack");
            Part(sign, "Title_SignBack", "Back", Vector3.zero, Felt);
            Part(sign, "Title_SignFrame", "Frame", Vector3.zero, Candy);
            var bulbs = new GameObject("Bulbs").transform; bulbs.SetParent(sign, false);
            var sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            foreach (var p in _bulbs)
            {
                var b = new GameObject("Bulb"); b.transform.SetParent(bulbs, false); b.transform.localPosition = p; b.transform.localScale = Vector3.one * 0.024f;
                b.AddComponent<MeshFilter>().sharedMesh = sphere;
                var r = b.AddComponent<MeshRenderer>(); r.sharedMaterial = Bulb; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (logoPrefab)
            {
                var lg = (GameObject)PrefabUtility.InstantiatePrefab(logoPrefab, sign);
                lg.name = "Logo"; lg.transform.localPosition = new Vector3(0, 0.03f, -0.03f); lg.transform.localScale = Vector3.one * 0.86f;
                foreach (var r in lg.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = logoMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            }
            Print(sign, "DREAM TOY BATTLE", new Vector3(0, -0.15f, -0.02f), 0.03f, new Color(1f, 0.92f, 0.8f), 0.9f, 22);

            // 开始：街机大按钮（底座 + 会下沉的糖果圆顶）+ 舞台前沿的小木牌
            var btn = new GameObject("BtnStart").transform; btn.SetParent(root.transform, false); btn.localPosition = L("Title_BtnBase");
            Part(btn, "Title_BtnBase", "Base", Vector3.zero, VC);
            Part(btn, "Title_BtnDome", "Dome", L("Title_BtnDome") - L("Title_BtnBase"), Candy);
            var col = btn.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0, 0.0f, -0.12f); col.size = new Vector3(0.34f, 0.22f, 0.5f);
            var plaque = Part(root.transform, "Title_Plaque", "Plaque", L("Title_Plaque"), VC);
            Print(plaque, "开始游戏", new Vector3(0, -0.002f, -0.0065f), 0.036f, new Color(0.36f, 0.2f, 0.42f), 0.28f, 8).name = "Label";

            // 选关 / 设置：两块字母积木，正面印字
            string[] labels = { "选关", "设置" };
            for (int i = 0; i < 2; i++)
            {
                var blk = Part(root.transform, "Title_Block", "Block_" + i, L("Title_Block_" + i), VC);
                blk.localRotation = Quaternion.Euler(0, i == 0 ? 14f : -14f, 0);
                var bc = blk.gameObject.AddComponent<BoxCollider>(); bc.size = Vector3.one * 0.14f;
                Print(blk, labels[i], new Vector3(0, -0.002f, -0.0625f), 0.034f, new Color(0.36f, 0.2f, 0.42f), 0.1f, 2).name = "Label";
            }

            // 舞台下一团柔和的接触影（浮在房间里也像“放着”的）
            var mascot = new GameObject("Mascot").transform; mascot.SetParent(root.transform, false); mascot.localPosition = new Vector3(0, L("Title_StageTop").y + 0.002f, L("Title_Stage").z);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var pf = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return pf;
        }
    }
}
