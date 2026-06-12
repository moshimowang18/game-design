using JN.Client.Model;
using QFramework;
using UnityEngine;

namespace JN.Client.Manager
{
    /// <summary>
    /// 酒楼日循环总控：准备 → 营业 → 结算。
    /// </summary>
    [MonoSingletonPath("[Manager]/TavernDayManager")]
    public class TavernDayManager : MonoSingleton<TavernDayManager>
    {
        private GameDayData currentDay;
        private DayPhase phase = DayPhase.Preparation;

        public GameDayData CurrentDay => currentDay;
        public DayPhase Phase => phase;

        /// <summary>
        /// 初始化模块依赖和默认状态。
        /// </summary>
        public void Init()
        {
            EventSystemManager.Instance.Initialize();

            var dataManager = DataManager.Instance;
            dataManager.Init();

            currentDay = dataManager.SaveData?.gameDay ?? new GameDayData();
            if (dataManager.SaveData != null)
            {
                dataManager.SaveData.gameDay = currentDay;
            }

            phase = currentDay.CurrentPhase;
            if (dataManager.PlayerData != null && dataManager.PlayerData.CurrentDay > 0)
            {
                currentDay.DayNumber = dataManager.PlayerData.CurrentDay;
            }
        }

        /// <summary>
        /// 开始新的一天，应用当日事件与客流修正。
        /// </summary>
        public void StartNewDay(int dayNumber)
        {
            EnsureInitialized();

            currentDay.DayNumber = Mathf.Max(1, dayNumber);
            currentDay.CurrentPhase = DayPhase.Preparation;
            currentDay.OperationTimeLimit = 120f;

            var eventId = EventSystemManager.Instance.GetTodaysEventId(currentDay.DayNumber);
            var dailyEvent = EventSystemManager.Instance.GetEventById(eventId);
            EventSystemManager.Instance.ApplyEventEffects(dailyEvent, currentDay);

            var lastResult = DataManager.Instance.SaveData?.lastOperationResult;
            if (lastResult != null)
            {
                currentDay.GuestFlowMultiplier = lastResult.StarRating switch
                {
                    5 => 1.3f,
                    4 => 1.1f,
                    3 => 1.0f,
                    2 => 0.8f,
                    _ => 0.6f
                };
                currentDay.VipProbabilityBonus = lastResult.StarRating switch
                {
                    5 => 0.5f,
                    4 => 0.2f,
                    3 => 0f,
                    2 => -0.1f,
                    _ => -0.3f
                };
            }
            else
            {
                currentDay.GuestFlowMultiplier = 1f;
                currentDay.VipProbabilityBonus = 0f;
            }

            phase = DayPhase.Preparation;

            if (DataManager.Instance.PlayerData != null)
            {
                DataManager.Instance.PlayerData.SelectedDishes.Clear();
            }

            SyncToSaveData();
            DataManager.Instance.SaveGame();
        }

        /// <summary>
        /// 进入准备阶段。
        /// </summary>
        public void EnterPreparationPhase()
        {
            EnsureInitialized();
            phase = DayPhase.Preparation;
            currentDay.CurrentPhase = DayPhase.Preparation;
            SyncToSaveData();
            DataManager.Instance.SaveGame();
        }

        /// <summary>
        /// 进入营业阶段。
        /// </summary>
        public void EnterOperationPhase()
        {
            EnsureInitialized();
            phase = DayPhase.Operation;
            currentDay.CurrentPhase = DayPhase.Operation;
            OperationManager.Instance.StartOperation();
            SyncToSaveData();
            DataManager.Instance.SaveGame();
        }

        /// <summary>
        /// 进入结算阶段并记录营业结果。
        /// </summary>
        public void EnterSettlementPhase(OperationResult result)
        {
            EnsureInitialized();
            phase = DayPhase.Settlement;
            currentDay.CurrentPhase = DayPhase.Settlement;

            if (result == null)
            {
                var opMgr = OperationManager.Instance;
                var activeEvent = EventSystemManager.Instance.GetEventById(currentDay.EventId);
                result = ScoreCalculator.Calculate(
                    opMgr.TotalCustomers,
                    opMgr.SatisfiedCustomers,
                    opMgr.CurrentRevenue,
                    opMgr.NegativeEventCount,
                    DataManager.Instance.PlayerData?.TavernLevel * 0.1f ?? 0f,
                    activeEvent);
            }

            if (DataManager.Instance.SaveData != null)
            {
                DataManager.Instance.SaveData.lastOperationResult = result;
                DataManager.Instance.SaveData.gameplay.lastOperationResult = result;
            }

            if (result != null && DataManager.Instance.PlayerData != null)
            {
                DataManager.Instance.PlayerData.coinNum += Mathf.RoundToInt(result.TotalRevenue);
            }

            SyncToSaveData();
            DataManager.Instance.SaveGame();
        }

        private void EnsureInitialized()
        {
            if (currentDay == null)
            {
                Init();
            }
        }

        private void SyncToSaveData()
        {
            var saveData = DataManager.Instance.SaveData;
            if (saveData == null)
            {
                return;
            }

            saveData.gameDay = currentDay;
            saveData.gameplay.gameDay = currentDay;

            if (saveData.player != null)
            {
                saveData.player.CurrentDay = currentDay.DayNumber;
            }
        }
    }
}
