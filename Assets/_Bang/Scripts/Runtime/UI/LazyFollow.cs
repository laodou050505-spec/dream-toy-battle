// 懒跟随锚点（XR 舒适度）：界面固定在世界里，不跟着头一起动；只有玩家转开超过 angle 度时，才慢慢转回到视线正前方。
// 高度跟随眼睛高度（坐着 / 站着都合适），距离固定。
using UnityEngine;

namespace Bang.Play.UI
{
    public class LazyFollow : MonoBehaviour
    {
        public Transform head;
        public float distance = 1.8f, heightOffset = -0.1f, angle = 40f, speed = 2.2f;
        float _yaw; bool _init, _moving;

        public void Snap()
        {
            if (!head) return;
            var f = head.forward; f.y = 0; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            _yaw = Quaternion.LookRotation(f).eulerAngles.y; _init = true; Place(1f);
        }

        void LateUpdate()
        {
            if (!head) { var c = Camera.main; if (c) head = c.transform; else return; }
            if (!_init) { Snap(); return; }
            var f = head.forward; f.y = 0; if (f.sqrMagnitude < 1e-4f) return;
            float want = Quaternion.LookRotation(f).eulerAngles.y;
            float d = Mathf.DeltaAngle(_yaw, want);
            if (Mathf.Abs(d) > angle) _moving = true;
            if (_moving)
            {
                _yaw = Mathf.MoveTowardsAngle(_yaw, want, Mathf.Abs(d) * speed * Time.unscaledDeltaTime + 10f * Time.unscaledDeltaTime);
                if (Mathf.Abs(Mathf.DeltaAngle(_yaw, want)) < 2f) _moving = false;
            }
            Place(Time.unscaledDeltaTime * 4f);
        }

        void Place(float k)
        {
            var rot = Quaternion.Euler(0, _yaw, 0);
            var target = new Vector3(head.position.x, head.position.y + heightOffset, head.position.z) + rot * Vector3.forward * distance;
            transform.position = Vector3.Lerp(transform.position, target, Mathf.Clamp01(k));
            transform.rotation = rot;
        }
    }
}
