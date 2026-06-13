using JN.Client.Model;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JN.Client.Manager
{
    /// <summary>
    /// 酒楼日循环总控：准备 → 营业 → 结算。
    /// </summary>
    [MonoSingletonPath("[Manager]/TavernDayManager")]
    public class TavernDayManager : MonoSingleton<TavernDayManager>
    {
        /// <summary>
        /// 场景加载后、各 MonoBehaviour.Start 之前，关闭残留的 3D 营业状态。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CloseStaleTavernOpenOnSceneLoad()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName != "GamePlay_Tavern"
                && sceneName != "Tavern_Gameplay"
                && sceneName != "SCN_Tavern_Gameplay")
            {
                return;
            }

            var dataManager = DataManager.Instance;
            if (!dataManager.IsInitialized)
            {
                dataManager.Init();
            }

            if (dataManager.SaveData?.tavern != null && dataManager.TavernData.isOpen)
            {
                dataManager.SetTavernOpen(false);
            }
        }

        private GameDayData currentDay;
        private DayPhase phase = DayPhase.Preparation;

        public GameDayData CurrentDay => currentDay;
        public DayPhase Phase => phase;

        /// <summary>
        /// 是否允许花钱升级/购买（仅准备阶段允许，避免营业中分心做策略操作）。
        /// 引导流程不受此限制（在调用方判断）。
        /// </summary>
        public bool CanSpendMoney()
        {
            return Phase == DayPhase.Preparation;
        }

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

            // 存档若停在营业阶段，但本次运行尚未 StartOperation，回到准备阶段
            if (phase == DayPhase.Operation && !OperationManager.Instance.IsOperating)
            {
                phase = DayPhase.Preparation;
                currentDay.CurrentPhase = DayPhase.Preparation;
            }
        }

        /// <summary>
        /// 开始新的一天，应用当日事件与客流修正。
        /// </summary>
        public void StartNewDay(int dayNumber)
        {
            EnsureInitialized();

            var player = DataManager.Instance.PlayerData;
            if (player != null)
            {
                var remaining = player.GetTotalDishStock();
                if (remaining > 0)
                {
                    Debug.Log($"[Day] 昨日剩余 {remaining} 份菜品变质丢弃");
                }

                player.ClearDishStock();
            }

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

            EnsureTavernClosedForDayCycle();
            SyncToSaveData();
            DataManager.Instance.SaveGame();
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
        }

        /// <summary>
        /// 进入准备阶段。
        /// </summary>
        public void EnterPreparationPhase()
        {
            EnsureInitialized();
            EnsureTavernClosedForDayCycle();
            phase = DayPhase.Preparation;
            currentDay.CurrentPhase = DayPhase.Preparation;
            SyncToSaveData();
            DataManager.Instance.SaveGame();
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
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
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
        }

        /// <summary>
        /// 进入结算阶段并记录营业结果。
        /// </summary>
        public void EnterSettlementPhase(OperationResult result)
        {
            EnsureInitialized();
            EnsureTavernClosedForDayCycle();
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
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
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

        /// <summary>
        /// 日循环进入准备/结算时关闭老系统 3D 营业，避免 isOpen 残留导致客人自动进场。
        /// </summary>
        private static void EnsureTavernClosedForDayCycle()
        {
            if (DataManager.Instance.SaveData?.tavern == null)
            {
                return;
            }

            if (DataManager.Instance.SaveData.tavern.isOpen)
            {
                DataManager.Instance.SetTavernOpen(false);
            }
        }
    }
}
