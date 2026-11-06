// 第 6 章：糖果拱门 + 门后世界「玩具箱」（夜晚的大玩具箱内部；玩法上门会换位置、前几章机关轮换出现）。
using System.Collections.Generic;
using Bang.Ambient;
using Bang.Portal;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bang.EditorTools
{
    public static partial class BangSceneBuilder
    {
        public static readonly Dictionary<string, (float h, float gloss, float sheen)> W6Sizes = new()
        {
            ["W6_DuckBeanie"] = (0.62f, 0.7f, 0.2f), ["W6_YarnBall"] = (0.28f, 0.02f, 0.8f), ["W6_GummyBear"] = (0.16f, 0.9f, 0),
            ["W6_WrappedCandy"] = (0.1f, 0.9f, 0), ["W6_NightLight"] = (0.45f, 0.5f, 0),
        };

        public static GameObject BuildPortalW6(Transform root, Vector3 pos, int stencilRef)
        {
            var portal = new GameObject("Portal_W6");
            portal.transform.SetParent(root, false);
            portal.transform.localPosition = pos;
            var frame = new GameObject("Frame_Candy").transform; frame.SetParent(portal.transform, false);
            Inst(Proc + "/W6_CandyArch.fbx", frame, BangArt.Mat("M_CandyVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.8f); m.SetFloat("_GlossSize", 60); m.SetFloat("_RimStrength", 0.5f); }));
            foreach (var (p, yaw) in new[] { (new Vector3(-0.98f, 0, 0.36f), 20f), (new Vector3(1.05f, 0, 0.3f), -25f) }) Place("W6_GummyBear", frame, p, yaw);
            foreach (var (p, yaw) in new[] { (new Vector3(-0.8f, 0, 0.5f), 70f), (new Vector3(0.85f, 0, 0.5f), -30f), (new Vector3(1.15f, 0, 0.45f), 10f) }) Place("W6_WrappedCandy", frame, p, yaw);
            PortalOpening(portal.transform, 6, stencilRef);   // 不规则虫洞门洞（遮罩 + 涡旋膜 + 发光内沿）

            var w = new GameObject("World_ToyBox");
            w.transform.SetParent(portal.transform, false);
            SkyAndBackdrop(w.transform, "W6", new Color(0.2f, 0.18f, 0.45f), new Color(0.5f, 0.38f, 0.7f));   // 箱口上方：360° 夜空布景
            Inst(Proc + "/W6_CloudRug.fbx", w.transform, BangArt.FeltVC);
            _groundRoot = null;
            var props = new GameObject("Props").transform; props.SetParent(w.transform, false);
            GameObject P(string n, float x, float z, float yaw) => Place(n, props, new Vector3(x, 0, z), yaw);

            // 大蜡笔：右后方立着一排（略微歪斜）
            int ci = 0;
            foreach (var (c, x, z, tilt) in new[] { ("Yellow", 1.7f, -4.4f, 3f), ("Blue", 2.0f, -4.5f, -2f), ("Pink", 2.3f, -4.35f, 4f), ("Mint", 2.6f, -4.55f, -5f) })
            {
                var g = Inst($"{Proc}/W6_Crayon_{c}.fbx", props, BangArt.Mat("M_CrayonVC", m => { m.SetFloat("_UseVertexColor", 1); m.SetFloat("_Gloss", 0.25f); m.SetFloat("_GlossSize", 18); }));
                if (!g) continue;
                g.transform.localPosition = new Vector3(x, 0, z);
                g.transform.localRotation = Quaternion.Euler(tilt, ci++ * 37f, tilt * 0.6f);
            }
            // 陀螺：在地毯边上旋转，转轴进动
            foreach (var (n, x, z, spd) in new[] { ("W6_Top_A", -1.3f, -2.4f, 240f), ("W6_Top_B", 0.95f, -2.1f, -300f), ("W6_Top_A", -0.4f, -3.8f, 200f) })
            {
                var pivot = new GameObject("TopPivot").transform; pivot.SetParent(props, false); pivot.localPosition = new Vector3(x, 0, z);
                var g = Inst($"{Proc}/{n}.fbx", pivot, BangArt.ToyVC);
                if (!g) continue;
                var pr = pivot.gameObject.AddComponent<Precess>(); pr.spinner = g.transform; pr.spinDegPerSec = spd;   // 陀螺：快速自转 + 转轴慢慢画圈（进动），不是来回摇
            }
            P("W6_DuckBeanie", -0.15f, -2.65f, 15);
            var horse = P("W4_RockingHorse", -2.1f, -3.6f, 35);
            if (horse) horse.transform.localScale *= 1.6f;          // 没人骑的摇摇马停着
            P("W6_YarnBall", -1.75f, -1.75f, 0); P("W6_YarnBall", 1.45f, -1.55f, 60);
            P("W6_NightLight", 0.75f, -4.2f, -10);
            // 前排字母积木 TOYBOX（地毯前方，略微错落）
            var letters = "TOYBOX";
            for (int i = 0; i < letters.Length; i++)
            {
                var b = Inst($"{Proc}/W6_Block_{letters[i]}.fbx", props, BangArt.BlockWood);
                if (!b) continue;
                b.transform.localPosition = new Vector3(0.95f - i * 0.38f, 0.18f, -1.15f - (i % 2) * 0.05f);   // 面朝 -Z 看，本地 +X 在画面左边：从右往左排才读成 TOYBOX
                b.transform.localRotation = Quaternion.Euler(0, (i % 3 - 1) * 6f, 0);
            }
            BuildToyBoxShell(w.transform, props);   // 后墙 + 星星贴纸 + 翻开的箱盖 + 横杆上的月亮挂铃（单摆 + 扭摆）
            Sparkles(w.transform, new Vector3(0.3f, 1.4f, -3.4f), new Vector3(6f, 2.4f, 5f), 14);
            Enrich(6, w.transform, props);   // 丰富度加密（远景 / 近景 / 中景 / 小生命）
            GiantRing(6, w.transform, props);   // 远处一圈巨型玩具（被玩具包围）
            Surround(6, w.transform, props);  // 房间四周和门洞两侧按主题摆满（玩具化露出哪个方向都不空）
            var pw = w.AddComponent<PortalWorld>();
            pw.stencilRef = stencilRef;
            pw.worldTint = new Color(0.72f, 0.7f, 0.95f);       // 夜里：受光偏蓝紫、偏暗；夜光星星和小夜灯是自发光
            pw.hazeColor = new Color(0.45f, 0.4f, 0.75f); pw.hazeStart = 7.0f; pw.hazeEnd = 16.0f; pw.hazeAmount = 0.09f;
            pw.Rebind();
            ShadowPolicy(w.transform);                                                         // 只让有体量的物体投影
            w.AddComponent<PortalCuller>().arch = portal.GetComponentInChildren<PortalArch>();   // 只渲染透过门洞 / 已玩具化区域看得到的物体
            Spill(6, portal.transform);   // 门前真实房间里的溢出物（门后世界从洞口淌出来一截）
            return portal;
        }
    }
}
