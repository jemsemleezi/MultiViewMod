using LudeonTK;
using RimWorld;
using Verse;

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
    }
}