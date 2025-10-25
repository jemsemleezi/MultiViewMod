//using LudeonTK;
using System.Collections.Generic;
using Verse;

namespace MultiViewMod
{
    /// <summary>
    /// 调试命令类
    /// </summary>
    [StaticConstructorOnStartup]
    public static class MultiViewCommands
    {
        static MultiViewCommands()
        {
            // 注册到游戏命令系统
        }

        [DebugAction("MultiView", "打开观察窗口", allowedGameStates = AllowedGameStates.Playing)]
        public static void OpenObservationWindow()
        {
            MultiViewController.Instance?.CreateNewObservationWindow();
        }

        [DebugAction("MultiView", "观察选中单位", allowedGameStates = AllowedGameStates.Playing)]
        public static void ObserveSelectedPawn()
        {
            if (Find.Selector.SingleSelectedThing is Pawn selectedPawn)
            {
                MultiViewController.Instance?.CreatePawnObservationWindow(selectedPawn);
            }
        }
    }
}