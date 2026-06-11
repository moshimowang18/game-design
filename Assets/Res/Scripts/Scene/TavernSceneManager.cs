using System.Collections;
using System.Collections.Generic;
using JN.Client.Config;
using JN.Client.Manager;
using JN.Client.Model;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace JN.Client.Scene
{
    /// <summary>
    /// 负责酒楼场景相关的运行时逻辑。
    /// </summary>
    public partial class TavernSceneManager : MonoBehaviour
    {
        private const float NavMeshSampleDistance = 2f;

        public static TavernSceneManager Instance;

        public Dictionary<int, TableAreaUI> AllTableUIs = new();
        public Dictionary<int, TableArea> AllTables = new();

        private readonly List<UIFollowData> uiFollowList = new();
        private readonly List<GameObject> customerTemplates = new();
        private readonly List<GameObject> dishPrefabs = new();
        private readonly List<StagedDishEntry> stagedDishEntries = new();
        private readonly List<TavernCustomerRuntimeController> activeCustomers = new();
        private readonly List<TavernCustomerRuntimeController> queuedCustomers = new();
        private readonly Dictionary<int, TavernCustomerRuntimeController> tableCustomers = new();
        private readonly Dictionary<int, List<TavernCustomerRuntimeController>> tableCustomerGroups = new();
        private readonly Dictionary<int, Coroutine> autoCleanRoutines = new();
        private readonly Dictionary<int, GameObject> activeCleanSmokeEffects = new();
        // 处于待升级流程的桌位编号集合：阻止顾客入座，保证升级动画期间桌子始终空闲
        private readonly HashSet<int> pendingUpgradeTableIds = new();
        private readonly Dictionary<string, GameObject> guideStaffVisuals = new();
        private readonly Dictionary<string, List<GameObject>> guideStaffVisualGroups = new();
        // 正在播放入场动画的员工集合：刷新世界状态时不要把它们瞬移回锚点。
        private readonly HashSet<GameObject> staffVisualsBeingAnimated = new();
        private readonly HashSet<string> guidePendingKitchenItems = new();
        private readonly List<GuideWorldButton> guideWorldButtons = new();
        private readonly List<GuideWorldLabel> guideWorldLabels = new();
        private readonly List<GuidePurchaseAnchor> guideKitchenAnchors = new();
        private readonly Dictionary<GameObject, Coroutine> waiterTaskRoutines = new();
        private readonly HashSet<GameObject> busyWaiters = new();
        private int reservedServeDishCount;

        [Header("UI 跟随设置")]
        [SerializeField] public GameObject tableUIPrefab;
        [SerializeField] public Transform canvasParent;
        [SerializeField] public Camera SceneCamera;
        [SerializeField] private List<GameObject> tableMovePrefabList = new();

        [Header("Gameplay")]
        [SerializeField] private List<GameObject> customerPrefabAssets = new();
        [SerializeField] private float customerSpawnInterval = 10f;
        [SerializeField] private float dishCookInterval = 5f;
        [SerializeField] private float dishEatDuration = 5f;
        [SerializeField] private float autoCleanDuration = 2f;
        [SerializeField] private int tableCheckoutIncome = 120;
        [SerializeField] private int maxQueueSize = 4;
        [SerializeField] private int maxActiveCustomers = 8;
        [SerializeField] private int initialCustomerBurst = 2;
        [SerializeField] private float queueSpacing = 0.9f;
        [SerializeField] private float spawnLaneSpacing = 0.45f;

        private Transform customerEntryPoint;
        private Transform customerExitPoint;
        private Transform objectMovePoint;
        private Transform sceneObjectsRoot;
        private Canvas sceneCanvas;
        private GameObject guideCounterObject;
        private GameObject guideCounterBuildBase;
        private GameObject guideStoveObject;
        private GameObject guideStoveBuildBase;
        private GameObject foodTableObject;
        private GameObject guideSteamerObject;
        private GameObject platePrefab;
        private readonly List<GameObject> guideStoveSceneObjects = new();
        private GuideWorldButton guideCounterButton;
        private GuideWorldButton guideStoveButton;
        private GuideWorldButton guideShopkeeperButton;
        private GuideWorldButton guideChefButton;
        private GuideWorldButton guideWaiterButton;
        private GuideWorldLabel nextCustomerTimerLabel;
        private Coroutine cookRoutine;
        private Coroutine waiterServiceRoutine;
        private Coroutine waiterTaskRoutine;
        private bool customerSpawnLoopActive;
        private bool hasNavMesh;
        private bool tableLv2UpgradeUnlockInProgress;
        private float nextCustomerSpawnRemaining = -1f;
        private int nextChefCookIndex;

        private class UIFollowData
        {
            public Transform uiTransform;
            public Transform targetTile;
            public TableAreaUI tableUI;
        }

        private class GuideWorldButton
        {
            public RectTransform rectTransform;
            public Button button;
            public Image image;
            public Text text;
            public TMP_Text tmpText;
            public Transform target;
            public Vector3 worldOffset;
            public Vector3 scale = Vector3.one;
        }

        private class GuideWorldLabel
        {
            public RectTransform rectTransform;
            public Text text;
            public TMP_Text tmpText;
            public Image progressBackground;
            public Image progressFill;
            public Image queueBackground;
            public CanvasGroup canvasGroup;
            public Transform target;
            public Vector3 worldOffset;
            public Vector3 scale = Vector3.one;
            public Sprite defaultProgressSprite;
            public Sprite queuedProgressSprite;
        }

        private class GuidePurchaseAnchor
        {
            public string itemKey;
            public string displayName;
            public GameObject sceneObject;
            public GameObject buildBase;
            public GuideWorldButton button;
            public string carrierPrefabPath;
        }

        private class StagedDishEntry
        {
            public GameObject rootObject;
            public GameObject dishPrefab;
        }

        /// <summary>
        /// 处理点击场景中的购买提示底板。
        /// </summary>
        /// <param name="pointerPosition">屏幕坐标。</param>
        /// <returns>命中购买提示并消费点击时返回 true。</returns>
        public static bool TryHandlePurchasePointerClick(Vector2 pointerPosition)
        {
            if (Instance == null || Camera.main == null)
            {
                return false;
            }

            var ray = Camera.main.ScreenPointToRay(pointerPosition);
            var hits = Physics.RaycastAll(ray, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (var index = 0; index < hits.Length; index++)
            {
                var hitCollider = hits[index].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                if (Instance.TryHandleGuideBuildBaseClick(hitCollider))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取当前运行时已缓存的餐盘预制体。
        /// </summary>
        /// <returns>餐盘预制体；未加载时返回 null。</returns>
        public GameObject GetPlatePrefab()
        {
            return platePrefab;
        }

        /// <summary>
        /// 初始化组件引用和运行时状态。
        /// </summary>
        private void Awake()
        {
            Instance = this;
            Signals.Get<TavernBusinessStateSignal>().AddListener(HandleBusinessStateChanged);
            Signals.Get<GameplayGuideProgressSignal>().AddListener(RefreshGuideWorldState);
            Signals.Get<TavernRuntimeChangedSignal>().AddListener(RefreshAllTableRuntimeState);
        }

        /// <summary>
        /// 销毁时释放监听、协程和运行时缓存。
        /// </summary>
        private void OnDestroy()
        {
            Signals.Get<TavernBusinessStateSignal>().RemoveListener(HandleBusinessStateChanged);
            Signals.Get<GameplayGuideProgressSignal>().RemoveListener(RefreshGuideWorldState);
            Signals.Get<TavernRuntimeChangedSignal>().RemoveListener(RefreshAllTableRuntimeState);
        }

        /// <summary>
        /// 在场景启动后补齐依赖并刷新初始显示。
        /// </summary>
        private void Start()
        {
            ResolveSceneAnchors();
            ConfigureSceneUiCanvas();
            ResolveGuideSceneObjects();
            InitTablesAndUIs();
            EnsureGuideWorldButtons();
            EnsureGuideWorldLabels();
            CacheCustomerTemplates();
            CacheDishPrefabs();
            ApplyTimingConfig();
            hasNavMesh = TryGetNavMeshPosition(customerEntryPoint != null ? customerEntryPoint.position : Vector3.zero, out _);

            DataManager.Instance.ResetTransientTavernState();
            ApplySavedTableStates();
            RefreshGuideWorldState();

            if (DataManager.Instance.TavernData.isOpen && hasNavMesh)
            {
                StartBusinessLoop();
            }

            RefreshNextCustomerTimerLabel();
            RefreshAllTableRuntimeState();
            TryRevealTableLv2UpgradeFeature();
        }

        /// <summary>
        /// 从 TbConfig 表读取玩法时间配置，并覆盖当前场景的默认时长。
        /// </summary>
        private void ApplyTimingConfig()
        {
            customerSpawnInterval = TbConfigRuntime.GetCustomerRefreshTime(customerSpawnInterval);
            dishCookInterval = TbConfigRuntime.GetChefCookTime(dishCookInterval);
            dishEatDuration = TbConfigRuntime.GetCustomerEatTime(dishEatDuration);
            autoCleanDuration = TbConfigRuntime.GetTableCleanTime(autoCleanDuration);
        }

        /// <summary>
        /// 逐帧处理输入、状态推进或动画刷新。
        /// </summary>
        private void Update()
        {
            if (DataManager.Instance == null || DataManager.Instance.TavernData == null)
            {
                return;
            }

            if (!DataManager.Instance.TavernData.isOpen)
            {
                nextCustomerSpawnRemaining = -1f;
                RefreshNextCustomerTimerLabel();
                return;
            }

            if (!customerSpawnLoopActive && hasNavMesh)
            {
                StartBusinessLoop();
            }

            if (customerSpawnLoopActive && nextCustomerSpawnRemaining < 0f)
            {
                nextCustomerSpawnRemaining = customerSpawnInterval;
            }

            if (customerSpawnLoopActive && nextCustomerSpawnRemaining >= 0f)
            {
                nextCustomerSpawnRemaining = Mathf.Max(0f, nextCustomerSpawnRemaining - Time.deltaTime);
                if (nextCustomerSpawnRemaining <= 0f)
                {
                    SpawnCustomerIfPossible();
                    nextCustomerSpawnRemaining = customerSpawnInterval;
                }
            }

            RefreshNextCustomerTimerLabel();
        }

        /// <summary>
        /// 刷新全部桌位运行时状态。
        /// </summary>
        private void RefreshAllTableRuntimeState()
        {
            foreach (var tablePair in AllTables)
            {
                var tableData = DataManager.Instance.GetTableData(tablePair.Key);
                if (tableData == null || !tableData.isUnlocked)
                {
                    tablePair.Value.ApplySaveState(tableData);
                    continue;
                }

                tablePair.Value.RefreshRuntimeState((TavernTableRuntimeState)tableData.runtimeState);
            }
        }

        /// <summary>
        /// 启动移动桌位。
        /// </summary>
        /// <param name="桌位编号">数据编号。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool StartMoveTable(int tableId)
        {
            if (tableId <= 0 || tableId > tableMovePrefabList.Count)
            {
                return false;
            }

            var tableMovePrefab = tableMovePrefabList[tableId - 1];
            if (tableMovePrefab == null)
            {
                return false;
            }

            PrepareMovePrefabForManualMovement(tableMovePrefab);

            var moveSignal = tableMovePrefab.GetComponent<MoveRotateSignal>();
            if (moveSignal != null)
            {
                moveSignal.ConfigureTableId(tableId);
                moveSignal.OnArrived -= HandleTableMoveArrived;
                moveSignal.OnArrived += HandleTableMoveArrived;
                // 升级时同一个 prefab 会被多次激活，必须先把内部状态机和位姿
                // 还原到初始点，否则会出现 finished=true 立刻不动的卡住现象。
                moveSignal.ResetMovement();
            }

            tableMovePrefab.SetActive(true);
            return true;

            void HandleTableMoveArrived()
            {
                if (AllTables.TryGetValue(tableId, out var table))
                {
                    table.MarkUnlocked();
                    PlayGuideBuildingSuccessEffect(ResolveGuideDeliveryEffectPosition(table.transform));
                }

                Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
            }
        }

        /// <summary>
        /// 标记或清除桌位的待升级状态。被标记的桌位在升级动画结束前不会再分配新顾客。
        /// </summary>
        /// <param name="tableId">桌位编号。</param>
        /// <param name="upgrading">true 表示进入待升级状态，false 表示清除。</param>
        public void MarkTableUpgrading(int tableId, bool upgrading)
        {
            if (upgrading)
            {
                pendingUpgradeTableIds.Add(tableId);
            }
            else
            {
                pendingUpgradeTableIds.Remove(tableId);
            }
        }

        /// <summary>
        /// 判断桌位是否处于待升级状态。
        /// </summary>
        /// <param name="tableId">桌位编号。</param>
        /// <returns>处于待升级流程时返回 true。</returns>
        public bool IsTableUpgrading(int tableId)
        {
            return pendingUpgradeTableIds.Contains(tableId);
        }

        /// <summary>
        /// 判断桌位当前是否仍被顾客或服务任务占用。升级流程在该方法返回 false 后才允许真正搬走桌子。
        /// </summary>
        /// <param name="tableId">桌位编号。</param>
        /// <returns>有顾客就坐或仍有未完成的服务/清扫派发时返回 true。</returns>
        public bool IsTableOccupied(int tableId)
        {
            if (TryGetTableCustomerGroup(tableId, out var customers) && customers.Count > 0)
            {
                return true;
            }

            if (assignedServeTableIds.Contains(tableId) || assignedCleanTableIds.Contains(tableId))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断桌位是否仍存在会阻塞升级开始的占用。
        /// 这里仅关注“当前顾客是否还没离开”以及“是否还有未完成的上菜任务”，
        /// 不再把清扫视为升级阻塞条件，保证顾客离场后可以直接搬桌。
        /// </summary>
        /// <param name="tableId">桌位编号。</param>
        /// <returns>存在升级阻塞占用时返回 true，否则返回 false。</returns>
        public bool HasUpgradeBlockingOccupancy(int tableId)
        {
            if (TryGetTableCustomerGroup(tableId, out var customers) && customers.Count > 0)
            {
                return true;
            }

            return assignedServeTableIds.Contains(tableId);
        }

        /// <summary>
        /// 桌位进入待升级流程前，取消自动清理和清扫派发，
        /// 避免顾客离桌后又先跑去清理，导致升级继续排队。
        /// </summary>
        /// <param name="tableId">桌位编号。</param>
        public void PreparePendingTableUpgrade(int tableId)
        {
            StopAutoClean(tableId);
            CancelWaiterCleanTask(tableId);
        }

        /// <summary>
        /// 处理桌位交互操作。
        /// </summary>
        /// <param name="桌位编号">数据编号。</param>
        public void HandleTableInteraction(int tableId)
        {
            var tableData = DataManager.Instance.GetTableData(tableId);
            if (tableData == null || !AllTables.TryGetValue(tableId, out var table))
            {
                return;
            }

            var state = (TavernTableRuntimeState)tableData.runtimeState;
            switch (state)
            {
                case TavernTableRuntimeState.WaitingOrder:
                    StopTableOrderWait(tableId);
                    DataManager.Instance.SetTableRuntimeState(tableId, TavernTableRuntimeState.WaitingServe);
                    table.RefreshRuntimeState(TavernTableRuntimeState.WaitingServe, "待上菜");
                    table.linkedUI?.StopStateCountdown();
                    if (tableCustomers.TryGetValue(tableId, out var orderingCustomer) && orderingCustomer != null)
                    {
                        orderingCustomer.ShowOrderBubbles(GetRandomOrderNames());
                    }

                    Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
                    break;
                case TavernTableRuntimeState.WaitingServe:
                    if (DataManager.Instance.TavernData.availableDishes <= 0)
                    {
                        table.RefreshRuntimeState(TavernTableRuntimeState.WaitingServe, "待上菜");
                        return;
                    }

                    if (!TryStartWaiterServeTask(tableId))
                    {
                        table.RefreshRuntimeState(TavernTableRuntimeState.WaitingServe, "待上菜");
                    }

                    break;
                case TavernTableRuntimeState.Checkout:
                    CompleteCheckout(tableId);
                    break;
            }
        }

        /// <summary>
        /// 获取排队顾客数量。
        /// </summary>
        /// <returns>返回计算后的数值。</returns>
        public int GetQueueCustomerCount()
        {
            return queuedCustomers.Count;
        }

        /// <summary>
        /// 获取下一位顾客刷新倒计时剩余秒数。
        /// </summary>
        public float GetNextCustomerSpawnRemaining()
        {
            return nextCustomerSpawnRemaining;
        }

        /// <summary>
        /// 获取顾客刷新间隔。
        /// </summary>
        public float GetCustomerSpawnInterval()
        {
            return customerSpawnInterval;
        }

        /// <summary>
        /// 设置场景中顾客进店倒计时标签显隐。
        /// </summary>
        public void SetWorldCustomerEnterProgressVisible(bool visible)
        {
            if (nextCustomerTimerLabel?.rectTransform != null)
            {
                nextCustomerTimerLabel.rectTransform.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// 获取当前顾客数量。
        /// </summary>
        /// <returns>返回计算后的数值。</returns>
        public int GetActiveCustomerCount()
        {
            return activeCustomers.Count;
        }

        /// <summary>
        /// 处理顾客入座通知。
        /// </summary>
        /// <param name="customer">顾客对象。</param>
        public void NotifyCustomerSeated(TavernCustomerRuntimeController customer)
        {
            if (customer == null || !AllTables.TryGetValue(customer.TableId, out var table))
            {
                return;
            }

            if (TryGetTableCustomerGroup(customer.TableId, out var customers))
            {
                for (var index = 0; index < customers.Count; index++)
                {
                    if (customers[index] == null || !customers[index].IsSeated)
                    {
                        return;
                    }
                }
            }

            DataManager.Instance.SetTableRuntimeState(customer.TableId, TavernTableRuntimeState.WaitingOrder);
            table.RefreshRuntimeState(TavernTableRuntimeState.WaitingOrder);
            table.linkedUI?.StopStateCountdown();
            StartTableOrderWait(customer.TableId);
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
        }

        /// <summary>
        /// 处理顾客等待结账通知。
        /// </summary>
        /// <param name="customer">顾客对象。</param>
        public void NotifyCustomerReadyCheckout(TavernCustomerRuntimeController customer)
        {
            if (customer == null || !AllTables.TryGetValue(customer.TableId, out var table))
            {
                return;
            }

            if (TryGetTableCustomerGroup(customer.TableId, out var customers))
            {
                for (var index = 0; index < customers.Count; index++)
                {
                    if (customers[index] == null || !customers[index].IsReadyCheckout)
                    {
                        return;
                    }
                }
            }

            DataManager.Instance.SetTableRuntimeState(customer.TableId, TavernTableRuntimeState.Checkout);
            table.RefreshRuntimeState(TavernTableRuntimeState.Checkout);
            table.ShowEmptyPlateVisual();
            table.linkedUI?.StopStateCountdown();
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
        }

        /// <summary>
        /// 处理顾客离店通知。
        /// </summary>
        /// <param name="customer">顾客对象。</param>
        public void NotifyCustomerExited(TavernCustomerRuntimeController customer)
        {
            if (customer != null && customer.TableId > 0)
            {
                if (TryGetTableCustomerGroup(customer.TableId, out var customers))
                {
                    customers.Remove(customer);
                    customers.RemoveAll(item => item == null);
                    if (customers.Count == 0)
                    {
                        tableCustomerGroups.Remove(customer.TableId);
                    }
                    else
                    {
                        tableCustomers[customer.TableId] = customers[0];
                    }
                }

                if (tableCustomers.TryGetValue(customer.TableId, out var currentCustomer) && currentCustomer == customer)
                {
                    tableCustomers.Remove(customer.TableId);
                }
            }

            activeCustomers.Remove(customer);
            queuedCustomers.Remove(customer);
            Destroy(customer.gameObject);
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
        }

        /// <summary>
        /// 获取指定桌位上的全部顾客。
        /// </summary>
        /// <param name="tableId">桌位编号。</param>
        /// <param name="customers">输出的顾客列表。</param>
        /// <returns>找到有效顾客组时返回 true，否则返回 false。</returns>
        private bool TryGetTableCustomerGroup(int tableId, out List<TavernCustomerRuntimeController> customers)
        {
            if (tableCustomerGroups.TryGetValue(tableId, out customers) && customers != null)
            {
                customers.RemoveAll(item => item == null);
                return true;
            }

            customers = null;
            return false;
        }

        /// <summary>
        /// 初始化桌位和界面绑定。
        /// </summary>
        private void InitTablesAndUIs()
        {
            var tablesInScene = FindObjectsByType<TableArea>(FindObjectsSortMode.None);
            foreach (var table in tablesInScene)
            {
                var id = table.GetTableIdFromInternal();
                AllTables[id] = table;

                if (tableUIPrefab == null || canvasParent == null)
                {
                    continue;
                }

                var uiObj = Instantiate(tableUIPrefab, canvasParent);
                var uiScript = uiObj != null ? uiObj.GetComponent<TableAreaUI>() : null;
                if (uiScript == null)
                {
                    continue;
                }

                table.linkedUI = uiScript;
                uiScript.InitBinding(table.transform);
                AllTableUIs[id] = uiScript;

                uiFollowList.Add(new UIFollowData
                {
                    uiTransform = uiObj.transform,
                    targetTile = table.transform,
                    tableUI = uiScript
                });
            }
        }

        /// <summary>
        /// 配置场景界面使用的画布。
        /// </summary>
        private void ConfigureSceneUiCanvas()
        {
            sceneCanvas = canvasParent != null ? canvasParent.GetComponentInParent<Canvas>() : null;
            if (sceneCanvas == null)
            {
                return;
            }

            sceneCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            sceneCanvas.worldCamera = null;

            var billboard = sceneCanvas.GetComponent<Billboard>();
            if (billboard != null)
            {
                billboard.enabled = false;
            }
        }

        /// <summary>
        /// 启动营业循环。
        /// </summary>
        private void StartBusinessLoop()
        {
            if (!customerSpawnLoopActive)
            {
                customerSpawnLoopActive = true;
                nextCustomerSpawnRemaining = customerSpawnInterval;
            }

            if (cookRoutine == null)
            {
                cookRoutine = StartCoroutine(CookDishLoop());
            }

            if (waiterServiceRoutine == null)
            {
                waiterServiceRoutine = StartCoroutine(WaiterServiceLoop());
            }

            SpawnInitialCustomers();
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
        }

        /// <summary>
        /// 停止营业循环。
        /// </summary>
        private void StopBusinessLoop()
        {
            customerSpawnLoopActive = false;
            nextCustomerSpawnRemaining = -1f;

            if (cookRoutine != null)
            {
                StopCoroutine(cookRoutine);
                cookRoutine = null;
            }

            if (waiterServiceRoutine != null)
            {
                StopCoroutine(waiterServiceRoutine);
                waiterServiceRoutine = null;
            }

            if (waiterTaskRoutine != null)
            {
                StopCoroutine(waiterTaskRoutine);
                waiterTaskRoutine = null;
            }

            // 清理小二任务派发缓存，下次开张能从干净状态重新开始
            ResetWaiterTaskState();
            pendingUpgradeTableIds.Clear();
            staffVisualsBeingAnimated.Clear();

            foreach (var routine in autoCleanRoutines.Values)
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }
            }

            autoCleanRoutines.Clear();
            StopAllTableOrderWaits();
            ClearPreparedDishQueue();
        }

        /// <summary>
        /// 随机获取顾客点单文案。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        private List<string> GetRandomOrderNames()
        {
            var result = new List<string>();
            if (Random.value > 0.45f)
            {
                return result;
            }

            var products = SO_Product.GetAll();
            if (products == null || products.Count == 0)
            {
                result.Add("包子");
                return result;
            }

            var randomIndex = Random.Range(0, products.Count);
            var product = products[randomIndex];
            if (product != null && !string.IsNullOrWhiteSpace(product.displayName))
            {
                result.Add(product.displayName);
            }

            if (result.Count == 0)
            {
                result.Add("包子");
            }

            return result;
        }
    }
}
