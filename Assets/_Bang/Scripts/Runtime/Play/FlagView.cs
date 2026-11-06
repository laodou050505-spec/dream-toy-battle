// 一面旗子的表现：
//   插下：从上方扎进地里（加速落下 → 触地压扁 → 弹回），旗布被震起一串波；
//   挨打：朝子弹方向晃（阻尼弹簧，像不倒翁），旗布再抖一下；血量环受伤后出现；
//   被打倒：朝受力方向侧倒（像真的倒下：先慢后快，触地弹一下），然后缩没；
//   玩具化：在房间里加一个覆盖种子，半径随旗子的玩具化贡献增长；被打倒后跟着贡献一起退回去。
using Bang.Logic;
using Bang.Play.Motion;
using UnityEngine;

namespace Bang.Play
{
    public class FlagView : MonoBehaviour
    {
        public int id;
        public Flag data;
        Transform _pivot;                 // 倒下 / 晃动绕底座转
        ClothWave _cloth;
        RingView _ring;
        int _seed = -1;
        float _age, _tiltVel; Vector3 _tiltAxis = Vector3.right; float _tilt;
        float _fallT = -1f; Vector3 _fallAxis = Vector3.right;
        bool _hidden;

        public void Init(Flag f, Material ringMat, Material blobMat = null)
        {
            id = f.id; data = f;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (blobMat) BlobShadow.Attach(transform, blobMat, f.type == "big" ? 0.14f : 0.1f);
            // 预制体挂到一个枢轴下（枢轴在底座中心，倒下绕它转）
            _pivot = new GameObject("Pivot").transform; _pivot.SetParent(transform, false);
            for (int i = transform.childCount - 1; i >= 0; i--) { var c = transform.GetChild(i); if (c != _pivot) c.SetParent(_pivot, false); }
            _cloth = GetComponentInChildren<ClothWave>();
            var fp = LogicSpace.ToUnity(f.pos); fp.y += GroundProbe.Height(fp, false, 0.06f);   // 旗子插在积木路上时立在路面
            transform.position = fp;
            var cam = Camera.main;
            if (cam) { var d = cam.transform.position - transform.position; d.y = 0; if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(Quaternion.Euler(0, Random.Range(-50f, 50f), 0) * d); }
            float h = f.type == "big" ? 0.78f : 0.52f;
            _ring = RingView.Create(transform, ringMat, f.type == "big" ? 0.13f : 0.1f);
            _ring.transform.localPosition = new Vector3(0, h + 0.08f, 0);
            _ring.SetColor(new Color(0.45f, 0.75f, 1f, 1));
            _ring.Snap(1);
            _hidden = f.hidden;
            _pivot.localScale = Vector3.zero;
            var toy = RoomToyification.Instance;
            if (toy) _seed = toy.AddSeed(transform.position, 0.15f);
            PlayFx.Instance?.Dust(transform.position, 1f, 6);
        }

        public void Sync(float dt)
        {
            var f = data;
            _age += dt;
            // 插下：0~0.12 秒从 0.25 米高加速扎下，触地后压扁再弹回（0.12~0.4 秒）
            if (_fallT < 0)
            {
                float t = _age;
                float y = t < 0.12f ? 0.25f * (1f - (t / 0.12f) * (t / 0.12f)) : 0f;
                float sq = t < 0.12f ? -0.1f : t < 0.4f ? Mathf.Sin((t - 0.12f) / 0.28f * Mathf.PI * 2f) * 0.18f * (1f - (t - 0.12f) / 0.28f) : 0f;
                _pivot.localPosition = new Vector3(0, y, 0);
                _pivot.localScale = new Vector3(1 + sq * 0.5f, 1 - sq, 1 + sq * 0.5f);
                if (t >= 0.12f && t - dt < 0.12f) { _cloth?.Kick(1f); PlayFx.Instance?.Dust(transform.position, 1.2f, 8); }
                // 挨打晃动：阻尼弹簧
                _tiltVel += (-_tilt * 160f - _tiltVel * 9f) * dt;
                _tilt += _tiltVel * dt;
                _pivot.localRotation = Quaternion.AngleAxis(_tilt, transform.InverseTransformDirection(_tiltAxis));
            }
            else
            {
                // 侧倒：重力式加速（角度 ∝ t²），0.45 秒到 88°，触地弹一下；1 秒后开始缩没
                _fallT += dt;
                float k = Mathf.Clamp01(_fallT / 0.45f);
                float ang = 88f * k * k;
                if (_fallT > 0.45f) ang = 88f - Mathf.Abs(Mathf.Sin((_fallT - 0.45f) * 18f)) * 9f * Mathf.Exp(-(_fallT - 0.45f) * 7f);
                _pivot.localRotation = Quaternion.AngleAxis(ang, transform.InverseTransformDirection(_fallAxis));
                if (_fallT > 0.45f && _fallT - dt <= 0.45f) PlayFx.Instance?.Dust(transform.position + Vector3.Cross(_fallAxis, Vector3.up) * -0.25f, 1f, 6);
                float s = _fallT < 1f ? 1f : Mathf.Max(0, 1f - (_fallT - 1f) / 0.6f);
                _pivot.localScale = Vector3.one * s;
            }
            // 玩具化种子：贡献越多覆盖越大；倒下后随贡献一起退
            var toy = RoomToyification.Instance;
            if (toy && _seed >= 0)
            {
                float share = f.cap > 0 ? (float)(f.amount / f.cap) : 0;
                toy.SetTarget(_seed, (f.dying ? (float)f.radius * share : Mathf.Max(0.15f, (float)f.radius * share)) * 1.8f);   // 画面上放大 1.8 倍：插下的旗子周围一大圈明显变成玩具世界
            }
        }

        public void OnHit(Vector3 dirWorld, double hp, double maxHp)
        {
            dirWorld.y = 0; if (dirWorld.sqrMagnitude < 1e-4f) dirWorld = -transform.forward;
            _tiltAxis = Vector3.Cross(Vector3.up, dirWorld.normalized);
            _tiltVel += 160f;
            _cloth?.Kick(0.6f);
            _ring.Show(true);
            _ring.SetFill((float)(hp / maxHp), true);
        }

        public void Fall(Vector3 dirWorld)
        {
            if (_fallT >= 0) return;
            dirWorld.y = 0; if (dirWorld.sqrMagnitude < 1e-4f) dirWorld = transform.right;
            _fallAxis = Vector3.Cross(Vector3.up, dirWorld.normalized);
            _fallT = 0;
            _ring.Show(false);
            _cloth?.Kick(1f);
        }

        void OnDestroy()
        {
            var toy = RoomToyification.Instance;
            if (toy && _seed >= 0) toy.RemoveSeed(_seed);
        }
    }
}
