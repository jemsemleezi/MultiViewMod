using LudeonTK;
using RimWorld;
using Verse;
using UnityEngine;

namespace MultiViewMod
{
    /// <summary>
    /// 调试命令类 - 1.6 兼容版本
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MultiViewCommands
    {
        static MultiViewCommands()
        {
            // 静态构造函数，确保类被初始化
        }

        // 打开观察窗口的调试命令
        [DebugAction("MultiView", "Open Observation Window", allowedGameStates = AllowedGameStates.Playing)]
        public static void OpenObservationWindow()
        {
            if (MultiViewController.Instance != null)
            {
                MultiViewController.Instance.CreateNewObservationWindow();
                Messages.Message("Observation window opened via debug command.", MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message("MultiViewController instance not available.", MessageTypeDefOf.NegativeEvent);
            }
        }

        // 观察选中单位的调试命令
        [DebugAction("MultiView", "Observe Selected Pawn", allowedGameStates = AllowedGameStates.Playing)]
        public static void ObserveSelectedPawn()
        {
            Pawn selectedPawn = Find.Selector?.SingleSelectedThing as Pawn;
            if (selectedPawn != null && MultiViewController.Instance != null)
            {
                MultiViewController.Instance.CreatePawnObservationWindow(selectedPawn);
                Messages.Message($"Observing pawn: {selectedPawn.Name}", MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message("Please select a valid pawn first.", MessageTypeDefOf.RejectInput);
            }
        }

        // 关闭所有观察窗口的调试命令
        [DebugAction("MultiView", "Close All Observation Windows", allowedGameStates = AllowedGameStates.Playing)]
        public static void CloseAllObservationWindows()
        {
            if (MultiViewController.Instance != null)
            {
                MultiViewController.Instance.CloseAllWindows();
                Messages.Message("All observation windows closed.", MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message("MultiViewController instance not available.", MessageTypeDefOf.NegativeEvent);
            }
        }

        // 在 MultiViewCommands.cs 中添加调试命令
        [DebugAction("MultiView", "Test Independent Camera", allowedGameStates = AllowedGameStates.Playing)]
        public static void TestIndependentCamera()
        {
            if (MultiViewController.Instance != null)
            {
                // 测试移动到地图不同位置
                var map = Find.CurrentMap;
                if (map != null)
                {
                    // 移动到地图随机位置
                    Vector3 randomPos = new Vector3(
                        Rand.Range(10, map.Size.x - 10),
                        15f,
                        Rand.Range(10, map.Size.z - 10)
                    );

                    // 这里需要访问相机控制器的内部方法来测试独立移动
                    Messages.Message($"测试独立相机移动到: {randomPos}", MessageTypeDefOf.NeutralEvent);
                }
            }
        }

        [DebugAction("MultiView", "Force Map Update", allowedGameStates = AllowedGameStates.Playing)]
        public static void ForceMapUpdate()
        {
            if (MultiViewController.Instance != null)
            {
                // 强制地图更新
                Messages.Message("强制地图更新", MessageTypeDefOf.NeutralEvent);
            }
        }
    }
}