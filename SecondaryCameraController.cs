// SecondaryCameraController.cs - 修复拖影问题版本
using UnityEngine;
using Verse;
using System.Collections.Generic;
using RimWorld;

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

        public Vector3 rootPos;
        public float rootSize = 24f;
        public CellRect currentViewRect;
        private bool isInitialized = false;
        private Pawn followTarget;
        private bool isFollowing = false;
        private Vector2 lastWindowSize;
        private int framesSinceLastRender = 0;
        private const int RENDER_INTERVAL = 2;

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

                UnityEngine.Object.DontDestroyOnLoad(cameraObject);
                // Log.Message("[MultiViewMod] 摄像机创建并连接到渲染纹理成功");
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] 摄像机创建失败: {e}");
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
        /// 更新相机位置和渲染 - 修复拖影版本
        /// </summary>
        public void UpdateCamera(Vector2 windowSize, Vector3? targetPos = null)
        {
            if (!IsReady()) return;

            try
            {
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
                    // 修复拖影：使用微小的Z轴偏移避免深度冲突
                    Vector3 adjustedPos = new Vector3(rootPos.x, rootPos.y, rootPos.z - 0.001f);
                    secondaryCamera.transform.position = adjustedPos;
                    secondaryCamera.orthographicSize = rootSize;

                    framesSinceLastRender++;
                    if (framesSinceLastRender >= RENDER_INTERVAL)
                    {
                        framesSinceLastRender = 0;

                        // 修复拖影：强制清除渲染纹理
                        RenderTexture.active = renderTexture;
                        GL.Clear(true, true, secondaryCamera.backgroundColor);
                        RenderTexture.active = null;

                        secondaryCamera.Render();
                    }

                    UpdateViewRect();
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

                // 根据缩放级别调整平移速度
                float panSpeed = rootSize * 0.015f;
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
        /// 更新视图矩形
        /// </summary>
        private void UpdateViewRect()
        {
            try
            {
                Map currentMap = Find.CurrentMap;
                if (currentMap == null) return;

                float aspectRatio = (float)textureSize.x / (float)textureSize.y;
                Vector2 viewSize = new Vector2(rootSize * aspectRatio, rootSize);

                currentViewRect = new CellRect(
                    Mathf.FloorToInt(rootPos.x - viewSize.x / 2f),
                    Mathf.FloorToInt(rootPos.z - viewSize.y / 2f),
                    Mathf.CeilToInt(viewSize.x),
                    Mathf.CeilToInt(viewSize.y)
                );

                currentViewRect.ClipInsideMap(currentMap);
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] View rect update failed: {e}");
            }
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
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            try
            {
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
            }
            catch (System.Exception e)
            {
                Log.Error($"[MultiViewMod] Cleanup failed: {e}");
            }
        }
    }
}