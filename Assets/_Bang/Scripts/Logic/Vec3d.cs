// 极简向量工具（纯数据 class Vec3d），逻辑层不依赖 UnityEngine。
// Vec3d 必须是 class（引用类型），以保留原型中的引用相等语义：
//   Enemy.pos、预定点、gazeRay.origin 与 head.pos 共享同一对象，原地修改 x/z。
namespace Bang.Logic
{
    public class Vec3d
    {
        public double x, y, z;
        public Vec3d(double x = 0, double y = 0, double z = 0) { this.x = x; this.y = y; this.z = z; }
        public static Vec3d New(double x = 0, double y = 0, double z = 0) => new Vec3d(x, y, z);
    }

    public static class V
    {
        public const double DEG = System.Math.PI / 180.0;

        public static Vec3d v(double x = 0, double y = 0, double z = 0) => new Vec3d(x, y, z);
        public static Vec3d clone(Vec3d a) => new Vec3d(a.x, a.y, a.z);
        public static Vec3d add(Vec3d a, Vec3d b) => new Vec3d(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vec3d sub(Vec3d a, Vec3d b) => new Vec3d(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vec3d scale(Vec3d a, double s) => new Vec3d(a.x * s, a.y * s, a.z * s);
        public static double dot(Vec3d a, Vec3d b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vec3d cross(Vec3d a, Vec3d b) => new Vec3d(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x
        );
        public static double len(Vec3d a) => System.Math.Sqrt(a.x * a.x + a.y * a.y + a.z * a.z);
        public static double dist(Vec3d a, Vec3d b) => System.Math.Sqrt(
            (a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y) + (a.z - b.z) * (a.z - b.z)
        );
        public static double distXZ(Vec3d a, Vec3d b) => System.Math.Sqrt(
            (a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z)
        );
        public static Vec3d norm(Vec3d a)
        {
            double l = len(a);
            if (l < 1e-12) l = 1;
            return new Vec3d(a.x / l, a.y / l, a.z / l);
        }
        public static Vec3d lerp(Vec3d a, Vec3d b, double t) => new Vec3d(
            a.x + (b.x - a.x) * t,
            a.y + (b.y - a.y) * t,
            a.z + (b.z - a.z) * t
        );
        // 两个方向的夹角（度）
        public static double angleDeg(Vec3d a, Vec3d b) => angleDeg(a.x, a.y, a.z, b.x, b.y, b.z);
        // 标量版（瞄准每帧对每个目标调用：不建临时 Vec3d；归一化、点乘顺序与原来 dot(norm(a), norm(b)) 一致）
        public static double angleDeg(double ax, double ay, double az, double bx, double by, double bz)
        {
            double la = System.Math.Sqrt(ax * ax + ay * ay + az * az); if (la < 1e-12) la = 1;
            double lb = System.Math.Sqrt(bx * bx + by * by + bz * bz); if (lb < 1e-12) lb = 1;
            double c = (ax / la) * (bx / lb) + (ay / la) * (by / lb) + (az / la) * (bz / lb);
            c = c < -1 ? -1 : c > 1 ? 1 : c;
            return System.Math.Acos(c) / DEG;
        }
        // 点到射线的夹角（度）：从 origin 看 p 与 dir 的偏离
        public static double angleTo(Vec3d origin, Vec3d dir, Vec3d p) => angleDeg(dir.x, dir.y, dir.z, p.x - origin.x, p.y - origin.y, p.z - origin.z);
        public static double clamp(double x, double a, double b) => x < a ? a : x > b ? b : x;
        // 绕 Y 轴旋转（弧度）
        public static Vec3d rotY(Vec3d a, double r)
        {
            double c = System.Math.Cos(r), s = System.Math.Sin(r);
            return new Vec3d(a.x * c + a.z * s, a.y, -a.x * s + a.z * c);
        }
    }
}
