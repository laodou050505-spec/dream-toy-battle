// 小怪的零件骨架（不用骨骼）：预制体里各零件挂在自己的关节枢轴上，EnemyMotion 按动画状态驱动它们。
// 层级约定（BangEnemyPrefabs 生成）：
//   Root（位置、朝向由规则层同步）
//     Body（整体弹跳、压扁拉长、受击后仰）
//       FootL / FootR（胯）   Pieces/Piece_0..n（身体积木，自下而上）
//       Upper（积木顶面；掉一块积木就往下落一块的高度）
//         Head（脖子）  ArmL / ArmR（肩）  Pack（背旗）
//       发条老鼠另有：Key（背上发条钥匙）、Tail_0..n（尾巴链，逐节滞后）、Feet_0..3
using UnityEngine;

namespace Bang.Play
{
    public enum MoveStyle { Hop, Scurry, Stomp, Slide, Fly, Float, Drive, Pop }

    public class EnemyRig : MonoBehaviour
    {
        public MoveStyle style = MoveStyle.Hop;
        public Transform body, upper, head, armL, armR, footL, footR, pack, key;
        [Tooltip("弹簧小丑盒：弹出来的小丑、弹簧")] public Transform pop, spring;
        [Tooltip("发条钥匙的安装朝向（杆从后背斜着往后伸），转动时绕杆本身（本地 Y）转")] public Quaternion keyBase = Quaternion.identity;
        public Transform[] pieces;            // 身体积木（= 血量刻度），自下而上
        public float pieceHeight = 0.087f;    // 每块积木的高度（掉一块，Upper 往下落这么多）
        public Transform[] tail;              // 尾巴链（根 → 尖）
        public Transform[] feet;              // 老鼠的小脚
        [Tooltip("一跳的步长（米）：跳跃节奏跟着实际移动距离走，脚不打滑")] public float hopLength = 0.16f;
        public float hopHeight = 0.045f;
        [Tooltip("走路时脚的摆动比例：脚长的小怪要小一点，脚尖不戳进地面")] public float footSwing = 0.25f;
        [Tooltip("待机时手臂也举起这么多（0～1）：拿枪的敌人平时半举着枪，一眼看出它会开枪")] public float holdAim;
        [HideInInspector] public Vector3 upperRest, headRest, packRest, popRest;

        void Awake()
        {
            if (upper) upperRest = upper.localPosition;
            if (head) headRest = head.localPosition;
            if (pack) packRest = pack.localPosition;
            if (pop) popRest = pop.localPosition;
        }
    }
}
