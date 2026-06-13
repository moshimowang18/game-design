using JN.Client;
using JN.Client.Model;
using QFramework;
using UnityEngine;

namespace JN.Client.Manager
{
    /// <summary>
    /// 老酒楼实时玩法（TavernSceneManager）专用运行时 API。
    /// 日循环系统请使用 TavernDayManager / OperationManager 与 PlayerModel.gameDay 数据。
    /// 以下方法保留给场景内 3D 客人/桌位演出，不与新日循环菜品槽位（SelectedDishes）混用。
    /// </summary>
    public partial class DataManager
    {
        /// <summary>
        /// 尝试处理领取贷款。
        /// </summary>
        /// <param name="loan金额">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool TryTakeLoan(out int loanAmount)
        {
            EnsureInitialized();
            loanAmount = 0;

            if (GetRemainingLoanCount() <= 0)
            {
                return false;
            }

            loanAmount = GetNextLoanAmount();
            GameplayData.openingLoanClaimed = true;
            GameplayData.loanCount += 1;
            GameplayData.pendingLoanAmount = 0;
            GameplayData.waitingForLoanApproval = false;
            ChangeCoinNum(loanAmount);
            SaveGame();
            return true;
        }

        /// <summary>
        /// 修改可用菜品。
        /// </summary>
        /// <param name="delta">参数值。</param>
        public void ChangeAvailableDishes(int delta)
        {
            EnsureTavernDefaults();
            SaveData.tavern.availableDishes = Mathf.Max(0, SaveData.tavern.availableDishes + delta);
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
            SaveGame();

            if (delta < 0)
            {
                var player = PlayerData;
                if (player != null && player.DishStock != null && player.DishStock.Count > 0)
                {
                    string topDishId = null;
                    var topStock = 0;
                    foreach (var kv in player.DishStock)
                    {
                        if (kv.Value > topStock)
                        {
                            topStock = kv.Value;
                            topDishId = kv.Key;
                        }
                    }

                    if (topDishId != null)
                    {
                        player.ConsumeDishStock(topDishId, -delta);
                        Debug.Log($"[Stock] 老厨师做菜，扣 {topDishId} 库存 {-delta} 份，剩余{player.GetDishStock(topDishId)}");
                    }
                }
            }
        }

        /// <summary>
        /// 重置临时酒楼状态。
        /// </summary>
        public void ResetTransientTavernState()
        {
            EnsureTavernDefaults();
            SaveData.tavern.availableDishes = 0;
            foreach (var table in SaveData.tavern.tables)
            {
                table.runtimeState = table.isUnlocked
                    ? (int)TavernTableRuntimeState.Idle
                    : (int)TavernTableRuntimeState.Locked;
            }

            tableNum = GetUnlockedTableCount();
            Signals.Get<TableNumSignal>().Dispatch();
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
            SaveGame();
        }
    }
}
