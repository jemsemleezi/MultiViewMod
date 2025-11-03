using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace MultiViewMod
{
    public class PostProcessingManager
    {
        private Camera secondaryCamera;
        private List<Component> activeEffects = new List<Component>();
        private Dictionary<Type, Component> effectCache = new Dictionary<Type, Component>();
        private int syncCounter = 0;
        private const int SYNC_INTERVAL = 60; // 每60帧同步一次

        public PostProcessingManager(Camera camera)
        {
            secondaryCamera = camera;
        }

        public void Initialize()
        {
            try
            {
                SyncWithMainCamera();
                SetEffectsEnabled(true);
                Log.Message($"[MultiViewMod] 后处理管理器初始化完成，找到 {activeEffects.Count} 个效果");
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 后处理管理器初始化失败: {e}");
            }
        }

        /// <summary>
        /// 与主相机同步后处理效果
        /// </summary>
        public void SyncWithMainCamera(bool preserveIntensity = true)
        {
            try
            {
                Camera mainCamera = Find.Camera;
                if (mainCamera == null || secondaryCamera == null)
                {
                    Log.Warning("[MultiViewMod] 无法同步后处理：相机为null");
                    return;
                }

                // 保存当前强度设置
                float currentIntensity = MultiViewMod.Settings.PostProcessingIntensity;

                Log.Message("[MultiViewMod] 开始增强同步后处理效果...");

                // 增强：获取主相机上所有可能的图像效果组件
                Component[] mainComponents = mainCamera.GetComponents<Component>();
                List<Component> imageEffects = new List<Component>();

                // 更广泛的图像效果检测
                foreach (Component mainComp in mainComponents)
                {
                    if (mainComp == null) continue;

                    Type compType = mainComp.GetType();
                    string typeName = compType.FullName;
                    string compName = compType.Name;

                    // 扩展检测条件
                    bool isImageEffect = false;

                    // 检测Unity标准图像效果
                    if (typeName != null && typeName.Contains("UnityStandardAssets.ImageEffects"))
                    {
                        isImageEffect = true;
                    }
                    // 检测RimWorld特有的图像效果
                    else if (typeName != null && (
                        typeName.Contains("RimWorld.PostProcess") ||
                        typeName.Contains("Verse.PostProcess") ||
                        typeName.Contains("ColorEffect") ||
                        typeName.Contains("Bloom") ||
                        typeName.Contains("ToneMapping") ||
                        typeName.Contains("Vignette")))
                    {
                        isImageEffect = true;
                    }
                    // 通过组件名称检测
                    else if (compName.Contains("PostProcess") ||
                             compName.Contains("Color") ||
                             compName.Contains("Bloom") ||
                             compName.Contains("Tone") ||
                             compName.Contains("Vignette") ||
                             compName.Contains("Effect"))
                    {
                        isImageEffect = true;
                    }

                    if (isImageEffect)
                    {
                        imageEffects.Add(mainComp);
                        Log.Message($"[MultiViewMod] 检测到图像效果: {compType.FullName}");
                    }
                }

                Log.Message($"[MultiViewMod] 总共检测到 {imageEffects.Count} 个图像效果组件");

                int copiedCount = 0;
                foreach (Component mainComp in imageEffects)
                {
                    Type compType = mainComp.GetType();

                    try
                    {
                        // 检查是否已存在该组件
                        Component existingComp = secondaryCamera.GetComponent(compType);
                        Component targetComp = existingComp;

                        if (existingComp == null)
                        {
                            // 创建新组件
                            targetComp = secondaryCamera.gameObject.AddComponent(compType);
                            activeEffects.Add(targetComp);
                            effectCache[compType] = targetComp;
                            Log.Message($"[MultiViewMod] 添加新效果: {compType.Name}");
                        }

                        // 复制所有属性
                        CopyComponentProperties(mainComp, targetComp, compType);

                        // 如果启用了同步且需要保持强度，重新应用强度设置
                        if (preserveIntensity && MultiViewMod.Settings.SyncPostProcessing)
                        {
                            ApplyIntensityToEffect(targetComp, currentIntensity);
                        }

                        copiedCount++;
                        Log.Message($"[MultiViewMod] 成功同步效果: {compType.Name}");
                    }
                    catch (Exception e)
                    {
                        Log.Error($"[MultiViewMod] 同步效果 {compType.Name} 失败: {e}");
                    }
                }

                // 如果启用了同步且需要保持强度，重新应用整体强度设置
                if (preserveIntensity && MultiViewMod.Settings.SyncPostProcessing)
                {
                    SetIntensity(currentIntensity, false); // 不触发重复同步
                }

                Log.Message($"[MultiViewMod] 后处理同步完成，成功处理了 {copiedCount} 个效果");

                // 增强：验证同步结果
                VerifySyncResult();
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 同步后处理失败: {e}");
            }
        }

        /// <summary>
        /// 验证同步结果
        /// </summary>
        private void VerifySyncResult()
        {
            try
            {
                Camera mainCamera = Find.Camera;
                if (mainCamera == null || secondaryCamera == null) return;

                List<string> mainEffects = new List<string>();
                List<string> secondaryEffects = new List<string>();

                // 获取主相机效果
                foreach (Component comp in mainCamera.GetComponents<Component>())
                {
                    if (comp != null && IsImageEffectComponent(comp))
                    {
                        mainEffects.Add(comp.GetType().Name);
                    }
                }

                // 获取次级相机效果
                foreach (Component comp in secondaryCamera.GetComponents<Component>())
                {
                    if (comp != null && IsImageEffectComponent(comp))
                    {
                        secondaryEffects.Add(comp.GetType().Name);
                    }
                }

                Log.Message($"[MultiViewMod] 同步验证 - 主相机效果: {mainEffects.Count} 个, 次级相机效果: {secondaryEffects.Count} 个");

                if (mainEffects.Count > 0)
                {
                    Log.Message($"[MultiViewMod] 主相机效果列表: {string.Join(", ", mainEffects)}");
                }

                if (secondaryEffects.Count > 0)
                {
                    Log.Message($"[MultiViewMod] 次级相机效果列表: {string.Join(", ", secondaryEffects)}");
                }

                // 检查缺失的效果
                var missingEffects = mainEffects.Except(secondaryEffects).ToList();
                if (missingEffects.Count > 0)
                {
                    Log.Warning($"[MultiViewMod] 缺失的效果: {string.Join(", ", missingEffects)}");
                }
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 同步验证失败: {e}");
            }
        }

        /// <summary>
        /// 判断是否为图像效果组件
        /// </summary>
        private bool IsImageEffectComponent(Component comp)
        {
            if (comp == null) return false;

            Type compType = comp.GetType();
            string typeName = compType.FullName;
            string compName = compType.Name;

            return (typeName != null && (
                typeName.Contains("UnityStandardAssets.ImageEffects") ||
                typeName.Contains("RimWorld.PostProcess") ||
                typeName.Contains("Verse.PostProcess") ||
                typeName.Contains("ColorEffect") ||
                compName.Contains("PostProcess") ||
                compName.Contains("Color") ||
                compName.Contains("Bloom") ||
                compName.Contains("Tone") ||
                compName.Contains("Vignette") ||
                compName.Contains("Effect")));
        }

        /// <summary>
        /// 设置后处理效果强度 - 修复版本
        /// </summary>
        public void SetIntensity(float intensity, bool allowResync = true)
        {
            try
            {
                if (secondaryCamera == null) return;

                // 确保强度在合理范围内
                intensity = Mathf.Clamp(intensity, 0.1f, 2.0f);

                Log.Message($"[MultiViewMod] 应用后处理强度: {intensity:F2}");

                // 应用强度到各个效果组件
                foreach (Component effect in activeEffects)
                {
                    if (effect == null) continue;
                    ApplyIntensityToEffect(effect, intensity);
                }

                // 只有在允许且启用同步时才重新同步
                if (allowResync && MultiViewMod.Settings.SyncPostProcessing)
                {
                    // 使用新的同步方法，保持强度设置
                    SyncWithMainCamera(true);
                }
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 设置后处理强度失败: {e}");
            }
        }

        /// <summary>
        /// 复制组件属性
        /// </summary>
        private void CopyComponentProperties(Component source, Component target, Type componentType)
        {
            try
            {
                // 复制字段
                FieldInfo[] fields = componentType.GetFields(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

                foreach (FieldInfo field in fields)
                {
                    try
                    {
                        object value = field.GetValue(source);
                        field.SetValue(target, value);
                    }
                    catch (Exception ex)
                    {
                        // 忽略无法复制的字段
                    }
                }

                // 复制属性
                PropertyInfo[] properties = componentType.GetProperties(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

                foreach (PropertyInfo property in properties)
                {
                    if (property.CanWrite && property.CanRead)
                    {
                        try
                        {
                            object value = property.GetValue(source);
                            property.SetValue(target, value);
                        }
                        catch (Exception ex)
                        {
                            // 忽略无法复制的属性
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 复制组件属性失败: {e}");
            }
        }

        /// <summary>
        /// 设置后处理效果强度
        /// </summary>
        public void SetIntensity(float intensity)
        {
            try
            {
                if (secondaryCamera == null) return;

                // 应用强度到各个效果组件
                foreach (Component effect in activeEffects)
                {
                    if (effect == null) continue;

                    ApplyIntensityToEffect(effect, intensity);
                }

                Log.Message($"[MultiViewMod] 后处理强度设置为: {intensity:F2}");
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 设置后处理强度失败: {e}");
            }
        }

        /// <summary>
        /// 对特定效果应用强度
        /// </summary>
        private void ApplyIntensityToEffect(Component effect, float intensity)
        {
            try
            {
                Type effectType = effect.GetType();
                string effectName = effectType.Name;

                // 根据效果类型调整不同的参数
                switch (effectName)
                {
                    case "Bloom":
                    case "BloomAndFlares":
                        SetEffectFloatProperty(effect, "bloomIntensity", intensity);
                        break;

                    case "ColorCorrectionCurves":
                    case "ColorCorrectionRamp":
                        SetEffectFloatProperty(effect, "saturation", intensity);
                        break;

                    case "VignetteAndChromaticAberration":
                        SetEffectFloatProperty(effect, "intensity", intensity * 0.5f); // 暗角强度减半
                        break;

                    case "Tonemapping":
                        SetEffectFloatProperty(effect, "exposureAdjustment", intensity);
                        break;

                    case "ContrastEnhance":
                        SetEffectFloatProperty(effect, "intensity", intensity);
                        break;

                    default:
                        // 尝试设置通用的强度属性
                        SetEffectFloatProperty(effect, "intensity", intensity);
                        SetEffectFloatProperty(effect, "bloomIntensity", intensity);
                        SetEffectFloatProperty(effect, "saturation", intensity);
                        break;
                }
            }
            catch (Exception e)
            {
                Log.Message($"[MultiViewMod] 应用强度到 {effect.GetType().Name} 失败: {e.Message}");
            }
        }

        /// <summary>
        /// 设置效果的浮点属性
        /// </summary>
        private void SetEffectFloatProperty(Component effect, string propertyName, float value)
        {
            try
            {
                PropertyInfo property = effect.GetType().GetProperty(propertyName);
                if (property != null && property.PropertyType == typeof(float))
                {
                    property.SetValue(effect, value, null);
                }

                // 也尝试字段
                FieldInfo field = effect.GetType().GetField(propertyName);
                if (field != null && field.FieldType == typeof(float))
                {
                    field.SetValue(effect, value);
                }
            }
            catch (Exception ex)
            {
                // 忽略设置失败的属性
            }
        }

        /// <summary>
        /// 启用/禁用后处理效果
        /// </summary>
        public void SetEffectsEnabled(bool enabled)
        {
            try
            {
                foreach (Component effect in activeEffects)
                {
                    if (effect is MonoBehaviour monoBehaviour)
                    {
                        monoBehaviour.enabled = enabled;
                    }
                }

                Log.Message($"[MultiViewMod] 后处理效果 {(enabled ? "启用" : "禁用")}");
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 设置后处理启用状态失败: {e}");
            }
        }

        /// <summary>
        /// 定期更新
        /// </summary>
        public void Update()
        {
            try
            {
                syncCounter++;

                // 定期同步设置（如果启用）- 减少同步频率避免性能问题
                if (MultiViewMod.Settings.SyncPostProcessing && syncCounter >= SYNC_INTERVAL)
                {
                    syncCounter = 0;

                    // 使用新的同步方法，保持用户设置的强度
                    SyncWithMainCamera(true);

                    Log.Message("[MultiViewMod] 定期同步后处理效果（保持强度）");
                }
                // 增强：每300帧进行一次深度同步验证
                if (syncCounter % 300 == 0)
                {
                    VerifySyncResult();
                }
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 后处理更新失败: {e}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (secondaryCamera != null)
                {
                    // 移除所有后处理组件
                    foreach (Component effect in activeEffects)
                    {
                        if (effect != null)
                        {
                            UnityEngine.Object.DestroyImmediate(effect);
                        }
                    }
                }

                activeEffects.Clear();
                effectCache.Clear();
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 后处理清理失败: {e}");
            }
        }
    }
}
