using UnityEngine;

namespace Bang.Ambient
{
    // 摇摇马 / 不倒翁：绕本地轴前后摇
    public class Rock : MonoBehaviour
    {
        public Vector3 axis = Vector3.right;
        public float degrees = 9f, speed = 1.6f, phase;
        Quaternion _rest;
        void OnEnable() => _rest = transform.localRotation;
        void Update()
        {
            if (!Application.isPlaying) return;
            transform.localRotation = _rest * Quaternion.AngleAxis(Mathf.Sin(Time.time * speed + phase) * degrees, axis);
        }
    }
}
