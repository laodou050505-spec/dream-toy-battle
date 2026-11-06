// 美术资源接入：材质库、FBX 导入设置、P2 模型 → 带 LOD 的预制体。
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bang.EditorTools
{
    public class BangModelPostprocessor : AssetPostprocessor
    {
        /// <summary>门框主体：开门时要逐帧改顶点（从一侧长到另一侧，见 PortalOpen），必须可读。</summary>
        static readonly string[] DoorFrames = { "/Proc/Portal_Bricks", "/Proc/W3_FeltArch", "/Proc/W4_BalloonArch", "/Proc/W5_GearFrame", "/Proc/W6_CandyArch" };

        /// <summary>封面上要描边的模型（只有最高一级 LOD）：这里不列进去，导入时会被改回不可读，描边算不了平滑法线。</summary>
        static readonly string[] CoverOutlined = { "/Proc/Gun/", "/Proc/Play/", "/Proc/UI/Th_", "/P2/W5_WindupBear/lod0", "/P2/W5_WindupBunny/lod0", "/P2/W4_RockingHorse/lod0", "/P2/W6_StackRings/lod0", "/P2/En_Planter/lod0" };

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/_Bang/Art/Models")) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;   // 材质统一由 BangArt 分配
            mi.importAnimation = false;
            mi.importCameras = false;
            mi.importLights = false;
            // 旗布要做顶点动画；小怪零件和封面描边的物件要在运行时算平滑法线（体积很小）；其余不留 CPU 副本省内存
            mi.isReadable = assetPath.Contains("Flag_Cloth") || assetPath.Contains("/Proc/Enemies/") || assetPath.Contains("W1E_MouseBody")
                || System.Array.Exists(CoverOutlined, assetPath.Contains) || System.Array.Exists(DoorFrames, assetPath.Contains) || ((assetPath.Contains("/P2/En_") || assetPath.Contains("/P2/B_") || assetPath.Contains("/P2/En_Minion") || assetPath.Contains("/P2/M_") || assetPath.Contains("/P2/P_")) && assetPath.Contains("/lod2"));   // 敌人描边外壳用最低一级 LOD，只有它要读顶点
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.importNormals = ModelImporterNormals.Import;
            mi.globalScale = 1f;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Bang/Art")) return;
            var ti = (TextureImporter)assetImporter;
            if (ti.maxTextureSize > 2048) ti.maxTextureSize = 2048;   // 用户要求：贴图不超过 2K（更小的上限保留，比如 UI 图标 256）
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            var a = ti.GetPlatformTextureSettings("Android");
            bool ui = assetPath.StartsWith("Assets/_Bang/Art/UI") || assetPath.Contains("/FX/");
            a.overridden = true; a.maxTextureSize = Mathf.Min(2048, ti.maxTextureSize); a.format = ui ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;   // UI 图标、字样要清楚
            ti.SetPlatformTextureSettings(a);
        }
    }

    public static class BangArt
    {
        public const string MatDir = "Assets/_Bang/Art/Materials";
        public const string P2Src = "ArtSource/ModelsP2";
        public const string P2Dst = "Assets/_Bang/Art/Models/P2";
        public const string PropDir = "Assets/_Bang/Prefabs/Props";

        static Shader Toy => Shader.Find("Bang/ToyLit");

        public static Material Mat(string name, System.Action<Material> setup, Shader shader = null)
        {
            Directory.CreateDirectory(MatDir);
            var path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader ? shader : Toy); AssetDatabase.CreateAsset(m, path); }
            else if (shader && m.shader != shader) m.shader = shader;
            setup?.Invoke(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // 常用材质
        public static Material ToyVC => Mat("M_ToyVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.55f); m.SetFloat("_GlossSize", 40); m.SetFloat("_RimStrength", 0.35f); });
        public static Material FeltVC => Mat("M_FeltVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.03f); m.SetFloat("_Sheen", 0.5f); m.SetFloat("_RimStrength", 0.06f); m.SetFloat("_ShadeSoft", 0.8f); });
        public static Material FeltGroundVC => Mat("M_FeltGroundVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Bang/Art/Textures/T_FeltGrain.png")); m.SetFloat("_Gloss", 0.02f); m.SetFloat("_Sheen", 0.55f); m.SetFloat("_RimStrength", 0.06f); m.SetFloat("_ShadeSoft", 0.8f); });
        /// <summary>草地：绒草贴图（Tools/make_turf_texture.py，短弯小草叶 + 柔和斑块，无斜条纹）乘顶点色。绒布纤维贴图留给绒布门框之类。</summary>
        public static Material TurfGroundVC => Mat("M_TurfGroundVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Bang/Art/Textures/T_TurfGrain.png")); m.SetFloat("_Gloss", 0.02f); m.SetFloat("_Sheen", 0.5f); m.SetFloat("_RimStrength", 0.06f); m.SetFloat("_ShadeSoft", 0.8f); });
        public static Material BlockWood => Mat("M_BlockWood", m => { m.SetFloat("_UseVertexColor", 1); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Bang/Art/Textures/T_WoodGrain.png")); m.SetFloat("_Gloss", 0.3f); m.SetFloat("_GlossSize", 24); m.SetFloat("_RimStrength", 0.15f); });
        public static Material SandVC => Mat("M_SandVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Bang/Art/Textures/T_SandGrain.png")); m.SetFloat("_Gloss", 0.04f); m.SetFloat("_Sheen", 0.3f); m.SetFloat("_RimStrength", 0.1f); m.SetFloat("_ShadeSoft", 0.75f); });
        public static Material WoodVC => Mat("M_WoodVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.18f); m.SetFloat("_GlossSize", 20); m.SetFloat("_RimStrength", 0.25f); });
        public static Material RainbowVC => Mat("M_RainbowVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.5f); m.SetFloat("_Ambient", 1.1f); m.SetFloat("_RimStrength", 0.6f); });
        public static Material PortalRim => Mat("M_PortalRim", m => { m.SetFloat("_UseVertexColor", 1); m.SetColor("_EmissionColor", new Color(0.55f, 0.42f, 0.3f)); m.SetFloat("_Gloss", 0.6f); m.SetFloat("_Ambient", 0.6f); });
        public static Material PortalMask(int r) => Mat("M_PortalMask_" + r, m => m.SetFloat("_StencilRef", r), Shader.Find("Bang/PortalMask"));
        public static Material BubbleFilm => Mat("M_BubbleFilm", null, Shader.Find("Bang/BubbleFilm"));

        public static GameObject Model(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        public static readonly float[] LodHeights = { 0.15f, 0.06f, 0.006f };   // LOD1→LOD2 在 6% 屏高（1 m 高的东西约 7 m 外）：LOD2 远处看不出，三角形少一大截

        /// <summary>已有道具预制体统一改成新的 LOD 档（去掉 LOD3、放宽切换）。</summary>
        [MenuItem("Bang/修正 P2 道具 LOD（去掉 LOD3）")]
        public static void FixPropLods()
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PropDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                var lg = root.GetComponent<LODGroup>();
                if (lg)
                {
                    var old = lg.GetLODs(); var lods = new System.Collections.Generic.List<LOD>();
                    for (int i = 0; i < old.Length; i++)
                    {
                        if (i < LodHeights.Length) lods.Add(new LOD(LodHeights[i], old[i].renderers));
                    }
                    var lod3 = root.transform.Find("LOD3"); if (lod3) Object.DestroyImmediate(lod3.gameObject);
                    lg.SetLODs(lods.ToArray()); lg.RecalculateBounds();
                    PrefabUtility.SaveAsPrefabAsset(root, path); n++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            Debug.Log($"[Bang] LOD 已修正 {n} 个道具预制体");
        }

        /// <summary>把 P2 管线产出（ArtSource/ModelsP2/名称/lod0..3.fbx + albedo.png）导入成预制体。返回预制体（没有产出时返回 null）。</summary>
        /// <summary>P2 重建失败、改成 Blender 程序模型的摆件（Tools/blender_props_fix.py → 名_lod0/1.fbx，顶点色）。有就优先用。</summary>
        public const string ProcFixDir = "Assets/_Bang/Art/Models/Proc/PropsFix";

        public static GameObject ImportP2(string name, float targetHeight, float gloss = 0.35f, float sheen = 0f)
        {
            if (File.Exists($"{ProcFixDir}/{name}_lod0.fbx"))
            {
                var vc = Mat("M_" + name + "_VC", m => { m.SetTexture("_BaseMap", null); m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", gloss); m.SetFloat("_Sheen", sheen); });
                return BuildLodPrefab(name, targetHeight, vc, i => $"{ProcFixDir}/{name}_lod{i}.fbx");
            }
            // 只接入 p2_finalize 审计全部合格（每级 LOD 0 开放边、0 非流形）的版本
            var src = Path.Combine(P2Src, name, "final");
            var rep = Path.Combine(src, "report.json");
            if (!File.Exists(Path.Combine(src, "lod0.fbx")) || !File.Exists(rep) || !File.ReadAllText(rep).Contains("\"pass\": true")) return null;
            var dst = $"{P2Dst}/{name}";
            Directory.CreateDirectory(dst);
            bool changed = false;
            foreach (var f in new[] { "lod0.fbx", "lod1.fbx", "lod2.fbx", "lod3.fbx", "albedo.png" })
            {
                var s = Path.Combine(src, f); var d = Path.Combine(dst, f);
                if (File.Exists(s) && (!File.Exists(d) || File.GetLastWriteTimeUtc(s) > File.GetLastWriteTimeUtc(d))) { File.Copy(s, d, true); changed = true; }
            }
            if (changed) AssetDatabase.Refresh();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dst}/albedo.png");
            var mat = Mat("M_" + name, m => { m.SetTexture("_BaseMap", tex); m.SetFloat("_Gloss", gloss); m.SetFloat("_Sheen", sheen); m.SetFloat("_UseVertexColor", 0); });

            return BuildLodPrefab(name, targetHeight, mat, i => $"{dst}/lod{i}.fbx");
        }

        static GameObject BuildLodPrefab(string name, float targetHeight, Material mat, System.Func<int, string> lodPath)
        {
            var root = new GameObject(name);
            var lods = new System.Collections.Generic.List<LOD>();
            // 用户：头显里模型明显破面 → 根因是 LOD 切得太早：屏高 < 45% 就离开最高精度、< 6% 换成只剩 3% 面数的 LOD3（屋顶凸点变尖碎片、叶子硬折角）。
            // 现在：LOD0 用到屏高 15%，LOD1（35% 面数，外观几乎一样）到 6%，LOD2 只在很远处；不再用 LOD3
            float[] h = LodHeights;
            Bounds b = default; bool hasB = false;
            for (int i = 0; i < h.Length; i++)
            {
                var m = Model(lodPath(i));
                if (!m) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(m);
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                inst.name = "LOD" + i;
                inst.transform.SetParent(root.transform, false);
                var rs = inst.GetComponentsInChildren<Renderer>();
                foreach (var r in rs) { r.sharedMaterial = mat; if (!hasB) { b = r.bounds; hasB = true; } else if (i == 0) b.Encapsulate(r.bounds); }
                lods.Add(new LOD(h[i], rs));
            }
            if (!hasB) { Object.DestroyImmediate(root); return null; }
            // 缩放到目标高度，底部落在 y=0、水平居中
            float s0 = targetHeight / Mathf.Max(b.size.y, 1e-4f);
            foreach (Transform c in root.transform) { c.localScale *= s0; c.localPosition = (c.localPosition - new Vector3(b.center.x, b.min.y, b.center.z)) * s0; }
            var lg = root.AddComponent<LODGroup>();
            lg.SetLODs(lods.ToArray());
            lg.RecalculateBounds();
            Directory.CreateDirectory(PropDir);
            var pf = PrefabUtility.SaveAsPrefabAsset(root, $"{PropDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return pf;
        }
    }
}
