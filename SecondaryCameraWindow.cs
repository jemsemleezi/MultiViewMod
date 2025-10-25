using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace MultiViewMod
{
    /// <summary>
    /// 次级相机观察窗口类 - 1.6 拖影修复版
    /// </summary>
    public class SecondaryCameraWindow : Window
    {
        private readonly SecondaryCameraController cameraController;
        private Vector2 lastMousePosition;
        private bool isDraggingView;
        private int updateCounter = 0;
        private const int UPDATE_INTERVAL = 3;

        public string windowTitle = "MultiViewMod_WindowTitle".Translate();
        public Pawn followTarget;

        // 新增：窗口固定状态
        public bool IsPinned { get; private set; } = false;

        public bool IsFollowing => cameraController?.IsFollowing() ?? false;

        public override Vector2 InitialSize => new Vector2(800, 600);

        public SecondaryCameraWindow(SecondaryCameraController controller)
        {
            cameraController = controller;

            // 1.6 兼容性修复：使用新的窗口初始化方式
            draggable = true;
            resizeable = true;
            preventCameraMotion = false;
            doCloseX = true;
            doWindowBackground = false;
            absorbInputAroundWindow = false;
            forcePause = false;

            // 1.6 中需要设置窗口层 - 使用更高的层级避免渲染冲突
            layer = WindowLayer.Super;
        }

        /// <summary>
        /// 在窗口打开前设置位置
        /// </summary>
        public override void PreOpen()
        {
            base.PreOpen();

            // 恢复保存的窗口位置，确保在屏幕范围内
            var savedRect = MultiViewMod.Settings.GetSavedWindowPosition();
            if (savedRect.HasValue)
            {
                Rect rectToUse = EnsureWindowInScreen(savedRect.Value);
                this.windowRect = rectToUse;
            }
            else
            {
                // 如果没有保存的位置，使用默认位置（屏幕中央）
                this.windowRect = new Rect(
                    (UI.screenWidth - InitialSize.x) / 2,
                    (UI.screenHeight - InitialSize.y) / 2,
                    InitialSize.x,
                    InitialSize.y
                );
            }

            // 恢复保存的缩放比例
            var savedZoom = MultiViewMod.Settings.GetSavedZoomLevel();
            if (savedZoom.HasValue && cameraController != null)
            {
                cameraController.rootSize = savedZoom.Value;
            }
        }

        /// <summary>
        /// 确保窗口在屏幕范围内
        /// </summary>
        private Rect EnsureWindowInScreen(Rect inputRect)
        {
            Rect resultRect = inputRect;

            // 获取屏幕尺寸
            float screenWidth = UI.screenWidth;
            float screenHeight = UI.screenHeight;

            // 确保窗口不会超出屏幕右侧
            if (resultRect.x + resultRect.width > screenWidth)
            {
                resultRect.x = screenWidth - resultRect.width - 5;
            }

            // 确保窗口不会超出屏幕底部
            if (resultRect.y + resultRect.height > screenHeight)
            {
                resultRect.y = screenHeight - resultRect.height - 5;
            }

            // 确保窗口不会超出屏幕左侧和顶部
            resultRect.x = Mathf.Max(5, resultRect.x);
            resultRect.y = Mathf.Max(5, resultRect.y);

            // 确保窗口有合理的最小尺寸
            resultRect.width = Mathf.Max(50, resultRect.width);
            resultRect.height = Mathf.Max(50, resultRect.height);

            return resultRect;
        }

        /// <summary>
        /// 窗口关闭时保存位置和缩放比例
        /// </summary>
        public override void PostClose()
        {
            base.PostClose();

            // 保存窗口位置
            MultiViewMod.Settings.SaveWindowPosition(this.windowRect);

            // 保存缩放比例
            if (cameraController != null)
            {
                MultiViewMod.Settings.SaveZoomLevel(cameraController.rootSize);
            }

            MultiViewMod.Settings.Write(); // 立即保存设置
        }

        /// <summary>
        /// 窗口调整大小时也保存位置
        /// </summary>
        public override void WindowOnGUI()
        {
            // 保存当前窗口位置
            var currentRect = this.windowRect;

            base.WindowOnGUI();

            // 如果窗口位置或大小发生变化，保存设置
            if (this.windowRect != currentRect)
            {
                MultiViewMod.Settings.SaveWindowPosition(this.windowRect);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            try
            {
                // 修复拖影：先绘制背景
                Widgets.DrawBoxSolid(inRect, new Color(0.13f, 0.13f, 0.13f));

                // 直接绘制观察视图，占据整个窗口
                DrawObservationView(inRect);

                // 在视图上方叠加绘制控制按钮
                DrawOverlayControls(inRect);

                // 处理输入
                HandleInput(inRect);
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Window rendering failed: {e}");
            }
        }

        /// <summary>
        /// 绘制观察视图 - 修复拖影版本
        /// </summary>
        private void DrawObservationView(Rect inRect)
        {
            try
            {
                updateCounter++;
                if (updateCounter >= UPDATE_INTERVAL)
                {
                    updateCounter = 0;
                    cameraController?.UpdateCamera(new Vector2(inRect.width, inRect.height));
                }

                RenderTexture renderTexture = cameraController?.GetRenderTexture();
                if (renderTexture != null)
                {
                    // 修复拖影：使用更稳定的绘制方式
                    Graphics.DrawTexture(inRect, renderTexture, new Rect(0, 0, 1, 1), 0, 0, 0, 0, GUI.color, null);
                }
                else
                {
                    Widgets.DrawBoxSolid(inRect, new Color(0.2f, 0.2f, 0.3f));
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(inRect, "MultiViewMod_Rendering".Translate());
                    Text.Anchor = TextAnchor.UpperLeft;
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] 观察视图绘制失败: {e}");
            }
        }

        /// <summary>
        /// 绘制叠加控制按钮
        /// </summary>
        private void DrawOverlayControls(Rect inRect)
        {
            try
            {
                // 创建控制条
                Rect controlBar = new Rect(inRect.x, inRect.y, inRect.width, 35f);

                // 计算右侧按钮区域的总宽度（增加一个固定按钮的宽度）
                float iconSize = 24f;
                float spacing = 5f;
                float rightButtonsWidth = iconSize * 5 + spacing * 4 + 10f; // 5个按钮

                // 窗口标题和缩放信息
                Rect titleRect = new Rect(controlBar.x + 10f, controlBar.y, controlBar.width - rightButtonsWidth - 20f, controlBar.height);
                Text.Font = GameFont.Small;

                // 构建标题字符串
                string title = BuildCompactTitle();

                // 使用白色文字确保在游戏背景下可见
                GUI.color = Color.white;

                // 使用不换行的标签绘制
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                Widgets.Label(titleRect, title);
                Text.WordWrap = true;
                Text.Anchor = TextAnchor.UpperLeft;

                // 缩放控制按钮
                float buttonsStartX = controlBar.xMax - rightButtonsWidth;

                // 缩小按钮图标 (-)
                Rect zoomOutRect = new Rect(buttonsStartX, controlBar.y + 5f, iconSize, iconSize);
                if (Widgets.ButtonImage(zoomOutRect, TexButton.Minus))
                {
                    cameraController?.HandleZoom(3f);
                }
                TooltipHandler.TipRegion(zoomOutRect, "MultiViewMod_ZoomOut".Translate());

                // 重置缩放按钮图标
                Rect resetZoomRect = new Rect(zoomOutRect.xMax + spacing, controlBar.y + 5f, iconSize, iconSize);
                if (Widgets.ButtonImage(resetZoomRect, TexButton.Reload))
                {
                    cameraController?.ResetZoom();
                }
                TooltipHandler.TipRegion(resetZoomRect, "MultiViewMod_ResetZoom".Translate());

                // 放大按钮图标 (+)
                Rect zoomInRect = new Rect(resetZoomRect.xMax + spacing, controlBar.y + 5f, iconSize, iconSize);
                if (Widgets.ButtonImage(zoomInRect, TexButton.Plus))
                {
                    cameraController?.HandleZoom(-3f);
                }
                TooltipHandler.TipRegion(zoomInRect, "MultiViewMod_ZoomIn".Translate());

                // 跟随按钮
                Rect followRect = new Rect(zoomInRect.xMax + spacing, controlBar.y + 5f, iconSize, iconSize);
                Texture2D followIcon = IsFollowing ? TexButton.CloseXSmall : TexButton.Search;
                if (Widgets.ButtonImage(followRect, followIcon))
                {
                    HandleFollowButtonClick();
                }
                string followTooltip = IsFollowing ? "MultiViewMod_StopFollow".Translate() : "MultiViewMod_FollowSelected".Translate();
                TooltipHandler.TipRegion(followRect, followTooltip);

                // 新增：固定窗口按钮（使用指北图标）
                Rect pinRect = new Rect(followRect.xMax + spacing, controlBar.y + 5f, iconSize, iconSize);

                // 使用不同的颜色来表示固定状态
                Color originalColor = GUI.color;
                if (IsPinned)
                {
                    GUI.color = Color.yellow; // 固定时显示为黄色
                }

                // 使用指北图标来表示固定功能
                if (Widgets.ButtonImage(pinRect, TexButton.LockNorthUp))
                {
                    ToggleWindowPin();
                }

                // 恢复原始颜色
                GUI.color = originalColor;

                string pinTooltip = IsPinned ?
                    "MultiViewMod_UnpinWindowTooltip".Translate() :
                    "MultiViewMod_PinWindowTooltip".Translate();
                TooltipHandler.TipRegion(pinRect, pinTooltip);
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Overlay controls drawing failed: {e}");
            }
        }

        /// <summary>
        /// 切换窗口固定状态
        /// </summary>
        private void ToggleWindowPin()
        {
            IsPinned = !IsPinned;

            // 根据固定状态更新窗口行为
            if (IsPinned)
            {
                // 固定时：禁用拖动和调整大小
                draggable = false;
                resizeable = false;
            }
            else
            {
                // 取消固定时：恢复拖动和调整大小
                draggable = true;
                resizeable = true;
            }
        }

        /// <summary>
        /// 构建紧凑格式的标题
        /// </summary>
        private string BuildCompactTitle()
        {
            string baseTitle = windowTitle;

            if (IsFollowing)
            {
                baseTitle += "MultiViewMod_FollowingText".Translate();
            }

            // 添加固定状态标识
            if (IsPinned)
            {
                baseTitle += "[固]";
            }

            // 添加缩放信息
            string zoomInfo = cameraController?.GetZoomInfo() ?? "缩放:--";
            return $"{baseTitle} | {zoomInfo}";
        }

        /// <summary>
        /// 处理跟随按钮点击
        /// </summary>
        private void HandleFollowButtonClick()
        {
            if (IsFollowing)
            {
                cameraController.CancelFollow();
                followTarget = null;
                windowTitle = "MultiViewMod_WindowTitle".Translate();
            }
            else
            {
                followTarget = Find.Selector.SingleSelectedThing as Pawn;
                if (followTarget != null)
                {
                    cameraController.SetFollowTarget(followTarget);
                    windowTitle = "MultiViewMod_WindowTitleFollowing".Translate(followTarget.Name);
                }
                else
                {
                    Messages.Message("MultiViewMod_SelectUnitFirst".Translate(), MessageTypeDefOf.RejectInput);
                }
            }
        }

        /// <summary>
        /// 处理输入事件
        /// </summary>
        private void HandleInput(Rect inRect)
        {
            Rect controlBar = new Rect(inRect.x, inRect.y, inRect.width, 35f);
            Rect contentArea = new Rect(inRect.x, inRect.y + 35f, inRect.width, inRect.height - 35f);

            if (controlBar.Contains(Event.current.mousePosition))
            {
                return;
            }

            if (!contentArea.Contains(Event.current.mousePosition))
                return;

            try
            {
                if (Event.current.type == EventType.ScrollWheel)
                {
                    cameraController?.HandleZoom(Event.current.delta.y);
                    Event.current.Use();
                }

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    isDraggingView = true;
                    lastMousePosition = Event.current.mousePosition;

                    if (IsFollowing)
                    {
                        cameraController.CancelFollow();
                        followTarget = null;
                        windowTitle = "MultiViewMod_WindowTitle".Translate();
                    }

                    Event.current.Use();
                }

                if (Event.current.type == EventType.MouseDrag && Event.current.button == 0 && isDraggingView)
                {
                    Vector2 delta = Event.current.mousePosition - lastMousePosition;
                    cameraController?.HandlePan(delta);
                    lastMousePosition = Event.current.mousePosition;
                    Event.current.Use();
                }

                if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                {
                    isDraggingView = false;
                    Event.current.Use();
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Input handling failed: {e}");
            }
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();

            try
            {
                if (IsFollowing && followTarget != null && !followTarget.Spawned)
                {
                    cameraController.CancelFollow();
                    followTarget = null;
                    windowTitle = "MultiViewMod_WindowTitle".Translate();
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Window update failed: {e}");
            }
        }

        /// <summary>
        /// 重置缩放比例
        /// </summary>
        public void ResetZoom()
        {
            cameraController?.ResetZoom();
        }

        /// <summary>
        /// 设置跟随目标
        /// </summary>
        public void SetFollowTarget(Pawn pawn)
        {
            followTarget = pawn;
            if (followTarget != null)
            {
                cameraController.SetFollowTarget(followTarget);
                windowTitle = "MultiViewMod_WindowTitleFollowing".Translate(pawn.Name);
            }
            else
            {
                cameraController.CancelFollow();
                windowTitle = "MultiViewMod_WindowTitle".Translate();
            }
        }
    }
}