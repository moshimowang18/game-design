using System.Collections.Generic;
using DG.Tweening;
using Newtonsoft.Json;
using QFramework;
using TMPro;
using UnityEngine;

namespace JN.Client.UI
{
    public class BuildingItemSceneControllerData : UIPanelData
    {
        public JN.Client.Scene.TileManager TileManager;
    }

    /// <summary>
    /// 负责建筑物件场景相关的运行时逻辑。
    /// </summary>
    public class BuildingItemSceneController : QFrameworkPanel<BuildingItemSceneControllerData>
    {
        private const string BuildingItemPrefabPath = "Assets/Res/Resources/UI/Item/BuildingItem.prefab";
        private readonly Dictionary<int, JN.Client.Scene.BuildingItemUI> m_ItemViews = new();

        private RectTransform m_RectTransform;
        private RectTransform m_ContentRoot;
        private JN.Client.Scene.TileManager m_TileManager;
        private Camera m_SceneCamera;
        private Canvas m_RootCanvas;

        /// <summary>
        /// 面板初始化时绑定控件和事件。
        /// </summary>
        protected override void OnPanelInit()
        {
            m_RectTransform = transform as RectTransform;
            m_RootCanvas = GetComponentInParent<Canvas>();

            for (var index = 0; index < transform.childCount; index++)
            {
                var child = transform.GetChild(index);
                child.gameObject.SetActive(child.name == "Content");
            }

            EnsureContentRoot();
        }

        /// <summary>
        /// 面板打开时读取数据并刷新显示。
        /// </summary>
        /// <param name="data">数据。</param>
        protected override void OnPanelOpen(BuildingItemSceneControllerData data)
        {
            BindTileManager(data.TileManager);
        }

        /// <summary>
        /// 面板关闭时清理临时状态和监听。
        /// </summary>
        protected override void OnPanelClose()
        {
            ClearItems();
            m_TileManager = null;
            m_SceneCamera = null;
        }

        /// <summary>
        /// 在帧末同步跟随 界面 和场景表现位置。
        /// </summary>
        private void LateUpdate()
        {
            if (m_TileManager == null || m_SceneCamera == null)
            {
                return;
            }

            foreach (var item in m_ItemViews.Values)
            {
                if (item == null)
                {
                    continue;
                }

                RefreshItemPosition(item);
            }
        }

        /// <summary>
        /// 处理绑定地块管理器相关逻辑。
        /// </summary>
        /// <param name="tileManager">参数值。</param>
        public void BindTileManager(JN.Client.Scene.TileManager tileManager)
        {
            if (tileManager == null)
            {
                return;
            }

            m_TileManager = tileManager;
            m_SceneCamera = tileManager.GetSceneCamera();
            RebuildItems();
        }

        /// <summary>
        /// 刷新地块。
        /// </summary>
        /// <param name="tileId">数据编号。</param>
        public void RefreshTile(int tileId)
        {
            if (m_TileManager == null || !m_TileManager.AllTiles.TryGetValue(tileId, out var tile))
            {
                return;
            }

            if (!m_ItemViews.TryGetValue(tileId, out var item) || item == null)
            {
                item = CreateItem(tile);
                if (item == null)
                {
                    return;
                }
            }

            item.SetData(tile.buildingInfo);
            RefreshItemPosition(item);
        }

        /// <summary>
        /// 刷新全部地块 UI，常用于购买地块后隐藏其它可购买提示。
        /// </summary>
        public void RefreshAllTiles()
        {
            if (m_TileManager == null)
            {
                return;
            }

            foreach (var tileId in m_TileManager.AllTiles.Keys)
            {
                RefreshTile(tileId);
            }
        }

        /// <summary>
        /// 确保内容根节点存在。
        /// </summary>
        private void EnsureContentRoot()
        {
            if (m_ContentRoot != null)
            {
                return;
            }

            m_ContentRoot = transform.Find("Content") as RectTransform;
            if (m_ContentRoot == null)
            {
                m_ContentRoot = m_RectTransform;
                Debug.LogWarning("[BuildingItemSceneController] 缺少静态 Content 节点，已回退使用面板根节点。");
            }

            m_ContentRoot.anchorMin = Vector2.zero;
            m_ContentRoot.anchorMax = Vector2.one;
            m_ContentRoot.offsetMin = Vector2.zero;
            m_ContentRoot.offsetMax = Vector2.zero;
            m_ContentRoot.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>
        /// 重建建筑条目的子物件显示。
        /// </summary>
        private void RebuildItems()
        {
            ClearItems();

            if (m_TileManager == null)
            {
                return;
            }

            foreach (var tile in m_TileManager.AllTiles.Values)
            {
                var item = CreateItem(tile);
                if (item == null)
                {
                    continue;
                }

                item.SetData(tile.buildingInfo);
                RefreshItemPosition(item);
            }
        }

        /// <summary>
        /// 创建物件。
        /// </summary>
        /// <param name="tile">参数值。</param>
        /// <returns>返回方法执行后的结果。</returns>
        private JN.Client.Scene.BuildingItemUI CreateItem(JN.Client.Scene.Tile tile)
        {
            if (tile == null || m_TileManager == null)
            {
                return null;
            }

            var buildingItemPrefab = ResolveBuildingItemPrefab();
            if (buildingItemPrefab == null)
            {
                return null;
            }

            var itemObject = Instantiate(buildingItemPrefab, m_ContentRoot);
            var item = itemObject.GetComponent<JN.Client.Scene.BuildingItemUI>();
            if (item == null)
            {
                Destroy(itemObject);
                return null;
            }

            item.Bind(tile);
            m_ItemViews[tile.tileId] = item;
            tile.linkedUI = item;
            return item;
        }

        /// <summary>
        /// 优先从 Resources 目录读取 BuildingItem 预制体，确保场景运行时与资源目录保持一致。
        /// </summary>
        private GameObject ResolveBuildingItemPrefab()
        {
            var prefab = GameplayResourceStore.LoadAsset<GameObject>(BuildingItemPrefabPath);
            if (prefab != null)
            {
                return prefab;
            }

            return m_TileManager != null ? m_TileManager.buildingUIPrefab : null;
        }

        /// <summary>
        /// 刷新物件位置。
        /// </summary>
        /// <param name="item">参数值。</param>
        private void RefreshItemPosition(JN.Client.Scene.BuildingItemUI item)
        {
            var screenPoint = m_SceneCamera.WorldToScreenPoint(item.GetWorldAnchorPosition());
            if (screenPoint.z <= 0f)
            {
                item.SetVisible(false);
                return;
            }

            var uiCamera = m_RootCanvas != null && m_RootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? m_RootCanvas.worldCamera
                : null;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_RectTransform,
                    screenPoint,
                    uiCamera,
                    out var localPoint))
            {
                item.SetAnchoredPosition(localPoint);
                item.SetVisible(true);
            }
            else
            {
                item.SetVisible(false);
            }
        }

        /// <summary>
        /// 清理物件s。
        /// </summary>
        private void ClearItems()
        {
            if (m_TileManager != null)
            {
                foreach (var tile in m_TileManager.AllTiles.Values)
                {
                    if (tile != null)
                    {
                        tile.linkedUI = null;
                    }
                }
            }

            foreach (var item in m_ItemViews.Values)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            m_ItemViews.Clear();
        }
        
       
    }
}
