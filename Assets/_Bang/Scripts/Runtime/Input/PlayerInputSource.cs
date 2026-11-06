// 玩家输入源：每帧给规则层一份 FrameInput（规则坐标），并告诉表现层每只手的枪该摆在哪（Unity 世界坐标）。
// 编辑器里用 DesktopInput（鼠标 = 视线、按键模拟手势），头显里用 PicoInput（手部追踪 + 眼动）。
using Bang.Logic;
using UnityEngine;

namespace Bang.Play.Input
{
    public abstract class PlayerInputSource : MonoBehaviour
    {
        /// <summary>本帧输入（规则坐标）。手势事件只在这一帧给出一次。</summary>
        public abstract FrameInput Poll(float dt);

        /// <summary>枪的位姿（枪原点在握把顶部、+Z 朝枪口）。这只手没在持枪时返回 false。</summary>
        public abstract bool GunPose(string side, out Vector3 pos, out Quaternion rot);

        /// <summary>视线（世界坐标），给准星 / 调试用。</summary>
        public abstract Ray GazeWorld { get; }

        /// <summary>双枪解锁后左手也能持枪。</summary>
        public bool dual;
    }
}
