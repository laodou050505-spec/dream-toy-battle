using UnityEngine;

namespace Bang.Ambient
{
    // 传送带上的小玩具：沿本地 X 从 -halfLength 走到 +halfLength，走到头回到起点；一边走一边左右摇晃（发条玩具的小碎步）
    public class BeltRider : MonoBehaviour
    {
        public Transform[] riders;
        public float halfLength = 0.75f, speed = 0.125f, speedMul = 1f, wobble = 6f;
        float[] _x; Quaternion[] _rot; Vector3[] _scale;
        void OnEnable()
        {
            if (riders == null) return;
            _x = new float[riders.Length]; _rot = new Quaternion[riders.Length]; _scale = new Vector3[riders.Length];
            for (int i = 0; i < riders.Length; i++) if (riders[i]) { _x[i] = riders[i].localPosition.x; _rot[i] = riders[i].localRotation; _scale[i] = riders[i].localScale; }
        }
        void Update()
        {
            if (!Application.isPlaying || riders == null || _x == null) return;
            for (int i = 0; i < riders.Length; i++)
            {
                var r = riders[i]; if (!r) continue;
                _x[i] += speed * speedMul * Time.deltaTime;
                if (_x[i] > halfLength) _x[i] -= halfLength * 2; else if (_x[i] < -halfLength) _x[i] += halfLength * 2;
                var p = r.localPosition; p.x = _x[i]; r.localPosition = p;
                // 两端是进料口 / 出料口：玩具在最后 12cm 缩进去、在开头 12cm 冒出来（不是凭空瞬移）
                float edge = Mathf.Min(_x[i] + halfLength, halfLength - _x[i]);
                r.localScale = _scale[i] * Mathf.SmoothStep(0, 1, Mathf.Clamp01(edge / 0.12f));
                r.localRotation = _rot[i] * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 9f + i) * wobble);
            }
        }
    }
}
