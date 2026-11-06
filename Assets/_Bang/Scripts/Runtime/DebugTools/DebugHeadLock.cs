// 截图验收用：编辑器里把头（主相机）锁在指定朝向，排在所有脚本之前和渲染前各写一次（盖过 TrackedPoseDriver / DesktopInput）。真机不挂。
using UnityEngine;

namespace Bang.Play
{
    [DefaultExecutionOrder(-10000)]
    public class DebugHeadLock : MonoBehaviour
    {
        public float yaw = 180f, pitch = 5f;
        void OnEnable() => Application.onBeforeRender += Apply;
        void OnDisable() => Application.onBeforeRender -= Apply;
        void Update() => Apply();
        void LateUpdate() => Apply();
        void Apply() { var c = Camera.main; if (c) c.transform.rotation = Quaternion.Euler(pitch, yaw, 0); }
    }
}
