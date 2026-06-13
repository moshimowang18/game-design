using System.Collections;
using System.Collections.Generic;
using JN.Client.Manager;
using JN.Client.Model;
using JN.Client.UI;
using QFramework;
using UnityEngine;
using UnityEngine.AI;

namespace JN.Client.Scene
{
    /// <summary>
    /// 负责酒楼场景相关的运行时逻辑。
    /// </summary>
    public partial class TavernSceneManager
    {
        private const float CookDemandPollInterval = 0.25f;
        private const float DishOnPlateYOffset = 0.025f;
        private const float FoodTablePlateSurfaceYOffset = 0.015f;
        private const float FoodTablePlateSpacingRatio = 0.22f;
        private const int FoodTablePlateColumnCount = 2;
        private const float FoodTablePlateRowSpacing = 0.2f;
        private const float GroupSpawnSpacing = 0.55f;

        /// <summary>
        /// 仅在存在待上菜需求且当前库存不足时，驱动厨师做菜补货。
        /// </summary>
        /// <returns>协程迭代器。</returns>
        private IEnumerator CookDishLoop()
        {
            var demandWait = new WaitForSeconds(CookDemandPollInterval);
            while (DataManager.Instance.TavernData.isOpen)
            {
                var activeChefs = GetGuideStaffVisuals(GuideChefVisualKey);
                if (activeChefs.Length == 0)
                {
                    yield return demandWait;
                    continue;
                }

                var pendingDishDemand = GetPendingDishDemand();
                if (pendingDishDemand <= 0)
                {
                    yield return demandWait;
                    continue;
                }

                var ingredientCount = GetIngredientQueueCount();
                if (ingredientCount <= 0)
                {
                    yield return demandWait;
                    continue;
                }

                var cookDemand = Mathf.Min(pendingDishDemand, ingredientCount);
                var cookingChefs = GetCookingChefs(activeChefs, cookDemand);
                if (cookingChefs.Length == 0)
                {
                    yield return demandWait;
                    continue;
                }

                for (var index = 0; index < cookingChefs.Length; index++)
                {
                    var chef = cookingChefs[index];
                    if (chef != null)
                    {
                        TavernRuntimeModalUI.ShowChefCookProgress(chef.transform, dishCookInterval, new Vector3(0f, 1.65f, 0f));
                    }
                }

                yield return PlayChefCookLoop(cookingChefs, dishCookInterval);
                ResetChefCookAnimations(cookingChefs);

                var cookedDishCount = Mathf.Min(cookingChefs.Length, cookDemand);
                if (cookedDishCount > 0)
                {
                    var player = DataManager.Instance.PlayerData;
                    for (var index = 0; index < cookedDishCount; index++)
                    {
                        if (!TryTakeIngredientForCook(out var dishId, out var finishedPrefab))
                        {
                            break;
                        }

                        if (player != null)
                        {
                            player.ConsumeDishStock(dishId, 1);
                        }

                        AddFinishedDishToFoodTable(dishId, finishedPrefab);
                        DataManager.Instance.ChangeAvailableDishes(1);
                        Debug.Log($"[Stock] 厨师开火: {dishId} 食材->成品, 剩余食材={player?.GetDishStock(dishId) ?? 0}");
                    }

                    Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
                }
            }
        }

        /// <summary>
        /// 按当前缺菜数量选择本轮真正参与做菜的厨师，使用轮转分配避免总是同一个人工作。
        /// </summary>
        /// <param name="activeChefs">当前所有可用厨师。</param>
        /// <param name="pendingDishDemand">当前仍缺的菜数量。</param>
        /// <returns>本轮执行做菜动作的厨师列表。</returns>
        private GameObject[] GetCookingChefs(GameObject[] activeChefs, int pendingDishDemand)
        {
            if (activeChefs == null || activeChefs.Length == 0 || pendingDishDemand <= 0)
            {
                return System.Array.Empty<GameObject>();
            }

            var cookCount = Mathf.Min(activeChefs.Length, pendingDishDemand);
            var result = new GameObject[cookCount];
            for (var index = 0; index < cookCount; index++)
            {
                var chefIndex = (nextChefCookIndex + index) % activeChefs.Length;
                result[index] = activeChefs[chefIndex];
            }

            nextChefCookIndex = (nextChefCookIndex + cookCount) % activeChefs.Length;
            return result;
        }

        /// <summary>
        /// 在整段做菜时间内循环触发厨师动作，避免动画播完后长时间停在原地。
        /// </summary>
        /// <param name="cookingChefs">本轮做菜厨师。</param>
        /// <param name="duration">本轮做菜持续时间。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator PlayChefCookLoop(GameObject[] cookingChefs, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration && DataManager.Instance.TavernData.isOpen)
            {
                PlayChefCookAnimation(cookingChefs);
                var waitDuration = Mathf.Min(1.35f, duration - elapsed);
                if (waitDuration <= 0f)
                {
                    yield break;
                }

                yield return new WaitForSeconds(waitDuration);
                elapsed += waitDuration;
            }
        }

        /// <summary>
        /// 计算当前仍缺多少份菜需要由厨师补做。
        /// 规则：只统计已点完单、正在等待上菜的桌位，并扣除现有库存。
        /// </summary>
        /// <returns>大于 0 表示需要继续做菜；0 表示当前无需开火。</returns>
        private int GetPendingDishDemand()
        {
            var waitingServeCount = 0;
            foreach (var tablePair in AllTables)
            {
                var tableData = DataManager.Instance.GetTableData(tablePair.Key);
                if (tableData == null || !tableData.isUnlocked)
                {
                    continue;
                }

                if ((TavernTableRuntimeState)tableData.runtimeState == TavernTableRuntimeState.WaitingServe)
                {
                    waitingServeCount++;
                }
            }

            return Mathf.Max(0, waitingServeCount - DataManager.Instance.TavernData.availableDishes);
        }

        /// <summary>
        /// 小二招聘后，循环检查需要点菜、上菜、结账和清理的桌位。
        /// </summary>
        /// <returns>协程迭代器。</returns>
        private IEnumerator WaiterServiceLoop()
        {
            var wait = new WaitForSeconds(0.75f);
            while (DataManager.Instance.TavernData.isOpen)
            {
                if (DataManager.Instance.GetHiredGuideWaiterCount() <= 0)
                {
                    yield return wait;
                    continue;
                }

                if (busyWaiters.Count >= Mathf.Max(1, GetGuideStaffVisuals(GuideWaiterVisualKey).Length))
                {
                    yield return wait;
                    continue;
                }

                if (TryHandleOneWaiterService())
                {
                    yield return new WaitForSeconds(0.45f);
                    continue;
                }

                EnsureAllWaitersReturnedHome();
                yield return wait;
            }
        }

        /// <summary>
        /// 尝试让小二处理一个当前最需要服务的桌位。
        /// 同一张桌不会被多个小二抢占，已派发的桌位会被跳过。
        /// </summary>
        /// <returns>成功处理任意桌位时返回 true，否则返回 false。</returns>
        private bool TryHandleOneWaiterService()
        {
            // 第一轮优先处理上菜任务，确保顾客等待时间最短
            foreach (var tablePair in AllTables)
            {
                var tableData = DataManager.Instance.GetTableData(tablePair.Key);
                if (tableData == null || !tableData.isUnlocked)
                {
                    continue;
                }

                var state = (TavernTableRuntimeState)tableData.runtimeState;
                if (state != TavernTableRuntimeState.WaitingServe)
                {
                    continue;
                }

                if (DataManager.Instance.TavernData.availableDishes <= 0)
                {
                    continue;
                }

                if (assignedServeTableIds.Contains(tablePair.Key))
                {
                    continue;
                }

                if (TryStartWaiterServeTask(tablePair.Key))
                {
                    return true;
                }
            }

            // 第二轮再处理清扫任务
            foreach (var tablePair in AllTables)
            {
                var tableData = DataManager.Instance.GetTableData(tablePair.Key);
                if (tableData == null || !tableData.isUnlocked)
                {
                    continue;
                }

                if ((TavernTableRuntimeState)tableData.runtimeState != TavernTableRuntimeState.Cleaning)
                {
                    continue;
                }

                if (pendingUpgradeTableIds.Contains(tablePair.Key))
                {
                    continue;
                }

                if (assignedCleanTableIds.Contains(tablePair.Key))
                {
                    continue;
                }

                if (TryStartWaiterCleanTask(tablePair.Key))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 处理生成初始顾客相关逻辑。
        /// </summary>
        private void SpawnInitialCustomers()
        {
            var targetCount = Mathf.Min(initialCustomerBurst, Mathf.Max(1, GetUnlockedSeatCapacity()));
            while (activeCustomers.Count < targetCount)
            {
                if (!SpawnCustomerIfPossible())
                {
                    break;
                }
            }
        }

        /// <summary>
        /// 处理生成顾客如果可行相关逻辑。
        /// </summary>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private bool SpawnCustomerIfPossible()
        {
            if (customerTemplates.Count == 0 || customerEntryPoint == null)
            {
                return false;
            }

            if (activeCustomers.Count >= GetDynamicMaxActiveCustomers())
            {
                return false;
            }

            if (DataManager.Instance.GetUnlockedTableCount() == 0 || queuedCustomers.Count >= maxQueueSize)
            {
                return false;
            }

            if (!TryGetSpawnPosition(out var spawnPosition))
            {
                return false;
            }

            var desiredGroupSize = ResolveSpawnGroupSize();
            if (desiredGroupSize <= 0)
            {
                return false;
            }

            if (desiredGroupSize > 1 && TryAssignFreeTableGroup(desiredGroupSize, spawnPosition))
            {
                Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
                return true;
            }

            if (desiredGroupSize > 1)
            {
                if (TryEnqueueCustomerGroup(desiredGroupSize, spawnPosition))
                {
                    Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
                    return true;
                }

                return false;
            }

            var runtimeController = SpawnCustomerRuntime(spawnPosition);
            if (runtimeController == null)
            {
                return false;
            }

            if (!TryAssignFreeTable(runtimeController))
            {
                queuedCustomers.Add(runtimeController);
                UpdateQueuePositions();
            }

            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
            return true;
        }

        /// <summary>
        /// 尝试处理分配空闲桌位。待升级桌位会被跳过，避免新顾客在升级期间入座。
        /// </summary>
        /// <param name="customer">顾客对象。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private bool TryAssignFreeTable(TavernCustomerRuntimeController customer)
        {
            foreach (var tablePair in AllTables)
            {
                var tableData = DataManager.Instance.GetTableData(tablePair.Key);
                if (tableData == null || !tableData.isUnlocked || (TavernTableRuntimeState)tableData.runtimeState != TavernTableRuntimeState.Idle)
                {
                    continue;
                }

                if (pendingUpgradeTableIds.Contains(tablePair.Key))
                {
                    continue;
                }

                if (!TryGetTableSeatApproachPosition(tablePair.Value, 0, out var tableTargetPosition))
                {
                    continue;
                }

                DataManager.Instance.SetTableRuntimeState(tablePair.Key, TavernTableRuntimeState.Reserved);
                tablePair.Value.RefreshRuntimeState(TavernTableRuntimeState.Reserved);
                tableCustomers[tablePair.Key] = customer;
                tableCustomerGroups[tablePair.Key] = new List<TavernCustomerRuntimeController> { customer };
                customer.AssignToTable(tablePair.Key, tableTargetPosition, 0);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 更新排队位置。
        /// </summary>
        private void UpdateQueuePositions()
        {
            for (var i = 0; i < queuedCustomers.Count; i++)
            {
                queuedCustomers[i].MoveToQueue(GetQueuePosition(i));
            }
        }

        /// <summary>
        /// 获取排队站位。
        /// </summary>
        /// <param name="index">参数值。</param>
        /// <returns>返回方法执行后的结果。</returns>
        private Vector3 GetQueuePosition(int index)
        {
            if (customerEntryPoint == null)
            {
                return Vector3.zero;
            }

            var forward = customerEntryPoint.forward.sqrMagnitude > 0.1f ? customerEntryPoint.forward.normalized : Vector3.back;
            var right = customerEntryPoint.right.sqrMagnitude > 0.1f ? customerEntryPoint.right.normalized : Vector3.right;
            var laneOffset = right * (((index % 2 == 0) ? -1 : 1) * spawnLaneSpacing);
            var depthOffset = -forward * queueSpacing * (index + 1);
            var candidate = customerEntryPoint.position + laneOffset + depthOffset;
            return TryGetNavMeshPosition(candidate, out var queuePosition) ? queuePosition : customerEntryPoint.position;
        }

        /// <summary>
        /// 处理完成结账相关逻辑。
        /// </summary>
        /// <param name="桌位编号">数据编号。</param>
        private void CompleteCheckout(int tableId)
        {
            if (!AllTables.ContainsKey(tableId))
            {
                return;
            }

            var groupSize = TryGetTableCustomerGroup(tableId, out var customers) ? Mathf.Max(1, customers.Count) : 1;
            var income = (tableCheckoutIncome + Random.Range(0, 40)) * groupSize;
            GameAudioManager.PlayCheckoutCoins();
            var coinTarget = GOReferenceManager.Instance != null ? GOReferenceManager.Instance.GetCoinTransform() : null;
            if (AllTables[tableId].linkedUI != null && coinTarget != null)
            {
                GameUIEffects.PlayCoinsFly(AllTables[tableId].linkedUI.transform, coinTarget);
            }

            DataManager.Instance.ChangeCoinNum(income);
            DataManager.Instance.AddTableIncome(tableId, income);
            DataManager.Instance.SetTableRuntimeState(tableId, TavernTableRuntimeState.Cleaning);
            AllTables[tableId].RefreshRuntimeState(TavernTableRuntimeState.Cleaning, "等待清理");
            AllTables[tableId].linkedUI?.StopStateCountdown();
            TryRevealTableLv2UpgradeFeature();
            if (customers != null)
            {
                for (var index = 0; index < customers.Count; index++)
                {
                    if (customers[index] != null)
                    {
                        customers[index].LeaveTavern();
                    }
                }
            }

            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
            // 通知新系统：3D客人成功结账
            Signals.Get<TavernCustomerCheckoutSignal>().Set(tableId, groupSize, income).Dispatch();
        }

        /// <summary>
        /// 达成四次结账后展示二级桌解锁提示，并在提示结束后开放桌子升级功能。
        /// </summary>
        private void TryRevealTableLv2UpgradeFeature()
        {
            if (tableLv2UpgradeUnlockInProgress || DataManager.Instance == null)
            {
                return;
            }

            if (DataManager.Instance.IsTableLv2UpgradeUnlocked())
            {
                return;
            }

            if (DataManager.Instance.TavernData == null || DataManager.Instance.TavernData.totalServedCustomers < 4)
            {
                return;
            }

            tableLv2UpgradeUnlockInProgress = true;
            TavernRuntimeModalUI.ShowNewFeatureOpenTableLv2Panel(() =>
            {
                tableLv2UpgradeUnlockInProgress = false;
                DataManager.Instance.UnlockTableLv2Upgrade();
            });
        }

        /// <summary>
        /// 处理桌位清扫完成流程。
        /// </summary>
        /// <param name="桌位编号">数据编号。</param>
        private void FinishCleaning(int tableId)
        {
            if (!AllTables.ContainsKey(tableId))
            {
                return;
            }

            StopAutoClean(tableId);
            if (activeCleanSmokeEffects.TryGetValue(tableId, out var smokeEffect))
            {
                activeCleanSmokeEffects.Remove(tableId);
                if (smokeEffect != null)
                {
                    Destroy(smokeEffect);
                }
            }

            tableCustomers.Remove(tableId);
            DataManager.Instance.SetTableRuntimeState(tableId, TavernTableRuntimeState.Idle);
            AllTables[tableId].RefreshRuntimeState(TavernTableRuntimeState.Idle);
            AllTables[tableId].linkedUI?.StopStateCountdown();
            AllTables[tableId].ClearDishVisual();

            TryAssignQueuedCustomers();
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
        }

        /// <summary>
        /// 启动自动清扫流程。
        /// </summary>
        /// <param name="桌位编号">数据编号。</param>
        private void StartAutoClean(int tableId)
        {
            StopAutoClean(tableId);
            autoCleanRoutines[tableId] = StartCoroutine(AutoCleanRoutine(tableId));
        }

        /// <summary>
        /// 停止自动清扫流程。
        /// </summary>
        /// <param name="桌位编号">数据编号。</param>
        private void StopAutoClean(int tableId)
        {
            if (!autoCleanRoutines.TryGetValue(tableId, out var routine) || routine == null)
            {
                return;
            }

            StopCoroutine(routine);
            autoCleanRoutines.Remove(tableId);
        }

        /// <summary>
        /// 按间隔检查并自动清扫桌位。
        /// </summary>
        /// <param name="桌位编号">数据编号。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator AutoCleanRoutine(int tableId)
        {
            yield return new WaitForSeconds(autoCleanDuration);
            autoCleanRoutines.Remove(tableId);

            var tableData = DataManager.Instance.GetTableData(tableId);
            if (tableData == null || (TavernTableRuntimeState)tableData.runtimeState != TavernTableRuntimeState.Cleaning)
            {
                yield break;
            }

            FinishCleaning(tableId);
        }

        /// <summary>
        /// 尝试给排队顾客分配空桌。
        /// </summary>
        private void TryAssignQueuedCustomers()
        {
            while (queuedCustomers.Count > 0)
            {
                if (!TryAssignQueuedCustomerGroup())
                {
                    break;
                }
            }

            UpdateQueuePositions();
        }

        /// <summary>
        /// 根据当前桌位容量，决定这次刷新的顾客组人数。
        /// </summary>
        /// <returns>1~4 之间的顾客人数。</returns>
        private int ResolveSpawnGroupSize()
        {
            var idlePreferredGroupSize = GetPreferredIdleSpawnGroupSize();
            if (idlePreferredGroupSize > 0)
            {
                return idlePreferredGroupSize;
            }

            return GetPreferredQueuedSpawnGroupSize();
        }

        /// <summary>
        /// 根据当前空桌情况，选择本轮应直接入店的一组顾客人数。
        /// 优先塞满现有空桌：有空四人桌就来四人，否则有空两人桌就来两人。
        /// </summary>
        /// <returns>可直接入店的整组人数；没有空桌时返回 0。</returns>
        private int GetPreferredIdleSpawnGroupSize()
        {
            if (HasIdleTableWithSeatCapacity(4))
            {
                return 4;
            }

            if (HasIdleTableWithSeatCapacity(2))
            {
                return 2;
            }

            return 0;
        }

        /// <summary>
        /// 店满后仍允许刷新排队顾客，但优先排更容易被后续空桌消化的小组人数。
        /// 如果项目同时存在二人桌和四人桌，这里优先排二人组；只有四人桌时才排四人组。
        /// </summary>
        /// <returns>适合加入队列的整组人数；没有任何已解锁桌位时返回 0。</returns>
        private int GetPreferredQueuedSpawnGroupSize()
        {
            if (HasUnlockedTableWithSeatCapacity(2))
            {
                return 2;
            }

            if (HasUnlockedTableWithSeatCapacity(4))
            {
                return 4;
            }

            return 0;
        }

        /// <summary>
        /// 判断当前是否存在可直接接待指定人数的空桌。
        /// </summary>
        /// <param name="groupSize">目标人数。</param>
        /// <returns>存在可用桌位时返回 true，否则返回 false。</returns>
        private bool HasIdleTableWithSeatCapacity(int groupSize)
        {
            foreach (var tablePair in AllTables)
            {
                var tableData = DataManager.Instance.GetTableData(tablePair.Key);
                if (tableData == null
                    || !tableData.isUnlocked
                    || (TavernTableRuntimeState)tableData.runtimeState != TavernTableRuntimeState.Idle
                    || pendingUpgradeTableIds.Contains(tablePair.Key))
                {
                    continue;
                }

                if (tablePair.Value != null && tablePair.Value.GetSeatCapacity() >= groupSize)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断当前是否存在已经解锁、且容量满足指定人数的桌位。
        /// 用于店内坐满时，仍然按对应桌型刷新一组排队顾客。
        /// </summary>
        /// <param name="groupSize">目标人数。</param>
        /// <returns>存在匹配桌位时返回 true。</returns>
        private bool HasUnlockedTableWithSeatCapacity(int groupSize)
        {
            foreach (var tablePair in AllTables)
            {
                var tableData = DataManager.Instance.GetTableData(tablePair.Key);
                if (tableData == null || !tableData.isUnlocked || tablePair.Value == null)
                {
                    continue;
                }

                if (tablePair.Value.GetSeatCapacity() >= groupSize)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 尝试把一组顾客直接分配到同一张空桌。
        /// </summary>
        /// <param name="groupSize">顾客人数。</param>
        /// <param name="spawnPosition">入口出生点。</param>
        /// <returns>成功创建并分配整组顾客时返回 true，否则返回 false。</returns>
        private bool TryAssignFreeTableGroup(int groupSize, Vector3 spawnPosition)
        {
            if (groupSize <= 1 || activeCustomers.Count + groupSize > GetDynamicMaxActiveCustomers())
            {
                return false;
            }

            foreach (var tablePair in AllTables)
            {
                var tableData = DataManager.Instance.GetTableData(tablePair.Key);
                if (tableData == null
                    || !tableData.isUnlocked
                    || (TavernTableRuntimeState)tableData.runtimeState != TavernTableRuntimeState.Idle
                    || pendingUpgradeTableIds.Contains(tablePair.Key)
                    || tablePair.Value == null
                    || tablePair.Value.GetSeatCapacity() < groupSize)
                {
                    continue;
                }

                var spawnedCustomers = new List<TavernCustomerRuntimeController>(groupSize);
                for (var seatIndex = 0; seatIndex < groupSize; seatIndex++)
                {
                    if (!TryGetTableSeatApproachPosition(tablePair.Value, seatIndex, out var tableTargetPosition))
                    {
                        for (var rollbackIndex = 0; rollbackIndex < spawnedCustomers.Count; rollbackIndex++)
                        {
                            if (spawnedCustomers[rollbackIndex] != null)
                            {
                                activeCustomers.Remove(spawnedCustomers[rollbackIndex]);
                                Destroy(spawnedCustomers[rollbackIndex].gameObject);
                            }
                        }

                        return false;
                    }

                    var runtimeController = SpawnCustomerRuntime(GetGroupSpawnPosition(spawnPosition, seatIndex, groupSize));
                    if (runtimeController == null)
                    {
                        for (var rollbackIndex = 0; rollbackIndex < spawnedCustomers.Count; rollbackIndex++)
                        {
                            if (spawnedCustomers[rollbackIndex] != null)
                            {
                                activeCustomers.Remove(spawnedCustomers[rollbackIndex]);
                                Destroy(spawnedCustomers[rollbackIndex].gameObject);
                            }
                        }

                        return false;
                    }

                    runtimeController.AssignToTable(tablePair.Key, tableTargetPosition, seatIndex);
                    spawnedCustomers.Add(runtimeController);
                }

                DataManager.Instance.SetTableRuntimeState(tablePair.Key, TavernTableRuntimeState.Reserved);
                tablePair.Value.RefreshRuntimeState(TavernTableRuntimeState.Reserved);
                tableCustomerGroups[tablePair.Key] = spawnedCustomers;
                tableCustomers[tablePair.Key] = spawnedCustomers[0];
                return true;
            }

            return false;
        }

        /// <summary>
        /// 当没有空桌可立即入座时，按目标桌位容量整组生成排队顾客，避免后续只拆出单人入座。
        /// </summary>
        /// <param name="groupSize">整组人数。</param>
        /// <param name="spawnPosition">入口出生点。</param>
        /// <returns>成功生成排队组时返回 true。</returns>
        private bool TryEnqueueCustomerGroup(int groupSize, Vector3 spawnPosition)
        {
            if (groupSize <= 1
                || queuedCustomers.Count + groupSize > maxQueueSize
                || activeCustomers.Count + groupSize > GetDynamicMaxActiveCustomers())
            {
                return false;
            }

            var spawnedCustomers = new List<TavernCustomerRuntimeController>(groupSize);
            for (var memberIndex = 0; memberIndex < groupSize; memberIndex++)
            {
                var runtimeController = SpawnCustomerRuntime(GetGroupSpawnPosition(spawnPosition, memberIndex, groupSize));
                if (runtimeController == null)
                {
                    for (var rollbackIndex = 0; rollbackIndex < spawnedCustomers.Count; rollbackIndex++)
                    {
                        if (spawnedCustomers[rollbackIndex] != null)
                        {
                            activeCustomers.Remove(spawnedCustomers[rollbackIndex]);
                            Destroy(spawnedCustomers[rollbackIndex].gameObject);
                        }
                    }

                    return false;
                }

                spawnedCustomers.Add(runtimeController);
                queuedCustomers.Add(runtimeController);
            }

            UpdateQueuePositions();
            return true;
        }

        /// <summary>
        /// 从排队队列中按桌位容量整组入座，Lv1 固定两人、Lv2 固定四人。
        /// </summary>
        /// <returns>成功分配任意一桌时返回 true。</returns>
        private bool TryAssignQueuedCustomerGroup()
        {
            var preferredGroupSizes = new[] { 4, 2 };
            for (var preferredIndex = 0; preferredIndex < preferredGroupSizes.Length; preferredIndex++)
            {
                var expectedGroupSize = preferredGroupSizes[preferredIndex];
                if (queuedCustomers.Count < expectedGroupSize)
                {
                    continue;
                }

                foreach (var tablePair in AllTables)
                {
                    var tableData = DataManager.Instance.GetTableData(tablePair.Key);
                    if (tableData == null
                        || !tableData.isUnlocked
                        || (TavernTableRuntimeState)tableData.runtimeState != TavernTableRuntimeState.Idle
                        || pendingUpgradeTableIds.Contains(tablePair.Key)
                        || tablePair.Value == null)
                    {
                        continue;
                    }

                    var groupSize = tablePair.Value.GetSeatCapacity();
                    if (groupSize != expectedGroupSize)
                    {
                        continue;
                    }

                    var assignedCustomers = new List<TavernCustomerRuntimeController>(groupSize);
                    for (var seatIndex = 0; seatIndex < groupSize; seatIndex++)
                    {
                        var customer = queuedCustomers[seatIndex];
                        if (customer == null
                            || !TryGetTableSeatApproachPosition(tablePair.Value, seatIndex, out var tableTargetPosition))
                        {
                            return false;
                        }

                        customer.AssignToTable(tablePair.Key, tableTargetPosition, seatIndex);
                        assignedCustomers.Add(customer);
                    }

                    queuedCustomers.RemoveRange(0, groupSize);
                    DataManager.Instance.SetTableRuntimeState(tablePair.Key, TavernTableRuntimeState.Reserved);
                    tablePair.Value.RefreshRuntimeState(TavernTableRuntimeState.Reserved);
                    tableCustomerGroups[tablePair.Key] = assignedCustomers;
                    tableCustomers[tablePair.Key] = assignedCustomers[0];
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 为指定座位计算一个“先走到附近、再坐下”的接近点，减少多人同时去同一张桌子时的相互挤压。
        /// </summary>
        /// <param name="table">目标桌位。</param>
        /// <param name="seatIndex">座位索引。</param>
        /// <param name="approachPosition">输出的接近点。</param>
        /// <returns>找到可寻路接近点时返回 true。</returns>
        private bool TryGetTableSeatApproachPosition(TableArea table, int seatIndex, out Vector3 approachPosition)
        {
            approachPosition = Vector3.zero;
            if (table == null)
            {
                return false;
            }

            if (!table.TryGetSeatPoseByIndex(seatIndex, out var seatPosition, out var lookAtPosition))
            {
                return TryGetNavMeshPosition(table.GetCustomerTargetPosition(), out approachPosition);
            }

            var awayFromTable = seatPosition - lookAtPosition;
            awayFromTable.y = 0f;
            if (awayFromTable.sqrMagnitude <= 0.0001f)
            {
                awayFromTable = (seatPosition - table.transform.position);
                awayFromTable.y = 0f;
            }

            if (awayFromTable.sqrMagnitude <= 0.0001f)
            {
                awayFromTable = table.transform.right;
            }

            awayFromTable.Normalize();
            var side = Vector3.Cross(Vector3.up, awayFromTable).normalized;
            var sideOffset = ((seatIndex % 2 == 0) ? -1f : 1f) * 0.08f;
            var candidate = seatPosition + awayFromTable * 0.28f + side * sideOffset;
            return TryGetNavMeshPosition(candidate, out approachPosition)
                   || TryGetNavMeshPosition(seatPosition, out approachPosition);
        }

        /// <summary>
        /// 生成单个顾客运行时对象。
        /// </summary>
        /// <param name="spawnPosition">出生坐标。</param>
        /// <returns>成功创建时返回控制器，否则返回 null。</returns>
        private TavernCustomerRuntimeController SpawnCustomerRuntime(Vector3 spawnPosition)
        {
            var template = customerTemplates[Random.Range(0, customerTemplates.Count)];
            var customerObj = Instantiate(template, spawnPosition, customerEntryPoint.rotation);
            customerObj.name = $"{template.name}_Runtime";

            PrepareSpawnedCustomer(customerObj, spawnPosition);

            var runtimeController = customerObj.GetComponent<TavernCustomerRuntimeController>();
            if (runtimeController == null)
            {
                runtimeController = customerObj.AddComponent<TavernCustomerRuntimeController>();
            }

            var exitPosition = GetExitPosition(spawnPosition);
            runtimeController.Initialize(this, spawnPosition, exitPosition);
            activeCustomers.Add(runtimeController);
            return runtimeController;
        }

        /// <summary>
        /// 根据当前已解锁桌位总座位数和排队上限，动态计算可存在的顾客总数。
        /// 避免固定上限 8 把后续整组顾客提前挡掉。
        /// </summary>
        /// <returns>当前允许的顾客总数上限。</returns>
        private int GetDynamicMaxActiveCustomers()
        {
            var seatCapacity = 0;
            foreach (var tablePair in AllTables)
            {
                var tableData = DataManager.Instance.GetTableData(tablePair.Key);
                if (tableData == null || !tableData.isUnlocked || tablePair.Value == null)
                {
                    continue;
                }

                seatCapacity += Mathf.Max(0, tablePair.Value.GetSeatCapacity());
            }

            return Mathf.Max(maxActiveCustomers, seatCapacity + maxQueueSize);
        }

        /// <summary>
        /// 统计当前所有已解锁桌位的总座位数。
        /// </summary>
        /// <returns>已解锁总座位数。</returns>
        private int GetUnlockedSeatCapacity()
        {
            var seatCapacity = 0;
            foreach (var tablePair in AllTables)
            {
                var tableData = DataManager.Instance.GetTableData(tablePair.Key);
                if (tableData == null || !tableData.isUnlocked || tablePair.Value == null)
                {
                    continue;
                }

                seatCapacity += Mathf.Max(0, tablePair.Value.GetSeatCapacity());
            }

            return seatCapacity;
        }

        /// <summary>
        /// 按整组人数把顾客出生点沿门口横向打散，避免多人刷在一起。
        /// </summary>
        /// <param name="baseSpawnPosition">组的基础出生点。</param>
        /// <param name="memberIndex">当前成员索引。</param>
        /// <param name="groupSize">整组人数。</param>
        /// <returns>当前成员的出生坐标。</returns>
        private Vector3 GetGroupSpawnPosition(Vector3 baseSpawnPosition, int memberIndex, int groupSize)
        {
            if (customerEntryPoint == null)
            {
                return baseSpawnPosition;
            }

            var right = customerEntryPoint.right.sqrMagnitude > 0.1f ? customerEntryPoint.right.normalized : Vector3.right;
            var centeredOffset = (memberIndex - (groupSize - 1) * 0.5f) * GroupSpawnSpacing;
            var candidate = baseSpawnPosition + right * centeredOffset;
            return TryGetNavMeshPosition(candidate, out var navMeshPosition) ? navMeshPosition : candidate;
        }

        /// <summary>
        /// 按菜品 ID 选择食材模型（裸放桌上）。
        /// </summary>
        private GameObject GetIngredientPrefabForDish(string dishId)
        {
            if (dishPrefabs.Count == 0)
            {
                return null;
            }

            return dishPrefabs[GetDishVisualIndex(dishId) % dishPrefabs.Count];
        }

        /// <summary>
        /// 按菜品 ID 选择成品菜模型（装盘后由小二端走）。
        /// </summary>
        private GameObject GetFinishedDishPrefabForDish(string dishId)
        {
            return GetIngredientPrefabForDish(dishId);
        }

        private static int GetDishVisualIndex(string dishId)
        {
            return dishId switch
            {
                "rice" => 0,
                "tofu" => 1,
                "fish" => 2,
                "herb_soup" => 3,
                "birdnest" => 0,
                "exotic_meat" => 1,
                _ => Mathf.Abs(dishId?.GetHashCode() ?? 0)
            };
        }

        /// <summary>
        /// 营业开始时，把备菜库存以食材模型摆到 FoodTable。
        /// </summary>
        public void StageIngredientStockFromPlayer(PlayerModel player)
        {
            ClearPreparedDishQueue();
            if (player?.DishStock == null)
            {
                return;
            }

            foreach (var kv in player.DishStock)
            {
                var ingredientPrefab = GetIngredientPrefabForDish(kv.Key);
                if (ingredientPrefab == null)
                {
                    continue;
                }

                for (var index = 0; index < kv.Value; index++)
                {
                    var root = CreateIngredientInstance(ingredientPrefab);
                    if (root == null)
                    {
                        continue;
                    }

                    stagedIngredientEntries.Add(new StagedDishEntry
                    {
                        rootObject = root,
                        dishPrefab = ingredientPrefab,
                        dishId = kv.Key
                    });
                }
            }

            RefreshFoodTableLayout();
            Debug.Log($"[Stock] 食材上桌: {GetIngredientQueueCount()} 份");
        }

        private void AddFinishedDishToFoodTable(string dishId, GameObject dishPrefab)
        {
            if (dishPrefab == null)
            {
                return;
            }

            stagedDishEntries.Add(new StagedDishEntry
            {
                rootObject = CreatePreparedDishInstance(dishPrefab),
                dishPrefab = dishPrefab,
                dishId = dishId
            });

            RefreshFoodTableLayout();
        }

        private bool TryTakeIngredientForCook(out string dishId, out GameObject finishedPrefab)
        {
            dishId = string.Empty;
            finishedPrefab = null;
            stagedIngredientEntries.RemoveAll(entry => entry == null || entry.rootObject == null);
            if (stagedIngredientEntries.Count == 0)
            {
                return false;
            }

            var entry = stagedIngredientEntries[0];
            stagedIngredientEntries.RemoveAt(0);
            if (entry.rootObject != null)
            {
                Destroy(entry.rootObject);
            }

            dishId = entry.dishId;
            finishedPrefab = GetFinishedDishPrefabForDish(dishId) ?? entry.dishPrefab;
            RefreshFoodTableLayout();
            return !string.IsNullOrEmpty(dishId) && finishedPrefab != null;
        }

        private int GetIngredientQueueCount()
        {
            stagedIngredientEntries.RemoveAll(entry => entry == null || entry.rootObject == null);
            return stagedIngredientEntries.Count;
        }

        /// <summary>
        /// 创建一份摆在 FoodTable 上的裸食材（无餐盘）。
        /// </summary>
        private GameObject CreateIngredientInstance(GameObject ingredientPrefab)
        {
            if (foodTableObject == null || !foodTableObject.activeInHierarchy || ingredientPrefab == null)
            {
                return null;
            }

            var instance = Instantiate(ingredientPrefab, foodTableObject.transform, false);
            instance.name = $"Ingredient_{ingredientPrefab.name}_{stagedIngredientEntries.Count + 1}";
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * 0.85f;
            return instance;
        }

        /// <summary>
        /// 随机获取桌面菜品预制体。
        /// </summary>
        /// <returns>返回匹配到的对象引用。</returns>
        private GameObject GetRandomDishPrefab()
        {
            if (dishPrefabs.Count == 0)
            {
                return null;
            }

            return dishPrefabs[Random.Range(0, dishPrefabs.Count)];
        }

        /// <summary>
        /// 从 FoodTable 取走一份已完成的菜品，并返回对应桌面菜品预制体。
        /// </summary>
        /// <returns>成功取到时返回对应桌面菜品预制体，否则返回 null。</returns>
        private GameObject TakePreparedDishPrefab()
        {
            stagedDishEntries.RemoveAll(entry => entry == null);
            if (stagedDishEntries.Count == 0)
            {
                return null;
            }

            var entry = stagedDishEntries[0];
            stagedDishEntries.RemoveAt(0);
            if (entry.rootObject != null)
            {
                Destroy(entry.rootObject);
            }

            RefreshPreparedDishLayout();
            return entry.dishPrefab;
        }

        /// <summary>
        /// 将尚未真正上桌的菜品退回 FoodTable 队列。
        /// </summary>
        /// <param name="dishPrefab">菜品预制体。</param>
        private void ReturnPreparedDishPrefab(GameObject dishPrefab)
        {
            if (dishPrefab == null)
            {
                return;
            }

            stagedDishEntries.Insert(0, new StagedDishEntry
            {
                rootObject = CreatePreparedDishInstance(dishPrefab),
                dishPrefab = dishPrefab
            });

            RefreshPreparedDishLayout();
        }

        /// <summary>
        /// 获取当前 FoodTable 队列中的成品菜数量。
        /// </summary>
        private int GetPreparedDishQueueCount()
        {
            stagedDishEntries.RemoveAll(entry => entry == null || entry.rootObject == null);
            return stagedDishEntries.Count;
        }

        /// <summary>
        /// 清空 FoodTable 上当前所有待取菜品。
        /// </summary>
        private void ClearPreparedDishQueue()
        {
            for (var index = 0; index < stagedDishEntries.Count; index++)
            {
                var entry = stagedDishEntries[index];
                if (entry?.rootObject != null)
                {
                    Destroy(entry.rootObject);
                }
            }

            stagedDishEntries.Clear();

            for (var index = 0; index < stagedIngredientEntries.Count; index++)
            {
                var entry = stagedIngredientEntries[index];
                if (entry?.rootObject != null)
                {
                    Destroy(entry.rootObject);
                }
            }

            stagedIngredientEntries.Clear();
        }

        /// <summary>
        /// 创建一份摆在 FoodTable 上的餐盘与菜品组合。
        /// </summary>
        /// <param name="dishPrefab">菜品预制体。</param>
        /// <returns>组合根对象；创建失败时返回 null。</returns>
        private GameObject CreatePreparedDishInstance(GameObject dishPrefab)
        {
            if (foodTableObject == null || !foodTableObject.activeInHierarchy || platePrefab == null || dishPrefab == null)
            {
                return null;
            }

            var plateInstance = Instantiate(platePrefab, foodTableObject.transform, false);
            plateInstance.name = $"PreparedPlate_{stagedDishEntries.Count + 1}";
            plateInstance.transform.localRotation = Quaternion.identity;
            plateInstance.transform.localScale = Vector3.one;

            var dishInstance = Instantiate(dishPrefab, plateInstance.transform, false);
            dishInstance.name = dishPrefab.name;
            dishInstance.transform.localPosition = Vector3.up * DishOnPlateYOffset;
            dishInstance.transform.localRotation = Quaternion.identity;
            dishInstance.transform.localScale = Vector3.one;
            return plateInstance;
        }

        /// <summary>
        /// 按固定队列把 FoodTable 上的食材与成品菜重新排布，避免重叠。
        /// </summary>
        private void RefreshFoodTableLayout()
        {
            if (foodTableObject == null)
            {
                return;
            }

            stagedIngredientEntries.RemoveAll(entry => entry == null || entry.rootObject == null);
            stagedDishEntries.RemoveAll(entry => entry == null || entry.rootObject == null);
            if (!TryGetFoodTableTopBounds(out var centerLocalX, out var topLocalY, out var halfWidth))
            {
                centerLocalX = 0f;
                topLocalY = 0f;
                halfWidth = 0.4f;
            }

            var spacingX = Mathf.Max(0.14f, halfWidth * FoodTablePlateSpacingRatio);
            var columnCount = Mathf.Max(1, FoodTablePlateColumnCount);
            LayoutFoodTableEntries(stagedIngredientEntries, centerLocalX, topLocalY, spacingX, columnCount, 0f, FoodTablePlateRowSpacing * 0.9f);

            var ingredientRows = Mathf.CeilToInt(stagedIngredientEntries.Count / (float)columnCount);
            var finishedRowOffset = ingredientRows > 0
                ? startRowZOffset(ingredientRows) + FoodTablePlateRowSpacing
                : 0f;
            LayoutFoodTableEntries(stagedDishEntries, centerLocalX, topLocalY, spacingX, columnCount, finishedRowOffset, FoodTablePlateRowSpacing);
        }

        private static float startRowZOffset(int rowCount)
        {
            return -FoodTablePlateRowSpacing * Mathf.Max(0, rowCount - 1) * 0.5f
                   + (rowCount - 1) * FoodTablePlateRowSpacing;
        }

        private void LayoutFoodTableEntries(
            List<StagedDishEntry> entries,
            float centerLocalX,
            float topLocalY,
            float spacingX,
            int columnCount,
            float rowZOffset,
            float rowSpacing)
        {
            if (entries.Count == 0)
            {
                return;
            }

            var rowCount = Mathf.CeilToInt(entries.Count / (float)columnCount);
            var startRowZ = -rowSpacing * Mathf.Max(0, rowCount - 1) * 0.5f + rowZOffset;
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry?.rootObject == null)
                {
                    continue;
                }

                var columnIndex = index % columnCount;
                var rowIndex = index / columnCount;
                var columnsInCurrentRow = Mathf.Min(columnCount, entries.Count - rowIndex * columnCount);
                var startX = centerLocalX - spacingX * Mathf.Max(0, columnsInCurrentRow - 1) * 0.5f;
                var localZ = startRowZ + rowIndex * rowSpacing;
                entry.rootObject.transform.localPosition = new Vector3(startX + spacingX * columnIndex, topLocalY + FoodTablePlateSurfaceYOffset, localZ);
                entry.rootObject.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// 按固定队列把 FoodTable 上的待取菜品重新排布，避免多份菜重叠。
        /// </summary>
        private void RefreshPreparedDishLayout()
        {
            RefreshFoodTableLayout();
        }

        /// <summary>
        /// 计算 FoodTable 顶面的本地坐标与可摆放宽度，用于把餐盘压到桌面上。
        /// </summary>
        /// <param name="centerLocalX">桌面中心的本地 X 坐标。</param>
        /// <param name="topLocalY">桌面顶面的本地 Y 坐标。</param>
        /// <param name="halfWidth">桌面半宽，用于排布多份菜。</param>
        /// <returns>成功获取渲染包围盒时返回 true，否则返回 false。</returns>
        private bool TryGetFoodTableTopBounds(out float centerLocalX, out float topLocalY, out float halfWidth)
        {
            centerLocalX = 0f;
            topLocalY = 0f;
            halfWidth = 0.4f;
            if (foodTableObject == null)
            {
                return false;
            }

            var renderers = foodTableObject.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            Bounds bounds = default;
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

            if (!hasBounds)
            {
                return false;
            }

            var topWorld = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            var localTop = foodTableObject.transform.InverseTransformPoint(topWorld);
            centerLocalX = localTop.x;
            topLocalY = localTop.y;
            halfWidth = Mathf.Max(0.25f, bounds.extents.x);
            return true;
        }

        /// <summary>
        /// 准备刚生成顾客的运行状态。
        /// </summary>
        /// <param name="customerObj">顾客对象。</param>
        /// <param name="spawnPosition">坐标。</param>
        private void PrepareSpawnedCustomer(GameObject customerObj, Vector3 spawnPosition)
        {
            foreach (var navMeshAgent in customerObj.GetComponentsInChildren<NavMeshAgent>(true))
            {
                navMeshAgent.enabled = false;
            }

            customerObj.transform.position = spawnPosition;
            customerObj.SetActive(true);
        }

        /// <summary>
        /// 尝试获取顾客生成位置。
        /// </summary>
        /// <param name="spawnPosition">坐标。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private bool TryGetSpawnPosition(out Vector3 spawnPosition)
        {
            if (customerEntryPoint == null)
            {
                spawnPosition = Vector3.zero;
                return false;
            }

            var right = customerEntryPoint.right.sqrMagnitude > 0.1f ? customerEntryPoint.right.normalized : Vector3.right;
            var forward = customerEntryPoint.forward.sqrMagnitude > 0.1f ? customerEntryPoint.forward.normalized : Vector3.back;

            for (var attempt = 0; attempt < 4; attempt++)
            {
                var laneIndex = (activeCustomers.Count + attempt) % 3 - 1;
                var candidate = customerEntryPoint.position + right * (laneIndex * (spawnLaneSpacing + 0.15f)) + forward * 0.75f;
                if (TryGetNavMeshPosition(candidate, out spawnPosition))
                {
                    return true;
                }
            }

            return TryGetNavMeshPosition(customerEntryPoint.position, out spawnPosition);
        }

        /// <summary>
        /// 获取出口位置。
        /// </summary>
        /// <param name="fallbackPosition">坐标。</param>
        /// <returns>返回方法执行后的结果。</returns>
        private Vector3 GetExitPosition(Vector3 fallbackPosition)
        {
            return customerExitPoint != null && TryGetNavMeshPosition(customerExitPoint.position, out var exitPosition)
                ? exitPosition
                : fallbackPosition;
        }
    }
}
