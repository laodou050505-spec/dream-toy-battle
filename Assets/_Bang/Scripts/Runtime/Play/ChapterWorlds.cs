// 章节世界：每章的门 + 门后世界存成单独的场景 World_W1..W6（都进安装包），开章时叠加加载、换章时卸掉上一章——
// 同一时间只占一章的内存。原来主场景里只烘焙了第 1 章的世界，第 2～6 章在头显里看到的一直是积木小镇。
// 多门章节（用户：小怪从别处出来就给它开一扇门）：世界场景里只有正前方 0° 一扇门，这里按该章逻辑门的角度
// 把门框 / 门洞 / 门口溢出的摆件复制到每扇门的位置（绕房间中心转 yaw = 逻辑角度）；同一个模板值，透过每扇门看到的都是同一个世界。
using System;
using System.Collections;
using System.Collections.Generic;
using Bang.Logic;
using Bang.Portal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bang.Play
{
    public class ChapterWorlds : MonoBehaviour
    {
        public static ChapterWorlds Instance { get; private set; }

        /// <summary>当前加载好的世界（1..6，0 = 还没有）。13 关里每两章共用一个世界，门的角度按章摆。</summary>
        public int Loaded { get; private set; }
        /// <summary>门是按哪一章摆的（同一个世界，不同章的门数和角度可能不同，例如终极决战）。</summary>
        public int DoorsFor { get; private set; }
        /// <summary>这一章的世界和门都好了。</summary>
        public bool ReadyFor(int ch) => Loaded == CHAPTERS.World(ch) && DoorsFor == ch && DoorsEndless == endless && !_loading;
        /// <summary>无尽模式：不管在哪个 Boss 的世界，一律按四扇门摆（用户：无尽模式的门一直是四个门）。GameDriver 开无尽前置 true，开章节前置 false。</summary>
        public bool endless;
        public bool DoorsEndless { get; private set; }
        public bool Loading => _loading;
        /// <summary>按逻辑门顺序排好的门（门根节点上挂 PortalOpen；第 i 个对应 Game.portals[i]）。</summary>
        public readonly List<Transform> Doors = new();

        Scene _scene; bool _loading; int _want;
        Transform _template;                                     // 世界场景里 0° 的那扇门（Portal_Wn，门后世界是它的子物体）
        readonly List<GameObject> _clones = new();
        readonly List<Action> _waiting = new();

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        public static string SceneName(int ch) => $"World_W{ch}";

        /// <summary>确保第 ch 章的世界已加载、门已按该章摆好；好了就回调（已经好了立刻回调）。</summary>
        public void Ensure(int ch, Action done)
        {
            ch = Mathf.Clamp(ch, 1, CHAPTERS.Count);
            int w = CHAPTERS.World(ch);
            if (Loaded == w && !_loading && DoorsFor == ch && DoorsEndless == endless) { done?.Invoke(); return; }
            // 同一个世界换一章（如第 9 → 10 章）也整场重新加载：从已经开过的门上复制门框 / 门洞，复制出来的门洞会变成一面白墙
            //（用户：第十章的传送门变成了纯白色）。直接进第 10 章走的就是完整加载，所以这里统一走同一条路。
            if (Loaded == w && !_loading) Loaded = 0;
            if (done != null) _waiting.Add(done);
            _want = w; _wantCh = ch;
            if (!_loading) StartCoroutine(LoadLoop());
        }
        int _wantCh = 1;

        IEnumerator LoadLoop()
        {
            _loading = true;
            while (Loaded != _want)
            {
                int ch = _want;
                ClearClones();
                Doors.Clear(); _template = null;
                if (_scene.IsValid() && _scene.isLoaded) { yield return SceneManager.UnloadSceneAsync(_scene); _scene = default; }
                Loaded = 0;
                string name = SceneName(ch);
                if (Application.CanStreamedLevelBeLoaded(name))
                {
                    var op = SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
                    while (!op.isDone) yield return null;
                    _scene = SceneManager.GetSceneByName(name);
                    Loaded = ch; SetupDoors(_wantCh);                              // 同一帧里先把门收起来（封面阶段门不出现）
                    yield return Resources.UnloadUnusedAssets();                 // 上一章的贴图 / 网格放掉
                }
                else
                {
                    Debug.LogWarning($"[Bang] 世界场景 {name} 不在打包列表里：用当前场景里现成的门（编辑器预览场景）");
                    Loaded = ch; SetupDoors(_wantCh);
                }
            }
            _loading = false;
            var w = _waiting.ToArray(); _waiting.Clear();
            foreach (var a in w) a?.Invoke();
        }

        void ClearClones()
        {
            foreach (var kv in _origMesh) if (kv.Key) kv.Key.sharedMesh = kv.Value;   // 模板门（0°）上换过的地皮 / 花草网格还原：下一章可能是单门
            foreach (var kv in _origMat) if (kv.Key) kv.Key.sharedMaterial = kv.Value;
            foreach (var g in _hiddenTpl) if (g) g.SetActive(true);
            _origMesh.Clear(); _origMat.Clear(); _hiddenTpl.Clear();
            foreach (var m in _clipped) if (m) Destroy(m);
            _clipped.Clear();
            foreach (var g in _clones) if (g) Destroy(g);
            _clones.Clear();
        }

        /// <summary>按该章逻辑门的角度摆门：0° 的门用世界场景里原有的，其余角度复制门框 / 门洞 / 门口溢出摆件；没有 0° 门的章把原有门框藏起来（世界留着）。
        /// 新的门一律先收起（封面阶段门不出现），开章时由 GameDriver 一起打开。</summary>
        void SetupDoors(int ch)
        {
            DoorsFor = ch; DoorsEndless = endless;
            ClearClones(); Doors.Clear();
            _template = FindTemplate();
            if (!_template) return;
            var parts = new List<Transform>();
            foreach (Transform c in _template) if (!c.GetComponent<PortalWorld>() && !c.GetComponentInChildren<PortalWorld>(true)) parts.Add(c);
            var gd0 = GetComponent<GameDriver>();
            bool seatedNow = gd0 && gd0.Game != null && gd0.Game.seated;
            var angles = endless ? CHAPTERS.PortalAngles(CHAPTERS.Count, seatedNow) : CHAPTERS.PortalAngles(ch, seatedNow);   // 坐姿模式时门收窄，和规则层一致；无尽一律四扇门
            bool hasZero = false;
            foreach (var a in angles) if (Math.Abs(a) < 0.5) hasZero = true;
            foreach (var p in parts) p.gameObject.SetActive(hasZero);
            foreach (var a in angles)
            {
                if (Math.Abs(a) < 0.5) { Doors.Add(_template); continue; }
                var q = Quaternion.Euler(0, (float)a, 0);
                var d = new GameObject($"Door_{a:0}").transform;
                d.SetParent(_template.parent, false);
                d.SetPositionAndRotation(q * _template.position, q * _template.rotation);
                d.localScale = _template.localScale;
                foreach (var p in parts)
                {
                    var c = Instantiate(p.gameObject, d, false);
                    c.name = p.name; c.SetActive(true);
                    c.transform.localPosition = p.localPosition; c.transform.localRotation = p.localRotation; c.transform.localScale = p.localScale;
                }
                _clones.Add(d.gameObject);
                Doors.Add(d);
            }
            // 门外地皮：每扇门换成按自己角度 + 扇区烘焙的网格（纹理对得上门里地面；和相邻门不重叠）；越过扇区分界的门口摆件收起来
            for (int di = 0; di < Doors.Count && di < angles.Length; di++)
            {
                var sp = Doors[di].Find("Spill"); if (!sp) continue;
                Bang.Portal.SpillVariants.Sector(angles, di, out float lo, out float hi);
                foreach (var pr in sp.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var sv = pr.GetComponent<Bang.Portal.SpillVariants>(); if (!sv) continue;
                    if (sv.For((float)angles[di], lo, hi, out var vm, out var vmat)) { var mf = pr.GetComponent<MeshFilter>(); if (mf) { Remember(mf); mf.sharedMesh = vm; } if (vmat) { if (!_origMat.ContainsKey(pr)) _origMat[pr] = pr.sharedMaterial; pr.sharedMaterial = vmat; } }
                }
                if (lo > -179f || hi < 179f)
                {
                    // 门口的花草 / 小路：整块网格，越过分界线的三角形去掉（不然和隔壁门的花草叠成一片）
                    foreach (var fm in sp.GetComponentsInChildren<MeshFilter>(true))
                    {
                        var n = fm.name + (fm.transform.parent ? fm.transform.parent.name : "");
                        if (!(n.Contains("Flora") || n.Contains("BrickPath")) || !fm.sharedMesh || !fm.sharedMesh.isReadable) continue;
                        Remember(fm); fm.sharedMesh = ClipMesh(fm.sharedMesh, fm.transform, lo, hi);
                    }
                    foreach (Transform c in sp)
                    {
                        if (c.name.StartsWith("SpillPatch") || c.name.Contains("Flora") || c.name.Contains("BrickPath")) continue;
                        var rp = _template.parent.InverseTransformPoint(c.position);
                        bool keep = Bang.Portal.SpillVariants.Inside(rp, lo, hi, 4f);
                        if (!keep && c.gameObject.activeSelf && Doors[di] == _template) _hiddenTpl.Add(c.gameObject);
                        c.gameObject.SetActive(keep);
                    }
                }
            }
            foreach (var d in Doors)
            {
                var po = d.GetComponent<PortalOpen>(); if (!po) po = d.gameObject.AddComponent<PortalOpen>();
                po.RefreshSpill();   // 地皮 / 花草网格换过了：开门动画重新收集，不然开完门会把旧网格换回来
                po.CloseNow();
            }
            var world = World;
            if (world)
                foreach (var d in Doors)
                {
                    var sp = d.Find("Spill"); if (!sp) continue;
                    foreach (var r in sp.GetComponentsInChildren<Renderer>(true))
                    {
                        var ms = r.sharedMaterials; bool dirty = false;
                        for (int i = 0; i < ms.Length; i++) { var o = world.OutsideOf(ms[i]); if (o != ms[i]) { ms[i] = o; dirty = true; } }
                        if (dirty) r.sharedMaterials = ms;
                    }
                }
            foreach (var c in _template.GetComponentsInChildren<PortalCuller>(true)) c.Collect();
            var gd = GetComponent<GameDriver>();                     // 规则层每扇门都对应这同一个世界（小怪在门后时套它的模板材质）
            if (gd && World) { gd.worlds = new PortalWorld[Doors.Count]; for (int i = 0; i < Doors.Count; i++) gd.worlds[i] = World; }
        }

        readonly List<Mesh> _clipped = new();
        readonly Dictionary<MeshFilter, Mesh> _origMesh = new();
        readonly Dictionary<MeshRenderer, Material> _origMat = new();
        readonly List<GameObject> _hiddenTpl = new();
        /// <summary>只记模板门的（复制出来的门换章时整个删掉，不用还原）。</summary>
        void Remember(MeshFilter mf) { if (mf.transform.IsChildOf(_template) && !_origMesh.ContainsKey(mf)) _origMesh[mf] = mf.sharedMesh; }
        /// <summary>按扇区剔除三角形（三角形重心不在扇区内就去掉），返回新网格（换章时随世界场景一起释放）。</summary>
        Mesh ClipMesh(Mesh src, Transform t, float lo, float hi)
        {
            var v = src.vertices; var room = _template.parent;
            var keep = new List<int>(); var tri = src.triangles;
            for (int i = 0; i < tri.Length; i += 3)
            {
                var c = (v[tri[i]] + v[tri[i + 1]] + v[tri[i + 2]]) / 3f;
                if (Bang.Portal.SpillVariants.Inside(room.InverseTransformPoint(t.TransformPoint(c)), lo, hi, 1f)) { keep.Add(tri[i]); keep.Add(tri[i + 1]); keep.Add(tri[i + 2]); }
            }
            var m = Instantiate(src); m.name = src.name + "_clip"; m.subMeshCount = 1; m.SetTriangles(keep, 0); m.RecalculateBounds();
            _clipped.Add(m); return m;
        }

        /// <summary>世界场景里那扇门（门洞的父物体，门后世界挂在它下面）；没加载世界场景时用当前场景里现成的（编辑器预览）。</summary>
        Transform FindTemplate()
        {
            foreach (var a in FindObjectsByType<PortalArch>(FindObjectsInactive.Include))
            {
                var t = a.transform.parent; if (!t) continue;
                if (_scene.IsValid() && t.gameObject.scene != _scene) continue;
                if (t.GetComponentInChildren<PortalWorld>(true)) return t;
            }
            return null;
        }

        /// <summary>当前章的门后世界（所有门看到的是同一个世界）。</summary>
        public PortalWorld World => _template ? _template.GetComponentInChildren<PortalWorld>(true) : null;

        /// <summary>门后世界的“Props”（门后摆件，登记障碍用）。</summary>
        public IEnumerable<Transform> WorldProps()
        {
            if (!_template) yield break;
            foreach (var t in _template.GetComponentsInChildren<Transform>())
                if (t.name == "Props") yield return t;
        }
    }
}
