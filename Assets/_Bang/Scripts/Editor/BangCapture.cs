// 截图审查：在指定机位临时建一台相机（带 URP 后期），渲染成 PNG。
//   BangCapture.Shot("/tmp/a.png", new Vector3(0,1.6,0), new Vector3(4,180,0), 70, 1600, 1000);
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Bang.EditorTools
{
    public static class BangCapture
    {
        public static void Shot(string path, Vector3 pos, Vector3 euler, float fov = 70, int w = 1600, int h = 1000)
        {
            var go = new GameObject("__CaptureCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = go.AddComponent<Camera>();
            var main = Camera.main;
            if (main) { cam.CopyFrom(main); }
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 100;
            var data = go.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;   // MR 透视下后处理会破坏 alpha 且耗性能，真机不用后处理，画面靠材质与光照
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        }
    }
}
