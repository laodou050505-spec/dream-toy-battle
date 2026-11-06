// 全局画面调色：真机不开后处理，对比度和饱和度只能在材质里拉。所有 Bang/ToyLit 材质读全局 _ToyGrade：
//   x 边缘光倍率（白边太强会把轮廓冲淡、画面发白）  y 环境光倍率（抬暗部，太高会发平）  z 饱和度  w 亮度
using UnityEngine;

namespace Bang
{
    [ExecuteAlways]
    public class ToyLook : MonoBehaviour
    {
        [Range(0, 1.5f)] public float rimScale = 0.45f;
        [Range(0, 1.5f)] public float ambientScale = 0.72f;
        [Range(0.5f, 1.6f)] public float saturation = 0.92f;
        [Range(0.6f, 1.3f)] public float exposure = 1.0f;
        [Tooltip("所有塑料高光的总倍率（用户：反光不要那么强）"), Range(0, 1f)] public float glossScale = 0.42f;
        [Tooltip("细颗粒：表面像磨砂塑料 / 绒面，不再是平涂"), Range(0, 0.5f)] public float grainFine = 0.16f;
        [Tooltip("大尺度斑驳：墙面、地面有手工上色的深浅变化"), Range(0, 0.5f)] public float grainBroad = 0.17f;
        static readonly int Id = Shader.PropertyToID("_ToyGrade"), Id2 = Shader.PropertyToID("_ToyGrade2"), IdGrain = Shader.PropertyToID("_ToyGrain");
        void OnEnable() => Apply();
        void OnValidate() => Apply();
        public void Apply()
        {
            Shader.SetGlobalVector(Id, new Vector4(rimScale, ambientScale, saturation, exposure));
            Shader.SetGlobalVector(Id2, new Vector4(glossScale, grainFine, grainBroad, 0));
            var grain = Resources.Load<Texture2D>("ToyGrain"); if (grain) Shader.SetGlobalTexture(IdGrain, grain);
        }
        void OnDisable() => Shader.SetGlobalVector(Id, Vector4.zero);
    }
}
