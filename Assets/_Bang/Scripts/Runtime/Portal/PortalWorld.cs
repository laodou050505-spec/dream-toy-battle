// 门后世界：把子物体的材质换成“模板相等”版本（只在门洞里、以及房间被玩具化覆盖的地方绘制），并统一设置景深雾和世界色调。
// 编辑器里也生效（ExecuteAlways）。原始材质序列化存在本组件里（originals），场景文件里存的始终是原始材质：
//   - 保存场景前自动还原原始材质、保存后再换回变体（变体是 DontSave 的运行时材质，写进场景会变成空引用 → 洋红色）；
//   - 打开场景 / 重新编译后，OnEnable 从 originals 重新生成变体。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bang.Portal
{
    [ExecuteAlways]
    public class PortalWorld : MonoBehaviour
    {
        [Range(1, 255)] public int stencilRef = 1;
        [Header("景深雾")]
        public Color hazeColor = new Color(1f, 0.93f, 0.9f, 1f);
        public float hazeStart = 5f, hazeEnd = 18f;
        [Range(0, 1)] public float hazeAmount = 0.18f;
        [Header("世界色调")]
        [Tooltip("乘在受光颜色上（自发光不受影响），比如黄昏城堡用偏紫的暗色")] public Color worldTint = Color.white;

        [Serializable] public struct Entry { public Renderer renderer; public Material[] materials; }
        [SerializeField, HideInInspector] List<Entry> originals = new();

        static readonly Dictionary<(Material, int), Material> Cache = new();
        bool _bound;
        static readonly int IdRef = Shader.PropertyToID("_StencilRef"), IdComp = Shader.PropertyToID("_StencilComp"), IdReadMask = Shader.PropertyToID("_StencilReadMask");

        void OnEnable()
        {
            Bind();
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnSaving;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += OnSaved;
#endif
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= OnSaving;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= OnSaved;
#endif
            Unbind();
        }

#if UNITY_EDITOR
        void OnSaving(UnityEngine.SceneManagement.Scene s, string path) { if (s == gameObject.scene) Unbind(); }
        void OnSaved(UnityEngine.SceneManagement.Scene s) { if (s == gameObject.scene && isActiveAndEnabled) Bind(); }
#endif

        /// <summary>重新记录原始材质并重新绑定（构建场景后、子物体增减后调用）。</summary>
        public void Rebind()
        {
            Unbind();
            Capture();
            Bind();
        }

        /// <summary>记录当前所有子渲染器的材质为原始材质（已经是变体的还原成对应原始材质）。</summary>
        void Capture()
        {
            originals.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = SourceOf(mats[i]);
                originals.Add(new Entry { renderer = r, materials = mats });
            }
        }

        static Material SourceOf(Material m)
        {
            if (!m) return m;
            foreach (var kv in Cache) if (kv.Value == m) return kv.Key.Item1;
            return m;
        }

        void Bind()
        {
            if (_bound) return;
            if (originals.Count == 0) Capture();
            foreach (var e in originals)
            {
                if (!e.renderer || e.materials == null) continue;
                var dst = new Material[e.materials.Length];
                for (int i = 0; i < dst.Length; i++) dst[i] = Variant(e.materials[i]);
                e.renderer.sharedMaterials = dst;
            }
            _bound = true;
        }

        void Unbind()
        {
            foreach (var e in originals)
                if (e.renderer && e.materials != null) e.renderer.sharedMaterials = e.materials;
            _bound = false;
        }

        /// <summary>动态物体（从门里走出来的小怪）用：取本世界的模板变体材质（有缓存，不重复创建）。</summary>
        public Material VariantOf(Material m)
        {
            if (!m || !m.HasProperty(IdRef)) return m;
            return Cache.TryGetValue((m, stencilRef), out var v) && v ? v : Variant(m);
        }

        Material Variant(Material m)
        {
            if (!m || !m.HasProperty(IdRef)) return m;
            var key = (m, stencilRef);
            if (Cache.TryGetValue(key, out var v) && v) v.CopyPropertiesFromMaterial(m);   // 原材质改过（贴图、参数）要同步过来
            else { v = new Material(m) { name = m.name + "_Portal" + stencilRef, hideFlags = HideFlags.DontSave }; Cache[key] = v; }
            v.SetFloat(IdRef, stencilRef);
            if (m.renderQueue < 2450) v.renderQueue = 1994;   // 门后世界（不透明）：覆盖遮罩 1980、门洞遮罩 1990 之后，真实表面淡入（1997）和其余游戏内容（2000+）之前
            v.SetFloat(IdComp, (float)CompareFunction.Equal);
            if (v.HasProperty(IdReadMask)) v.SetFloat(IdReadMask, stencilRef < 8 ? 7 : 255);   // 门洞 64|n、玩具化覆盖 128|n 都通过
            ApplyHaze(v);
            return v;
        }

        static readonly Dictionary<(Material, PortalWorld), Material> TintCache = new();
        /// <summary>门外地皮 / 门口溢出摆件用：不加门洞模板（房间里直接看得到），只套本世界的色调和景深雾，
        /// 让门里门外是同一块地（用户：传送门门口和游戏场景不是一个地方，能看出来）。</summary>
        public Material OutsideOf(Material m)
        {
            if (!m || !m.HasProperty("_WorldTint")) return m;
            if (TintCache.TryGetValue((m, this), out var v) && v) return v;
            v = new Material(m) { name = m.name + "_Out", hideFlags = HideFlags.DontSave };
            ApplyHaze(v); TintCache[(m, this)] = v; return v;
        }

        void ApplyHaze(Material v)
        {
            if (v.HasProperty("_WorldTint")) v.SetColor("_WorldTint", worldTint);
            if (!v.HasProperty("_HazeAmount")) return;
            v.SetColor("_HazeColor", hazeColor);
            v.SetFloat("_HazeStart", hazeStart);
            v.SetFloat("_HazeEnd", hazeEnd);
            v.SetFloat("_HazeAmount", hazeAmount);
        }
    }
}
