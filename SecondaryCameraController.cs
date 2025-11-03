// SecondaryCameraController.cs - 修复拖影问题版本
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace MultiViewMod
{
    /// <summary>
    /// 次级相机控制器，管理渲染纹理和相机行为 - 1.6 拖影修复版
    /// </summary>
    public class SecondaryCameraController
    {
        private Camera secondaryCamera;
        private RenderTexture renderTexture;
        private readonly Vector2Int textureSize = new Vector2Int(800, 600);

        // 更新相机位置和渲染
        private CellRect currentSecondaryViewRect;

        // 更新pawn相机外显示实现
        private int viewportId = -1;

        public Vector3 rootPos;
        public float rootSize = 24f;
        public CellRect currentViewRect;
        private bool isInitialized = false;
        private Pawn followTarget;
        private bool isFollowing = false;
        private Vector2 lastWindowSize;
        private int framesSinceLastRender = 0;
        private const int RENDER_INTERVAL = 1;//相机渲染频率

        // 平移速度相关参数
        private const float BASE_PAN_SPEED = 0.015f;
        private const float MIN_PAN_SPEED_FACTOR = 0.3f;
        private const float MAX_PAN_SPEED_FACTOR = 3.0f;
        private const float PAN_SPEED_EXPONENT = 0.7f; // 指数因子，控制缩放对速度的影响程度

        // 使用模组设置中的值
        public float GetMinZoom() => MultiViewMod.Settings?.MinZoom ?? 0.5f;
        public float GetMaxZoom() => MultiViewMod.Settings?.MaxZoom ?? 120f;
        public float GetDefaultZoom() => MultiViewMod.Settings?.DefaultZoom ?? 24f;
        public float GetZoomSpeedFactor() => MultiViewMod.Settings?.ZoomSpeedFactor ?? 1.0f;

        /// <summary>
        /// 初始化相机控制器
        /// </summary>
        public void Initialize()
        {
            try
            {
                // Log.Message("[MultiViewMod] 阶段1: 尝试创建渲染纹理");

                if (Find.Camera == null || Find.CurrentMap == null)
                {
                    Log.Warning("[MultiViewMod] 游戏尚未完全加载，延迟渲染纹理创建");
                    return;
                }

                // 使用更高质量的渲染纹理设置
                renderTexture = new RenderTexture(textureSize.x, textureSize.y, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 2,  // 增加抗锯齿
                    filterMode = FilterMode.Bilinear, // 使用双线性过滤减少闪烁
                    wrapMode = TextureWrapMode.Clamp,
                    autoGenerateMips = false, // 禁用Mipmaps避免闪烁
                    depth = 24
                };

                if (!renderTexture.Create())
                {
                    Log.Error("[MultiViewMod] Failed to create render texture");
                    renderTexture = null;
                    return;
                }

                // Log.Message("[MultiViewMod] 渲染纹理创建成功");
                CreateSecondaryCamera();
                isInitialized = true;
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] 阶段1初始化失败: {e}");
                isInitialized = false;
            }
        }

        /// <summary>
        /// 创建次级相机
        /// </summary>
        private void CreateSecondaryCamera()
        {
            try
            {
                // Log.Message("[MultiViewMod] 创建次级摄像机");

                GameObject cameraObject = new GameObject("SecondaryCamera_MultiViewMod")
                {
                    layer = LayerMask.NameToLayer("Default")
                };

                secondaryCamera = cameraObject.AddComponent<Camera>();
                Camera mainCamera = Find.Camera;

                if (mainCamera == null)
                {
                    Log.Error("[MultiViewMod] Main camera is null");
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                    return;
                }

                secondaryCamera.CopyFrom(mainCamera);
                secondaryCamera.targetTexture = renderTexture;
                secondaryCamera.depth = mainCamera.depth - 1;
                secondaryCamera.cullingMask = GetSecondaryViewMask();
                secondaryCamera.enabled = true;
                secondaryCamera.allowMSAA = true; // 启用MSAA
                secondaryCamera.allowHDR = false;

                // 修复拖影的关键设置
                secondaryCamera.clearFlags = CameraClearFlags.SolidColor; // 使用纯色清除
                secondaryCamera.backgroundColor = new Color(0.13f, 0.13f, 0.13f, 1f); // 使用深灰色背景
                secondaryCamera.orthographic = true;
                secondaryCamera.useOcclusionCulling = false; // 禁用遮挡剔除避免渲染问题

                Map currentMap = Find.CurrentMap;
                if (currentMap != null)
                {
                    rootPos = new Vector3(currentMap.Size.x / 2f, 15f, currentMap.Size.z / 2f);
                }
                else
                {
                    rootPos = new Vector3(50f, 15f, 50f);
                }

                // 设置默认缩放
                rootSize = GetDefaultZoom();

                OptimizedPostProcessingCopy();

                //CopyPostProcessingEffects();

                //SearchForRenderMethods();

                //CopyMainCameraSettings();

                UnityEngine.Object.DontDestroyOnLoad(cameraObject);
                // Log.Message("[MultiViewMod] 摄像机创建并连接到渲染纹理成功");
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] 摄像机创建失败: {e}");
            }
        }

        private void OptimizedPostProcessingCopy()
        {
            try
            {
                Camera mainCamera = Find.Camera;
                if (mainCamera == null || secondaryCamera == null) return;

                // 只复制最关键的效果，避免性能开销
                string[] optimizedEffects = {
            "ColorCorrectionCurves",    // 颜色校正（最关键）
            "ColorCorrectionLookup",    // 颜色查找表
            "Tonemapping",              // 色调映射
            "Bloom",                    // 泛光效果
            "VignetteAndChromaticAberration" // 暗角
        };

                foreach (string effectName in optimizedEffects)
                {
                    try
                    {
                        Component sourceComp = mainCamera.GetComponent(effectName);
                        if (sourceComp != null)
                        {
                            // 检查是否已存在
                            Component existingComp = secondaryCamera.GetComponent(effectName);
                            if (existingComp == null)
                            {
                                Component newComp = secondaryCamera.gameObject.AddComponent(sourceComp.GetType());
                                CopyComponentProperties(sourceComp, newComp, sourceComp.GetType());
                                Log.Message($"[MultiViewMod] 优化复制: {effectName}");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Message($"[MultiViewMod] 复制 {effectName} 跳过: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 优化后处理复制失败: {e}");
            }
        }

        private void CopyPostProcessingEffects()
        {
            try
            {
                Camera mainCamera = Find.Camera;
                if (mainCamera == null || secondaryCamera == null) return;

                Log.Message("[MultiViewMod] === 开始复制后处理效果 ===");

                // 获取主相机上所有的图像效果组件
                Component[] mainComponents = mainCamera.GetComponents<Component>();

                foreach (Component comp in mainComponents)
                {
                    if (comp == null) continue;

                    Type compType = comp.GetType();
                    string typeName = compType.FullName;

                    // 检查是否是图像效果组件
                    if (typeName != null && typeName.Contains("UnityStandardAssets.ImageEffects"))
                    {
                        Log.Message($"[MultiViewMod] 发现图像效果组件: {compType.Name}");

                        try
                        {
                            // 复制组件到次级相机
                            Component secondaryComp = secondaryCamera.gameObject.AddComponent(compType);
                            CopyComponentProperties(comp, secondaryComp, compType);

                            Log.Message($"[MultiViewMod] 成功复制: {compType.Name}");
                        }
                        catch (Exception e)
                        {
                            Log.Error($"[MultiViewMod] 复制 {compType.Name} 失败: {e}");
                        }
                    }
                }

                Log.Message("[MultiViewMod] === 后处理效果复制完成 ===");
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 复制后处理效果失败: {e}");
            }
        }

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

                        // 记录重要的颜色相关属性
                        if (field.Name.ToLower().Contains("color") ||
                            field.Name.ToLower().Contains("curve") ||
                            field.Name.ToLower().Contains("tone"))
                        {
                            Log.Message($"[MultiViewMod]   字段: {field.Name} = {value}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Message($"[MultiViewMod]   字段复制失败 {field.Name}: {ex.Message}");
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

                            // 记录重要的颜色相关属性
                            if (property.Name.ToLower().Contains("color") ||
                                property.Name.ToLower().Contains("curve") ||
                                property.Name.ToLower().Contains("tone"))
                            {
                                Log.Message($"[MultiViewMod]   属性: {property.Name} = {value}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Message($"[MultiViewMod]   属性复制失败 {property.Name}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 复制组件属性失败: {e}");
            }
        }

        private void CopyMainCameraSettings()
        {
            if (Find.Camera == null) return;

            Camera mainCamera = Find.Camera;

            // 复制关键的渲染设置
            secondaryCamera.renderingPath = mainCamera.renderingPath;
            secondaryCamera.allowHDR = mainCamera.allowHDR;
            secondaryCamera.allowMSAA = mainCamera.allowMSAA;
            //secondaryCamera.backgroundColor = mainCamera.backgroundColor;
            // 确保使用相同的投影矩阵
            //secondaryCamera.projectionMatrix = mainCamera.projectionMatrix;

            // 设置相同的近远裁剪平面
            secondaryCamera.nearClipPlane = mainCamera.nearClipPlane;
            secondaryCamera.farClipPlane = mainCamera.farClipPlane;

            // 确保使用正确的渲染路径
            //secondaryCamera.renderingPath = RenderingPath.Forward; // 环世界通常使用前向渲染

            // 尝试复制后期处理效果
            //var mainPostProcess = mainCamera.GetComponent<PostProcessLayer>();
            //if (mainPostProcess != null)
            //{
            //    var secondaryPostProcess = secondaryCamera.gameObject.AddComponent<PostProcessLayer>();
            //    // 复制后期处理设置...
            //}
        }

        private void SearchForRenderMethods()
        {
            try
            {
                Log.Message("[MultiViewMod] === 搜索渲染方法 ===");

                // 搜索包含特定方法的类型
                Assembly[] allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (Assembly assembly in allAssemblies)
                {
                    string assemblyName = assembly.GetName().Name;
                    if (assemblyName.Contains("Assembly-CSharp") ||
                        assemblyName.Contains("RimWorld") ||
                        assemblyName.Contains("Verse"))
                    {
                        try
                        {
                            Type[] types = assembly.GetTypes();
                            foreach (Type type in types)
                            {
                                SearchTypeForRenderMethods(type);
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Message($"[MultiViewMod] 无法加载程序集 {assemblyName} 的类型: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"[MultiViewMod] 搜索渲染方法失败: {e}");
            }
        }

        private void SearchTypeForRenderMethods(Type type)
        {
            try
            {
                // 查找 OnRenderImage 方法（Unity标准后处理方法）
                MethodInfo onRenderImage = type.GetMethod("OnRenderImage",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (onRenderImage != null)
                {
                    Log.Message($"[MultiViewMod] 找到 OnRenderImage 方法在: {type.FullName}");
                }

                // 查找其他渲染相关方法
                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (MethodInfo method in methods)
                {
                    if (method.Name.Contains("PostProcess") &&
                        !method.Name.Contains("PostProcess") && // 排除我们之前找到的那个
                        (method.GetParameters().Length == 2 || method.GetParameters().Length == 3))
                    {
                        ParameterInfo[] parameters = method.GetParameters();
                        bool hasRenderTextureParams = parameters.Any(p => p.ParameterType == typeof(RenderTexture));

                        if (hasRenderTextureParams)
                        {
                            Log.Message($"[MultiViewMod] 找到可能的图像后处理方法: {type.FullName}.{method.Name}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 忽略无法访问的类型
            }
        }

        /// <summary>
        /// 获取次级相机的视图遮罩
        /// </summary>
        private int GetSecondaryViewMask()
        {
            try
            {
                int mask = 0;
                mask |= 1 << LayerMask.NameToLayer("Default");
                mask |= 1 << LayerMask.NameToLayer("Terrain");
                mask |= 1 << LayerMask.NameToLayer("Buildings");
                mask |= 1 << LayerMask.NameToLayer("Pawns");
                mask |= 1 << LayerMask.NameToLayer("Items");
                mask |= 1 << LayerMask.NameToLayer("FogOfWar");
                mask |= 1 << LayerMask.NameToLayer("Visceral");
                mask |= 1 << LayerMask.NameToLayer("Projectiles");
                return mask;
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Error getting view mask: {e}");
                return -1;
            }
        }

        /// <summary>
        /// 检查相机是否准备就绪
        /// </summary>
        public bool IsReady()
        {
            return isInitialized && secondaryCamera != null && renderTexture != null;
        }

        /// <summary>
        /// 更新相机位置和渲染 - 修复拖影版本 - 增强版本
        /// </summary>
        public void UpdateCamera(Vector2 windowSize, Vector3? targetPos = null)
        {
            if (!IsReady()) return;

            try
            {
                // 现有更新逻辑...
                bool windowSizeChanged = windowSize != lastWindowSize;
                if (windowSizeChanged)
                {
                    lastWindowSize = windowSize;
                    UpdateViewport(windowSize);
                }

                if (targetPos.HasValue)
                {
                    rootPos = targetPos.Value;
                }

                if (isFollowing && followTarget != null && followTarget.Spawned)
                {
                    rootPos = followTarget.DrawPos;
                    rootPos.y = 15f;
                }

                if (secondaryCamera != null)
                {
                    Vector3 adjustedPos = new Vector3(rootPos.x, rootPos.y, rootPos.z - 0.001f);
                    secondaryCamera.transform.position = adjustedPos;
                    secondaryCamera.orthographicSize = rootSize;

                    framesSinceLastRender++;
                    if (framesSinceLastRender >= RENDER_INTERVAL)
                    {
                        framesSinceLastRender = 0;

                        // 更新次级相机视口并注册
                        UpdateViewRect();
                        if (!currentSecondaryViewRect.IsEmpty)
                        {
                            SecondaryViewportManager.RegisterViewport(currentSecondaryViewRect);
                        }

                        RenderTexture.active = renderTexture;
                        GL.Clear(true, true, secondaryCamera.backgroundColor);
                        RenderTexture.active = null;

                        secondaryCamera.Render();
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Camera update failed: {e}");
            }
        }

        /// <summary>
        /// 处理缩放输入
        /// </summary>
        public void HandleZoom(float zoomDelta)
        {
            if (!IsReady()) return;

            try
            {
                float minZoom = GetMinZoom();
                float maxZoom = GetMaxZoom();
                float zoomSpeedFactor = GetZoomSpeedFactor();

                // 改进的缩放逻辑：使用指数缩放，小值时变化慢，大值时变化快
                float zoomFactor = 1f;

                if (zoomDelta < 0) // 后滚 - 放大视野（减小size值）
                {
                    // 根据当前缩放级别调整缩放因子
                    if (rootSize < 10f)
                        zoomFactor = 0.95f; // 近距离时变化更细腻
                    else if (rootSize < 30f)
                        zoomFactor = 0.90f;
                    else if (rootSize < 60f)
                        zoomFactor = 0.85f;
                    else
                        zoomFactor = 0.80f; // 远距离时变化更快
                }
                else if (zoomDelta > 0) // 前滚 - 缩小视野（增大size值）
                {
                    // 根据当前缩放级别调整缩放因子
                    if (rootSize < 10f)
                        zoomFactor = 1.05f; // 近距离时变化更细腻
                    else if (rootSize < 30f)
                        zoomFactor = 1.10f;
                    else if (rootSize < 60f)
                        zoomFactor = 1.15f;
                    else
                        zoomFactor = 1.20f; // 远距离时变化更快
                }

                // 应用缩放速度系数
                zoomFactor = Mathf.Pow(zoomFactor, zoomSpeedFactor);

                float newSize = rootSize * zoomFactor;

                // 应用缩放限制
                rootSize = Mathf.Clamp(newSize, minZoom, maxZoom);

                // Log.Message($"[MultiViewMod] 缩放: {rootSize:F1} (范围: {minZoom}-{maxZoom})");
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Zoom handling failed: {e}");
            }
        }

        /// <summary>
        /// 处理平移输入
        /// </summary>
        public void HandlePan(Vector2 delta)
        {
            if (!IsReady()) return;

            try
            {
                if (isFollowing)
                {
                    isFollowing = false;
                    followTarget = null;
                }

                // 优化后的平移速度计算：使用非线性函数根据缩放级别调整速度
                float panSpeed = CalculateAdaptivePanSpeed();

                // 根据缩放级别调整平移速度
                //float panSpeed = rootSize * 0.015f;
                rootPos.x -= delta.x * panSpeed;
                rootPos.z += delta.y * panSpeed;
                ClampToMapBounds();
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Pan handling failed: {e}");
            }
        }

        /// <summary>
        /// 计算自适应平移速度
        /// </summary>
        private float CalculateAdaptivePanSpeed()
        {
            try
            {
                float minZoom = GetMinZoom();
                float maxZoom = GetMaxZoom();

                // 归一化当前缩放级别 (0-1范围)
                float normalizedZoom = (rootSize - minZoom) / (maxZoom - minZoom);

                // 使用指数函数计算速度因子，在极端缩放级别下速度变化更平缓
                float speedFactor = Mathf.Pow(normalizedZoom, PAN_SPEED_EXPONENT);

                // 将速度因子映射到最小和最大速度范围
                speedFactor = MIN_PAN_SPEED_FACTOR + speedFactor * (MAX_PAN_SPEED_FACTOR - MIN_PAN_SPEED_FACTOR);

                // 应用基础速度和窗口尺寸影响
                float adaptiveSpeed = BASE_PAN_SPEED * speedFactor * rootSize;

                // 限制最大和最小速度，避免极端情况
                float minSpeed = BASE_PAN_SPEED * MIN_PAN_SPEED_FACTOR * minZoom;
                float maxSpeed = BASE_PAN_SPEED * MAX_PAN_SPEED_FACTOR * maxZoom;
                adaptiveSpeed = Mathf.Clamp(adaptiveSpeed, minSpeed, maxSpeed);

                // 调试信息（可选）
                // Log.Message($"[MultiViewMod] 平移速度: {adaptiveSpeed:F4} (缩放: {rootSize:F1}, 因子: {speedFactor:F2})");

                return adaptiveSpeed;
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] 计算平移速度失败: {e}");
                return BASE_PAN_SPEED * rootSize; // 回退到原始计算方式
            }
        }


        /// <summary>
        /// 快速缩放到指定大小
        /// </summary>
        public void ZoomTo(float targetSize)
        {
            float minZoom = GetMinZoom();
            float maxZoom = GetMaxZoom();
            rootSize = Mathf.Clamp(targetSize, minZoom, maxZoom);
            // Log.Message($"[MultiViewMod] 快速缩放到: {rootSize:F1}");
        }

        /// <summary>
        /// 重置缩放
        /// </summary>
        public void ResetZoom()
        {
            rootSize = GetDefaultZoom();
            // Log.Message($"[MultiViewMod] 重置缩放: {rootSize:F1}");
        }

        /// <summary>
        /// 更新视口大小
        /// </summary>
        private void UpdateViewport(Vector2 size)
        {
            try
            {
                if (renderTexture != null &&
                    (renderTexture.width != (int)size.x || renderTexture.height != (int)size.y) &&
                    size.x > 100 && size.y > 100)
                {
                    renderTexture.Release();
                    renderTexture = new RenderTexture((int)size.x, (int)size.y, 24, RenderTextureFormat.ARGB32)
                    {
                        antiAliasing = 2,
                        filterMode = FilterMode.Bilinear,
                        wrapMode = TextureWrapMode.Clamp,
                        autoGenerateMips = false,
                        depth = 24
                    };

                    if (renderTexture.Create() && secondaryCamera != null)
                    {
                        secondaryCamera.targetTexture = renderTexture;
                    }
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Viewport update failed: {e}");
            }
        }

        /// <summary>
        /// 更新视图矩形 - 增强版本 - 修复区块边界问题版本
        /// </summary>
        private void UpdateViewRect()
        {
            try
            {
                Map currentMap = Find.CurrentMap;
                if (currentMap == null)
                {
                    currentSecondaryViewRect = CellRect.Empty;
                    return;
                }

                float aspectRatio = (float)textureSize.x / (float)textureSize.y;
                Vector2 viewSize = new Vector2(rootSize * aspectRatio, rootSize);

                // 计算基础视口
                CellRect baseViewRect = new CellRect(
                    Mathf.FloorToInt(rootPos.x - viewSize.x / 2f),
                    Mathf.FloorToInt(rootPos.z - viewSize.y / 2f),
                    Mathf.CeilToInt(viewSize.x),
                    Mathf.CeilToInt(viewSize.y)
                );

                // 扩展视口以确保覆盖完整的区块边界
                currentSecondaryViewRect = ExpandToSectionBounds(baseViewRect, currentMap);
                currentSecondaryViewRect.ClipInsideMap(currentMap);

                // 注册/更新视口
                if (!currentSecondaryViewRect.IsEmpty)
                {
                    if (viewportId == -1)
                    {
                        viewportId = SecondaryViewportManager.RegisterViewport(currentSecondaryViewRect);
                    }
                    else
                    {
                        SecondaryViewportManager.UpdateViewport(viewportId, currentSecondaryViewRect);
                    }

                    // 调试信息
                    // Log.Message($"[MultiViewMod] 次级相机视口: {currentSecondaryViewRect}, 扩展前: {baseViewRect}");
                }
                else if (viewportId != -1)
                {
                    SecondaryViewportManager.UnregisterViewport(viewportId);
                    viewportId = -1;
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] View rect update failed: {e}");
                currentSecondaryViewRect = CellRect.Empty;

                if (viewportId != -1)
                {
                    SecondaryViewportManager.UnregisterViewport(viewportId);
                    viewportId = -1;
                }
            }
        }

        /// <summary>
        /// 扩展CellRect到完整的区块边界
        /// </summary>
        private CellRect ExpandToSectionBounds(CellRect rect, Map map)
        {
            if (rect.IsEmpty) return rect;

            const int SECTION_SIZE = 17; // RimWorld 区块大小

            // 计算区块坐标
            int minSectionX = Mathf.FloorToInt((float)rect.minX / SECTION_SIZE);
            int minSectionZ = Mathf.FloorToInt((float)rect.minZ / SECTION_SIZE);
            int maxSectionX = Mathf.CeilToInt((float)rect.maxX / SECTION_SIZE);
            int maxSectionZ = Mathf.CeilToInt((float)rect.maxZ / SECTION_SIZE);

            // 扩展到的区块边界
            int expandedMinX = minSectionX * SECTION_SIZE;
            int expandedMinZ = minSectionZ * SECTION_SIZE;
            int expandedMaxX = (maxSectionX * SECTION_SIZE) + SECTION_SIZE - 1;
            int expandedMaxZ = (maxSectionZ * SECTION_SIZE) + SECTION_SIZE - 1;

            // 确保不超出地图边界
            expandedMinX = Mathf.Max(expandedMinX, 0);
            expandedMinZ = Mathf.Max(expandedMinZ, 0);
            expandedMaxX = Mathf.Min(expandedMaxX, map.Size.x - 1);
            expandedMaxZ = Mathf.Min(expandedMaxZ, map.Size.z - 1);

            return new CellRect(expandedMinX, expandedMinZ,
                               expandedMaxX - expandedMinX + 1,
                               expandedMaxZ - expandedMinZ + 1);
        }

        /// <summary>
        /// 限制相机在地图边界内
        /// </summary>
        private void ClampToMapBounds()
        {
            try
            {
                Map map = Find.CurrentMap;
                if (map == null) return;

                float aspectRatio = (float)textureSize.x / (float)textureSize.y;
                Vector2 viewSize = new Vector2(rootSize * aspectRatio, rootSize);

                // 添加边界缓冲，避免相机过于接近地图边缘
                float bufferX = viewSize.x * 0.1f;
                float bufferZ = viewSize.y * 0.1f;

                rootPos.x = Mathf.Clamp(rootPos.x, viewSize.x / 2f, map.Size.x - viewSize.x / 2f);
                rootPos.z = Mathf.Clamp(rootPos.z, viewSize.y / 2f, map.Size.z - viewSize.y / 2f);
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Map bounds clamping failed: {e}");
            }
        }

        /// <summary>
        /// 获取渲染纹理
        /// </summary>
        public RenderTexture GetRenderTexture()
        {
            return IsReady() ? renderTexture : null;
        }

        /// <summary>
        /// 设置跟随目标
        /// </summary>
        public void SetFollowTarget(Pawn pawn)
        {
            followTarget = pawn;
            isFollowing = (pawn != null);

            if (isFollowing && followTarget.Spawned)
            {
                rootPos = followTarget.DrawPos;
                rootPos.y = 15f;

                // 确保缩放比例在设置范围内
                float minZoom = GetMinZoom();
                float maxZoom = GetMaxZoom();
                rootSize = Mathf.Clamp(rootSize, minZoom, maxZoom);

                if (secondaryCamera != null)
                {
                    secondaryCamera.transform.position = new Vector3(rootPos.x, rootPos.y, rootPos.z);
                    secondaryCamera.orthographicSize = rootSize;
                }

                // Log.Message($"[MultiViewMod] 开始跟随目标: {followTarget.Name}, 保持缩放: {rootSize:F1}");
            }
        }

        /// <summary>
        /// 取消跟随
        /// </summary>
        public void CancelFollow()
        {
            isFollowing = false;
            followTarget = null;
            // Log.Message("[MultiViewMod] 取消跟随目标");
        }

        /// <summary>
        /// 检查是否正在跟随
        /// </summary>
        public bool IsFollowing()
        {
            return isFollowing && followTarget != null;
        }

        /// <summary>
        /// 获取当前缩放信息
        /// </summary>
        public string GetZoomInfo()
        {
            float minZoom = GetMinZoom();
            float maxZoom = GetMaxZoom();
            float zoomPercent = (rootSize - minZoom) / (maxZoom - minZoom) * 100f;
            return "MultiViewMod_ZoomInfo".Translate(rootSize.ToString("F1"), zoomPercent.ToString("F0"));
        }

        /// <summary>
        /// 清理资源 - 增强版本
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // 取消注册视口
                if (!currentSecondaryViewRect.IsEmpty)
                {
                    SecondaryViewportManager.UpdateViewport(viewportId, currentSecondaryViewRect);
                }

                // 现有清理逻辑...
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                    renderTexture = null;
                }

                if (secondaryCamera != null && secondaryCamera.gameObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(secondaryCamera.gameObject);
                    secondaryCamera = null;
                }

                isInitialized = false;
                isFollowing = false;
                followTarget = null;

                // 增加相机外pawn显示实现
                if (viewportId != -1)
                {
                    SecondaryViewportManager.UnregisterViewport(viewportId);
                    viewportId = -1;
                }
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Cleanup failed: {e}");
            }
        }
    }
}