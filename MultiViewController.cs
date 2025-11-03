// MultiViewController.cs - 修复空引用异常版本
using UnityEngine;
using Verse;
using System.Collections.Generic;
using RimWorld;

namespace MultiViewMod
{
    /// <summary>
    /// MultiView模组控制器，管理观察窗口的创建和快捷键处理
    /// </summary>
    public class MultiViewController : GameComponent
    {
        public static MultiViewController Instance;

        private SecondaryCameraController cameraController;
        private readonly List<SecondaryCameraWindow> openWindows;
        private bool initializationAttempted = false;
        private int initializationDelayFrames = 30;
        private int frameCount = 0;

        public MultiViewController(Game game) : this()
        {
        }

        // 1.6 兼容性：添加无参构造函数
        public MultiViewController()
        {
            Instance = this;
            openWindows = new List<SecondaryCameraWindow>();
        }

        public override void LoadedGame()
        {
            initializationAttempted = false;
            frameCount = 0;
        }

        public override void StartedNewGame()
        {
            initializationAttempted = false;
            frameCount = 0;
        }

        public override void GameComponentUpdate()
        {
            // 确保在游戏主线程中执行
            if (Current.ProgramState != ProgramState.Playing) return;

            // 优化：减少初始化检查频率
            if (!initializationAttempted && Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null)
            {
                frameCount++;
                if (frameCount >= initializationDelayFrames)
                {
                    InitializeWhenReady();
                }
            }

            // 处理快捷键
            HandleHotkeys();
        }

        /// <summary>
        /// 清理已关闭的窗口
        /// </summary>
        private void CleanupClosedWindows()
        {
            try
            {
                // 移除所有已经关闭的窗口
                for (int i = openWindows.Count - 1; i >= 0; i--)
                {
                    var window = openWindows[i];
                    if (window == null || !Find.WindowStack.IsOpen(window))
                    {
                        openWindows.RemoveAt(i);
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Cleanup closed windows failed: {e}");
            }
        }

        /// <summary>
        /// 处理快捷键输入
        /// </summary>
        private void HandleHotkeys()
        {
            if (!MultiViewMod.Settings.EnableHotkeys) return;

            try
            {
                // 检查快捷键定义
                if (MultiViewKeyBindings.MultiView_OpenWindow == null)
                {
                    Log.Error("[MultiViewMod] MultiView_OpenWindow KeyBindingDef 为 null");
                    return;
                }

                // 在检查快捷键之前先清理已关闭的窗口
                CleanupClosedWindows();

                // 打开/关闭窗口快捷键 - 切换功能
                if (MultiViewMod.Settings.OpenWindowHotkey && MultiViewKeyBindings.MultiView_OpenWindow.JustPressed)
                {
                    if (openWindows.Count > 0)
                    {
                        // 有窗口时关闭最顶部窗口
                        CloseTopWindow();
                        Messages.Message("MultiViewMod_WindowClosed".Translate(), MessageTypeDefOf.NeutralEvent);
                    }
                    else
                    {
                        // 无窗口时创建新窗口
                        CreateNewObservationWindow();
                        Messages.Message("MultiViewMod_WindowOpened".Translate(), MessageTypeDefOf.NeutralEvent);
                    }
                }

                // 关闭窗口快捷键 - 有窗口时才关闭，无窗口时给出提示
                // 检查窗口是否固定，固定窗口不能被快捷键关闭
                if (MultiViewMod.Settings.CloseWindowHotkey && MultiViewKeyBindings.MultiView_CloseWindow.JustPressed)
                {
                    CleanupClosedWindows(); // 再次清理确保状态正确
                    if (openWindows.Count > 0)
                    {
                        var topWindow = openWindows[openWindows.Count - 1];

                        // 修复空引用：检查窗口是否有效
                        if (topWindow == null || !Find.WindowStack.IsOpen(topWindow))
                        {
                            // 移除无效窗口
                            openWindows.Remove(topWindow);
                            Messages.Message("MultiViewMod_NoWindowsOpen".Translate(), MessageTypeDefOf.NeutralEvent);
                            return;
                        }

                        // 检查窗口是否固定
                        if (topWindow.IsPinned)
                        {
                            Messages.Message("MultiViewMod_WindowPinned".Translate(), MessageTypeDefOf.RejectInput);
                            return;
                        }

                        CloseTopWindow();
                        Messages.Message("MultiViewMod_WindowClosed".Translate(), MessageTypeDefOf.NeutralEvent);
                    }
                    else
                    {
                        Messages.Message("MultiViewMod_NoWindowsOpen".Translate(), MessageTypeDefOf.NeutralEvent);
                    }
                }

                // 切换跟随模式快捷键
                if (MultiViewMod.Settings.ToggleFollowHotkey && MultiViewKeyBindings.MultiView_ToggleFollow.JustPressed && openWindows.Count > 0)
                {
                    ToggleFollowForTopWindow();
                }

                // 重置缩放快捷键
                if (MultiViewMod.Settings.ResetZoomHotkey && MultiViewKeyBindings.MultiView_ResetZoom.JustPressed && openWindows.Count > 0)
                {
                    ResetZoomForTopWindow();
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Hotkey handling failed: {e}");
            }
        }

        /// <summary>
        /// 关闭最顶部的窗口
        /// </summary>
        private void CloseTopWindow()
        {
            if (openWindows.Count > 0)
            {
                var topWindow = openWindows[openWindows.Count - 1];
                if (topWindow != null && Find.WindowStack != null && Find.WindowStack.IsOpen(topWindow))
                {
                    topWindow.Close();
                    openWindows.Remove(topWindow);
                }
                else
                {
                    // 如果窗口无效，从列表中移除
                    openWindows.Remove(topWindow);
                }
            }
        }

        /// <summary>
        /// 切换最顶部窗口的跟随模式
        /// </summary>
        private void ToggleFollowForTopWindow()
        {
            if (openWindows.Count > 0)
            {
                var topWindow = openWindows[openWindows.Count - 1];
                if (topWindow != null && Find.WindowStack != null && Find.WindowStack.IsOpen(topWindow))
                {
                    if (topWindow.IsFollowing)
                    {
                        topWindow.SetFollowTarget(null);
                    }
                    else
                    {
                        Pawn selected = Find.Selector?.SingleSelectedThing as Pawn;
                        if (selected != null)
                        {
                            topWindow.SetFollowTarget(selected);
                        }
                        else
                        {
                            Messages.Message("MultiViewMod_SelectUnitFirst".Translate(), MessageTypeDefOf.RejectInput);
                        }
                    }
                }
                else
                {
                    // 如果窗口无效，从列表中移除
                    openWindows.Remove(topWindow);
                }
            }
        }

        /// <summary>
        /// 重置最顶部窗口的缩放
        /// </summary>
        private void ResetZoomForTopWindow()
        {
            if (openWindows.Count > 0)
            {
                var topWindow = openWindows[openWindows.Count - 1];
                if (topWindow != null && Find.WindowStack != null && Find.WindowStack.IsOpen(topWindow))
                {
                    topWindow.ResetZoom();
                }
                else
                {
                    // 如果窗口无效，从列表中移除
                    openWindows.Remove(topWindow);
                }
            }
        }

        /// <summary>
        /// 在游戏准备就绪时初始化
        /// </summary>
        private void InitializeWhenReady()
        {
            if (initializationAttempted) return;

            try
            {
                if (Find.Camera == null || Find.CurrentMap == null)
                {
                    return;
                }

                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    try
                    {
                        cameraController = new SecondaryCameraController();
                        cameraController.Initialize();
                        initializationAttempted = true;
                    }
                    catch (System.Exception e)
                    {
                        Log.Error($"[MultiViewMod] Initialization failed: {e}");
                        initializationAttempted = true;
                    }
                });
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Initialization scheduling failed: {e}");
                initializationAttempted = true;
            }
        }

        /// <summary>
        /// 检查是否可以创建窗口
        /// </summary>
        public bool CanCreateWindow()
        {
            return cameraController != null && cameraController.IsReady();
        }

        /// <summary>
        /// 创建新的观察窗口 - 修复空引用版本
        /// </summary>
        public void CreateNewObservationWindow()
        {
            try
            {
                // 修复空引用：确保相机控制器存在
                if (cameraController == null)
                {
                    cameraController = new SecondaryCameraController();
                    cameraController.Initialize();
                }

                // 修复空引用：检查相机控制器是否准备就绪
                if (cameraController == null || !cameraController.IsReady())
                {
                    Log.Warning("[MultiViewMod] 相机控制器未准备好，无法创建观察窗口");
                    return;
                }

                // 修复空引用：检查 WindowStack 是否存在
                if (Find.WindowStack == null)
                {
                    Log.Error("[MultiViewMod] WindowStack 为 null，无法创建窗口");
                    return;
                }

                // 先清理已关闭的窗口，确保状态正确
                CleanupClosedWindows();

                SecondaryCameraWindow newWindow = new SecondaryCameraWindow(cameraController);

                // 应用自动跟随设置
                if (MultiViewMod.Settings != null && MultiViewMod.Settings.AutoFollowSelected)
                {
                    Pawn selectedPawn = Find.Selector?.SingleSelectedThing as Pawn;
                    if (selectedPawn != null)
                    {
                        newWindow.SetFollowTarget(selectedPawn);
                    }
                }

                Find.WindowStack.Add(newWindow);
                openWindows.Add(newWindow);
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] 创建观察窗口失败: {e}");
            }
        }

        /// <summary>
        /// 创建特定Pawn的观察窗口
        /// </summary>
        public void CreatePawnObservationWindow(Pawn pawn)
        {
            if (pawn == null) return;

            try
            {
                // 修复空引用：确保相机控制器存在
                if (cameraController == null)
                {
                    cameraController = new SecondaryCameraController();
                    cameraController.Initialize();
                }

                // 修复空引用：检查相机控制器是否准备就绪
                if (cameraController == null || !cameraController.IsReady())
                {
                    Log.Warning("[MultiViewMod] 相机控制器未准备好，无法创建观察窗口");
                    return;
                }

                // 修复空引用：检查 WindowStack 是否存在
                if (Find.WindowStack == null)
                {
                    Log.Error("[MultiViewMod] WindowStack 为 null，无法创建窗口");
                    return;
                }

                SecondaryCameraWindow newWindow = new SecondaryCameraWindow(cameraController);
                newWindow.SetFollowTarget(pawn);
                Find.WindowStack.Add(newWindow);
                openWindows.Add(newWindow);
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] 创建Pawn观察窗口失败: {e}");
            }
        }

        /// <summary>
        /// 关闭所有窗口 - 修复空引用版本
        /// </summary>
        public void CloseAllWindows()
        {
            try
            {
                // 修复空引用：检查 WindowStack 是否存在
                if (Find.WindowStack == null)
                {
                    Log.Warning("[MultiViewMod] WindowStack 为 null，无法关闭窗口");
                    openWindows.Clear();
                    return;
                }

                // 创建临时列表来避免在迭代时修改集合
                var windowsToClose = new List<SecondaryCameraWindow>(openWindows);

                foreach (var window in windowsToClose)
                {
                    if (window != null && Find.WindowStack.IsOpen(window))
                    {
                        try
                        {
                            window.Close();
                        }
                        catch (System.Exception e)
                        {
                            Log.Error($"[MultiViewMod] 关闭窗口时出错: {e}");
                        }
                    }
                }
                openWindows.Clear();
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Failed to close windows: {e}");
                // 确保无论如何都清空列表
                openWindows.Clear();
            }
        }

        public override void FinalizeInit()
        {
            try
            {
                // 清理所有注册的视口
                SecondaryViewportManager.ClearAllViewports();

                if (cameraController != null)
                {
                    cameraController.Cleanup();
                    cameraController = null;
                }
                CloseAllWindows();
                initializationAttempted = false;
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] FinalizeInit failed: {e}");
            }
        }
    }
}