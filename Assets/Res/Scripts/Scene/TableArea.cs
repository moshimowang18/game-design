using System.Collections;
using System.Collections.Generic;
using JN.Client.Manager;
using JN.Client.Model;
using JN.Client.UI;
using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JN.Client.Scene
{
    [RequireComponent(typeof(Collider))]
    /// <summary>
    /// 负责桌位区域相关的运行时逻辑。
    /// </summary>
    public class TableArea : MonoBehaviour
    {
        private const int UnlockCost = 500;
        private const int MaxTableLevel = 3;
        private const int TableUpgradeBaseCost = 800;
        private const float UpgradeWaitPollInterval = 0.25f;
        private const float DishOnPlateYOffset = 0.025f;
        private static readonly Vector3 TableLv2ExactScale = new(38.6f, 38.6f, 38.6f);
        private const float TableLv2LocalYOffset = 0.0623f;
        private const float TableLv2SeatedCustomerY = 0.14f;
        // 升级时按 SO_Equipment.equipmentId == 2 这条配置取等级对应的桌子 预制体。
        private const int TableEquipmentLookupId = 2;
        private const string DefaultSeatDecorationPath = "Assets/Res/Resources/Equipment/P_Equipment_StoolLv1.prefab";
        // 按等级查表加载凳子 预制体。索引 0 对应 Lv1，依此类推。
        private static readonly string[] StoolPrefabPathsByLevel = new[]
        {
            "Assets/Res/Resources/Equipment/P_Equipment_StoolLv1.prefab",
            "Assets/Res/Resources/Equipment/P_Equipment_StoolLv2.prefab",
            "Assets/Res/Resources/Equipment/P_Equipment_StoolLv3.prefab",
        };

        /// <summary>
        /// 处理桌位编号相关逻辑。
        /// </summary>
        public int tableId;

        /// <summary>
        /// 处理绑定的界面相关逻辑。
        /// </summary>
        public TableAreaUI linkedUI;

        [SerializeField] public GameObject tableObj;
        [SerializeField] private GameObject canBuildObj;
        [SerializeField] public float pressedScale = 0.9f;
        [SerializeField] private GameObject seatDecorationPrefab;

        private readonly List<Transform> seatSlots = new();
        private readonly List<GameObject> spawnedSeatDecorations = new();

        private Vector3 originalScale;
        private Vector3 defaultTableLocalScale = Vector3.one;
        private Transform productPlacement;
        private GameObject currentDishVisual;
        private Coroutine pendingUpgradeRoutine;
        // 当前桌面 3D 模型对应的等级，用于升级时判断是否需要替换 预制体。
        private int currentTableModelLevel;
        private Collider m_TableCollider;

        /// <summary>
        /// 初始化组件引用和运行时状态。
        /// </summary>
        private void Awake()
        {
            GetTableIdFromInternal();
            m_TableCollider = GetComponent<Collider>();
            originalScale = transform.localScale;
            defaultTableLocalScale = tableObj != null ? tableObj.transform.localScale : Vector3.one;
            CacheRuntimePoints();
            HideLegacyBuildSceneUi();
            EnsureBuildIndicatorCollider();
            EnsureSeatDecorations();
            ApplySaveState(DataManager.Instance.GetTableData(tableId));
        }

        /// <summary>
        /// 处理场景点击射线命中的桌位区域。
        /// </summary>
        /// <param name="pointerPosition">屏幕坐标。</param>
        /// <returns>命中并消费点击时返回 true。</returns>
        public static bool TryHandlePointerClick(Vector2 pointerPosition)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return false;
            }

            var ray = mainCamera.ScreenPointToRay(pointerPosition);
            var hits = Physics.RaycastAll(ray, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (var index = 0; index < hits.Length; index++)
            {
                var hitCollider = hits[index].collider;
                if (hitCollider == null)
                {
                    continue;
                }

                var tableArea = hitCollider.GetComponentInParent<TableArea>();
                if (tableArea == null)
                {
                    continue;
                }

                if (tableArea.m_TableCollider != null && hitCollider != tableArea.m_TableCollider && hitCollider.GetComponentInParent<TableArea>() != tableArea)
                {
                    continue;
                }

                tableArea.HandlePrimaryAction();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 响应桌位点击。
        /// </summary>
        private void HandlePrimaryAction()
        {
            transform.localScale = originalScale;

            var tableData = DataManager.Instance.GetTableData(tableId);
            if (tableData == null)
            {
                return;
            }

            if (!tableData.isUnlocked)
            {
                TryUnlockTable();
                return;
            }

            // 升级桌位只能在准备阶段做（覆盖原 !isOpen 判断）
            if (TavernDayManager.Instance != null && !TavernDayManager.Instance.CanSpendMoney())
            {
                Debug.Log("[TableArea] 营业中不能升级桌位，请等准备阶段");
                return;
            }

            // 已经在等待升级动画的桌位禁止再次弹出升级面板，避免重复扣费
            if (TavernSceneManager.Instance != null && TavernSceneManager.Instance.IsTableUpgrading(tableId))
            {
                return;
            }

            if (DataManager.Instance != null && !DataManager.Instance.IsTableLv2UpgradeUnlocked())
            {
                return;
            }

            TavernRuntimeModalUI.ShowTableUpgradePanel(this, BeginUpgradeTableWithDelivery);
        }

        /// <summary>
        /// 应用存档状态。
        /// </summary>
        /// <param name="tableData">数据。</param>
        public void ApplySaveState(TavernTableSaveData tableData)
        {
            linkedUI?.BindTable(this);
            var isUnlocked = tableData != null && tableData.isUnlocked;
            var canPurchaseTable = DataManager.Instance.CanPurchaseGuideTables();
            if (canBuildObj != null)
            {
                canBuildObj.SetActive(!isUnlocked && canPurchaseTable);
            }

            HideLegacyBuildSceneUi();

            // 已解锁的桌位先按存档的等级把场景中的 3D 模型刷新到位，再决定是否显示。
            if (isUnlocked && tableData != null)
            {
                EnsureTableModelForLevel(Mathf.Max(1, tableData.level));
            }

            if (tableObj != null)
            {
                tableObj.SetActive(isUnlocked);
            }

            if (linkedUI != null)
            {
                linkedUI.SetUnlockPrompt(!isUnlocked && canPurchaseTable, UnlockCost);
                if (isUnlocked && tableData != null)
                {
                    linkedUI.RefreshState((TavernTableRuntimeState)tableData.runtimeState);
                }
                else
                {
                    linkedUI.HideStatus();
                }
            }

            if (!isUnlocked)
            {
                ClearDishVisual();
            }
        }

        /// <summary>
        /// 隐藏旧版建造场景界面。
        /// </summary>
        private void HideLegacyBuildSceneUi()
        {
            if (canBuildObj == null)
            {
                return;
            }

            foreach (var child in canBuildObj.GetComponentsInChildren<Transform>(true))
            {
                if (child == canBuildObj.transform)
                {
                    continue;
                }

                if (child.name.StartsWith("btn_"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 刷新运行时状态。
        /// </summary>
        /// <param name="state">参数值。</param>
        /// <param name="customText">参数值。</param>
        public void RefreshRuntimeState(TavernTableRuntimeState state, string customText = null)
        {
            if (linkedUI == null)
            {
                return;
            }

            linkedUI.BindTable(this);
            linkedUI.SetUnlockPrompt(false, UnlockCost);
            linkedUI.RefreshState(state, customText);

            if (state == TavernTableRuntimeState.Idle || state == TavernTableRuntimeState.Checkout)
            {
                if (state != TavernTableRuntimeState.Checkout)
                {
                    ClearDishVisual();
                }
            }
        }

        /// <summary>
        /// 标记当前桌位已解锁，会一并清理可能存在的待升级标记，
        /// 让搬运动画结束后桌子能立即接客。
        /// </summary>
        public void MarkUnlocked()
        {
            HideBuildIndicator();

            // 搬运动画到达后按当前存档等级切换桌子模型，保证 Lv2/Lv3 视觉同步刷新。
            var tableData = DataManager.Instance.GetTableData(tableId);
            var targetLevel = tableData != null ? Mathf.Max(1, tableData.level) : 1;
            EnsureTableModelForLevel(targetLevel);

            if (tableObj != null)
            {
                tableObj.SetActive(true);
            }

            linkedUI?.BindTable(this);
            linkedUI?.SetUnlockPrompt(false, UnlockCost);
            linkedUI?.RefreshState(TavernTableRuntimeState.Idle);

            if (TavernSceneManager.Instance != null)
            {
                TavernSceneManager.Instance.MarkTableUpgrading(tableId, false);
            }
        }

        /// <summary>
        /// 获取顾客目标位置。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        public Vector3 GetCustomerTargetPosition()
        {
            var capacity = GetSeatCapacity();
            if (capacity > 0 && seatSlots.Count > 0 && seatSlots[0] != null)
            {
                return seatSlots[0].position;
            }

            // 如果场景里没有显式座位点，则退回到桌子右侧的经验位置。
            var baseTransform = tableObj != null && tableObj.activeSelf ? tableObj.transform : transform;
            return baseTransform.position + transform.right * 0.45f;
        }

        /// <summary>
        /// 尝试处理获取主座位姿态。
        /// </summary>
        /// <param name="seatPosition">坐标。</param>
        /// <param name="lookAtPosition">坐标。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool TryGetPrimarySeatPose(out Vector3 seatPosition, out Vector3 lookAtPosition)
        {
            return TryGetSeatPoseByIndex(0, out seatPosition, out lookAtPosition);
        }

        /// <summary>
        /// 按座位索引获取指定的入座点与朝向目标。
        /// </summary>
        /// <param name="seatIndex">座位索引，从 0 开始。</param>
        /// <param name="seatPosition">输出的座位坐标。</param>
        /// <param name="lookAtPosition">输出的朝向目标坐标。</param>
        /// <returns>找到可用座位时返回 true，否则返回 false。</returns>
        public bool TryGetSeatPoseByIndex(int seatIndex, out Vector3 seatPosition, out Vector3 lookAtPosition)
        {
            var capacity = GetSeatCapacity();
            if (capacity > 0
                && seatSlots.Count > 0
                && seatIndex >= 0
                && seatIndex < Mathf.Min(capacity, seatSlots.Count)
                && seatSlots[seatIndex] != null)
            {
                seatPosition = seatSlots[seatIndex].position;
                lookAtPosition = (tableObj != null ? tableObj.transform.position : transform.position);
                return true;
            }

            seatPosition = transform.position;
            lookAtPosition = transform.position + transform.forward;
            return false;
        }

        /// <summary>
        /// 尝试处理获取最近座位姿态。
        /// </summary>
        /// <param name="referencePosition">坐标。</param>
        /// <param name="seatPosition">坐标。</param>
        /// <param name="lookAtPosition">坐标。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool TryGetNearestSeatPose(Vector3 referencePosition, out Vector3 seatPosition, out Vector3 lookAtPosition)
        {
            var capacity = Mathf.Min(GetSeatCapacity(), seatSlots.Count);
            Transform nearestSeat = null;
            var nearestSqrDistance = float.MaxValue;
            for (var index = 0; index < capacity; index++)
            {
                var seatSlot = seatSlots[index];
                if (seatSlot == null)
                {
                    continue;
                }

                var sqrDistance = (seatSlot.position - referencePosition).sqrMagnitude;
                if (sqrDistance >= nearestSqrDistance)
                {
                    continue;
                }

                nearestSqrDistance = sqrDistance;
                nearestSeat = seatSlot;
            }

            if (nearestSeat != null)
            {
                seatPosition = nearestSeat.position;
                lookAtPosition = (tableObj != null ? tableObj.transform.position : transform.position);
                return true;
            }

            return TryGetPrimarySeatPose(out seatPosition, out lookAtPosition);
        }

        /// <summary>
        /// 获取当前桌位可容纳的顾客数量。
        /// </summary>
        /// <returns>当前桌位的座位数量。</returns>
        public int GetSeatCapacity()
        {
            return GetSeatDecorationTargetCount();
        }

        /// <summary>
        /// 获取顾客入座后的目标 Y 坐标。
        /// </summary>
        /// <returns>当前桌位对应的入座 Y 坐标。</returns>
        public float GetSeatedCustomerY()
        {
            return currentTableModelLevel == 2 ? TableLv2SeatedCustomerY : transform.position.y;
        }

        /// <summary>
        /// 获取顾客入座时的额外平面偏移，用于微调四人桌不同座位的 x/z 位置。
        /// </summary>
        /// <param name="seatIndex">座位索引。</param>
        /// <param name="seatPosition">座位世界坐标。</param>
        /// <returns>额外的世界坐标偏移。</returns>
        public Vector3 GetSeatSnapPlanarOffset(int seatIndex, Vector3 seatPosition)
        {
            if (currentTableModelLevel != 2)
            {
                return Vector3.zero;
            }

            var center = tableObj != null ? tableObj.transform.position : transform.position;
            var toSeat = seatPosition - center;
            toSeat.y = 0f;
            if (toSeat.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            var tableRight = tableObj != null ? tableObj.transform.right : transform.right;
            var tableForward = tableObj != null ? tableObj.transform.forward : transform.forward;
            tableRight.y = 0f;
            tableForward.y = 0f;
            if (tableRight.sqrMagnitude <= 0.0001f || tableForward.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            tableRight.Normalize();
            tableForward.Normalize();

            var xSign = Mathf.Sign(Vector3.Dot(toSeat, tableRight));
            var zSign = Mathf.Sign(Vector3.Dot(toSeat, tableForward));
            if (Mathf.Approximately(xSign, 0f))
            {
                xSign = (seatIndex % 2 == 0) ? -1f : 1f;
            }

            if (Mathf.Approximately(zSign, 0f))
            {
                zSign = seatIndex < 2 ? 1f : -1f;
            }

            return tableRight * (0.035f * xSign) + tableForward * (0.025f * zSign);
        }

        /// <summary>
        /// 显示桌面菜品表现。
        /// </summary>
        /// <param name="dishPrefab">参数值。</param>
        public void ShowDishVisual(GameObject dishPrefab)
        {
            ClearDishVisual();
            if (productPlacement == null)
            {
                return;
            }

            currentDishVisual = CreatePlateVisual(dishPrefab);
        }

        /// <summary>
        /// 显示顾客吃完后的空盘表现，供结账与清扫阶段使用。
        /// </summary>
        public void ShowEmptyPlateVisual()
        {
            ClearDishVisual();
            if (productPlacement == null)
            {
                return;
            }

            currentDishVisual = CreatePlateVisual(null);
        }

        /// <summary>
        /// 获取桌面特效播放位置，优先使用菜品摆放点，缺失时回退到桌子中心上方。
        /// </summary>
        /// <returns>桌面特效世界坐标。</returns>
        public Vector3 GetTableEffectPosition()
        {
            if (productPlacement != null)
            {
                return productPlacement.position;
            }

            var baseTransform = tableObj != null ? tableObj.transform : transform;
            return baseTransform.position + Vector3.up * 0.65f;
        }

        /// <summary>
        /// 清理桌面菜品表现。
        /// </summary>
        public void ClearDishVisual()
        {
            if (currentDishVisual == null)
            {
                return;
            }

            Destroy(currentDishVisual);
            currentDishVisual = null;
        }

        /// <summary>
        /// 创建桌面餐盘表现，可选附带一道菜。
        /// </summary>
        /// <param name="dishPrefab">菜品预制体；为空时仅显示空盘。</param>
        /// <returns>生成出的桌面表现根节点。</returns>
        private GameObject CreatePlateVisual(GameObject dishPrefab)
        {
            var plate = TavernSceneManager.Instance != null ? TavernSceneManager.Instance.GetPlatePrefab() : null;
            if (plate == null)
            {
                if (dishPrefab == null)
                {
                    return null;
                }

                var dishOnly = Instantiate(dishPrefab, productPlacement, false);
                dishOnly.name = dishPrefab.name;
                dishOnly.transform.localPosition = Vector3.zero;
                dishOnly.transform.localRotation = Quaternion.identity;
                dishOnly.transform.localScale = Vector3.one;
                return dishOnly;
            }

            var plateInstance = Instantiate(plate, productPlacement, false);
            plateInstance.name = dishPrefab == null ? "EmptyPlate_Runtime" : $"DiningPlate_{dishPrefab.name}";
            plateInstance.transform.localPosition = Vector3.zero;
            plateInstance.transform.localRotation = Quaternion.identity;
            plateInstance.transform.localScale = Vector3.one;

            if (dishPrefab == null)
            {
                return plateInstance;
            }

            var dishInstance = Instantiate(dishPrefab, plateInstance.transform, false);
            dishInstance.name = dishPrefab.name;
            dishInstance.transform.localPosition = Vector3.up * DishOnPlateYOffset;
            dishInstance.transform.localRotation = Quaternion.identity;
            dishInstance.transform.localScale = Vector3.one;
            return plateInstance;
        }

        /// <summary>
        /// 获取桌位内部编号。
        /// </summary>
        /// <returns>返回计算后的数值。</returns>
        public int GetTableIdFromInternal()
        {
            if (tableId <= 0)
            {
                var idx = gameObject.name.LastIndexOf('_');
                if (idx >= 0 && int.TryParse(gameObject.name[(idx + 1)..], out var id))
                {
                    tableId = id;
                }
            }

            return tableId;
        }

        /// <summary>
        /// 处理是否可以服务相关逻辑。
        /// </summary>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool CanServeNow()
        {
            var tableData = DataManager.Instance.GetTableData(tableId);
            if (tableData == null)
            {
                return false;
            }

            var state = (TavernTableRuntimeState)tableData.runtimeState;
            return state == TavernTableRuntimeState.WaitingOrder || state == TavernTableRuntimeState.Checkout;
        }

        /// <summary>
        /// 处理操作按钮点击。
        /// </summary>
        public void HandleActionButtonClick()
        {
            TavernSceneManager.Instance.HandleTableInteraction(tableId);
        }

        /// <summary>
        /// 缓存运行时点位。
        /// </summary>
        private void CacheRuntimePoints()
        {
            seatSlots.Clear();

            var searchRoot = tableObj != null ? tableObj.transform : transform;
            foreach (Transform child in searchRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.Contains("SeatSlot"))
                {
                    seatSlots.Add(child);
                    continue;
                }

                if (child.name == "ProductPlacement")
                {
                    productPlacement = child;
                }
            }
        }

        /// <summary>
        /// 确保座位装饰。会根据当前桌子等级 (currentTableModelLevel) 选用对应等级的凳子 预制体，
        /// 旧的、不匹配等级的凳子会被销毁并重新生成。
        /// </summary>
        private void EnsureSeatDecorations()
        {
            if (seatSlots.Count == 0)
            {
                return;
            }

            var targetSeatCount = Mathf.Min(GetSeatDecorationTargetCount(), seatSlots.Count);

            // 优先使用当前等级的凳子 预制体，找不到时回落到 Inspector 配置或默认 Lv1。
            var levelStool = LoadStoolPrefabForLevel(Mathf.Max(1, currentTableModelLevel));
            var prefab = levelStool != null ? levelStool : seatDecorationPrefab;
            if (prefab == null)
            {
                prefab = LoadDefaultSeatDecoration();
            }

            if (prefab == null)
            {
                return;
            }

            spawnedSeatDecorations.RemoveAll(item => item == null);

            // 先把和当前等级 prefab 不一致的旧凳子清掉，避免升级后还残留旧造型。
            for (var index = spawnedSeatDecorations.Count - 1; index >= 0; index--)
            {
                var stool = spawnedSeatDecorations[index];
                if (stool == null)
                {
                    spawnedSeatDecorations.RemoveAt(index);
                    continue;
                }

                if (stool.name != prefab.name)
                {
                    Destroy(stool);
                    spawnedSeatDecorations.RemoveAt(index);
                }
            }

            for (var seatIndex = 0; seatIndex < seatSlots.Count; seatIndex++)
            {
                var seatSlot = seatSlots[seatIndex];
                if (seatSlot == null)
                {
                    continue;
                }

                var shouldShowSeatDecoration = seatIndex < targetSeatCount;

                // 同步座位槽下挂的旧凳子，确保只剩当前等级的造型。
                for (var childIndex = seatSlot.childCount - 1; childIndex >= 0; childIndex--)
                {
                    var child = seatSlot.GetChild(childIndex);
                    if (child == null)
                    {
                        continue;
                    }

                    if (!shouldShowSeatDecoration || child.name != prefab.name)
                    {
                        Destroy(child.gameObject);
                    }
                }

                if (!shouldShowSeatDecoration)
                {
                    continue;
                }

                if (seatSlot.childCount > 0)
                {
                    continue;
                }

                var stool = Instantiate(prefab, seatSlot);
                stool.name = prefab.name;
                stool.transform.localPosition = Vector3.zero;
                stool.transform.localRotation = Quaternion.identity;
                spawnedSeatDecorations.Add(stool);
            }
        }

        /// <summary>
        /// 按当前桌子等级换算应该显示几个凳子，同时也作为顾客容量。
        /// </summary>
        /// <returns>目标凳子数量。</returns>
        private int GetSeatDecorationTargetCount()
        {
            var level = Mathf.Max(1, currentTableModelLevel);
            return level switch
            {
                1 => 2,
                2 => 4,
                _ => 4
            };
        }

        /// <summary>
        /// 加载默认座位装饰。
        /// </summary>
        /// <returns>返回匹配到的对象引用。</returns>
        private static GameObject LoadDefaultSeatDecoration()
        {
            return GameplayResourceStore.LoadAsset<GameObject>(DefaultSeatDecorationPath);
        }

        /// <summary>
        /// 按桌子等级加载对应的凳子 预制体。Lv1~Lv3 一一对应到固定路径，超出范围时回落到最高一级。
        /// </summary>
        /// <param name="level">桌子等级。</param>
        /// <returns>对应等级的凳子 预制体；运行时无 AssetDatabase 时返回 null。</returns>
        private static GameObject LoadStoolPrefabForLevel(int level)
        {
            if (StoolPrefabPathsByLevel == null || StoolPrefabPathsByLevel.Length == 0)
            {
                return null;
            }

            var clamped = Mathf.Clamp(level, 1, StoolPrefabPathsByLevel.Length);
            var path = StoolPrefabPathsByLevel[clamped - 1];
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            return GameplayResourceStore.LoadAsset<GameObject>(path);
        }

        /// <summary>
        /// 按指定等级切换桌面 3D 模型。等级未变化时不做处理，
        /// 等级变更时销毁当前 tableObj，并用 SO_Equipment 配置中的对应 预制体重建。
        /// </summary>
        /// <param name="level">目标等级 (1~MaxTableLevel)。</param>
        private void EnsureTableModelForLevel(int level)
        {
            level = Mathf.Max(1, level);
            if (currentTableModelLevel == 0 && tableObj != null)
            {
                // 场景里手摆的初始桌子按惯例都是 Lv1 模型，先记下来避免无谓重建。
                currentTableModelLevel = 1;
            }

            if (level == currentTableModelLevel)
            {
                ApplyTableVisualAdjustments(level);
                return;
            }

            var levelPrefab = LoadTableLevelPrefab(level);
            if (levelPrefab == null)
            {
                return;
            }

            // 复用旧模型的父级、位置、缩放和旋转，确保升级后仍严格贴合场景里
            // 原先的桌位锚点，再通过底部包围盒对齐消除不同等级 prefab 的 pivot 差异。
            Transform anchor;
            Vector3 localPosition;
            Quaternion localRotation;
            Vector3 localScale;
            var hasOldBounds = TryGetRendererBounds(tableObj, out var oldBounds);
            if (tableObj != null)
            {
                anchor = tableObj.transform.parent != null ? tableObj.transform.parent : transform;
                localPosition = tableObj.transform.localPosition;
                localRotation = tableObj.transform.localRotation;
                localScale = defaultTableLocalScale;
                Destroy(tableObj);
            }
            else
            {
                anchor = transform;
                localPosition = Vector3.zero;
                localRotation = Quaternion.identity;
                localScale = Vector3.one;
            }

            var instance = Instantiate(levelPrefab, anchor, false);
            instance.name = levelPrefab.name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = ApplyLevelScale(localScale, level);

            if (hasOldBounds && TryGetRendererBounds(instance, out var newBounds))
            {
                var bottomDelta = oldBounds.min.y - newBounds.min.y;
                if (Mathf.Abs(bottomDelta) > 0.0001f)
                {
                    instance.transform.position += Vector3.up * bottomDelta;
                }
            }

            tableObj = instance;
            defaultTableLocalScale = localScale;
            currentTableModelLevel = level;
            ApplyTableVisualAdjustments(level);

            // 新模型有自己的 SeatSlot 与 ProductPlacement，重新缓存并按当前等级补一遍凳子。
            CacheRuntimePoints();
            EnsureSeatDecorations();
        }

        /// <summary>
        /// 根据等级应用桌子模型的场景缩放修正。
        /// </summary>
        /// <param name="baseScale">原始缩放。</param>
        /// <param name="level">桌子等级。</param>
        /// <returns>修正后的缩放。</returns>
        private static Vector3 ApplyLevelScale(Vector3 baseScale, int level)
        {
            return level == 2 ? TableLv2ExactScale : baseScale;
        }

        /// <summary>
        /// 对当前桌子模型执行等级相关的视觉修正。
        /// </summary>
        /// <param name="level">桌子等级。</param>
        private void ApplyTableVisualAdjustments(int level)
        {
            if (tableObj == null)
            {
                return;
            }

            tableObj.transform.localScale = ApplyLevelScale(defaultTableLocalScale, level);
            var localPosition = tableObj.transform.localPosition;
            localPosition.y = level == 2 ? TableLv2LocalYOffset : 0f;
            tableObj.transform.localPosition = localPosition;
        }

        /// <summary>
        /// 读取对象及其子节点渲染器包围盒。
        /// </summary>
        /// <param name="target">目标对象。</param>
        /// <param name="bounds">输出包围盒。</param>
        /// <returns>读取成功时返回 true，否则返回 false。</returns>
        private static bool TryGetRendererBounds(GameObject target, out Bounds bounds)
        {
            bounds = default;
            if (target == null)
            {
                return false;
            }

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds;
        }

        /// <summary>
        /// 通过 SO_Equipment 配置取出指定等级的桌子 预制体，
        /// 优先匹配带完整等级配置的 Equipment 数据。
        /// </summary>
        /// <param name="level">目标等级。</param>
        /// <returns>对应等级的桌子 预制体；找不到时返回 null。</returns>
        private static GameObject LoadTableLevelPrefab(int level)
        {
            var equipment = SO_Equipment.GetById(TableEquipmentLookupId);
            if (equipment == null)
            {
                return null;
            }

            var levelConfig = equipment.GetLevelConfig(level);
            return levelConfig != null ? levelConfig.scenePrefab : null;
        }

        /// <summary>
        /// 尝试处理解锁桌位。
        /// </summary>
        private void TryUnlockTable()
        {
            if (TavernDayManager.Instance != null && !TavernDayManager.Instance.CanSpendMoney())
            {
                Debug.Log("[TableArea] 营业中不能解锁桌位，请等准备阶段");
                return;
            }

            if (!DataManager.Instance.CanPurchaseGuideTables())
            {
                return;
            }

            if (DataManager.Instance.PlayerData.coinNum < UnlockCost)
            {
                return;
            }

            // 解锁时播放金币飞行动效，再扣钱并触发搬桌流程。
            var start = GOReferenceManager.Instance.GetCoinTransform();
            if (start != null && linkedUI != null)
            {
                GameUIEffects.PlayCoinsFly(start, linkedUI.transform);
            }

            DataManager.Instance.ChangeCoinNum(-UnlockCost);
            DataManager.Instance.UnlockTable(tableId);
            GameAudioManager.PlayConstruction();
            HideBuildIndicator();
            linkedUI?.SetUnlockPrompt(false, UnlockCost);
            // 如果搬运 预制体 缺失或未配置，直接兜底落桌，避免“扣钱后看不到桌子”。
            if (TavernSceneManager.Instance == null || !TavernSceneManager.Instance.StartMoveTable(tableId))
            {
                MarkUnlocked();
            }
        }

        /// <summary>
        /// 隐藏桌位地面购买提示，避免购买后 SpriteRenderer 继续留在场景里。
        /// </summary>
        private void HideBuildIndicator()
        {
            if (canBuildObj != null)
            {
                canBuildObj.SetActive(false);
            }
        }

        /// <summary>
        /// 为桌位购买提示底板补齐碰撞体，支持直接点击场景提示购买。
        /// </summary>
        private void EnsureBuildIndicatorCollider()
        {
            if (canBuildObj == null)
            {
                return;
            }

            var renderers = canBuildObj.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            Bounds? combinedBounds = null;
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                combinedBounds = combinedBounds.HasValue
                    ? Encapsulate(combinedBounds.Value, renderer.bounds)
                    : renderer.bounds;
            }

            if (!combinedBounds.HasValue)
            {
                return;
            }

            var boxCollider = canBuildObj.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = canBuildObj.AddComponent<BoxCollider>();
            }

            boxCollider.isTrigger = true;
            boxCollider.center = canBuildObj.transform.InverseTransformPoint(combinedBounds.Value.center);
            boxCollider.size = new Vector3(
                Mathf.Max(0.2f, combinedBounds.Value.size.x),
                Mathf.Max(0.2f, combinedBounds.Value.size.y),
                Mathf.Max(0.2f, combinedBounds.Value.size.z));
        }

        /// <summary>
        /// 合并两个包围盒。
        /// </summary>
        private static Bounds Encapsulate(Bounds a, Bounds b)
        {
            a.Encapsulate(b.min);
            a.Encapsulate(b.max);
            return a;
        }

        /// <summary>
        /// 升级面板确认后入口：把桌位标记为待升级，并启动等待协程，
        /// 等到当前顾客离开、桌位真正空闲后再触发搬运动画。
        /// </summary>
        private void BeginUpgradeTableWithDelivery()
        {
            var upgradeCost = GetPendingUpgradeCost();
            if (upgradeCost <= 0)
            {
                return;
            }

            if (DataManager.Instance.PlayerData.coinNum < upgradeCost)
            {
                TavernRuntimeModalUI.ShowFloatingWarning("金币不足，无法升级桌子");
                return;
            }

            if (TavernSceneManager.Instance != null)
            {
                TavernSceneManager.Instance.MarkTableUpgrading(tableId, true);
                TavernSceneManager.Instance.PreparePendingTableUpgrade(tableId);
            }

            // 同一桌只允许一条升级协程，避免重复点击导致并发触发
            if (pendingUpgradeRoutine != null)
            {
                StopCoroutine(pendingUpgradeRoutine);
            }

            pendingUpgradeRoutine = StartCoroutine(UpgradeTableWithDeliveryRoutine());
        }

        /// <summary>
        /// 等待桌位空闲后执行升级与搬运动画的协程。
        /// 顺序：等待顾客 → 数据升级 → 隐藏旧桌 → 启动搬运。
        /// </summary>
        /// <returns>协程迭代器。</returns>
        private IEnumerator UpgradeTableWithDeliveryRoutine()
        {
            var wait = new WaitForSeconds(UpgradeWaitPollInterval);
            while (true)
            {
                var data = DataManager.Instance.GetTableData(tableId);
                if (data == null || !data.isUnlocked)
                {
                    pendingUpgradeRoutine = null;
                    if (TavernSceneManager.Instance != null)
                    {
                        TavernSceneManager.Instance.MarkTableUpgrading(tableId, false);
                    }
                    yield break;
                }

                var tavern = TavernSceneManager.Instance;
                var state = (TavernTableRuntimeState)data.runtimeState;
                var blockedByCustomer = state != TavernTableRuntimeState.Idle
                                        && state != TavernTableRuntimeState.Cleaning
                                        && tavern != null
                                        && tavern.HasUpgradeBlockingOccupancy(tableId);
                if (CanStartUpgradeAfterCurrentCustomerLeaves(state, blockedByCustomer))
                {
                    break;
                }

                yield return wait;
            }

            var upgradeCost = GetPendingUpgradeCost();
            if (upgradeCost <= 0 || DataManager.Instance.PlayerData.coinNum < upgradeCost)
            {
                pendingUpgradeRoutine = null;
                if (TavernSceneManager.Instance != null)
                {
                    TavernSceneManager.Instance.MarkTableUpgrading(tableId, false);
                }

                TavernRuntimeModalUI.ShowFloatingWarning("金币不足，无法升级桌子");
                yield break;
            }

            if (TavernDayManager.Instance != null && !TavernDayManager.Instance.CanSpendMoney())
            {
                Debug.Log("[TableArea] 营业中不能升级桌位");
                pendingUpgradeRoutine = null;
                if (TavernSceneManager.Instance != null)
                {
                    TavernSceneManager.Instance.MarkTableUpgrading(tableId, false);
                }

                yield break;
            }

            // 数据先升级，确保后续动画/状态刷新读取到的是新等级
            var upgraded = DataManager.Instance.UpgradeTable(tableId);
            if (upgraded)
            {
                DataManager.Instance.ChangeCoinNum(-upgradeCost);
                GameAudioManager.PlayConstruction();
            }
            else if (TavernSceneManager.Instance != null)
            {
                TavernSceneManager.Instance.MarkTableUpgrading(tableId, false);
            }

            // 移除旧桌表现：隐藏桌身、清掉桌面菜品，留出位置给搬运动画
            if (tableObj != null)
            {
                tableObj.SetActive(false);
            }

            ClearDishVisual();

            var tavernManager = TavernSceneManager.Instance;
            var moveStarted = upgraded && tavernManager != null && tavernManager.StartMoveTable(tableId);
            if (!moveStarted)
            {
                // 搬运 prefab 缺失或已满级时直接还原可见状态，避免“扣钱后看不到桌子”。
                // MarkUnlocked 内部会一并清掉待升级标记，让桌位重新可被分配。
                MarkUnlocked();
            }

            // 搬运动画启动成功的情况下，待升级标记会在 MoveArrived → MarkUnlocked 时统一清掉，
            // 这里不能提前清掉，否则动画途中新顾客会立刻入座，桌子还没真正落地。
            pendingUpgradeRoutine = null;
        }

        /// <summary>
        /// 读取当前桌位下一次升级所需花费，满级时返回 0。
        /// </summary>
        private int GetPendingUpgradeCost()
        {
            var tableData = DataManager.Instance.GetTableData(tableId);
            if (tableData == null || !tableData.isUnlocked)
            {
                return 0;
            }

            var currentLevel = Mathf.Max(1, tableData.level);
            if (currentLevel >= MaxTableLevel)
            {
                return 0;
            }

            var targetLevel = currentLevel + 1;
            return TableUpgradeBaseCost * Mathf.Max(2, targetLevel);
        }

        /// <summary>
        /// 判断桌位当前是否已经满足开始升级搬运的条件。
        /// 规则：只要当前顾客和上菜任务已经结束，就允许从清理态直接进入搬桌，
        /// 不再强制等待桌位恢复到 Idle。
        /// </summary>
        /// <param name="state">当前桌位状态。</param>
        /// <param name="blockedByCustomer">是否仍被顾客或上菜任务占用。</param>
        /// <returns>满足开始升级条件时返回 true，否则返回 false。</returns>
        private static bool CanStartUpgradeAfterCurrentCustomerLeaves(TavernTableRuntimeState state, bool blockedByCustomer)
        {
            if (blockedByCustomer)
            {
                return false;
            }

            return state switch
            {
                TavernTableRuntimeState.Reserved => false,
                TavernTableRuntimeState.WaitingOrder => false,
                TavernTableRuntimeState.WaitingServe => false,
                TavernTableRuntimeState.Dining => false,
                TavernTableRuntimeState.Checkout => false,
                _ => true,
            };
        }
    }
}
