// 增强的 SecondaryViewportManager 完整实现
using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace MultiViewMod
{
    /// <summary>
    /// 增强的次级相机视口管理器 - 处理多视口渲染和可见性
    /// </summary>
    public static class SecondaryViewportManager
    {
        private static readonly HashSet<CellRect> activeViewports = new HashSet<CellRect>();
        private static readonly Dictionary<int, CellRect> viewportById = new Dictionary<int, CellRect>();
        private static int nextViewportId = 1;

        private static CellRect? cachedCombinedViewport = null;
        private static int lastUpdateFrame = -1;
        private static int lastCleanupFrame = -1;
        private const int CLEANUP_INTERVAL = 60; // 每60帧清理一次

        public static int RegisterViewport(CellRect viewport)
        {
            if (viewport.IsEmpty) return -1;

            int viewportId = nextViewportId++;
            activeViewports.Add(viewport);
            viewportById[viewportId] = viewport;
            InvalidateCache();

            Log.Message($"[MultiViewMod] 注册视口 #{viewportId}: {viewport}");
            return viewportId;
        }

        public static void UpdateViewport(int viewportId, CellRect newViewport)
        {
            if (viewportById.ContainsKey(viewportId) && !newViewport.IsEmpty)
            {
                activeViewports.Remove(viewportById[viewportId]);
                activeViewports.Add(newViewport);
                viewportById[viewportId] = newViewport;
                InvalidateCache();
            }
        }

        public static void UnregisterViewport(int viewportId)
        {
            if (viewportById.TryGetValue(viewportId, out CellRect viewport))
            {
                activeViewports.Remove(viewport);
                viewportById.Remove(viewportId);
                InvalidateCache();
                Log.Message($"[MultiViewMod] 注销视口 #{viewportId}");
            }
        }

        public static void ClearAllViewports()
        {
            activeViewports.Clear();
            viewportById.Clear();
            InvalidateCache();
            Log.Message("[MultiViewMod] 清除所有视口");
        }

        public static CellRect GetCombinedViewport(CellRect mainViewport)
        {
            // 定期清理无效视口
            if (Time.frameCount - lastCleanupFrame > CLEANUP_INTERVAL)
            {
                CleanupInvalidViewports();
                lastCleanupFrame = Time.frameCount;
            }

            // 使用帧缓存避免重复计算
            if (lastUpdateFrame == Time.frameCount && cachedCombinedViewport.HasValue)
            {
                return cachedCombinedViewport.Value;
            }

            CellRect combined = mainViewport;

            foreach (var viewport in activeViewports)
            {
                if (!viewport.IsEmpty)
                {
                    combined = combined.Encapsulate(viewport);
                }
            }

            // 限制在地图范围内
            Map currentMap = Find.CurrentMap;
            if (currentMap != null)
            {
                combined.ClipInsideMap(currentMap);
            }

            cachedCombinedViewport = combined;
            lastUpdateFrame = Time.frameCount;

            return combined;
        }

        public static bool HasActiveViewports()
        {
            return activeViewports.Count > 0;
        }

        public static bool IsPawnInAnyViewport(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null)
                return false;

            foreach (var viewport in activeViewports)
            {
                if (viewport.Contains(pawn.Position))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsThingInAnyViewport(Thing thing)
        {
            if (thing == null || !thing.Spawned || thing.Map == null)
                return false;

            foreach (var viewport in activeViewports)
            {
                if (viewport.Contains(thing.Position))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsSectionInAnyViewport(CellRect sectionBounds)
        {
            foreach (var viewport in activeViewports)
            {
                if (viewport.Overlaps(sectionBounds))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool IsCellInAnyViewport(IntVec3 cell)
        {
            foreach (var viewport in activeViewports)
            {
                if (viewport.Contains(cell))
                {
                    return true;
                }
            }
            return false;
        }

        private static void CleanupInvalidViewports()
        {
            // 移除空的视口
            activeViewports.RemoveWhere(viewport => viewport.IsEmpty);

            // 同步 viewportById 字典
            var idsToRemove = new List<int>();
            foreach (var kvp in viewportById)
            {
                if (!activeViewports.Contains(kvp.Value) || kvp.Value.IsEmpty)
                {
                    idsToRemove.Add(kvp.Key);
                }
            }

            foreach (int id in idsToRemove)
            {
                viewportById.Remove(id);
            }
        }

        private static void InvalidateCache()
        {
            cachedCombinedViewport = null;
        }

        // 调试方法
        public static string GetDebugInfo()
        {
            return $"活跃视口: {activeViewports.Count}, 缓存: {cachedCombinedViewport.HasValue}";
        }
    }
}