using JN.Client;
using JN.Client.Model;
using QFramework;
using UnityEngine;

namespace JN.Client.Manager
{
    /// <summary>
    /// 负责数据相关的运行时逻辑。
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
