using System.Collections.Generic;
using DG.Tweening;
using JN.Client.Manager;
using JN.Client.Messages;
using JN.Client.UI;
using QFramework;
using TMPro;
using UnityEngine;
namespace JN.Client.Scene
{
    /// <summary>
    /// 负责地块相关的运行时逻辑。
    /// </summary>
    public class TileManager : MonoBehaviour
    {
        private const string WarningPrefabPath = "Assets/Res/Resources/UI/Menu/WarningPrefab.prefab";

        public static TileManager Instance;

        public Dictionary<int, BuildingInfo> AllBuildingDatas = new();
        public Dictionary<int, BuildingItemUI> AllBuildingUIs = new();
        public Dictionary<int, Tile> AllTiles = new();

        [Header("地块 UI 配置")]
        [SerializeField] public GameObject buildingUIPrefab;
        [SerializeField] public Camera SceneCamera;

        [Header("建筑等级 Prefab")]
        [SerializeField] private GameObject buildingLevel1Prefab;
        [SerializeField] private GameObject buildingLevel2Prefab;
        [SerializeField] private GameObject buildingLevel3Prefab;
        
        [SerializeField] private GameObject warningPrefab;

        [Header("调试测试")]
        [SerializeField] private bool useVirtualTestData = true;

        private bool m_Initialized;

        /// <summary>
        /// 初始化组件引用和运行时状态。
        /// </summary>
        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// 在场景启动后补齐依赖并刷新初始显示。
        /// </summary>
        private void Start()
        {
            if (m_Initialized)
            {
                return;
            }

            m_Initialized = true;
            JiangNanUIKitBootstrap.Initialize();
            FetchBuildingDataFromSave();
            ApplyVirtualTestDataIfNeeded();
            InitTiles();
            OpenBuildingScenePanel();
        }

        /// <summary>
        /// 获取场景相机。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        public Camera GetSceneCamera()
        {
            return SceneCamera != null ? SceneCamera : Camera.main;
        }

        /// <summary>
        /// 按等级获取建筑预制体。
        /// </summary>
        /// <param name="buildingLevel">等级。</param>
        /// <returns>返回匹配到的对象引用。</returns>
        public GameObject GetBuildingPrefabForLevel(int buildingLevel)
        {
            return buildingLevel switch
            {
                1 => buildingLevel1Prefab,
                2 => buildingLevel2Prefab,
                3 => buildingLevel3Prefab,
                _ => null
            };
        }

        /// <summary>
        /// 更新地块。
        /// </summary>
        /// <param name="tileId">数据编号。</param>
        /// <param name="new信息">参数值。</param>
        public void UpdateTile(int tileId, BuildingInfo newInfo)
        {
            AllBuildingDatas[tileId] = newInfo;
            DataManager.Instance.UpsertBuildingInfo(newInfo);

            if (AllTiles.TryGetValue(tileId, out var tile))
            {
                tile.SetBuildingInfoData(newInfo);
            }

            UIKit.GetPanel<BuildingItemSceneController>()?.RefreshTile(tileId);
        }

        /// <summary>
        /// 刷新全部地块的场景表现和跟随 UI。
        /// </summary>
        public void RefreshAllTileViews()
        {
            foreach (var tilePair in AllTiles)
            {
                var tileId = tilePair.Key;
                var tile = tilePair.Value;
                if (tile == null)
                {
                    continue;
                }

                tile.SetBuildingInfoData(AllBuildingDatas.TryGetValue(tileId, out var info) ? info : null);
            }

            UIKit.GetPanel<BuildingItemSceneController>()?.RefreshAllTiles();
        }

        /// <summary>
        /// 初始化地块列表和虚拟建筑数据。
        /// </summary>
        private void InitTiles()
        {
            AllTiles.Clear();
            var tilesInScene = FindObjectsByType<Tile>(FindObjectsSortMode.None);

            foreach (var tile in tilesInScene)
            {
                var id = tile.GetTileIdFromInternal();
                AllTiles[id] = tile;

                if (AllBuildingDatas.TryGetValue(id, out var info))
                {
                    tile.SetBuildingInfoData(info);
                }
                else
                {
                    // 没有存档数据的地块按空地处理。
                    tile.SetBuildingInfoData(null);
                }
            }
        }

        /// <summary>
        /// 打开建筑场景面板。
        /// </summary>
        private void OpenBuildingScenePanel()
        {
            var panelData = new BuildingItemSceneControllerData
            {
                TileManager = this
            };

            var panel = UIKit.GetPanel<BuildingItemSceneController>();
            if (panel == null)
            {
                UIKit.OpenPanel<BuildingItemSceneController>(UILevel.Common, panelData);
            }
            else
            {
                panel.Open(panelData);
            }
           
        }

        /// <summary>
        /// 从存档读取大地图建筑数据。
        /// </summary>
        private void FetchBuildingDataFromSave()
        {
            AllBuildingDatas.Clear();
            foreach (var info in DataManager.Instance.GetTownBuildingInfos())
            {
                AllBuildingDatas[info.tileId] = new BuildingInfo
                {
                    tileId = info.tileId, //8个
                    name = info.name,
                    playerId = info.playerId,
                    status = info.status,
                    buildingLevel = info.buildingLevel,
                    buildingTime = info.buildingTime,
                    buildingId = info.buildingId,
                    value = info.value,
                    celebrationTime = info.celebrationTime
                };
            }
        }

        /// <summary>
        /// 应用虚拟测试数据如果需要。
        /// </summary>
        private void ApplyVirtualTestDataIfNeeded()
        {
            if (!useVirtualTestData)
            {
                return;
            }

            // 大地图 测试模式下先放 4 个不属于自己的建筑，方便验证 界面 和点击限制。
            ApplyVirtualBuilding(1, 21, "东市茶铺", Random.Range(1, 4), 2, 0, 0);
            ApplyVirtualBuilding(2, 22, "西街酒肆", Random.Range(1, 4), 2, 0, 0);
            ApplyVirtualBuilding(3, 23, "南坊布庄", Random.Range(1, 4), 2, 0, 0);
            ApplyVirtualBuilding(5, 24, "北巷点心铺", Random.Range(1, 4), 2, 0, 0);
        }

        private void ApplyVirtualBuilding(
            int tileId,
            int playerId,
            string name,
            int buildingLevel,
            int status,
            int buildingTime,
            int celebrationTime)
        {
            AllBuildingDatas[tileId] = new BuildingInfo
            {
                tileId = tileId,
                playerId = playerId,
                name = name,
                buildingId = 1,
                buildingLevel = buildingLevel,
                status = status,
                buildingTime = buildingTime,
                celebrationTime = celebrationTime
            };
        }

        private void SpawnWarning(string text, Transform parent, GameObject obj = null, bool isRed = true)
        {
            var prefab = ResolveWarningPrefab();
            if (prefab == null)
            {
                Debug.LogWarning($"[TileManager] 缺少提示预制体：{WarningPrefabPath}");
                return;
            }

            var warning = Instantiate(prefab, parent);
            var warningText = warning.GetComponent<TMP_Text>();
            if (warningText == null)
            {
                warningText = warning.GetComponentInChildren<TMP_Text>(true);
            }

            if (warningText != null)
            {
                warningText.text = text;
            }

            if (!isRed)
            {
                if (warningText != null)
                {
                    warningText.color = Color.white;
                }
            }
            var currentPos = warning.transform.position;
            if (obj == null)
            {
                warning.transform.position = new Vector3(currentPos.x + 25, currentPos.y + 45, currentPos.z);
            }
            else
            {
                var pos = Camera.main.WorldToScreenPoint(obj.transform.position);
                warning.transform.position = new Vector3(pos.x + 25, pos.y + 45, pos.z);
            }
            var newPos = warning.transform.position;

            warning.transform.DOMove(new Vector3(newPos.x + 45f, newPos.y + 65f, newPos.z), 1.5f).SetEase(Ease.InQuad);
            var canvas = warning.GetComponent<CanvasGroup>();
            canvas.DOFade(0f, 1f).SetDelay(.5f);
            Destroy(warning.gameObject, 2f);
        }

        /// <summary>
        /// 获取提示预制体，优先用场景拖拽，其次走运行时资源加载。
        /// </summary>
        /// <returns>提示预制体。</returns>
        private GameObject ResolveWarningPrefab()
        {
            if (warningPrefab != null)
            {
                return warningPrefab;
            }

            warningPrefab = GameplayResourceStore.LoadAsset<GameObject>(WarningPrefabPath);
            return warningPrefab;
        }

        /// <summary>
        /// 销毁时释放监听、协程和运行时缓存。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            var panel = UIKit.GetPanel<BuildingItemSceneController>();
            if (panel != null)
            {
                UIKit.ClosePanel<BuildingItemSceneController>();
            }
        }
    }
}
