using System.Collections.Generic;
using JN.Client.Manager;
using JN.Client.Messages;
using JN.Client.UI;
using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JN.Client.Scene
{
    [RequireComponent(typeof(Collider))]
    /// <summary>
    /// 负责地块相关的运行时逻辑。
    /// </summary>
    public class Tile : MonoBehaviour
    {
        private const string AddLandSpritePath = "Assets/Res/Resources/Textures/UI/Panel/TavernScene/add_land.png";
        private const string LandPurchaseSpritePrefabPath = "Assets/Res/Resources/Scenes/Town/LandPurchaseSprite.prefab";
        private const string LandPriceWorldPrefabPath = "Assets/Res/Resources/Scenes/Town/LandPriceWorld.prefab";

        private static int ResolveSelfPlayerId()
        {
            return DataManager.Instance != null ? DataManager.Instance.GetLocalPlayerNumericId() : 0;
        }

        public int tileId;
        public BuildingInfo buildingInfo;
        public BuildingItemUI linkedUI;

        [SerializeField] private GameObject groundIndicator;
        [SerializeField] private SpriteRenderer landPurchaseSpriteRenderer;
        [SerializeField] private LandPriceWorld landPriceWorld;
        [SerializeField] private Vector3 landPurchaseSpriteOffset = new(0f, 0.08f, 0f);
        [SerializeField] private Vector3 landPurchaseSpriteEuler = new(90f, 0f, 0f);
        [SerializeField] private Vector3 landPurchaseSpriteScale = new(30f, 30f, 30f);
        [SerializeField] private float landPurchaseSpriteBoundsPadding = 1.2f;
        [SerializeField] private Transform buildingRoot;
        [SerializeField] private bool snapBuildingToTileCenter = true;

        private GameObject m_CurrentBuildingVisual;
        private int m_CurrentVisualLevel;
        private Sprite m_AddLandSprite;
        private Collider m_TileCollider;

        /// <summary>
        /// 初始化组件引用和运行时状态。
        /// </summary>
        private void Awake()
        {
            GetTileIdFromInternal();
            m_TileCollider = GetComponent<Collider>();
        }

        /// <summary>
        /// 尝试通过场景相机射线命中当前地块。
        /// </summary>
        /// <param name="pointerPosition">点击坐标。</param>
        /// <returns>命中当前地块时返回 true。</returns>
        public static bool TryHandlePointerClick(Vector2 pointerPosition)
        {
            if (IsPointerOverInteractiveUI(pointerPosition))
            {
                return false;
            }

            var cameras = ResolveRaycastCameras();
            for (var cameraIndex = 0; cameraIndex < cameras.Count; cameraIndex++)
            {
                var rayCamera = cameras[cameraIndex];
                if (rayCamera == null)
                {
                    continue;
                }

                var ray = rayCamera.ScreenPointToRay(pointerPosition);
                var hits = Physics.RaycastAll(ray, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                for (var index = 0; index < hits.Length; index++)
                {
                    var hitCollider = hits[index].collider;
                    if (hitCollider == null)
                    {
                        continue;
                    }

                    var hitTile = hitCollider.GetComponentInParent<Tile>();
                    if (hitTile == null)
                    {
                        continue;
                    }

                    hitTile.HandlePrimaryActionFromUI();
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 设置建筑信息数据。
        /// </summary>
        /// <param name="info">参数值。</param>
        public void SetBuildingInfoData(BuildingInfo info)
        {
            buildingInfo = info;

            if (groundIndicator != null)
            {
                // 没有建筑归属时显示空地提示，方便玩家识别可购买地块。
                groundIndicator.SetActive(false);
            }

            RefreshLandPurchaseSprite();
            RefreshLandPriceWorld();
            RefreshBuildingVisual();
            linkedUI?.SetData(info);
        }

        /// <summary>
        /// 获取地块内部编号。
        /// </summary>
        /// <returns>返回计算后的数值。</returns>
        public int GetTileIdFromInternal()
        {
            if (tileId <= 0)
            {
                var index = gameObject.name.LastIndexOf('_');
                if (index >= 0 && int.TryParse(gameObject.name[(index + 1)..], out var id))
                {
                    tileId = id;
                }
            }

            return tileId;
        }

        /// <summary>
        /// 处理来自界面的主要操作。
        /// </summary>
        public void HandlePrimaryActionFromUI()
        {
            var selfPlayerId = ResolveSelfPlayerId();

            if (buildingInfo != null && buildingInfo.playerId == selfPlayerId && buildingInfo.buildingLevel <= 0)
            {
                OpenBuildWindow();
                return;
            }

            // 别人的建筑和地块只展示信息，不允许购买或进入。
            if (buildingInfo != null && buildingInfo.playerId != 0)
            {
                return;
            }

            if (!DataManager.Instance.TryPurchaseTownLand(tileId, out var message))
            {
                Debug.LogWarning($"[Tile] 购买地块失败：{message}");
                return;
            }

            var coinTransform = GOReferenceManager.Instance != null ? GOReferenceManager.Instance.GetCoinTransform() : null;
            if (coinTransform != null && linkedUI != null)
            {
                GameUIEffects.PlayCoinsFly(coinTransform, linkedUI.transform);
            }

            var newInfo = DataManager.Instance.GetTownBuildingInfos().Find(info => info.tileId == tileId);
            TileManager.Instance.UpdateTile(tileId, newInfo);
            TileManager.Instance.RefreshAllTileViews();
        }

        /// <summary>
        /// 打开自己地块上的建筑建造窗口。
        /// </summary>
        private void OpenBuildWindow()
        {
            var data = new NewBuildingWindowControllerData
            {
                tileId = tileId,
                confirmAction = () =>
                {
                    if (groundIndicator != null)
                    {
                        groundIndicator.SetActive(false);
                    }
                }
            };

            UIKit.OpenPanel<NewBuildingWindowController>(UILevel.PopUI, data);
            Signals.Get<StartBuildingSignal>().Dispatch(tileId);
        }

        /// <summary>
        /// 处理来自界面的进入酒楼操作。
        /// </summary>
        public void EnterTavernFromUI()
        {
            // 只有自己名下且已建成的建筑才能进入酒楼。
            var selfPlayerId = ResolveSelfPlayerId();
            if (buildingInfo == null
                || buildingInfo.playerId != selfPlayerId
                || buildingInfo.status != 2)
            {
                return;
            }

            DataManager.Instance.SetActiveOwnedBuilding(tileId, buildingInfo.buildingLevel);

            // 进入酒楼后补齐酒楼状态栏与开业窗口。
            StartCoroutine(GameManager.Instance.LoadSceneAsync("GamePlay_Tavern", () =>
            {
                if (UIKit.GetPanel<TownStatusBarPanelController>() != null)
                {
                    UIKit.ClosePanel<TownStatusBarPanelController>();
                }

                UIKit.OpenPanel<TavernStatusBarPanelController>(UILevel.Common);
                UIKit.OpenPanel<StartOpeningWindowController>(UILevel.PopUI);
            }));
        }

        /// <summary>
        /// 处理指针是否悬停在可交互界面相关逻辑。
        /// </summary>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private static bool IsPointerOverInteractiveUI(Vector2 pointerPosition)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = pointerPosition
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            for (var i = 0; i < results.Count; i++)
            {
                var hit = results[i].gameObject;
                if (hit == null)
                {
                    continue;
                }

                if (hit.GetComponentInParent<Selectable>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 刷新建筑表现。
        /// </summary>
        private void RefreshBuildingVisual()
        {
            if (buildingInfo == null
                || buildingInfo.playerId == 0
                || buildingInfo.buildingLevel <= 0
                || buildingInfo.status != 2)
            {
                ClearBuildingVisual();
                return;
            }

            var prefab = TileManager.Instance != null
                ? TileManager.Instance.GetBuildingPrefabForLevel(buildingInfo.buildingLevel)
                : null;
            if (prefab == null)
            {
                ClearBuildingVisual();
                return;
            }

            if (m_CurrentBuildingVisual != null && m_CurrentVisualLevel == buildingInfo.buildingLevel)
            {
                return;
            }

            ClearBuildingVisual();

            var parent = buildingRoot != null ? buildingRoot : transform;
            m_CurrentBuildingVisual = Instantiate(prefab, parent);
            m_CurrentBuildingVisual.name = $"Tile_{tileId}_BuildingLv{buildingInfo.buildingLevel}";
            if (snapBuildingToTileCenter)
            {
                // 建筑表现默认吸附到地块中心，避免不同 预制体 原点不一致带来偏移。
                m_CurrentBuildingVisual.transform.localPosition = Vector3.zero;
            }

            m_CurrentVisualLevel = buildingInfo.buildingLevel;
        }

        /// <summary>
        /// 清理建筑表现。
        /// </summary>
        private void ClearBuildingVisual()
        {
            if (m_CurrentBuildingVisual != null)
            {
                Destroy(m_CurrentBuildingVisual);
                m_CurrentBuildingVisual = null;
            }

            m_CurrentVisualLevel = 0;
        }

        /// <summary>
        /// 刷新场景内的可购买地块加号；加号使用 SpriteRenderer，不走 UI 层。
        /// </summary>
        private void RefreshLandPurchaseSprite()
        {
            var selfPlayerId = ResolveSelfPlayerId();
            var canPurchaseLand = (buildingInfo == null || buildingInfo.playerId == 0)
                                  && !DataManager.Instance.HasOwnedTownLand(selfPlayerId);

            var renderer = EnsureLandPurchaseSpriteRenderer();
            if (renderer == null)
            {
                return;
            }

            ApplyLandPurchaseSpriteLayout(renderer);
            renderer.gameObject.SetActive(canPurchaseLand);
        }

        /// <summary>
        /// 刷新场景内的地块价格标牌。
        /// </summary>
        private void RefreshLandPriceWorld()
        {
            if (landPriceWorld == null)
            {
                return;
            }

            landPriceWorld.gameObject.SetActive(false);
        }

        /// <summary>
        /// 获取或创建场景内地块价格标牌。
        /// </summary>
        private LandPriceWorld EnsureLandPriceWorld()
        {
            var prefab = LoadLandPriceWorldPrefab();
            if (landPriceWorld != null && (prefab == null || landPriceWorld.HasConfiguredBindings))
            {
                landPriceWorld.Bind(this);
                return landPriceWorld;
            }

            var existing = transform.Find("LandPrice_World");
            if (existing != null)
            {
                var existingWorld = existing.GetComponent<LandPriceWorld>();
                if (prefab == null && existingWorld != null)
                {
                    landPriceWorld = existingWorld;
                    landPriceWorld.Bind(this);
                    return landPriceWorld;
                }

                Destroy(existing.gameObject);
            }

            if (landPriceWorld != null && landPriceWorld.transform.parent == transform)
            {
                Destroy(landPriceWorld.gameObject);
                landPriceWorld = null;
            }

            if (prefab != null)
            {
                var instance = Instantiate(prefab, transform, false);
                instance.name = "LandPrice_World";
                landPriceWorld = instance.GetComponent<LandPriceWorld>();
            }

            if (landPriceWorld == null)
            {
                var go = new GameObject("LandPrice_World");
                go.transform.SetParent(transform, false);
                landPriceWorld = go.AddComponent<LandPriceWorld>();
            }

            landPriceWorld.Bind(this);
            return landPriceWorld;
        }

        /// <summary>
        /// 读取场景内地块价格标牌预制体。
        /// </summary>
        /// <returns>读取成功返回预制体，否则返回 null。</returns>
        private static GameObject LoadLandPriceWorldPrefab()
        {
            var prefab = GameplayResourceStore.LoadAsset<GameObject>(LandPriceWorldPrefabPath);
            if (prefab != null)
            {
                return prefab;
            }

            return Resources.Load<GameObject>("Scenes/Town/LandPriceWorld");
        }

        /// <summary>
        /// 获取或加载地块上的场景加号 SpriteRenderer。
        /// </summary>
        /// <returns>可购买地块加号渲染器。</returns>
        private SpriteRenderer EnsureLandPurchaseSpriteRenderer()
        {
            if (landPurchaseSpriteRenderer != null)
            {
                return landPurchaseSpriteRenderer;
            }

            if (groundIndicator != null)
            {
                landPurchaseSpriteRenderer = groundIndicator.GetComponent<SpriteRenderer>();
            }

            if (landPurchaseSpriteRenderer == null)
            {
                var prefab = LoadLandPurchaseSpritePrefab();
                if (prefab != null)
                {
                    var spriteObject = Instantiate(prefab, transform, false);
                    spriteObject.name = "LandPurchaseSprite";
                    landPurchaseSpriteRenderer = spriteObject.GetComponent<SpriteRenderer>();
                }
                else
                {
                    Debug.LogWarning($"[Tile] 缺少地块购买提示预制体：{LandPurchaseSpritePrefabPath}，已改为运行时创建精灵节点兜底。");
                    var spriteObject = new GameObject("LandPurchaseSprite");
                    spriteObject.transform.SetParent(transform, false);
                    landPurchaseSpriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
                }
            }

            var loadedAddLandSprite = LoadAddLandSprite();
            if (loadedAddLandSprite != null)
            {
                // 仅在成功读取到新贴图时覆盖，避免把 prefab 自带 sprite 清空。
                landPurchaseSpriteRenderer.sprite = loadedAddLandSprite;
            }

            if (landPurchaseSpriteRenderer.sprite == null)
            {
                landPurchaseSpriteRenderer.gameObject.SetActive(false);
                return landPurchaseSpriteRenderer;
            }

            ApplyLandPurchaseSpriteLayout(landPurchaseSpriteRenderer);
            EnsureLandPurchaseSpriteCollider(landPurchaseSpriteRenderer);
            return landPurchaseSpriteRenderer;
        }

        /// <summary>
        /// 将绿色地块图片铺到当前地块碰撞盒中心，并覆盖整个地块。
        /// </summary>
        /// <param name="renderer">绿色地块渲染器。</param>
        private void ApplyLandPurchaseSpriteLayout(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.sortingOrder = 50;
            renderer.transform.localEulerAngles = landPurchaseSpriteEuler;
            // renderer.transform.localScale = ResolveLandPurchaseSpriteScale(renderer.sprite);

            var tileCollider = GetComponent<Collider>();
            if (tileCollider == null)
            {
                renderer.transform.localPosition = landPurchaseSpriteOffset;
                return;
            }

            var worldCenter = tileCollider.bounds.center + transform.TransformVector(landPurchaseSpriteOffset);
            renderer.transform.position = worldCenter;
            EnsureLandPurchaseSpriteCollider(renderer);
        }

        /// <summary>
        /// 根据地块碰撞盒尺寸计算绿色加号缩放，使其覆盖整个地块。
        /// </summary>
        /// <param name="sprite">绿色加号图片。</param>
        /// <returns>适配地块尺寸后的本地缩放。</returns>
        private Vector3 ResolveLandPurchaseSpriteScale(Sprite sprite)
        {
            var tileCollider = GetComponent<Collider>();
            if (sprite == null || tileCollider == null || sprite.bounds.size.x <= 0f || sprite.bounds.size.y <= 0f)
            {
                return landPurchaseSpriteScale;
            }

            var lossyScale = landPurchaseSpriteRenderer != null ? landPurchaseSpriteRenderer.transform.lossyScale : Vector3.one;
            var bounds = tileCollider.bounds;
            var parentScale = landPurchaseSpriteRenderer != null && landPurchaseSpriteRenderer.transform.parent != null
                ? landPurchaseSpriteRenderer.transform.parent.lossyScale
                : transform.lossyScale;
            var safeScaleX = Mathf.Abs(parentScale.x) > 0.0001f ? Mathf.Abs(parentScale.x) : Mathf.Max(0.0001f, Mathf.Abs(lossyScale.x));
            var safeScaleZ = Mathf.Abs(parentScale.z) > 0.0001f ? Mathf.Abs(parentScale.z) : Mathf.Max(0.0001f, Mathf.Abs(lossyScale.z));
            var scaleX = Mathf.Max(bounds.size.x, bounds.size.z) / (sprite.bounds.size.x * safeScaleX);
            var scaleY = Mathf.Max(bounds.size.x, bounds.size.z) / (sprite.bounds.size.y * safeScaleZ);
            return new Vector3(scaleX, scaleY, 1f) * landPurchaseSpriteBoundsPadding;
        }

        /// <summary>
        /// 读取可购买地块使用的绿色加号图片。
        /// </summary>
        /// <returns>读取成功返回 add_land Sprite，否则返回 null。</returns>
        private Sprite LoadAddLandSprite()
        {
            if (m_AddLandSprite != null)
            {
                return m_AddLandSprite;
            }

            m_AddLandSprite = GameplayResourceStore.LoadAsset<Sprite>(AddLandSpritePath);
            return m_AddLandSprite;
        }

        /// <summary>
        /// 读取可购买地块场景提示预制体。
        /// </summary>
        /// <returns>读取成功返回预制体，否则返回 null。</returns>
        private static GameObject LoadLandPurchaseSpritePrefab()
        {
            var prefab = GameplayResourceStore.LoadAsset<GameObject>(LandPurchaseSpritePrefabPath);
            if (prefab != null)
            {
                return prefab;
            }

            // 再兜底一次直接 Resources 路径，规避完整路径转换异常时的漏载。
            return Resources.Load<GameObject>("Scenes/Town/LandPurchaseSprite");
        }

        /// <summary>
        /// 为绿色地块提示节点补齐点击碰撞体，覆盖当前地块区域。
        /// </summary>
        /// <param name="renderer">绿色地块渲染器。</param>
        private void EnsureLandPurchaseSpriteCollider(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            var tileCollider = m_TileCollider != null ? m_TileCollider : GetComponent<Collider>();
            if (tileCollider == null)
            {
                return;
            }

            var boxCollider = renderer.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = renderer.gameObject.AddComponent<BoxCollider>();
            }

            boxCollider.isTrigger = true;
            var bounds = tileCollider.bounds;
            boxCollider.center = renderer.transform.InverseTransformPoint(bounds.center);
            boxCollider.size = new Vector3(bounds.size.x, Mathf.Max(0.2f, bounds.size.y + 0.5f), bounds.size.z);
        }

        /// <summary>
        /// 获取可用于场景点击射线的摄像机列表。
        /// </summary>
        /// <returns>按优先级排序后的摄像机列表。</returns>
        private static List<Camera> ResolveRaycastCameras()
        {
            var result = new List<Camera>(4);
            if (Camera.main != null)
            {
                result.Add(Camera.main);
            }

            var cameras = Camera.allCameras;
            for (var index = 0; index < cameras.Length; index++)
            {
                var camera = cameras[index];
                if (camera == null || !camera.isActiveAndEnabled || result.Contains(camera))
                {
                    continue;
                }

                result.Add(camera);
            }

            return result;
        }
    }
}
