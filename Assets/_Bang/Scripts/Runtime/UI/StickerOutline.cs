// 封面描边：挂在封面根节点上，给所有网格加反向外壳（Bang/ToyOutline，深梅紫），沿平滑法线外推（用户：有描边就要全部都有）。
// 只跳过自带轮廓的（按钮、标题字、放射底）、细线、眼珠和低级 LOD。
// 线宽统一（用户：封面上所有描边看起来要一样）；宽度是视角值，远近看起来一样粗。
// 舞台玩具和漂浮物的外壳沿视线往后推一点——外轮廓衬着背景还在，脸和图案上的细小凹凸被自身挡住（用户：脸上有描边不好看）。
using System.Collections.Generic;
using UnityEngine;

namespace Bang.Play.UI
{
    public class StickerOutline : MonoBehaviour
    {
        public Material mat;
        public string[] skip = { "Sunburst", "Label", "Outline", "Glow", "Halo", "Pool", "Beam", "Backdrop", "String", "Eye_", "Glint", "LOD1", "LOD2", "BtnStart", "Block_", "Plaque", "PracticeSign", "Char_", "Logo_EN" };
        const float Width = 0.0022f, DepthPush = 0.03f;   // 线宽：视角值（FarBoost：远近看起来一样粗）
        bool _done;

        void Start() => Apply();

        /// <summary>网格所属的独立物件：漂浮物 / 舞台玩具 / 标题上的物件 / 剧场部件 / 单颗灯泡。</summary>
        Transform ActorOf(Transform t)
        {
            for (; t.parent && t.parent != transform; t = t.parent)
            {
                var p = t.parent.name;
                if (p == "Floaters" || p == "Cast" || p == "Sign" || p == "Theater" || p == "Bulbs" || p == "FootBulbs") break;
            }
            return t;
        }

        public void Apply()
        {
            if (_done || !mat) return; _done = true;
            foreach (var mr in GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.GetComponent<TMPro.TMP_Text>() || mr.name.EndsWith("_sticker")) continue;
                bool bad = false;
                for (var t = mr.transform; t && t != transform.parent; t = t.parent)
                    foreach (var k in skip) if (t.name.Contains(k)) { bad = true; break; }
                if (bad) continue;
                var mf = mr.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                var g = new GameObject(mr.name + "_sticker"); g.transform.SetParent(mr.transform, false);
                g.AddComponent<MeshFilter>().sharedMesh = EnemyView.Smoothed(mf.sharedMesh);
                var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                var actor = ActorOf(mr.transform);
                var block = new MaterialPropertyBlock();
                block.SetFloat("_Width", Width);       // 所有物件同一线宽（用户：封面上所有描边看起来要一样；原来按体量算，手枪 / 礼盒细、熊粗，差近 3 倍）
                var group = actor.parent ? actor.parent.name : "";
                if (group == "Floaters" || group == "Cast" || group == "Sign") block.SetFloat("_DepthPush", DepthPush);
                r.SetPropertyBlock(block);
            }
        }
    }
}
