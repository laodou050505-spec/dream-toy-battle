// 打包守卫：Gradle 工程生成后（在 PICO SDK 的清单注入之后）检查 AndroidManifest 里有没有 VR 应用标记。
// 缺了 pvr.app.type / 手部追踪权限，头显会把游戏当成 2D 窗口跑（不是 MR），所以直接让打包失败。
#if UNITY_ANDROID
using System.IO;
using UnityEditor.Android;
using UnityEditor.Build;

namespace Bang.EditorTools
{
    internal class BangManifestGuard : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10000;          // 排在 PICO 的 PXR_Manifest（默认 0）之后

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var m = File.ReadAllText(Path.Combine(path, "src/main/AndroidManifest.xml"));
            foreach (var need in new[] { "pvr.app.type", "com.picovr.permission.HAND_TRACKING", "com.picovr.permission.EYE_TRACKING" })
                if (!m.Contains(need))
                    throw new BuildFailedException($"[Bang] AndroidManifest 缺少 {need}：PICO 清单注入没生效，装到头显会变成 2D 窗口。先确认 XR 加载器（PICO）已启用、平台已是 Android 并完成编译，再重新打包。");
        }
    }
}
#endif
