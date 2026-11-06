// 房间玩具化道具 → 预制体（玩法里小怪插旗后在真实地板 / 墙面上生成），并提供一个预览布置。
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bang.EditorTools
{
    public static class BangRoomToys
    {
        public const string Dir = "Assets/_Bang/Prefabs/Room";
        const string Src = "Assets/_Bang/Art/Models/Proc/Room";
        static readonly string[] Names = { "Toy_PuzzleMat_A", "Toy_PuzzleMat_B", "Toy_PuzzleMat_C", "Toy_PuzzleMat_D", "Toy_Fence", "Toy_Flag", "Toy_BlockTower", "Toy_GrassTuft", "Toy_Sticker_Star", "Toy_Sticker_Heart", "Toy_Sticker_Cloud" };

        [MenuItem("Bang/2 生成房间玩具化预制体")]
        public static void BuildPrefabs()
        {
            Directory.CreateDirectory(Dir);
            foreach (var n in Names)
            {
                var m = AssetDatabase.LoadAssetAtPath<GameObject>($"{Src}/{n}.fbx");
                if (!m) { Debug.LogWarning("[Bang] 缺少 " + n); continue; }
                var g = (GameObject)PrefabUtility.InstantiatePrefab(m);
                PrefabUtility.UnpackPrefabInstance(g, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var mat = n.Contains("Flag") || n.Contains("Sticker") ? BangArt.FeltVC : BangArt.ToyVC;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
                PrefabUtility.SaveAsPrefabAsset(g, $"{Dir}/{n}.prefab");
                Object.DestroyImmediate(g);
            }
            // 扫地机器人（P2）：玩法里清理玩具化区域
            var vac = BangArt.ImportP2("Toy_VacuumRobot", 0.14f, 0.6f, 0f);
            if (vac) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(vac), $"{Dir}/Toy_VacuumRobot.prefab");
            AssetDatabase.SaveAssets();
            Debug.Log("[Bang] 房间玩具化预制体 → " + Dir);
        }

        /// <summary>预览：在客厅地毯前铺一片玩具化区域（拼图地垫 + 栅栏 + 旗子 + 草 + 积木塔 + 墙贴 + 扫地机器人）。</summary>
        public static void Preview(Transform root)
        {
            var area = new GameObject("RoomToyPreview").transform; area.SetParent(root, false);
            GameObject P(string n, Vector3 p, float yaw)
            {
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{n}.prefab");
                if (!pf) return null;
                var g = (GameObject)PrefabUtility.InstantiatePrefab(pf, area);
                g.transform.localPosition = p; g.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                return g;
            }
            string[] mats = { "Toy_PuzzleMat_A", "Toy_PuzzleMat_B", "Toy_PuzzleMat_C", "Toy_PuzzleMat_D" };
            for (int i = 0; i < 4; i++) for (int j = 0; j < 3; j++) P(mats[(i + j) % 4], new Vector3(1.2f + i * 0.3f, 0.002f, -1.2f - j * 0.3f), 0);
            P("Toy_Fence", new Vector3(1.6f, 0.02f, -0.95f), 0); P("Toy_Fence", new Vector3(2.15f, 0.02f, -0.95f), 0);
            P("Toy_Flag", new Vector3(1.75f, 0.02f, -1.5f), 20);
            P("Toy_BlockTower", new Vector3(2.0f, 0.02f, -1.75f), 30);
            foreach (var p in new[] { new Vector3(1.3f, 0.02f, -1.65f), new Vector3(2.15f, 0.02f, -1.3f), new Vector3(1.5f, 0.02f, -1.85f) }) P("Toy_GrassTuft", p, 0);
            P("Toy_VacuumRobot", new Vector3(0.9f, 0f, -1.4f), -60);
            // 右墙（x=3.95）上的贴纸：朝 -X
            foreach (var (n, p) in new[] { ("Toy_Sticker_Star", new Vector3(3.94f, 1.4f, -1.4f)), ("Toy_Sticker_Heart", new Vector3(3.94f, 1.15f, -1.8f)), ("Toy_Sticker_Cloud", new Vector3(3.94f, 1.6f, -2.1f)) })
            { var g = P(n, p, -90); if (g) g.transform.localScale *= 2f; }
        }
    }
}
