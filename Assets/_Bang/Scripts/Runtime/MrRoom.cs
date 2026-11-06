// 真机上的“真实房间表面”（编辑器里用预览客厅代替）：玩具化画在这些表面上——没覆盖的地方透明露出真实房间（视频透视），
// 被覆盖的地方露出门后的玩具世界，从门口和每面旗子开始一步步“吞掉”真实房间。
//   1) 保底：一整块铺在真实地板上的面（追踪原点就是地面），任何情况下地面都看得到蔓延；
//   2) 空间网格：头显实时扫描到的墙、家具网格（PICO 空间网格，需要用户允许空间数据），玩具化会爬上墙和桌子；
//      网格块带碰撞体，打空时的蜡笔痕也能留在真实的墙上。
// 所有表面都挂在 XR Origin 下面（追踪空间），开局对齐游戏空间时跟着一起转，不会错位。
using System;
using System.Collections;
using System.Collections.Generic;
using ByteDance.PICO.XR;
using UnityEngine;

namespace Bang
{
    public class MrRoom : MonoBehaviour
    {
        public Transform trackingOrigin;
        public Material surfaceMat, maskMat;          // Bang/ToyCover（MR 模式）+ Bang/ToyCoverMask
        public GameObject meshBlockPrefab;            // 空间网格块：MeshFilter + MeshRenderer[表面, 遮罩] + MeshCollider
        public float floorSize = 14f;
        public bool editorToo;                        // 调试：编辑器里也生成（平时编辑器用预览客厅）

        PXR_SpatialMeshManager _mesh;
        /// <summary>真实房间的碰撞体（扫描到的墙 / 桌子 / 地板）：被打飞的零件按它们弹、落（用户：击飞要先从桌上飞起再落到地上，不能穿过桌子）。</summary>
        public static readonly HashSet<Collider> RoomColliders = new();
        Transform _floor;

        [Tooltip("包住玩家的整圈覆盖壳半径（米）：不依赖空间扫描，玩具化满了以后任何方向都被盖住")] public float shellRadius = 7f;

        void Start()
        {
            if (Application.isEditor && !editorToo) return;
            if (!trackingOrigin && Camera.main) trackingOrigin = Camera.main.transform.parent;
            BuildFloor();
            BuildShell();
            // 覆盖度按视线方向投到虚拟罩子上算（见 ToyCover.hlsl ToyProj），整圈覆盖壳已经包住所有方向：
            // 地板面片和扫描到的墙 / 家具不再参与显示（只留碰撞体给蜡笔痕），真实房间里有什么都不影响、不遮挡
            if (_floor) foreach (var r in _floor.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
            StartCoroutine(StartSpatialMesh());
        }

        // 3) 覆盖壳：一个朝内的大球，和地板一样挂“表面 + 遮罩”两个材质。
        //    真机试玩：只靠地板 + 扫描到的墙，没扫到的方向永远盖不上（看起来只有眼前一块、天空有破洞）。
        //    壳在真实墙后面：扫到墙的地方墙面先写深度挡住它，没扫到的地方由壳兜底——玩具化从门口长满整个 360°。
        void BuildShell()
        {
            var g = new GameObject("MrCoverShell");
            g.transform.SetParent(trackingOrigin, false);
            g.transform.localPosition = new Vector3(0, 1.2f, 0);
            g.AddComponent<MeshFilter>().sharedMesh = InwardSphere(shellRadius, 48, 24);
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterials = new[] { surfaceMat, maskMat };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }

        /// <summary>法线朝内的经纬球（两极封口、没有缝）。</summary>
        static Mesh InwardSphere(float rad, int seg, int rings)
        {
            var v = new Vector3[(seg + 1) * (rings + 1)]; var n = new Vector3[v.Length];
            for (int y = 0; y <= rings; y++)
                for (int x = 0; x <= seg; x++)
                {
                    float th = Mathf.PI * y / rings, ph = 2 * Mathf.PI * x / seg;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    v[y * (seg + 1) + x] = d * rad; n[y * (seg + 1) + x] = -d;
                }
            var t = new int[seg * rings * 6]; int k = 0;
            for (int y = 0; y < rings; y++)
                for (int x = 0; x < seg; x++)
                {
                    int a = y * (seg + 1) + x, b = a + seg + 1;
                    t[k++] = a; t[k++] = b; t[k++] = a + 1;            // 绕序朝内：从球心往外看是正面（Unity 顺时针为正面）
                    t[k++] = a + 1; t[k++] = b; t[k++] = b + 1;
                }
            var m = new Mesh { name = "CoverShell", vertices = v, normals = n, triangles = t };
            m.bounds = new Bounds(Vector3.zero, Vector3.one * rad * 2);
            return m;
        }

        void BuildFloor()
        {
            var g = new GameObject("MrFloor"); _floor = g.transform;
            _floor.SetParent(trackingOrigin, false);
            _floor.localPosition = new Vector3(0, 0.003f, 0); _floor.localRotation = Quaternion.Euler(90, 0, 0);
            _floor.localScale = Vector3.one * floorSize;
            g.AddComponent<MeshFilter>().sharedMesh = Play.PlayFx.Quad;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterials = new[] { surfaceMat, maskMat };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = g.AddComponent<BoxCollider>(); col.size = new Vector3(1, 1, 0.002f);    // 打空的蜡笔痕可以落在地板上
            RoomColliders.Add(col);
        }

        IEnumerator StartSpatialMesh()
        {
            if (!meshBlockPrefab) yield break;
            yield return new WaitForSeconds(1.5f);                 // 等网格子系统起来（太早启用，管理器会把自己关掉）
            var go = new GameObject("SpatialMesh"); go.SetActive(false);
            go.transform.SetParent(trackingOrigin, false);
            _mesh = go.AddComponent<PXR_SpatialMeshManager>();
            _mesh.meshPrefab = meshBlockPrefab;
            PXR_SpatialMeshManager.MeshAdded += OnMesh;
            PXR_SpatialMeshManager.MeshUpdated += OnMesh;
            go.SetActive(true);
            for (int i = 0; i < 10 && _mesh && !_mesh.enabled; i++) { yield return new WaitForSeconds(2f); if (_mesh) _mesh.enabled = true; }
        }

        void OnDestroy()
        {
            PXR_SpatialMeshManager.MeshAdded -= OnMesh;
            PXR_SpatialMeshManager.MeshUpdated -= OnMesh;
        }

        /// <summary>管理器把网格块放在“追踪坐标当世界坐标”的位置；改成挂在 XR Origin 下按局部坐标摆，开局对齐时跟着走。</summary>
        void OnMesh(Guid id, GameObject g)
        {
            if (!g || !trackingOrigin) return;
            var p = g.transform.position; var q = g.transform.rotation;
            if (g.transform.parent != trackingOrigin) g.transform.SetParent(trackingOrigin, false);
            g.transform.localPosition = p; g.transform.localRotation = q;
            var mf = g.GetComponentInChildren<MeshFilter>();
            if (mf && mf.sharedMesh) mf.sharedMesh.RecalculateNormals();
            foreach (var r in g.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;      // 只留碰撞体（蜡笔痕、碎片落点），不参与显示
            foreach (var c in g.GetComponentsInChildren<Collider>()) RoomColliders.Add(c);
        }
    }
}
