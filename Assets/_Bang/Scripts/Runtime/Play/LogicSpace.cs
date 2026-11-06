// 规则层（右手系、正前方 −Z）和 Unity（左手系）之间的坐标转换：玩家都朝 −Z、门都在 z=−3.2，只需要把 x 取反。
// 朝向：规则层 yaw = atan2(dx, dz)（规则坐标），对应 Unity 绕 y 轴的角度为 −yaw（度）。
using Bang.Logic;
using UnityEngine;

namespace Bang.Play
{
    public static class LogicSpace
    {
        public static Vector3 ToUnity(Vec3d v) => v == null ? Vector3.zero : new Vector3(-(float)v.x, (float)v.y, (float)v.z);
        public static Vec3d ToLogic(Vector3 v) => new Vec3d(-v.x, v.y, v.z);
        public static float YawDeg(double logicYaw) => -(float)logicYaw * Mathf.Rad2Deg;
    }
}
