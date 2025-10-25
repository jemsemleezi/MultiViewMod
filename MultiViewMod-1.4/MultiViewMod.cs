// MultiViewMod.cs
using RimWorld;
using UnityEngine;
using Verse;

namespace MultiViewMod
{
    /// <summary>
    /// MultiView模组主类，处理模组设置界面
    /// </summary>
    public class MultiViewMod : Mod
    {
        public static MultiViewSettings Settings;

        public MultiViewMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MultiViewSettings>();
        }

        public override string SettingsCategory()
        {
            return "MultiViewMod_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            // 计算内容总高度
            float totalHeight = 800f;

            // 创建滚动视图
            Rect viewRect = new Rect(0, 0, inRect.width - 20f, totalHeight);
            Rect scrollRect = new Rect(inRect.x, inRect.y, inRect.width, inRect.height - 40f);

            Widgets.BeginScrollView(scrollRect, ref scrollPosition, viewRect);

            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            // 快捷键设置区域
            listing.Label("MultiViewMod_HotkeySettings".Translate());
            listing.GapLine();

            listing.CheckboxLabeled("MultiViewMod_EnableHotkeys".Translate(), ref Settings.EnableHotkeys,
                "MultiViewMod_EnableHotkeysTip".Translate());
            listing.Gap();

            if (Settings.EnableHotkeys)
            {
                listing.CheckboxLabeled("MultiViewMod_OpenWindowHotkey".Translate() + " (" + MultiViewKeyBindings.MultiView_OpenWindow.MainKeyLabel + ")",
                    ref Settings.OpenWindowHotkey, "MultiViewMod_OpenWindowHotkeyTip".Translate());
                listing.Gap();

                listing.CheckboxLabeled("MultiViewMod_CloseWindowHotkey".Translate() + " (" + MultiViewKeyBindings.MultiView_CloseWindow.MainKeyLabel + ")",
                    ref Settings.CloseWindowHotkey, "MultiViewMod_CloseWindowHotkeyTip".Translate());
                listing.Gap();

                listing.CheckboxLabeled("MultiViewMod_ToggleFollowHotkey".Translate() + " (" + MultiViewKeyBindings.MultiView_ToggleFollow.MainKeyLabel + ")",
                    ref Settings.ToggleFollowHotkey, "MultiViewMod_ToggleFollowHotkeyTip".Translate());
                listing.Gap();

                listing.CheckboxLabeled("MultiViewMod_ResetZoomHotkey".Translate() + " (" + MultiViewKeyBindings.MultiView_ResetZoom.MainKeyLabel + ")",
                    ref Settings.ResetZoomHotkey, "MultiViewMod_ResetZoomHotkeyTip".Translate());
                listing.Gap();

                listing.Label("MultiViewMod_HotkeyNote".Translate());
                listing.Gap();
            }

            // 缩放设置区域
            listing.Label("MultiViewMod_ZoomSettings".Translate());
            listing.GapLine();

            listing.Label($"{"MultiViewMod_ZoomSpeedFactor".Translate()}: {Settings.ZoomSpeedFactor.ToStringPercent()}");
            Settings.ZoomSpeedFactor = listing.Slider(Settings.ZoomSpeedFactor, 0.1f, 3f);
            listing.Gap();

            listing.Label($"{"MultiViewMod_MinZoom".Translate()}: {Settings.MinZoom:F1}");
            Settings.MinZoom = listing.Slider(Settings.MinZoom, 0.1f, 10f);
            listing.Gap();

            listing.Label($"{"MultiViewMod_MaxZoom".Translate()}: {Settings.MaxZoom:F1}");
            Settings.MaxZoom = listing.Slider(Settings.MaxZoom, 50f, 200f);
            listing.Gap();

            listing.Label($"{"MultiViewMod_DefaultZoom".Translate()}: {Settings.DefaultZoom:F1}");
            Settings.DefaultZoom = listing.Slider(Settings.DefaultZoom, 5f, 50f);
            listing.Gap();

            // 窗口设置区域
            listing.Gap();
            listing.Label("MultiViewMod_WindowSettings".Translate());
            listing.GapLine();

            listing.CheckboxLabeled("MultiViewMod_AutoFollowSelected".Translate(), ref Settings.AutoFollowSelected,
                "MultiViewMod_AutoFollowSelectedTip".Translate());
            listing.Gap();

            listing.CheckboxLabeled("MultiViewMod_RememberWindowPosition".Translate(), ref Settings.RememberWindowPosition,
                "MultiViewMod_RememberWindowPositionTip".Translate());
            listing.Gap();

            listing.CheckboxLabeled("MultiViewMod_RememberZoomLevel".Translate(), ref Settings.RememberZoomLevel,
                "MultiViewMod_RememberZoomLevelTip".Translate());

            listing.End();
            Widgets.EndScrollView();

            // 底部按钮区域
            DrawBottomButtons(inRect);
        }

        /// <summary>
        /// 绘制底部按钮
        /// </summary>
        private void DrawBottomButtons(Rect inRect)
        {
            Rect bottomRect = new Rect(inRect.x, inRect.yMax - 35f, inRect.width, 35f);

            // 重置按钮
            if (Widgets.ButtonText(new Rect(bottomRect.x, bottomRect.y, 120f, 35f), "MultiViewMod_ResetToDefaults".Translate()))
            {
                Settings.ResetToDefaults();
                Messages.Message("MultiViewMod_SettingsReset".Translate(), MessageTypeDefOf.PositiveEvent);
            }

            // 应用按钮
            if (Widgets.ButtonText(new Rect(bottomRect.xMax - 100f, bottomRect.y, 100f, 35f), "MultiViewMod_Apply".Translate()))
            {
                Settings.Write();
                Messages.Message("MultiViewMod_SettingsApplied".Translate(), MessageTypeDefOf.PositiveEvent);
            }
        }

        // 滚动位置变量
        private Vector2 scrollPosition = Vector2.zero;
    }
}