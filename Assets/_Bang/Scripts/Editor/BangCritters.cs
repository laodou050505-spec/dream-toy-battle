// 在门后世界里放一群折纸小鸟 / 毛毡蝴蝶（Tools/blender_critters.py 生成的身体 + 翅膀，翅膀单独扇动）。
using Bang.Ambient;
using UnityEditor;
using UnityEngine;

namespace Bang.EditorTools
{
    public static class BangCritters
    {
        const string Dir = "Assets/_Bang/Art/Models/Proc/Critters";
        static readonly string[] BirdCols = { "Pink", "Mint", "Lemon" };
        static readonly string[] FlyCols = { "Pink", "Lav", "Lemon", "Blue" };

        /// <summary>world 本地坐标：center 为航线中心，radius 为航线半径范围，height 为飞行高度范围。</summary>
        public static void Spawn(Transform world, bool butterflies, int count, Vector3 center, Vector2 radius, Vector2 height, int seed)
        {
            var rnd = new System.Random(seed);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var root = new GameObject(butterflies ? "Butterflies" : "Birds").transform; root.SetParent(world, false);
            for (int i = 0; i < count; i++)
            {
                string col = butterflies ? FlyCols[i % FlyCols.Length] : BirdCols[i % BirdCols.Length];
                string pre = butterflies ? "SH_Fly_" : "SH_Bird_";
                var mat = butterflies ? BangArt.FeltVC : BangArt.Mat("M_PaperVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.08f); m.SetFloat("_RimStrength", 0.3f); m.SetFloat("_ShadeSoft", 0.4f); });
                var c = new GameObject(butterflies ? "Butterfly" : "Bird").transform; c.SetParent(root, false);
                var body = Load($"{Dir}/{pre}Body_{col}.fbx", c, mat);
                if (!body) { Object.DestroyImmediate(c.gameObject); continue; }
                float off = butterflies ? 0.006f : 0.022f;
                var wr = new GameObject("WingR").transform; wr.SetParent(c, false); wr.localPosition = new Vector3(off, butterflies ? 0.004f : 0.02f, butterflies ? 0.006f : 0.01f);
                var wl = new GameObject("WingL").transform; wl.SetParent(c, false); wl.localPosition = new Vector3(-off, wr.localPosition.y, wr.localPosition.z);
                Load($"{Dir}/{pre}Wing_{col}.fbx", wr, mat);
                var l = Load($"{Dir}/{pre}Wing_{col}.fbx", wl, mat);
                if (l) l.transform.localScale = new Vector3(-1, 1, 1);
                float s = butterflies ? R(1.6f, 2.4f) : R(1.4f, 2.0f);     // 门后世界是放大的玩具尺度
                c.localScale = Vector3.one * s;
                var cr = c.gameObject.AddComponent<Critter>();
                cr.wingL = wl; cr.wingR = wr; cr.butterfly = butterflies;
                cr.center = center + new Vector3(R(-0.6f, 0.6f), 0, R(-0.5f, 0.5f));
                cr.radius = new Vector2(R(radius.x * 0.6f, radius.x), R(radius.y * 0.6f, radius.y));
                cr.height = R(height.x, height.y); cr.bob = butterflies ? R(0.1f, 0.25f) : R(0.15f, 0.4f);
                cr.speed = butterflies ? R(0.25f, 0.4f) : R(0.5f, 0.8f); cr.phase = R(0, 6.28f);
                cr.Snap();                                          // 参数赋完再摆到航线上（AddComponent 时 OnEnable 用的是默认值）
            }
        }

        static GameObject Load(string path, Transform parent, Material mat)
        {
            var m = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!m) return null;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(m, parent);
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            return g;
        }
    }
}
