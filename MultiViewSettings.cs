// MultiViewSettings.cs
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MultiViewMod
{
    /// <summary>
    /// MultiView模组设置类，处理设置数据的保存和加载
    /// </summary>
    public class MultiViewSettings : ModSettings
    {
        // 缩放设置
        public float ZoomSpeedFactor = 1.0f;
        public float MinZoom = 0.5f;
        public float MaxZoom = 120f;
        public float DefaultZoom = 12f;

        // 窗口设置
        public bool AutoFollowSelected = true;
        public bool RememberWindowPosition = true;
        public bool RememberZoomLevel = true;

        // 快捷键设置
        public bool EnableHotkeys = true;
        public bool OpenWindowHotkey = true;
        public bool CloseWindowHotkey = true;
        public bool ToggleFollowHotkey = true;
        public bool ResetZoomHotkey = true;

        // 窗口位置记忆
        public float SavedWindowX = 0f;
        public float SavedWindowY = 0f;
        public float SavedWindowWidth = 800f;
        public float SavedWindowHeight = 600f;
        public bool HasSavedPosition = false;

        // 缩放比例记忆
        public float SavedZoomLevel = 12f;
        public bool HasSavedZoom = false;

        public override void ExposeData()
        {
            base.ExposeData();

            // 保存缩放设置
            Scribe_Values.Look(ref ZoomSpeedFactor, "ZoomSpeedFactor", 1.0f);
            Scribe_Values.Look(ref MinZoom, "MinZoom", 0.5f);
            Scribe_Values.Look(ref MaxZoom, "MaxZoom", 120f);
            Scribe_Values.Look(ref DefaultZoom, "DefaultZoom", 12f);

            // 保存窗口设置
            Scribe_Values.Look(ref AutoFollowSelected, "AutoFollowSelected", false);
            Scribe_Values.Look(ref RememberWindowPosition, "RememberWindowPosition", true);
            Scribe_Values.Look(ref RememberZoomLevel, "RememberZoomLevel", true);

            // 保存快捷键设置
            Scribe_Values.Look(ref EnableHotkeys, "EnableHotkeys", true);
            Scribe_Values.Look(ref OpenWindowHotkey, "OpenWindowHotkey", true);
            Scribe_Values.Look(ref CloseWindowHotkey, "CloseWindowHotkey", true);
            Scribe_Values.Look(ref ToggleFollowHotkey, "ToggleFollowHotkey", true);
            Scribe_Values.Look(ref ResetZoomHotkey, "ResetZoomHotkey", true);

            // 保存窗口位置
            Scribe_Values.Look(ref SavedWindowX, "SavedWindowX", 0f);
            Scribe_Values.Look(ref SavedWindowY, "SavedWindowY", 0f);
            Scribe_Values.Look(ref SavedWindowWidth, "SavedWindowWidth", 800f);
            Scribe_Values.Look(ref SavedWindowHeight, "SavedWindowHeight", 600f);
            Scribe_Values.Look(ref HasSavedPosition, "HasSavedPosition", false);

            // 保存缩放比例
            Scribe_Values.Look(ref SavedZoomLevel, "SavedZoomLevel", 12f);
            Scribe_Values.Look(ref HasSavedZoom, "HasSavedZoom", false);

            // 仅保留关键日志
            // Log.Message($"[MultiViewMod] 设置已{(Scribe.mode == LoadSaveMode.Saving ? "保存" : "加载")}");
        }

        /// <summary>
        /// 保存窗口位置
        /// </summary>
        public void SaveWindowPosition(Rect rect)
        {
            // 只有在启用记住窗口位置时才保存
            if (!RememberWindowPosition)
            {
                HasSavedPosition = false;
                return;
            }

            SavedWindowX = rect.x;
            SavedWindowY = rect.y;
            SavedWindowWidth = rect.width;
            SavedWindowHeight = rect.height;
            HasSavedPosition = true;

            // Log.Message($"[MultiViewMod] 窗口位置已保存: {rect}");
        }

        /// <summary>
        /// 获取保存的窗口位置
        /// </summary>
        public Rect? GetSavedWindowPosition()
        {
            // 只有在启用记住窗口位置且有保存的位置时才返回
            if (!RememberWindowPosition || !HasSavedPosition) return null;

            var rect = new Rect(SavedWindowX, SavedWindowY, SavedWindowWidth, SavedWindowHeight);
            // Log.Message($"[MultiViewMod] 获取保存的窗口位置: {rect}");
            return rect;
        }

        /// <summary>
        /// 保存缩放比例
        /// </summary>
        public void SaveZoomLevel(float zoomLevel)
        {
            // 只有在启用记住缩放比例时才保存
            if (!RememberZoomLevel)
            {
                HasSavedZoom = false;
                return;
            }

            SavedZoomLevel = Mathf.Clamp(zoomLevel, MinZoom, MaxZoom);
            HasSavedZoom = true;

            // Log.Message($"[MultiViewMod] 缩放比例已保存: {SavedZoomLevel}");
        }

        /// <summary>
        /// 获取保存的缩放比例
        /// </summary>
        public float? GetSavedZoomLevel()
        {
            // 只有在启用记住缩放比例且有保存的值时才返回
            if (!RememberZoomLevel || !HasSavedZoom) return null;

            // Log.Message($"[MultiViewMod] 获取保存的缩放比例: {SavedZoomLevel}");
            return SavedZoomLevel;
        }

        /// <summary>
        /// 重置设置为默认值
        /// </summary>
        public void ResetToDefaults()
        {
            ZoomSpeedFactor = 1.0f;
            MinZoom = 0.5f;
            MaxZoom = 120f;
            DefaultZoom = 12f;
            AutoFollowSelected = true;
            RememberWindowPosition = true;
            RememberZoomLevel = true;
            EnableHotkeys = true;
            OpenWindowHotkey = true;
            CloseWindowHotkey = true;
            ToggleFollowHotkey = true;
            ResetZoomHotkey = true;

            Log.Message("[MultiViewMod] 设置已重置为默认值");
        }
    }
}