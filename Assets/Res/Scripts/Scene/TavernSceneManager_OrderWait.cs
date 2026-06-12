using System.Collections;
using System.Collections.Generic;
using JN.Client.Manager;
using JN.Client.Model;
using QFramework;
using UnityEngine;

namespace JN.Client.Scene
{
    public partial class TavernSceneManager
    {
        private const float CustomerOrderWaitDuration = 5f;

        private readonly Dictionary<int, Coroutine> tableOrderWaitRoutines = new();

        /// <summary>
        /// 桌位进入待点单后开始 5 秒耐心倒计时。
        /// </summary>
        private void StartTableOrderWait(int tableId)
        {
            StopTableOrderWait(tableId, false);
            tableOrderWaitRoutines[tableId] = StartCoroutine(TableOrderWaitRoutine(tableId));
        }

        /// <summary>
        /// 停止桌位点单等待计时，并在需要时隐藏顾客头顶进度条。
        /// </summary>
        private void StopTableOrderWait(int tableId, bool hideCustomerProgress = true)
        {
            if (tableOrderWaitRoutines.TryGetValue(tableId, out var routine))
            {
                if (routine != null)
                {
                    StopCoroutine(routine);
                }

                tableOrderWaitRoutines.Remove(tableId);
            }

            if (!hideCustomerProgress || !TryGetTableCustomerGroup(tableId, out var customers))
            {
                return;
            }

            for (var index = 0; index < customers.Count; index++)
            {
                customers[index]?.StopOrderWait();
            }
        }

        /// <summary>
        /// 停止全部桌位的点单等待计时。
        /// </summary>
        private void StopAllTableOrderWaits()
        {
            var tableIds = new List<int>(tableOrderWaitRoutines.Keys);
            for (var index = 0; index < tableIds.Count; index++)
            {
                StopTableOrderWait(tableIds[index]);
            }
        }

        /// <summary>
        /// 桌位点单等待超时后，让顾客生气并离店。
        /// </summary>
        private IEnumerator TableOrderWaitRoutine(int tableId)
        {
            if (!TryGetTableCustomerGroup(tableId, out var customers) || customers.Count == 0)
            {
                tableOrderWaitRoutines.Remove(tableId);
                yield break;
            }

            for (var index = 0; index < customers.Count; index++)
            {
                customers[index]?.StartOrderWait(CustomerOrderWaitDuration);
            }

            yield return new WaitForSeconds(CustomerOrderWaitDuration);

            tableOrderWaitRoutines.Remove(tableId);
            var tableData = DataManager.Instance.GetTableData(tableId);
            if (tableData == null || tableData.runtimeState != (int)TavernTableRuntimeState.WaitingOrder)
            {
                StopTableOrderWait(tableId);
                yield break;
            }

            HandleTableOrderTimeout(tableId);
        }

        /// <summary>
        /// 处理点单超时：展示生气表情后让顾客离开，并释放桌位。
        /// </summary>
        private void HandleTableOrderTimeout(int tableId)
        {
            if (!AllTables.TryGetValue(tableId, out var table))
            {
                return;
            }

            var leavingCustomers = new List<TavernCustomerRuntimeController>();
            if (TryGetTableCustomerGroup(tableId, out var customers))
            {
                for (var index = 0; index < customers.Count; index++)
                {
                    if (customers[index] != null)
                    {
                        leavingCustomers.Add(customers[index]);
                    }
                }
            }

            DataManager.Instance.SetTableRuntimeState(tableId, TavernTableRuntimeState.Idle);
            table.RefreshRuntimeState(TavernTableRuntimeState.Idle);
            table.linkedUI?.StopStateCountdown();
            table.ClearDishVisual();

            for (var index = 0; index < leavingCustomers.Count; index++)
            {
                var customer = leavingCustomers[index];
                if (customer == null)
                {
                    continue;
                }

                customer.PlayAngryLeavePresentation(() =>
                {
                    if (customer != null)
                    {
                        customer.LeaveTavern();
                    }
                });
            }

            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
            // 通知新系统：客人不耐烦离店
            var groupSize = leavingCustomers.Count;
            Signals.Get<TavernCustomerAngryLeaveSignal>().Set(tableId, groupSize).Dispatch();
        }
    }
}
