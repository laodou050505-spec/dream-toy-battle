// 地面高度：规则层的敌人都走在 y = 0，但积木路、铁轨、地毯高出地面几厘米，门后世界的地形还有起伏——不抬高脚就陷进去。
// 这些地面件在世界场景里挂了隐形碰撞体（子物体 GroundCol / WorldGroundCol，Ignore Raycast 层，不影响别的射线）。
// 房间里只认 GroundCol：门后世界的地形网格一直延伸到房间下面，但房间里是看不见的，不能跟着它起伏。
using UnityEngine;

namespace Bang.Play
{
    public static class GroundProbe
    {
        const int Mask = 1 << 2;
        static readonly RaycastHit[] Hits = new RaycastHit[8];

        /// <summary>behindDoor = 这个位置在门后世界里；radius = 脚掌范围：积木之间有缝，只看脚下一个点会落进缝里。返回地面高度（米）。</summary>
        public static float Height(Vector3 p, bool behindDoor, float radius = 0f)
        {
            float best = Sample(p.x, p.z, behindDoor);
            if (radius <= 0f) return best;
            best = Mathf.Max(best, Sample(p.x + radius, p.z, behindDoor), Sample(p.x - radius, p.z, behindDoor));
            return Mathf.Max(best, Sample(p.x, p.z + radius, behindDoor), Sample(p.x, p.z - radius, behindDoor));
        }

        static float Sample(float x, float z, bool behindDoor)
        {
            int n = Physics.RaycastNonAlloc(new Vector3(x, 4f, z), Vector3.down, Hits, 6f, Mask, QueryTriggerInteraction.Ignore);
            float best = 0f; bool any = false;
            for (int i = 0; i < n; i++)
            {
                var c = Hits[i].collider; if (!behindDoor && c.name != "GroundCol") continue;
                float y = Hits[i].point.y;
                if (!any || y > best) { best = y; any = true; }
            }
            return any ? best : 0f;
        }
    }
}
