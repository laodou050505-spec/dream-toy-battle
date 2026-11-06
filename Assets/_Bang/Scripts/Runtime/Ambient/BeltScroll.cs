using UnityEngine;

namespace Bang.Ambient
{
    // 传送带皮带：用 MaterialPropertyBlock 滚动 _BaseMap_ST 的偏移（不改共享材质）。speedMul 由玩法设置（发条钥匙打中时反转）。
    [RequireComponent(typeof(Renderer))]
    public class BeltScroll : MonoBehaviour
    {
        public float speed = 0.25f;          // UV/秒（皮带 UV 每米 2 个单位）
        public float speedMul = 1f;
        Renderer _r; MaterialPropertyBlock _mpb; float _off;
        static readonly int Id = Shader.PropertyToID("_BaseMap_ST");
        void OnEnable() { _r = GetComponent<Renderer>(); _mpb = new MaterialPropertyBlock(); }
        void Update()
        {
            if (!Application.isPlaying) return;
            _off = Mathf.Repeat(_off + speed * speedMul * Time.deltaTime, 1f);
            _r.GetPropertyBlock(_mpb);
            _mpb.SetVector(Id, new Vector4(1, 1, -_off, 0));
            _r.SetPropertyBlock(_mpb);
        }
    }
}
