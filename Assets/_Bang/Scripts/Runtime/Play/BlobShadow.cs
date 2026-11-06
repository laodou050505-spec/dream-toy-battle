// 脚下的软影子（代替实时阴影）：MR 里真实地板收不到实时阴影，小怪会像飘着；软影子在真实地板上也能把它们“按”在地上，
// 而且比每个零件都投实时阴影省得多。跳起来时影子变小变淡（离地越高越小），落地时恢复——跟着身体高度走。
using UnityEngine;

namespace Bang.Play
{
    public class BlobShadow : MonoBehaviour
    {
        public Transform follow;          // 身体（读它离地的高度）
        public float radius = 0.2f, alpha = 0.38f;
        MeshRenderer _r; MaterialPropertyBlock _mpb;
        static readonly int IdColor = Shader.PropertyToID("_Color");

        public static BlobShadow Attach(Transform parent, Material mat, float radius, Transform follow = null)
        {
            var g = new GameObject("BlobShadow"); g.transform.SetParent(parent, false);
            g.transform.localPosition = new Vector3(0, 0.004f, 0); g.transform.localRotation = Quaternion.Euler(90, 0, 0);
            g.AddComponent<MeshFilter>().sharedMesh = PlayFx.Quad;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            var b = g.AddComponent<BlobShadow>(); b._r = r; b._mpb = new MaterialPropertyBlock(); b.radius = radius; b.follow = follow;
            b.Apply(0);
            return b;
        }

        void LateUpdate()
        {
            float h = follow ? Mathf.Max(0, follow.position.y - transform.parent.position.y) : 0;
            Apply(h);
            // 影子永远贴地、水平（父物体可能在倒下 / 摆动）
            var p = transform.parent.position; transform.position = new Vector3(p.x, 0.004f, p.z);
            transform.rotation = Quaternion.Euler(90, 0, 0);
        }

        void Apply(float height)
        {
            float k = Mathf.Clamp01(1f - height * 4f);
            float s = radius * 2f * Mathf.Lerp(0.7f, 1f, k);
            var ps = transform.parent ? transform.parent.lossyScale.x : 1f;
            transform.localScale = Vector3.one * s / Mathf.Max(0.01f, ps);
            _mpb.SetColor(IdColor, new Color(0.25f, 0.18f, 0.32f, alpha * Mathf.Lerp(0.5f, 1f, k)));
            _r.SetPropertyBlock(_mpb);
        }
    }
}
