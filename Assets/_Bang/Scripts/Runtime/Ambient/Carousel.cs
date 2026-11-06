using UnityEngine;

namespace Bang.Ambient
{
    // 旋转木马：每 period 秒里转 spinTime 秒（缓起缓停），马儿由曲轴带着随转盘角度上下起伏；玩法里转动期间门里不断飘出气球兵，打摇柄可以提前停。
    public class Carousel : MonoBehaviour
    {
        public Transform rotor;                  // 顶棚 + 马儿
        public Transform[] horses;
        public float period = 20f, spinTime = 6f, maxDegPerSec = 55f, bob = 0.06f;
        public float timeOffset = 3f;
        public bool Spinning { get; private set; }
        float _stopUntil = -1f, _speed, _rotorAngle;
        Vector3[] _base;

        void OnEnable()
        {
            if (horses == null) return;
            _base = new Vector3[horses.Length];
            for (int i = 0; i < horses.Length; i++) if (horses[i]) _base[i] = horses[i].localPosition;
        }

        /// <summary>摇柄被打中：立刻刹车，直到本轮结束都不转。</summary>
        public void ForceStop() { _stopUntil = Time.time + spinTime; }

        void Update()
        {
            if (!Application.isPlaying || !rotor) return;
            float t = Mathf.Repeat(Time.time + timeOffset, period);
            bool want = t < spinTime && Time.time > _stopUntil;
            Spinning = want;
            _speed = Mathf.MoveTowards(_speed, want ? maxDegPerSec : 0f, maxDegPerSec * Time.deltaTime / 1.2f);   // 1.2 秒加速/刹停
            rotor.Rotate(0, -_speed * Time.deltaTime, 0, Space.Self);              // 俯视逆时针转（马头朝向就是逆时针的切线方向；原来顺时针转，马在倒着跑）
            if (horses == null || _base == null) return;
            // 马的起伏由顶棚里的曲轴带动：高度跟转盘转过的角度走（转一圈起伏 3 次），转盘停下马也停在当时的高度
            _rotorAngle += _speed * Time.deltaTime;
            for (int i = 0; i < horses.Length; i++)
                if (horses[i]) horses[i].localPosition = _base[i] + Vector3.up * Mathf.Sin(_rotorAngle * Mathf.Deg2Rad * 3f + i * 2.1f) * bob;
        }
    }
}
