// 气球靶的摆动：气球是浮力来源（轻轻上下浮动，周期 2.6 秒），靶盘像单摆一样挂在绳子下端——
// 周期由绳长决定 T = 2π√(L/g)（0.22 米 ≈ 0.94 秒），平时只有极小的余摆；被打中时朝子弹方向踢一下，再阻尼衰减。
using UnityEngine;

namespace Bang.Play.Motion
{
    public class TargetSway : MonoBehaviour
    {
        public Transform board;           // 靶盘（绕挂点摆）
        public Transform balloon;         // 气球 + 绳子（原点在挂点）
        public float ropeLength = 0.22f;
        public float damping = 1.2f;
        float _angX, _velX, _angZ, _velZ, _t;
        Vector3 _hook0;

        void Start() { _t = Random.value * 10f; _hook0 = balloon ? balloon.localPosition : Vector3.up * 0.17f; }

        public void Hit(Vector3 dirWorld, float strength = 1f)
        {
            var d = transform.InverseTransformDirection(dirWorld);
            _velX += d.z * 90f * strength;     // 往后摆
            _velZ -= d.x * 90f * strength;
        }

        void Update()
        {
            float dt = Time.deltaTime; _t += dt;
            float w2 = 9.81f / Mathf.Max(0.05f, ropeLength);                 // ω² = g / L
            _velX += (-w2 * _angX - damping * _velX) * dt; _angX += _velX * dt;
            _velZ += (-w2 * _angZ - damping * _velZ) * dt; _angZ += _velZ * dt;
            float bob = Mathf.Sin(_t * Mathf.PI * 2f / 2.6f) * 0.01f;
            float idle = Mathf.Sin(_t * Mathf.PI * 2f / 0.94f) * 1.2f;        // 余摆：和单摆同周期
            // 整个靶子绕挂点摆（挂点 = 绳子下端），气球带着挂点一起上下浮
            var hook = _hook0 + Vector3.up * bob;
            if (balloon) balloon.localPosition = hook;
            if (board)
            {
                var rot = Quaternion.Euler(_angX + idle, 0, _angZ);
                board.localRotation = rot;
                board.localPosition = hook + rot * (-_hook0);
            }
            if (balloon) balloon.localRotation = Quaternion.Euler(-(_angX + idle) * 0.3f, 0, -_angZ * 0.3f);
        }
    }
}
