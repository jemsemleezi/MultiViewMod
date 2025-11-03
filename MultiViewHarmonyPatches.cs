// MultiViewHarmonyPatches.cs
using HarmonyLib;
using RimWorld;
using Verse;

namespace MultiViewMod
{
    [StaticConstructorOnStartup]
    public static class MultiViewHarmonyPatches
    {
        static MultiViewHarmonyPatches()
        {
            try
            {
                var harmony = new Harmony("MultiViewMod.Patches");
                harmony.PatchAll();
                Log.Message("[MultiViewMod] Harmony patches applied successfully");
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Failed to apply Harmony patches: {e}");
            }
        }

        /// <summary>
        /// 补丁MapDrawer的ViewRect属性，扩展渲染视口
        /// </summary>
        [HarmonyPatch(typeof(MapDrawer), "ViewRect", MethodType.Getter)]
        public static class MapDrawer_ViewRect_Patch
        {
            public static void Postfix(MapDrawer __instance, ref CellRect __result)
            {
                try
                {
                    // 如果有活跃的次级相机视口，合并视口
                    if (SecondaryViewportManager.HasActiveViewports())
                    {
                        CellRect combinedView = SecondaryViewportManager.GetCombinedViewport(__result);
                        if (!combinedView.IsEmpty && combinedView != __result)
                        {
                            __result = combinedView;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Log.Error($"[MultiViewMod] Error in MapDrawer_ViewRect_Patch: {e}");
                }
            }
        }

        // 在 MultiViewHarmonyPatches.cs 中添加
        [HarmonyPatch(typeof(CameraDriver), "CurrentViewRect", MethodType.Getter)]
        public static class CameraDriver_CurrentViewRect_Patch
        {
            public static void Postfix(ref CellRect __result)
            {
                try
                {
                    // 如果有活跃的次级相机视口，合并视口
                    if (SecondaryViewportManager.HasActiveViewports())
                    {
                        CellRect combinedView = SecondaryViewportManager.GetCombinedViewport(__result);
                        if (!combinedView.IsEmpty && combinedView != __result)
                        {
                            __result = combinedView;
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Log.Error($"[MultiViewMod] Error in CameraDriver_CurrentViewRect_Patch: {e}");
                }
            }
        }
    }
}