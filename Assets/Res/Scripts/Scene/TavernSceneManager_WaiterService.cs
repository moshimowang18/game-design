using System.Collections;
using System.Collections.Generic;
using JN.Client;
using JN.Client.Manager;
using JN.Client.Model;
using QFramework;
using UnityEngine;
using UnityEngine.AI;

namespace JN.Client.Scene
{
    public partial class TavernSceneManager
    {
        #region Waiter Service

        private const float WaiterMoveSpeed = 1.15f;
        private const float WaiterReachDistance = 0.12f;
        private const float WaiterTurnSpeed = 360f;
        private const float WaiterLookAheadDistance = 0.35f;
        private const float WalkAnimationSpeed = 0.65f;
        private const float CleanSmokeScale = 0.18f;
        private const float WaiterVisualScaleMultiplier = 0.76f;
        private const float WaiterMoveTotalTimeout = 8f;
        private const float WaiterMoveStuckCheckInterval = 0.4f;
        private const float WaiterMoveStuckProgressThreshold = 0.02f;
        private const int PreferredWaiterStaffId = 5;
        private const string CleanSmokeEffectPath = "Assets/Res/Resources/Effect/Effect_Smoke.prefab";
        private const string WaiterCleanTrigger = "TrClean";
        private const string WaiterSpeedParam = "Speed";
        private const string AnimatorMovementState = "Movement";
        private const string AnimatorBaseLayerMovementState = "Base Layer.Movement";
        private const string ChefCookState = "Cook";
        private const string ChefBaseLayerCookState = "Base Layer.Cook";
        private const string AnimatorIsSittingParam = "IsSitting";
        private const string AnimatorIsEatingParam = "IsEating";
        private const string ChefCookTrigger = "TrCook";
        private static GameObject cleanSmokeEffectPrefab;

        // 小二与桌位的派发关系：避免多个小二被分配到同一张桌处理同一件事
        private readonly HashSet<int> assignedServeTableIds = new();
        private readonly HashSet<int> assignedCleanTableIds = new();
        private readonly Dictionary<GameObject, int> waiterServeAssignments = new();
        private readonly Dictionary<GameObject, int> waiterCleanAssignments = new();

        /// <summary>
        /// 尝试派发小二上菜任务。
        /// </summary>
        /// <param name="tableId">需要上菜的桌位编号。</param>
        /// <returns>成功派发时返回 true，否则返回 false。</returns>
        private bool TryStartWaiterServeTask(int tableId)
        {
            if (assignedServeTableIds.Contains(tableId))
            {
                return false;
            }

            var waiter = GetAvailableServiceWaiterVisual();
            if (DataManager.Instance.GetHiredGuideWaiterCount() <= 0
                || waiter == null
                || !HasAvailablePreparedDishForServe())
            {
                return false;
            }

            reservedServeDishCount++;
            StartWaiterTask(waiter, WaiterServeRoutine(waiter, tableId), serveTableId: tableId);
            return true;
        }

        /// <summary>
        /// 尝试派发小二清扫桌位任务。
        /// </summary>
        /// <param name="tableId">需要清扫的桌位编号。</param>
        /// <returns>成功派发时返回 true，否则返回 false。</returns>
        private bool TryStartWaiterCleanTask(int tableId)
        {
            if (assignedCleanTableIds.Contains(tableId) || IsTableUpgrading(tableId))
            {
                return false;
            }

            var waiter = GetAvailableServiceWaiterVisual();
            if (DataManager.Instance.GetHiredGuideWaiterCount() <= 0 || waiter == null)
            {
                return false;
            }

            StartWaiterTask(waiter, WaiterCleanRoutine(waiter, tableId), cleanTableId: tableId);
            return true;
        }

        /// <summary>
        /// 取消指定桌位正在排队或执行中的清扫任务，
        /// 让待升级桌在顾客离场后可以直接进入搬桌流程。
        /// </summary>
        /// <param name="tableId">桌位编号。</param>
        private void CancelWaiterCleanTask(int tableId)
        {
            assignedCleanTableIds.Remove(tableId);
            StopCleanSmokeEffect(tableId, null);

            GameObject targetWaiter = null;
            foreach (var pair in waiterCleanAssignments)
            {
                if (pair.Value == tableId)
                {
                    targetWaiter = pair.Key;
                    break;
                }
            }

            if (targetWaiter == null)
            {
                return;
            }

            if (waiterTaskRoutines.TryGetValue(targetWaiter, out var routine) && routine != null)
            {
                StopCoroutine(routine);
            }

            ResetWaiterServiceAnimation(targetWaiter.GetComponentInChildren<Animator>(true));
            ReleaseWaiterAssignments(targetWaiter);
            waiterTaskRoutines.Remove(targetWaiter);
            busyWaiters.Remove(targetWaiter);
        }

        /// <summary>
        /// 小二先到灶台取菜，再寻路到桌边完成上菜。
        /// </summary>
        /// <param name="tableId">需要上菜的桌位编号。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator WaiterServeRoutine(GameObject waiter, int tableId)
        {
            if (waiter == null || !AllTables.TryGetValue(tableId, out var table))
            {
                FinishWaiterTask(waiter);
                yield break;
            }

            yield return MoveWaiterToDishPickup(waiter);
            var dishPrefab = TakePreparedDishPrefab();
            if (dishPrefab == null)
            {
                ReleaseReservedServeDish();
                FinishWaiterTask(waiter);
                yield break;
            }

            var tableData = DataManager.Instance.GetTableData(tableId);
            if (tableData != null
                && (TavernTableRuntimeState)tableData.runtimeState == TavernTableRuntimeState.WaitingServe)
            {
                yield return MoveWaiterToTable(waiter, table);
                ServeTableByWaiter(tableId, table, dishPrefab);
            }
            else
            {
                ReturnPreparedDishPrefab(dishPrefab);
                ReleaseReservedServeDish();
            }

            FinishWaiterTask(waiter);
        }

        /// <summary>
        /// 小二寻路到桌边播放清扫动作，清扫完成后释放桌位。
        /// </summary>
        /// <param name="tableId">需要清扫的桌位编号。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator WaiterCleanRoutine(GameObject waiter, int tableId)
        {
            if (waiter == null || !AllTables.TryGetValue(tableId, out var table))
            {
                FinishWaiterTask(waiter);
                yield break;
            }

            table.RefreshRuntimeState(TavernTableRuntimeState.Cleaning, "清理中");
            yield return MoveWaiterToTable(waiter, table);

            var animator = waiter.GetComponentInChildren<Animator>(true);
            TriggerAnimator(animator, WaiterCleanTrigger);
            SetAnimatorSpeed(animator, 0f);
            GameAudioManager.PlayWiping();
            var smokeEffect = PlayCleanSmokeEffect(tableId, table);
            yield return new WaitForSeconds(autoCleanDuration);
            StopCleanSmokeEffect(tableId, smokeEffect);

            var tableData = DataManager.Instance.GetTableData(tableId);
            if (tableData != null && (TavernTableRuntimeState)tableData.runtimeState == TavernTableRuntimeState.Cleaning)
            {
                FinishCleaning(tableId);
            }

            ResetWaiterServiceAnimation(animator);
            FinishWaiterTask(waiter);
        }

        /// <summary>
        /// 当小二没有任务时，确保所有闲置小二回到招聘后放置的原始站位。
        /// </summary>
        private void EnsureAllWaitersReturnedHome()
        {
            var waiters = GetGuideStaffVisuals(GuideWaiterVisualKey);
            for (var index = 0; index < waiters.Length; index++)
            {
                var waiter = waiters[index];
                if (waiter == null || busyWaiters.Contains(waiter) || waiterTaskRoutines.ContainsKey(waiter))
                {
                    continue;
                }

                if (!TryGetWaiterHomePose(index, out var homePosition, out var homeRotation, out var homeScale))
                {
                    continue;
                }

                if (Vector3.Distance(waiter.transform.position, homePosition) <= 0.1f)
                {
                    waiter.transform.rotation = homeRotation;
                    waiter.transform.localScale = ResolveGuideStaffVisualScale(GuideWaiterVisualKey, homeScale);
                    SetAnimatorSpeed(waiter.GetComponentInChildren<Animator>(true), 0f);
                    continue;
                }

                StartWaiterTask(waiter, ReturnWaiterHomeThenIdle(waiter));
            }
        }

        /// <summary>
        /// 回到原点后清空小二任务引用，供服务循环继续派发新任务。
        /// </summary>
        /// <param name="waiter">小二表现对象。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator ReturnWaiterHomeThenIdle(GameObject waiter)
        {
            yield return ReturnWaiterHome(waiter);
            FinishWaiterTask(waiter);
        }

        /// <summary>
        /// 把小二移动到桌位附近可通行的服务点。
        /// </summary>
        /// <param name="waiter">小二表现对象。</param>
        /// <param name="table">目标桌位。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator MoveWaiterToTable(GameObject waiter, TableArea table)
        {
            var targetPosition = ResolveTableServicePosition(table, waiter.transform.position);
            yield return MoveCharacterAlongNavMesh(waiter.transform, targetPosition, WaiterMoveSpeed, true);
            yield return RotateCharacterToFace(waiter.transform, table.transform.position);
        }

        /// <summary>
        /// 把小二移动到灶台或蒸笼旁边，表现为先到出餐点取菜。
        /// </summary>
        /// <param name="waiter">小二表现对象。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator MoveWaiterToDishPickup(GameObject waiter)
        {
            if (waiter == null)
            {
                yield break;
            }

            var pickupTarget = ResolveDishPickupTarget();
            if (pickupTarget == null)
            {
                yield break;
            }

            var targetPosition = ResolveObjectServicePosition(pickupTarget, waiter.transform.position);
            yield return MoveCharacterAlongNavMesh(waiter.transform, targetPosition, WaiterMoveSpeed, true);
            yield return RotateCharacterToFace(waiter.transform, pickupTarget.transform.position);
        }

        /// <summary>
        /// 执行小二上菜后的数据和表现切换。
        /// </summary>
        /// <param name="tableId">桌位编号。</param>
        /// <param name="table">桌位对象。</param>
        private void ServeTableByWaiter(int tableId, TableArea table, GameObject dishPrefab)
        {
            ReleaseReservedServeDish();
            DataManager.Instance.ChangeAvailableDishes(-1);
            DataManager.Instance.SetTableRuntimeState(tableId, TavernTableRuntimeState.Dining);
            table.RefreshRuntimeState(TavernTableRuntimeState.Dining);
            table.linkedUI?.StartStateCountdown(TavernTableRuntimeState.Dining, dishEatDuration, "用餐中");
            table.ShowDishVisual(dishPrefab);
            if (TryGetTableCustomerGroup(tableId, out var diningCustomers))
            {
                for (var index = 0; index < diningCustomers.Count; index++)
                {
                    if (diningCustomers[index] != null)
                    {
                        diningCustomers[index].BeginDining(dishEatDuration);
                    }
                }
            }

            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
            Signals.Get<TavernDishServedSignal>().Set(tableId).Dispatch();
        }

        /// <summary>
        /// 在桌面播放清扫烟雾特效。
        /// </summary>
        /// <param name="table">正在清扫的桌位。</param>
        private GameObject PlayCleanSmokeEffect(int tableId, TableArea table)
        {
            if (table == null)
            {
                return null;
            }

            StopCleanSmokeEffect(tableId, null);

            var prefab = LoadCleanSmokeEffectPrefab();
            if (prefab == null)
            {
                return null;
            }

            var effect = Instantiate(prefab, table.GetTableEffectPosition(), Quaternion.identity);
            if (effect == null)
            {
                return null;
            }

            effect.name = "Effect_Smoke_CleanRuntime";
            effect.transform.localScale = Vector3.one * CleanSmokeScale;
            effect.SetActive(true);
            foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.loop = true;
                particle.gameObject.SetActive(true);
                particle.Clear(true);
                particle.Play(true);
            }

            activeCleanSmokeEffects[tableId] = effect;
            return effect;
        }

        /// <summary>
        /// 停止桌面清扫烟雾循环并延迟销毁。
        /// </summary>
        /// <param name="effect">烟雾特效实例。</param>
        private void StopCleanSmokeEffect(int tableId, GameObject effect)
        {
            if (effect == null && activeCleanSmokeEffects.TryGetValue(tableId, out var activeEffect))
            {
                effect = activeEffect;
            }

            if (effect == null)
            {
                return;
            }

            if (activeCleanSmokeEffects.TryGetValue(tableId, out var trackedEffect) && trackedEffect == effect)
            {
                activeCleanSmokeEffects.Remove(tableId);
            }

            foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Destroy(effect, 1.2f);
        }

        /// <summary>
        /// 加载并缓存桌面清扫烟雾特效。
        /// </summary>
        /// <returns>烟雾特效预制体。</returns>
        private static GameObject LoadCleanSmokeEffectPrefab()
        {
            if (cleanSmokeEffectPrefab != null)
            {
                return cleanSmokeEffectPrefab;
            }

            cleanSmokeEffectPrefab = GameplayResourceStore.LoadAsset<GameObject>(CleanSmokeEffectPath);
            return cleanSmokeEffectPrefab;
        }

        /// <summary>
        /// 获取可执行服务动作的小二表现，不存在时按招聘配置创建。
        /// </summary>
        /// <returns>小二表现对象。</returns>
        private GameObject GetAvailableServiceWaiterVisual()
        {
            var waiters = GetGuideStaffVisuals(GuideWaiterVisualKey);
            for (var index = 0; index < waiters.Length; index++)
            {
                var waiter = waiters[index];
                if (waiter == null || busyWaiters.Contains(waiter))
                {
                    continue;
                }

                EnsureWaiterAnimationReceiver(waiter);
                return waiter;
            }

            var hasHomePose = TryGetWaiterHomePose(0, out var homePosition, out var homeRotation, out var homeScale);
            var waiterVisual = GetOrCreateGuideStaffVisual(GuideWaiterVisualKey, StaffRole.Waiter, PreferredWaiterStaffId);
            if (waiterVisual == null)
            {
                return null;
            }

            EnsureWaiterAnimationReceiver(waiterVisual);
            if (hasHomePose)
            {
                waiterVisual.transform.position = homePosition;
                waiterVisual.transform.rotation = homeRotation;
                waiterVisual.transform.localScale = ResolveGuideStaffVisualScale(GuideWaiterVisualKey, homeScale);
            }

            return waiterVisual;
        }

        /// <summary>
        /// 读取已经存在的小二表现。
        /// </summary>
        /// <returns>小二表现对象。</returns>
        private GameObject GetExistingServiceWaiterVisual()
        {
            if (guideStaffVisuals.TryGetValue(GuideWaiterVisualKey, out var waiter) && waiter != null)
            {
                EnsureWaiterAnimationReceiver(waiter);
                return waiter;
            }

            waiter = GameObject.Find($"{GuideWaiterVisualKey}_GuideVisual");
            EnsureWaiterAnimationReceiver(waiter);
            return waiter;
        }

        /// <summary>
        /// 给小二动画器补充动画事件接收器，防止清扫动画事件无人接收。
        /// </summary>
        /// <param name="waiter">小二表现对象。</param>
        private static void EnsureWaiterAnimationReceiver(GameObject waiter)
        {
            if (waiter == null)
            {
                return;
            }

            var animator = waiter.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.GetComponent<WaiterAnimationEventReceiver>() != null)
            {
                return;
            }

            animator.gameObject.AddComponent<WaiterAnimationEventReceiver>();
        }

        /// <summary>
        /// 获取小二没有任务时返回的场景标记位。
        /// </summary>
        /// <returns>场景标记位。</returns>
        private static bool TryGetWaiterHomePose(int index, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            var baseMarker = FindSceneTransformByName(GuideWaiterMarkerName) ?? FindSceneTransformByName("WaiterF1_1");
            if (baseMarker == null)
            {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                scale = Vector3.one;
                return false;
            }

            var stackOffset = GetGuideStaffStackOffset(GuideWaiterVisualKey, index);
            position = baseMarker.position + baseMarker.right * stackOffset.x + baseMarker.up * stackOffset.y + baseMarker.forward * stackOffset.z;
            rotation = baseMarker.rotation;
            scale = baseMarker.lossyScale;
            return true;
        }

        /// <summary>
        /// 让小二回到招聘后站立的原点。
        /// </summary>
        /// <param name="waiter">小二表现对象。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator ReturnWaiterHome(GameObject waiter)
        {
            var waiters = GetGuideStaffVisuals(GuideWaiterVisualKey);
            var waiterIndex = System.Array.IndexOf(waiters, waiter);
            if (waiter == null || !TryGetWaiterHomePose(Mathf.Max(0, waiterIndex), out var homePosition, out var homeRotation, out var homeScale))
            {
                yield break;
            }

            yield return MoveCharacterAlongNavMesh(waiter.transform, homePosition, WaiterMoveSpeed, true);
            waiter.transform.rotation = homeRotation;
            waiter.transform.localScale = ResolveGuideStaffVisualScale(GuideWaiterVisualKey, homeScale);
            SetAnimatorSpeed(waiter.GetComponentInChildren<Animator>(true), 0f);
        }

        /// <summary>
        /// 记录某个小二已经开始执行独立任务，可选地登记任务关联的桌位编号，避免被其他循环重复派发。
        /// </summary>
        /// <param name="waiter">小二对象。</param>
        /// <param name="routine">任务协程。</param>
        /// <param name="serveTableId">本次任务关联的上菜桌位编号；为空表示不属于上菜任务。</param>
        /// <param name="cleanTableId">本次任务关联的清扫桌位编号；为空表示不属于清扫任务。</param>
        private void StartWaiterTask(GameObject waiter, IEnumerator routine, int? serveTableId = null, int? cleanTableId = null)
        {
            if (waiter == null || routine == null)
            {
                return;
            }

            // 终止前一个未结束的协程，避免对同一个小二同时跑多个任务
            if (waiterTaskRoutines.TryGetValue(waiter, out var existingRoutine) && existingRoutine != null)
            {
                StopCoroutine(existingRoutine);
            }

            // 先释放旧派发，再写入本次任务的桌位映射，确保派发记录与协程是同一事务
            ReleaseWaiterAssignments(waiter);
            if (serveTableId.HasValue)
            {
                assignedServeTableIds.Add(serveTableId.Value);
                waiterServeAssignments[waiter] = serveTableId.Value;
            }

            if (cleanTableId.HasValue)
            {
                assignedCleanTableIds.Add(cleanTableId.Value);
                waiterCleanAssignments[waiter] = cleanTableId.Value;
            }

            busyWaiters.Add(waiter);
            waiterTaskRoutines[waiter] = StartCoroutine(routine);
        }

        /// <summary>
        /// 清理某个小二的任务占用状态。
        /// </summary>
        /// <param name="waiter">小二对象。</param>
        private void FinishWaiterTask(GameObject waiter)
        {
            if (waiter == null)
            {
                return;
            }

            ReleaseWaiterAssignments(waiter);
            waiterTaskRoutines.Remove(waiter);
            busyWaiters.Remove(waiter);
        }

        /// <summary>
        /// 释放小二关联的桌位派发记录，让下一个调度循环重新选择目标。
        /// </summary>
        /// <param name="waiter">小二对象。</param>
        private void ReleaseWaiterAssignments(GameObject waiter)
        {
            if (waiter == null)
            {
                return;
            }

            if (waiterServeAssignments.TryGetValue(waiter, out var serveTableId))
            {
                assignedServeTableIds.Remove(serveTableId);
                waiterServeAssignments.Remove(waiter);
            }

            if (waiterCleanAssignments.TryGetValue(waiter, out var cleanTableId))
            {
                assignedCleanTableIds.Remove(cleanTableId);
                waiterCleanAssignments.Remove(waiter);
            }
        }

        /// <summary>
        /// 全量清空小二任务队列与派发缓存，通常在打烊或场景重置时调用。
        /// </summary>
        private void ResetWaiterTaskState()
        {
            foreach (var pair in waiterTaskRoutines)
            {
                if (pair.Value != null)
                {
                    StopCoroutine(pair.Value);
                }
            }

            waiterTaskRoutines.Clear();
            busyWaiters.Clear();
            assignedServeTableIds.Clear();
            assignedCleanTableIds.Clear();
            waiterServeAssignments.Clear();
            waiterCleanAssignments.Clear();
            reservedServeDishCount = 0;

            foreach (var effect in activeCleanSmokeEffects.Values)
            {
                if (effect != null)
                {
                    Destroy(effect);
                }
            }

            activeCleanSmokeEffects.Clear();
        }

        /// <summary>
        /// 当前是否还有未被预占的成品菜可以派给小二。
        /// </summary>
        private bool HasAvailablePreparedDishForServe()
        {
            var freeDishCount = Mathf.Max(0, DataManager.Instance.TavernData.availableDishes - reservedServeDishCount);
            return freeDishCount > 0 && GetPreparedDishQueueCount() > reservedServeDishCount;
        }

        /// <summary>
        /// 释放一份已预占但尚未真正上桌的菜品名额。
        /// </summary>
        private void ReleaseReservedServeDish()
        {
            reservedServeDishCount = Mathf.Max(0, reservedServeDishCount - 1);
        }

        /// <summary>
        /// 在桌位周围选择离小二最近且可寻路的服务点。
        /// </summary>
        /// <param name="table">桌位对象。</param>
        /// <param name="fromPosition">小二当前坐标。</param>
        /// <returns>可寻路服务点。</returns>
        private static Vector3 ResolveTableServicePosition(TableArea table, Vector3 fromPosition)
        {
            if (table == null)
            {
                return fromPosition;
            }

            var center = table.transform.position;
            TryGetNavMeshPosition(fromPosition, out fromPosition);
            var directions = new[]
            {
                table.transform.forward,
                -table.transform.forward,
                table.transform.right,
                -table.transform.right,
                (fromPosition - center).normalized
            };

            var bestPosition = center;
            var bestDistance = float.MaxValue;
            for (var index = 0; index < directions.Length; index++)
            {
                var direction = directions[index];
                if (direction.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                var candidate = center + direction.normalized * 0.75f;
                if (!TryGetNavMeshPosition(candidate, out var navMeshPosition))
                {
                    continue;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(fromPosition, navMeshPosition, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                var distance = Vector3.Distance(fromPosition, navMeshPosition);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestPosition = navMeshPosition;
            }

            return TryGetNavMeshPosition(bestPosition, out var fallbackPosition) ? fallbackPosition : bestPosition;
        }

        /// <summary>
        /// 获取上菜取菜点，优先使用蒸笼，其次使用灶台。
        /// </summary>
        /// <returns>取菜目标对象。</returns>
        private GameObject ResolveDishPickupTarget()
        {
            if (foodTableObject != null && foodTableObject.activeInHierarchy)
            {
                return foodTableObject;
            }

            if (guideSteamerObject != null && guideSteamerObject.activeInHierarchy)
            {
                return guideSteamerObject;
            }

            if (guideStoveObject != null && guideStoveObject.activeInHierarchy)
            {
                return guideStoveObject;
            }

            return FindSceneGameObjectByName("Steamer_1")
                   ?? FindSceneGameObjectByName("Steamer")
                   ?? FindSceneGameObjectByName("BigStove")
                   ?? FindSceneGameObjectByName("灶台");
        }

        /// <summary>
        /// 在目标物体周围选择离小二最近且可寻路的交互点。
        /// </summary>
        /// <param name="targetObject">目标物体。</param>
        /// <param name="fromPosition">小二当前坐标。</param>
        /// <returns>可寻路交互点。</returns>
        private static Vector3 ResolveObjectServicePosition(GameObject targetObject, Vector3 fromPosition)
        {
            if (targetObject == null)
            {
                return fromPosition;
            }

            TryGetNavMeshPosition(fromPosition, out fromPosition);
            var center = ResolveObjectCenter(targetObject);
            var transform = targetObject.transform;
            var directions = new[]
            {
                transform.forward,
                -transform.forward,
                transform.right,
                -transform.right,
                (fromPosition - center).normalized
            };

            var bestPosition = center;
            var bestDistance = float.MaxValue;
            for (var index = 0; index < directions.Length; index++)
            {
                var direction = directions[index];
                if (direction.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                var candidate = center + direction.normalized * 0.85f;
                if (!TryGetNavMeshPosition(candidate, out var navMeshPosition))
                {
                    continue;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(fromPosition, navMeshPosition, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                var distance = Vector3.Distance(fromPosition, navMeshPosition);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                bestPosition = navMeshPosition;
            }

            return TryGetNavMeshPosition(bestPosition, out var fallbackPosition) ? fallbackPosition : bestPosition;
        }

        /// <summary>
        /// 根据渲染包围盒获取物体中心，缺少渲染器时使用根节点坐标。
        /// </summary>
        /// <param name="targetObject">目标物体。</param>
        /// <returns>物体中心坐标。</returns>
        private static Vector3 ResolveObjectCenter(GameObject targetObject)
        {
            var renderers = targetObject.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return targetObject.transform.position;
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds.center;
        }

        /// <summary>
        /// 沿导航网格路径移动角色，并同步速度参数驱动行走动画。
        /// 加入卡住检测与整体超时，避免角色在拐点附近原地空转。
        /// </summary>
        /// <param name="target">需要移动的角色根节点。</param>
        /// <param name="destination">目标坐标。</param>
        /// <param name="speed">移动速度。</param>
        /// <param name="snapToNavMesh">是否把起点和终点吸附到导航网格。</param>
        /// <returns>协程迭代器。</returns>
        private static IEnumerator MoveCharacterAlongNavMesh(Transform target, Vector3 destination, float speed, bool snapToNavMesh)
        {
            if (target == null)
            {
                yield break;
            }

            var animator = target.GetComponentInChildren<Animator>(true);
            PrepareAnimatorForMovement(animator);
            var start = target.position;
            if (snapToNavMesh)
            {
                TryGetNavMeshPosition(start, out start);
                TryGetNavMeshPosition(destination, out destination);
                target.position = start;
            }

            var corners = BuildMovementCorners(start, destination);
            SetAnimatorSpeed(animator, WalkAnimationSpeed);
            var totalElapsed = 0f;
            for (var cornerIndex = 0; cornerIndex < corners.Count; cornerIndex++)
            {
                var corner = corners[cornerIndex];
                var stuckSamplePosition = target.position;
                var stuckSampleTime = 0f;
                while (Vector3.Distance(target.position, corner) > WaiterReachDistance)
                {
                    if (target == null)
                    {
                        yield break;
                    }

                    if (totalElapsed > WaiterMoveTotalTimeout)
                    {
                        target.position = destination;
                        SetAnimatorSpeed(animator, 0f);
                        yield break;
                    }

                    var nextPosition = Vector3.MoveTowards(target.position, corner, speed * Time.deltaTime);
                    var direction = ResolveMovementLookDirection(target.position, corners, cornerIndex, corner);
                    if (direction.sqrMagnitude > 0.0001f)
                    {
                        var lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                        target.rotation = Quaternion.RotateTowards(target.rotation, lookRotation, WaiterTurnSpeed * Time.deltaTime);
                    }

                    target.position = nextPosition;

                    var deltaTime = Time.deltaTime;
                    totalElapsed += deltaTime;
                    stuckSampleTime += deltaTime;
                    if (stuckSampleTime >= WaiterMoveStuckCheckInterval)
                    {
                        if (Vector3.Distance(stuckSamplePosition, target.position) < WaiterMoveStuckProgressThreshold)
                        {
                            // 视为卡死，直接吸附到当前拐点继续后续路径，避免无限循环
                            target.position = corner;
                            break;
                        }

                        stuckSampleTime = 0f;
                        stuckSamplePosition = target.position;
                    }

                    yield return null;
                }
            }

            target.position = destination;
            SetAnimatorSpeed(animator, 0f);
        }

        /// <summary>
        /// 根据当前路径拐点计算移动朝向，提前看向下一段路径避免到拐角处突然转身。
        /// </summary>
        /// <param name="currentPosition">当前坐标。</param>
        /// <param name="corners">导航路径拐点。</param>
        /// <param name="cornerIndex">当前拐点索引。</param>
        /// <param name="currentCorner">当前正在靠近的拐点。</param>
        /// <returns>水平移动朝向。</returns>
        private static Vector3 ResolveMovementLookDirection(Vector3 currentPosition, System.Collections.Generic.List<Vector3> corners, int cornerIndex, Vector3 currentCorner)
        {
            var lookTarget = currentCorner;
            if (corners != null
                && cornerIndex + 1 < corners.Count
                && Vector3.Distance(currentPosition, currentCorner) <= WaiterLookAheadDistance)
            {
                lookTarget = corners[cornerIndex + 1];
            }

            var direction = lookTarget - currentPosition;
            direction.y = 0f;
            return direction;
        }

        /// <summary>
        /// 根据导航网格路径生成移动拐点，寻路失败时退回直线路径。
        /// </summary>
        /// <param name="start">起点坐标。</param>
        /// <param name="destination">终点坐标。</param>
        /// <returns>路径拐点列表。</returns>
        private static System.Collections.Generic.List<Vector3> BuildMovementCorners(Vector3 start, Vector3 destination)
        {
            var corners = new System.Collections.Generic.List<Vector3>();
            var path = new NavMeshPath();
            if (NavMesh.CalculatePath(start, destination, NavMesh.AllAreas, path) && path.corners != null && path.corners.Length > 0)
            {
                for (var index = 1; index < path.corners.Length; index++)
                {
                    corners.Add(path.corners[index]);
                }
            }

            if (corners.Count == 0)
            {
                corners.Add(destination);
            }

            return corners;
        }

        /// <summary>
        /// 让角色只在水平面上朝向目标点。
        /// </summary>
        /// <param name="target">需要旋转的角色。</param>
        /// <param name="lookAtPosition">朝向目标坐标。</param>
        private static void FaceTargetOnGround(Transform target, Vector3 lookAtPosition)
        {
            if (target == null)
            {
                return;
            }

            var direction = lookAtPosition - target.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                target.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }

        /// <summary>
        /// 让角色平滑转向目标点，避免服务动作开始前瞬间大幅度扭头。
        /// </summary>
        /// <param name="target">需要旋转的角色。</param>
        /// <param name="lookAtPosition">朝向目标坐标。</param>
        /// <returns>协程迭代器。</returns>
        private static IEnumerator RotateCharacterToFace(Transform target, Vector3 lookAtPosition)
        {
            if (target == null)
            {
                yield break;
            }

            var direction = lookAtPosition - target.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                yield break;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            var timeout = 0.35f;
            while (timeout > 0f && Quaternion.Angle(target.rotation, targetRotation) > 1f)
            {
                target.rotation = Quaternion.RotateTowards(target.rotation, targetRotation, WaiterTurnSpeed * Time.deltaTime);
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>
        /// 触发厨师做菜动作。
        /// </summary>
        private void PlayChefCookAnimation(GameObject[] chefs)
        {
            if (chefs == null || chefs.Length == 0)
            {
                return;
            }

            for (var index = 0; index < chefs.Length; index++)
            {
                var chef = chefs[index];
                if (chef != null)
                {
                    // 招聘入场中的厨师仅播放走路，不参与做菜触发，避免出现边走边做菜。
                    if (staffVisualsBeingAnimated.Contains(chef))
                    {
                        continue;
                    }

                    PlayChefCookAnimationInternal(chef.GetComponentInChildren<Animator>(true));
                }
            }
        }

        /// <summary>
        /// 触发单个厨师的做菜动画。
        /// 先尝试走触发器，若控制器没有及时切换，再兜底切到 Cook 状态。
        /// </summary>
        /// <param name="animator">厨师动画器。</param>
        private static void PlayChefCookAnimationInternal(Animator animator)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            SetAnimatorSpeed(animator, 0f);
            if (IsAnimatorInCookState(animator))
            {
                return;
            }

            if (HasAnimatorCookState(animator))
            {
                CrossFadeStateIfAvailable(animator, ChefBaseLayerCookState, ChefCookState);
                return;
            }

            if (HasAnimatorParameter(animator, ChefCookTrigger, AnimatorControllerParameterType.Trigger))
            {
                animator.ResetTrigger(ChefCookTrigger);
                animator.SetTrigger(ChefCookTrigger);
            }
        }

        /// <summary>
        /// 把当前参与做菜的厨师切回普通站立/移动状态，避免做完后残留在 Cook pose。
        /// </summary>
        /// <param name="chefs">本轮参与做菜的厨师列表。</param>
        private void ResetChefCookAnimations(GameObject[] chefs)
        {
            if (chefs == null || chefs.Length == 0)
            {
                return;
            }

            for (var index = 0; index < chefs.Length; index++)
            {
                var chef = chefs[index];
                if (chef == null)
                {
                    continue;
                }

                ResetChefCookAnimationInternal(chef.GetComponentInChildren<Animator>(true));
            }
        }

        /// <summary>
        /// 结束单个厨师的做菜状态，恢复到正常待机或移动状态。
        /// </summary>
        /// <param name="animator">厨师动画器。</param>
        private static void ResetChefCookAnimationInternal(Animator animator)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            SetAnimatorSpeed(animator, 0f);
            if (HasAnimatorParameter(animator, ChefCookTrigger, AnimatorControllerParameterType.Trigger))
            {
                animator.ResetTrigger(ChefCookTrigger);
            }

            CrossFadeMovementStateIfAvailable(animator);
        }

        /// <summary>
        /// 判断动画器当前是否已经在 Cook 状态，避免每次轮询都重复打断动作。
        /// </summary>
        /// <param name="animator">厨师动画器。</param>
        /// <returns>当前已经在做菜状态时返回 true。</returns>
        private static bool IsAnimatorInCookState(Animator animator)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return false;
            }

            var currentState = animator.GetCurrentAnimatorStateInfo(0);
            return currentState.IsName(ChefBaseLayerCookState) || currentState.IsName(ChefCookState);
        }

        /// <summary>
        /// 判断控制器里是否存在 Cook 状态。
        /// </summary>
        /// <param name="animator">动画器。</param>
        /// <returns>存在 Cook 状态时返回 true。</returns>
        private static bool HasAnimatorCookState(Animator animator)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return false;
            }

            return animator.HasState(0, Animator.StringToHash(ChefBaseLayerCookState))
                   || animator.HasState(0, Animator.StringToHash(ChefCookState));
        }

        /// <summary>
        /// 根据动画器参数安全触发指定 Trigger。
        /// </summary>
        /// <param name="animator">动画器。</param>
        /// <param name="triggerName">Trigger 参数名。</param>
        private static void TriggerAnimator(Animator animator, string triggerName)
        {
            if (animator == null || string.IsNullOrEmpty(triggerName))
            {
                return;
            }

            foreach (var parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
                {
                    animator.SetTrigger(triggerName);
                    return;
                }
            }
        }

        /// <summary>
        /// 把角色从服务或入座状态切回移动准备状态，避免上一段动作残留到下一段路。
        /// </summary>
        /// <param name="animator">角色动画器。</param>
        private static void PrepareAnimatorForMovement(Animator animator)
        {
            if (animator == null)
            {
                return;
            }

            SetAnimatorSpeed(animator, 0f);
            if (HasAnimatorParameter(animator, WaiterCleanTrigger, AnimatorControllerParameterType.Trigger))
            {
                animator.ResetTrigger(WaiterCleanTrigger);
            }

            if (HasAnimatorParameter(animator, AnimatorIsSittingParam, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(AnimatorIsSittingParam, false);
            }

            if (HasAnimatorParameter(animator, AnimatorIsEatingParam, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(AnimatorIsEatingParam, false);
            }

            CrossFadeMovementStateIfAvailable(animator);
        }

        /// <summary>
        /// 把小二从清扫状态切回待机状态，避免清扫动作残留到下一段路。
        /// </summary>
        /// <param name="animator">小二动画器。</param>
        private static void ResetWaiterServiceAnimation(Animator animator)
        {
            if (animator == null)
            {
                return;
            }

            SetAnimatorSpeed(animator, 0f);
            if (HasAnimatorParameter(animator, WaiterCleanTrigger, AnimatorControllerParameterType.Trigger))
            {
                animator.ResetTrigger(WaiterCleanTrigger);
            }

            CrossFadeMovementStateIfAvailable(animator);
        }

        /// <summary>
        /// 仅在控制器确实包含移动状态时切回 Movement，避免不同 NPC 控制器被硬切到不存在的状态。
        /// </summary>
        /// <param name="animator">角色动画器。</param>
        private static void CrossFadeMovementStateIfAvailable(Animator animator)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            CrossFadeStateIfAvailable(animator, AnimatorBaseLayerMovementState, AnimatorMovementState);
        }

        /// <summary>
        /// 仅在控制器确实存在目标状态时执行 CrossFade。
        /// </summary>
        /// <param name="animator">角色动画器。</param>
        /// <param name="fullPathStateName">完整状态名。</param>
        /// <param name="shortStateName">短状态名。</param>
        private static void CrossFadeStateIfAvailable(Animator animator, string fullPathStateName, string shortStateName)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            var fullPathHash = Animator.StringToHash(fullPathStateName);
            var shortNameHash = Animator.StringToHash(shortStateName);
            if (animator.HasState(0, fullPathHash))
            {
                animator.CrossFade(fullPathHash, 0.12f, 0);
                return;
            }

            if (animator.HasState(0, shortNameHash))
            {
                animator.CrossFade(shortNameHash, 0.12f, 0);
            }
        }

        /// <summary>
        /// 根据动画器参数安全设置移动速度。
        /// </summary>
        /// <param name="animator">动画器。</param>
        /// <param name="speed">速度值。</param>
        private static void SetAnimatorSpeed(Animator animator, float speed)
        {
            if (animator == null)
            {
                return;
            }

            foreach (var parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float && parameter.name == WaiterSpeedParam)
                {
                    animator.SetFloat(WaiterSpeedParam, speed);
                    return;
                }
            }
        }

        #endregion
    }
}
