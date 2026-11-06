using UnityEngine;

namespace Bang.Ambient
{
    // 陀螺：spinner 绕自身轴快速自转；本物体（陀螺尖着地的点）让转轴倾斜一个小角度、并慢慢绕竖直方向画圈（进动），
    // 进动角速度 ≈ 常数 / 自转角速度（转得越快画圈越慢、倾角越小）。
    public class Precess : MonoBehaviour
    {
        public Transform spinner;
        public float spinDegPerSec = 240f, tiltDeg = 5f;
        float _spin, _prec;
        void Update()
        {
            if (!Application.isPlaying) return;
            float w = Mathf.Abs(spinDegPerSec);
            _spin += spinDegPerSec * Time.deltaTime;
            _prec += 9000f / Mathf.Max(w, 60f) * Mathf.Sign(spinDegPerSec) * Time.deltaTime;
            float tilt = tiltDeg * Mathf.Clamp(240f / Mathf.Max(w, 60f), 0.5f, 2f);
            transform.localRotation = Quaternion.Euler(0, _prec, 0) * Quaternion.Euler(tilt, 0, 0) * Quaternion.Euler(0, -_prec, 0);
            if (spinner) spinner.localRotation = Quaternion.Euler(0, _spin, 0);
        }
    }
}
