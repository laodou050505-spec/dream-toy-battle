// 门后世界的“透过窗口”裁剪：门后世界只在门洞里、以及房间被玩具化覆盖的地方看得到（模板测试在像素阶段），
// 但顶点、阴影照样全算。这里每隔一帧算一次：从眼睛出发，穿过每个“窗口”（门洞 + 各覆盖区）的视锥里有没有这个物体，
// 没有就 forceRenderingOff（连阴影一起省掉）。覆盖区大到包住大半个房间时直接全部打开。
// 窗口近似成球：门洞 = 洞口中心 + 半对角线；覆盖区 = RoomToyification.GetCoverSpheres。
// 判定用包围盒 8 个角 + 中心是否落在“眼睛→窗口球”的圆锥里、且在窗口所在平面之外（窗口前面、房间里的东西透过窗口看不到）。
using System.Collections.Generic;
using UnityEngine;

namespace Bang.Portal
{
    [DefaultExecutionOrder(1000)]
    public class PortalCuller : MonoBehaviour
    {
        [Tooltip("门洞（取它的 LocalBounds 作窗口）")] public PortalArch arch;
        [Tooltip("包围盒最大边超过这个（米）的物体永远画（地面、天球）")] public float alwaysSize = 10f;
        [Tooltip("锥角余量（度）：双眼间距、头部移动")] public float marginDeg = 7f;
        [Tooltip("任一覆盖区半径超过这个（米）就不再裁剪")] public float fullOpenRadius = 5.5f;
        [Tooltip("只在房间地面被覆盖时才画的物体名关键字（房间地面范围内的花草）")] public string roomOnlyKey = "_RoomFlora";

        struct Item { public Renderer r; public bool always, roomOnly; }
        readonly List<Item> _items = new();
        readonly List<Vector4> _windows = new(), _cover = new();
        readonly Vector3[] _pts = new Vector3[9];
        int _frame;

        void OnEnable() => Collect();

        public void Collect()
        {
            _items.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) { _items.Add(new Item { r = r, always = false }); continue; }
                var s = r.bounds.size;
                _items.Add(new Item { r = r, always = Mathf.Max(s.x, Mathf.Max(s.y, s.z)) > alwaysSize, roomOnly = r.name.Contains(roomOnlyKey) || r.transform.parent && r.transform.parent.name.Contains(roomOnlyKey) });
            }
        }

        void OnDisable() { foreach (var it in _items) if (it.r) it.r.forceRenderingOff = false; }

        void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if ((++_frame & 1) == 1) return;                     // 每两帧算一次
            var cam = Camera.main; if (!cam) return;
            Evaluate(cam.transform.position);
        }

        /// <summary>按眼睛位置算一遍可见性并应用（编辑器里也能调，用来统计负载）。</summary>
        public void Evaluate(Vector3 eye)
        {
            if (_items.Count == 0) Collect();
            _windows.Clear();
            bool full = false, anyCover = false;
            // 每扇门一个窗口（多门章节的门是运行时按逻辑门复制出来的；门框没长出来的门不算）
            foreach (var a in PortalArch.All)
            {
                if (!a || a.transform.lossyScale.x < 0.05f) continue;
                var b = a.LocalBounds; var c = a.transform.TransformPoint(b.center);
                _windows.Add(new Vector4(c.x, c.y, c.z, b.extents.magnitude * Mathf.Max(a.transform.lossyScale.x, 0.01f) + 0.1f));
            }
            if (PortalArch.All.Count == 0 && arch)
            {
                var b = arch.LocalBounds; var c = arch.transform.TransformPoint(b.center);
                _windows.Add(new Vector4(c.x, c.y, c.z, b.extents.magnitude * Mathf.Max(arch.transform.lossyScale.x, 0.01f) + 0.1f));
            }
            var toy = RoomToyification.Instance;
            if (toy)
            {
                toy.GetCoverSpheres(_cover);
                foreach (var s in _cover) { _windows.Add(s); anyCover = true; if (s.w > fullOpenRadius) full = true; }
            }
            foreach (var it in _items)
            {
                if (!it.r) continue;
                bool vis;
                if (it.always || full) vis = !it.roomOnly || anyCover;
                else if (it.roomOnly) vis = anyCover;
                else vis = Visible(it.r.bounds, eye);
                it.r.forceRenderingOff = !vis;
            }
        }

        bool Visible(Bounds b, Vector3 eye)
        {
            var mn = b.min; var mx = b.max;
            _pts[0] = b.center;
            _pts[1] = new Vector3(mn.x, mn.y, mn.z); _pts[2] = new Vector3(mx.x, mn.y, mn.z); _pts[3] = new Vector3(mn.x, mx.y, mn.z); _pts[4] = new Vector3(mx.x, mx.y, mn.z);
            _pts[5] = new Vector3(mn.x, mn.y, mx.z); _pts[6] = new Vector3(mx.x, mn.y, mx.z); _pts[7] = new Vector3(mn.x, mx.y, mx.z); _pts[8] = new Vector3(mx.x, mx.y, mx.z);
            foreach (var w in _windows)
            {
                var wc = new Vector3(w.x, w.y, w.z); var d = wc - eye; float dl = d.magnitude;
                if (dl < w.w) return true;                        // 眼睛就在窗口里（贴着覆盖区）
                var axis = d / dl;
                float cosLim = Mathf.Cos(Mathf.Min(89f, Mathf.Asin(Mathf.Min(1f, w.w / dl)) * Mathf.Rad2Deg + marginDeg) * Mathf.Deg2Rad);
                float near = dl - w.w;                             // 窗口近边：比它更近的点在房间里，透过窗口看不到
                for (int i = 0; i < 9; i++)
                {
                    var v = _pts[i] - eye; float vl = v.magnitude;
                    if (vl < 1e-4f) return true;
                    float along = Vector3.Dot(v, axis);
                    if (along < near) continue;
                    if (along / vl >= cosLim) return true;
                }
            }
            return false;
        }
    }
}
