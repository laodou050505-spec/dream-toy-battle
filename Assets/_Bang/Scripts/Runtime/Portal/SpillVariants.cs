// 门外地皮的“按门角度”版本（编辑器 BangSpillMatch.BakeVariants 烘焙）：
//   多门章节把 0° 门复制到 ±35°/±80° 等角度时，复制出来的地皮如果还用 0° 那套 UV，地板砖纹理和那扇门里看到的地面对不上
//   （用户：门前那一片要和门里接上，尤其地板砖纹理要对上）；几扇门挨着时，相邻两块地皮还会叠在一起（用户：门前不要重叠）。
//   每种“门角度 + 左右两侧的分界角”一份网格：超出分界线（与相邻门的角平分线）的顶点收到分界线上，再按顶点真实所在的位置
//   到门后世界地面上取样（UV + 顶点色），材质也换成取样到的那块地面。
// 角度约定：和 ChapterWorlds 复制门一致——门在 Euler(0, a, 0) * (0, 0, -r)，即点 (x, z) 的角度 φ = atan2(-x, -z)。
using UnityEngine;

namespace Bang.Portal
{
    public class SpillVariants : MonoBehaviour
    {
        public float[] angles = new float[0], lo = new float[0], hi = new float[0];
        public Mesh[] meshes = new Mesh[0];
        public Material[] mats = new Material[0];

        /// <summary>该门角度 + 分界的网格和材质；没有烘焙过返回 false。</summary>
        public bool For(float angle, float l, float h, out Mesh mesh, out Material mat)
        {
            mesh = null; mat = null;
            for (int i = 0; i < angles.Length; i++)
                if (Mathf.Abs(Mathf.DeltaAngle(angles[i], angle)) < 0.5f && Mathf.Abs(lo[i] - l) < 0.5f && Mathf.Abs(hi[i] - h) < 0.5f && i < meshes.Length && meshes[i])
                { mesh = meshes[i]; mat = i < mats.Length ? mats[i] : null; return true; }
            return false;
        }

        /// <summary>第 i 扇门的扇区：和相邻门之间取角平分线；没有相邻门的一侧放开（±180）。</summary>
        public static void Sector(double[] angles, int i, out float l, out float h)
        {
            double a = angles[i], below = double.NegativeInfinity, above = double.PositiveInfinity;
            for (int j = 0; j < angles.Length; j++)
            {
                if (j == i) continue;
                if (angles[j] < a && angles[j] > below) below = angles[j];
                if (angles[j] > a && angles[j] < above) above = angles[j];
            }
            l = double.IsNegativeInfinity(below) ? -180f : (float)((a + below) * 0.5);
            h = double.IsPositiveInfinity(above) ? 180f : (float)((a + above) * 0.5);
        }

        public static float Angle(Vector3 p) => Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg;

        /// <summary>扇区外的点绕原点转到分界线上（半径不变）。p 是门的父节点（房间中心）坐标。</summary>
        public static Vector3 Clip(Vector3 p, float l, float h)
        {
            if (l <= -179f && h >= 179f) return p;
            float a = Angle(p), c = Mathf.Clamp(a, l, h);
            if (Mathf.Abs(c - a) < 1e-4f) return p;
            float r = new Vector2(p.x, p.z).magnitude, rad = c * Mathf.Deg2Rad;
            return new Vector3(-Mathf.Sin(rad) * r, p.y, -Mathf.Cos(rad) * r);
        }

        /// <summary>点（带角度余量 marginDeg）是否在扇区内。</summary>
        public static bool Inside(Vector3 p, float l, float h, float marginDeg)
        {
            float a = Angle(p); return a >= l + marginDeg && a <= h - marginDeg;
        }
    }
}
