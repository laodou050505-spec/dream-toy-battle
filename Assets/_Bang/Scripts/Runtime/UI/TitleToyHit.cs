// 封面上能被枪打中的独立物件：挨一枪被打得往后弹、转一下，再弹回原位（阻尼振荡）。
// 叠加在原有动画（漂动 / 表演）之上，不抢它们的位置控制。
using UnityEngine;

namespace Bang.Play.UI
{
    public class TitleToyHit : MonoBehaviour
    {
        const float Stiff = 90f, Damp = 7f;                       // 弹簧：约 0.7 秒回稳
        Vector3 _off, _vel, _spin, _spinV, _lastOff; Quaternion _lastRot = Quaternion.identity;
        Vector3 _written; Quaternion _writtenR; bool _has;

        /// <summary>dir = 世界空间的子弹方向。</summary>
        public void Hit(Vector3 dir)
        {
            var p = transform.parent;
            _vel += (p ? p.InverseTransformDirection(dir) : dir).normalized * 1.1f;
            _spinV += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized * 320f;
        }

        void LateUpdate()
        {
            // 别的脚本这一帧没改过我们（没有漂动）：先撤掉上一帧叠加的量；改过了就是新的基准
            if (_has && transform.localPosition == _written && transform.localRotation == _writtenR)
            {
                transform.localPosition -= _lastOff;
                transform.localRotation *= Quaternion.Inverse(_lastRot);
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            _vel += (-Stiff * _off - Damp * _vel) * dt; _off += _vel * dt;
            _spinV += (-Stiff * _spin - Damp * _spinV) * dt; _spin += _spinV * dt;
            if (_off.sqrMagnitude < 1e-8f && _vel.sqrMagnitude < 1e-6f && _spin.sqrMagnitude < 1e-3f && _spinV.sqrMagnitude < 1e-1f) { _off = _vel = _spin = _spinV = Vector3.zero; _has = false; return; }
            _lastOff = _off; _lastRot = Quaternion.Euler(_spin);
            transform.localPosition += _lastOff; transform.localRotation *= _lastRot;
            _written = transform.localPosition; _writtenR = transform.localRotation; _has = true;
        }
    }
}
