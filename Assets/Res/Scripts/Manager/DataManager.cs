using System;
using System.Collections.Generic;
using System.IO;
using JN.Client.Messages;
using JN.Client.Model;
using Newtonsoft.Json;
using QFramework;
using UnityEngine;
namespace JN.Client.Manager
{
    /// <summary>
    /// 负责数据相关的运行时逻辑。
    /// </summary>
    public partial class DataManager : MonoSingleton<DataManager>
    {
        private const int SaveVersion = 1;
        private const int DefaultTableCount = 6;
        private const int MaxLoanCount = 4;
        private const int FirstLoanAmount = 20000;
        private const int NextLoanStepAmount = 0;
        private const int CounterEquipmentId = 0;
        private const int StoveEquipmentId = 3;
        private const int ShopkeeperStaffId = 1;
        private const int ChefStaffId = 4;
        private const int WaiterStaffId = 5;
        private const int TownTileCount = 8;
        private const int SelfPlayerBuildingId = 10;
        private const int TownLandPurchaseCost = 3000;
        private const string CoinLogColor = "#FFA500";

        private static string SavePath => Path.Combine(Application.persistentDataPath, "gamesave.json");
        private static string SaveDirectoryPath => Path.Combine(Application.persistentDataPath, "saves");

        public GameSaveData SaveData { get; private set; }
        public PlayerModel PlayerData { get; private set; }

        public int tableNum { get; private set; }
        public bool IsInitialized { get; private set; }
        public bool HasCreatedPlayer => PlayerData != null && !string.IsNullOrWhiteSpace(PlayerData.playerName?.Trim());

        public LocalGameplaySaveData GameplayData
        {
            get
            {
                EnsureInitialized();
                return SaveData.gameplay;
            }
        }

        public TavernSaveData TavernData
        {
            get
            {
                EnsureInitialized();
                return SaveData.tavern;
            }
        }

        public TownSaveData TownData
        {
            get
            {
                EnsureInitialized();
                return SaveData.town;
            }
        }

        public GameplayGuideSaveData GameplayGuideData
        {
            get
            {
                EnsureGameplayDefaults();
                SyncGameplayGuideProgress();
                return SaveData.gameplay.gameplayGuide;
            }
        }

        /// <summary>
        /// 初始化模块依赖和默认状态。
        /// </summary>
        public void Init()
        {
            EnsureInitialized();
        }

        /// <summary>
        /// 获取当前玩家在城镇玩法中使用的数字编号（本地存档生成，不依赖服务器）。
        /// </summary>
        public int GetLocalPlayerNumericId()
        {
            if (SaveData == null)
            {
                EnsureInitialized();
            }

            if (PlayerData != null && int.TryParse(PlayerData.playerId, out var parsed) && parsed > 0)
            {
                return parsed;
            }

            return SaveData != null ? SaveData.gameplay.localPlayerNumericId : 0;
        }

        /// <summary>
        /// 获取当前玩家剩余可贷款次数。
        /// </summary>
        /// <returns>返回计算后的数值。</returns>
        public int GetRemainingLoanCount()
        {
            EnsureInitialized();
            return Mathf.Max(0, MaxLoanCount - GameplayData.loanCount);
        }

        /// <summary>
        /// 获取下一次贷款金额。
        /// </summary>
        /// <returns>返回计算后的数值。</returns>
        public int GetNextLoanAmount()
        {
            EnsureInitialized();
            return FirstLoanAmount + (GameplayData.loanCount * NextLoanStepAmount);
        }

        /// <summary>
        /// 判断是否已经领取开局贷款。
        /// </summary>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool HasClaimedOpeningLoan()
        {
            EnsureInitialized();
            return GameplayData.openingLoanClaimed;
        }

        /// <summary>
        /// 判断是否需要显示开局贷款窗口。
        /// </summary>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool ShouldShowOpeningLoanWindow()
        {
            EnsureInitialized();
            return !GameplayData.openingLoanClaimed;
        }

        /// <summary>
        /// 修改铜钱数量。
        /// </summary>
        /// <param name="change数量">参数值。</param>
        public void ChangeCoinNum(int changeNum)
        {
            EnsureInitialized();

            var beforeNum = PlayerData.coinNum;
            var afterNum = PlayerData.coinNum + changeNum;
            if (afterNum < 0)
            {
                return;
            }

            PlayerData.coinNum = afterNum;
            Debug.Log($"<color={CoinLogColor}>[Coin Change] Change={changeNum:+#;-#;0} Before={beforeNum} After={afterNum}</color>");
            Signals.Get<UpdateCoinNumSignal>().Dispatch(changeNum);
            SaveGame();
        }

        /// <summary>
        /// 创建新玩家本地数据并写入初始存档。
        /// </summary>
        /// <param name="playerName">名称。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool CreatePlayer(string playerName)
        {
            return LoginOrCreatePlayer(playerName);
        }

        /// <summary>
        /// 加载或创建本地玩家存档。
        /// </summary>
        /// <param name="playerName">名称。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool LoginOrCreatePlayer(string playerName)
        {
            EnsureInitialized();

            var trimmedName = NormalizePlayerName(playerName);
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                return false;
            }

            var namedSavePath = GetNamedSavePath(trimmedName);
            if (File.Exists(namedSavePath) && TryLoadSaveFromPath(namedSavePath, out var existingSave))
            {
                SaveData = existingSave;
            }
            else
            {
                SaveData = CreateDefaultSave();
                SaveData.player.playerId = Guid.NewGuid().ToString("N");
                SaveData.player.createdAtUtcTicks = DateTime.UtcNow.Ticks;
            }

            SaveData.player ??= new PlayerModel();
            SaveData.gameplay ??= new LocalGameplaySaveData();
            SaveData.town ??= new TownSaveData();
            SaveData.tavern ??= new TavernSaveData();

            SaveData.player.playerName = trimmedName;

            if (string.IsNullOrWhiteSpace(SaveData.player.playerId))
            {
                SaveData.player.playerId = Guid.NewGuid().ToString("N");
            }

            if (SaveData.player.createdAtUtcTicks <= 0)
            {
                SaveData.player.createdAtUtcTicks = DateTime.UtcNow.Ticks;
            }

            PlayerData = SaveData.player;
            SaveData.gameplay.localPlayerNumericId = ResolveLocalPlayerNumericId(SaveData.gameplay.localPlayerNumericId);

            EnsureTownBuildingDefaults();
            EnsureTavernDefaults();
            EnsureGameplayDefaults();
            tableNum = GetUnlockedTableCount();
            SaveGame();
            return true;
        }

        /// <summary>
        /// 获取恢复场景名称。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        public string GetResumeSceneName()
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(SaveData.lastSceneName))
            {
                return "Town";
            }

            return SaveData.lastSceneName switch
            {
                "SCN_Town_Main" => "Town",
                "SCN_Tavern_Gameplay" => "GamePlay_Tavern",
                "Tavern_Gameplay" => "GamePlay_Tavern",
                _ => SaveData.lastSceneName
            };
        }

        /// <summary>
        /// 记录上一次场景。
        /// </summary>
        /// <param name="sceneName">名称。</param>
        public void RecordLastScene(string sceneName)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(sceneName) || sceneName == "Start" || sceneName == "SCN_Common_Start")
            {
                return;
            }

            SaveData.lastSceneName = sceneName switch
            {
                "SCN_Town_Main" => "Town",
                "SCN_Tavern_Gameplay" => "GamePlay_Tavern",
                "Tavern_Gameplay" => "GamePlay_Tavern",
                _ => sceneName
            };
            SaveGame();
        }

        /// <summary>
        /// 获取大地图建筑信息列表。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        public List<BuildingInfo> GetTownBuildingInfos()
        {
            EnsureTownBuildingDefaults();
            return SaveData.town.buildingInfos;
        }

        /// <summary>
        /// 处理新增或更新建筑信息相关逻辑。
        /// </summary>
        /// <param name="building信息">参数值。</param>
        public void UpsertBuildingInfo(BuildingInfo buildingInfo)
        {
            if (buildingInfo == null)
            {
                return;
            }

            EnsureTownBuildingDefaults();
            var buildingInfos = SaveData.town.buildingInfos;
            var existingIndex = buildingInfos.FindIndex(info => info.tileId == buildingInfo.tileId);
            if (existingIndex >= 0)
            {
                buildingInfos[existingIndex] = CloneBuildingInfo(buildingInfo);
            }
            else
            {
                buildingInfos.Add(CloneBuildingInfo(buildingInfo));
            }

            SaveGame();
        }

        /// <summary>
        /// 处理是否拥有大地图建筑相关逻辑。
        /// </summary>
        /// <param name="playerId">数据编号。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool HasOwnedTownBuilding(int playerId = SelfPlayerBuildingId)
        {
            EnsureTownBuildingDefaults();
            if (playerId <= 0)
            {
                return false;
            }

            return SaveData.town.buildingInfos.Exists(info => info != null && info.playerId == playerId);
        }

        /// <summary>
        /// 判断当前玩家是否已经购买过大地图地块，包含未建造和建造中的地块。
        /// </summary>
        /// <param name="playerId">玩家编号。</param>
        /// <returns>已经拥有任意地块时返回 true。</returns>
        public bool HasOwnedTownLand(int playerId)
        {
            EnsureTownBuildingDefaults();
            if (playerId <= 0)
            {
                return false;
            }

            return SaveData.town.buildingInfos.Exists(info => info != null && info.playerId == playerId);
        }

        /// <summary>
        /// 判断当前玩家是否已经拥有建成建筑。
        /// </summary>
        /// <param name="playerId">玩家编号。</param>
        /// <returns>存在已建成建筑时返回 true。</returns>
        public bool HasCompletedTownBuilding(int playerId)
        {
            EnsureTownBuildingDefaults();
            if (playerId <= 0)
            {
                return false;
            }

            return SaveData.town.buildingInfos.Exists(info => info != null
                                                              && info.playerId == playerId
                                                              && info.status == 2
                                                              && info.buildingLevel > 0);
        }

        /// <summary>
        /// 获取已拥有大地图建筑。
        /// </summary>
        /// <param name="playerId">数据编号。</param>
        /// <returns>返回方法执行后的结果。</returns>
        public BuildingInfo GetOwnedTownBuilding(int playerId = SelfPlayerBuildingId)
        {
            EnsureTownBuildingDefaults();
            if (playerId <= 0)
            {
                return null;
            }

            return SaveData.town.buildingInfos.Find(info => info != null && info.playerId == playerId);
        }

        /// <summary>
        /// 获取购买大地图地块需要的铜钱数量。
        /// </summary>
        /// <returns>地块购买价格。</returns>
        public int GetTownLandPurchaseCost(int tileId = 0)
        {
            return tileId == 2 || tileId == 3
                ? TownLandPurchaseCost + 500
                : TownLandPurchaseCost;
        }

        /// <summary>
        /// 尝试购买大地图地块；每个玩家最多只能拥有一个地块。
        /// </summary>
        /// <param name="tileId">地块编号。</param>
        /// <param name="message">返回失败或成功原因。</param>
        /// <returns>购买成功时返回 true。</returns>
        public bool TryPurchaseTownLand(int tileId, out string message)
        {
            EnsureTownBuildingDefaults();
            var selfPlayerId = ResolveCurrentPlayerId();
            if (selfPlayerId <= 0)
            {
                message = "当前玩家数据异常，无法购买地块";
                return false;
            }

            if (HasOwnedTownLand(selfPlayerId))
            {
                message = "每位玩家暂时只能拥有一个地块";
                return false;
            }

            var buildingInfo = SaveData.town.buildingInfos.Find(info => info != null && info.tileId == tileId);
            if (buildingInfo == null)
            {
                message = "未找到目标地块";
                return false;
            }

            if (buildingInfo.playerId != 0)
            {
                message = "该地块已被购买";
                return false;
            }

            var purchaseCost = GetTownLandPurchaseCost(tileId);
            if (PlayerData.coinNum < purchaseCost)
            {
                message = $"铜钱不足，购买地块需要 {purchaseCost}";
                return false;
            }

            ChangeCoinNum(-purchaseCost);
            buildingInfo.playerId = selfPlayerId;
            buildingInfo.name = PlayerData.playerName;
            buildingInfo.buildingId = 0;
            buildingInfo.buildingLevel = 0;
            buildingInfo.buildingTime = 0;
            buildingInfo.status = 0;
            PlayerData.buildId = tileId;
            SaveGame();
            message = "地块购买成功";
            return true;
        }

        /// <summary>
        /// 尝试在已购买地块上开始建造建筑。
        /// </summary>
        /// <param name="tileId">地块编号。</param>
        /// <param name="buildingLevel">建筑等级。</param>
        /// <param name="coinChange">铜钱变化值，负数表示花费。</param>
        /// <param name="buildDuration">建造持续时间。</param>
        /// <param name="message">返回失败或成功原因。</param>
        /// <returns>开始建造成功时返回 true。</returns>
        public bool TryStartTownBuilding(int tileId, int buildingLevel, int coinChange, int buildDuration, out string message)
        {
            EnsureTownBuildingDefaults();
            var selfPlayerId = ResolveCurrentPlayerId();
            var buildingInfo = SaveData.town.buildingInfos.Find(info => info != null && info.tileId == tileId);
            if (buildingInfo == null || buildingInfo.playerId != selfPlayerId)
            {
                message = "请先购买该地块";
                return false;
            }

            if (buildingInfo.buildingLevel > 0 || buildingInfo.status != 0)
            {
                message = "该地块已经开始建造";
                return false;
            }

            if (PlayerData.coinNum + coinChange < 0)
            {
                message = "铜钱不足，无法建造该建筑";
                return false;
            }

            ChangeCoinNum(coinChange);
            buildingInfo.name = PlayerData.playerName;
            buildingInfo.buildingId = 1;
            buildingInfo.buildingLevel = Mathf.Clamp(buildingLevel, 1, 3);
            buildingInfo.buildingTime = Mathf.Max(0, buildDuration);
            buildingInfo.status = buildingInfo.buildingTime > 0 ? 1 : 2;
            UpsertBuildingInfo(buildingInfo);
            message = "建筑开始建造";
            return true;
        }

        /// <summary>
        /// 设置当前已拥有建筑。
        /// </summary>
        /// <param name="tileId">数据编号。</param>
        /// <param name="buildingLevel">等级。</param>
        public void SetActiveOwnedBuilding(int tileId, int buildingLevel)
        {
            EnsureGameplayDefaults();

            SaveData.gameplay.activeShopId = (byte)Mathf.Clamp(tileId, 0, byte.MaxValue);
            SaveData.gameplay.ownedShops.Clear();
            SaveData.gameplay.ownedShops.Add(new LocalShopSaveData
            {
                mapSlotIndex = (byte)Mathf.Clamp(tileId, 0, byte.MaxValue),
                shopTypeId = 1,
                shopLevel = (byte)Mathf.Clamp(buildingLevel, 1, byte.MaxValue)
            });

            SaveGame();
        }

        /// <summary>
        /// 解析当前登录玩家的数字编号。
        /// </summary>
        /// <returns>解析成功返回玩家编号，否则返回 0。</returns>
        private int ResolveCurrentPlayerId()
        {
            return GetLocalPlayerNumericId();
        }

        /// <summary>
        /// 获取桌位数据。
        /// </summary>
        /// <param name="桌位编号">数据编号。</param>
        /// <returns>返回方法执行后的结果。</returns>
        public TavernTableSaveData GetTableData(int tableId)
        {
            EnsureTavernDefaults();
            return SaveData.tavern.tables.Find(table => table.tableId == tableId);
        }

        /// <summary>
        /// 获取全部桌位数据。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        public IReadOnlyList<TavernTableSaveData> GetAllTableData()
        {
            EnsureTavernDefaults();
            return SaveData.tavern.tables;
        }

        /// <summary>
        /// 获取已解锁桌位数量。
        /// </summary>
        /// <returns>返回计算后的数值。</returns>
        public int GetUnlockedTableCount()
        {
            EnsureTavernDefaults();
            return SaveData.tavern.tables.FindAll(table => table.isUnlocked).Count;
        }

        /// <summary>
        /// 处理解锁桌位相关逻辑。
        /// </summary>
        /// <param name="桌位编号">数据编号。</param>
        public void UnlockTable(int tableId)
        {
            var tableData = GetTableData(tableId);
            if (tableData == null)
            {
                return;
            }

            tableData.isUnlocked = true;
            tableData.runtimeState = (int)TavernTableRuntimeState.Idle;
            tableNum = GetUnlockedTableCount();
            SyncGameplayGuideProgress();
            Signals.Get<TableNumSignal>().Dispatch();
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
            Signals.Get<GameplayGuideProgressSignal>().Dispatch();
            SaveGame();
        }

        /// <summary>
        /// 桌子允许的最高等级，UI 与升级逻辑共用同一上限。
        /// </summary>
        public const int MaxTableLevel = 3;

        /// <summary>
        /// 升级指定桌位等级，达到最高等级时不再叠加。
        /// </summary>
        /// <param name="tableId">桌位编号。</param>
        /// <returns>真实发生升级时返回 true，已经满级或桌位无效时返回 false。</returns>
        public bool UpgradeTable(int tableId)
        {
            var tableData = GetTableData(tableId);
            if (tableData == null || !tableData.isUnlocked)
            {
                return false;
            }

            var currentLevel = Mathf.Max(1, tableData.level);
            if (currentLevel >= MaxTableLevel)
            {
                return false;
            }

            tableData.level = currentLevel + 1;
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
            SaveGame();
            return true;
        }

        /// <summary>
        /// 设置桌位运行时状态。
        /// </summary>
        /// <param name="桌位编号">数据编号。</param>
        /// <param name="state">参数值。</param>
        public void SetTableRuntimeState(int tableId, TavernTableRuntimeState state)
        {
            var tableData = GetTableData(tableId);
            if (tableData == null)
            {
                return;
            }

            tableData.runtimeState = (int)state;
            SaveGame();
        }

        /// <summary>
        /// 添加桌位收入。
        /// </summary>
        /// <param name="桌位编号">数据编号。</param>
        /// <param name="income">参数值。</param>
        public void AddTableIncome(int tableId, int income)
        {
            var tableData = GetTableData(tableId);
            if (tableData == null)
            {
                return;
            }

            tableData.totalIncome += income;
            tableData.totalServedCustomers += 1;
            SaveData.tavern.totalIncome += income;
            SaveData.tavern.totalServedCustomers += 1;
            SaveData.gameplay.dailyRevenue += income;
            SaveData.gameplay.totalDepositedIncome += income;
            SaveGame();
        }

        /// <summary>
        /// 当前是否已经解锁桌子升级功能。
        /// </summary>
        /// <returns>已解锁时返回 true。</returns>
        public bool IsTableLv2UpgradeUnlocked()
        {
            EnsureTavernDefaults();
            return SaveData.tavern.tableLv2UpgradeUnlocked;
        }

        /// <summary>
        /// 标记桌子升级功能已解锁。
        /// </summary>
        public void UnlockTableLv2Upgrade()
        {
            EnsureTavernDefaults();
            if (SaveData.tavern.tableLv2UpgradeUnlocked)
            {
                return;
            }

            SaveData.tavern.tableLv2UpgradeUnlocked = true;
            SaveGame();
        }

        /// <summary>
        /// 设置酒楼开业状态并通知场景刷新。
        /// </summary>
        /// <param name="is打开">参数值。</param>
        public void SetTavernOpen(bool isOpen)
        {
            EnsureTavernDefaults();
            SaveData.tavern.isOpen = isOpen;
            SaveData.gameplay.shopOpened = isOpen;

            if (isOpen)
            {
                SaveData.gameplay.shopOpenDuration = 0f;
                SaveData.gameplay.dailyRevenue = 0;
                GameplayGuideData.onboardingCompleted = true;
            }

            Signals.Get<TavernBusinessStateSignal>().Dispatch(isOpen);
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
            Signals.Get<GameplayGuideProgressSignal>().Dispatch();
            SaveGame();
        }

        /// <summary>
        /// 确保初始化状态。
        /// </summary>
        private void EnsureInitialized()
        {
            if (IsInitialized && SaveData != null)
            {
                return;
            }

            LoadOrCreateSave();
        }

        /// <summary>
        /// 确保核心初始化状态。
        /// </summary>
        private void EnsureInitializedCore()
        {
            if (SaveData != null)
            {
                return;
            }

            LoadOrCreateSave();
        }

        /// <summary>
        /// 解析本地玩家数字编号。
        /// </summary>
        /// <param name="currentValue">参数值。</param>
        /// <returns>返回方法执行后的结果。</returns>
        private ushort ResolveLocalPlayerNumericId(ushort currentValue)
        {
            if (currentValue > 0)
            {
                return currentValue;
            }

            var seed = !string.IsNullOrWhiteSpace(PlayerData?.playerId)
                ? PlayerData.playerId
                : NormalizePlayerName(PlayerData?.playerName);

            if (string.IsNullOrWhiteSpace(seed))
            {
                return 1;
            }

            unchecked
            {
                var hash = 17;
                for (var index = 0; index < seed.Length; index++)
                {
                    hash = (hash * 31) + seed[index];
                }

                return (ushort)Mathf.Clamp(Math.Abs(hash % 60000) + 1, 1, ushort.MaxValue);
            }
        }

        /// <summary>
        /// 规范化玩家名称。
        /// </summary>
        /// <param name="playerName">名称。</param>
        /// <returns>返回方法执行后的结果。</returns>
        private static string NormalizePlayerName(string playerName)
        {
            return string.IsNullOrWhiteSpace(playerName) ? null : playerName.Trim();
        }

        /// <summary>
        /// 获取名称d存档路径。
        /// </summary>
        /// <param name="playerName">名称。</param>
        /// <returns>返回方法执行后的结果。</returns>
        private static string GetNamedSavePath(string playerName)
        {
            var safeName = Uri.EscapeDataString(NormalizePlayerName(playerName) ?? "player");
            return Path.Combine(SaveDirectoryPath, $"{safeName}.json");
        }

        /// <summary>
        /// 响应应用聚焦事件并同步状态。
        /// </summary>
        /// <param name="focus">参数值。</param>
        private void OnApplicationFocus(bool focus)
        {
            if (!focus)
            {
                SaveGame();
            }
        }

        /// <summary>
        /// 响应应用暂停事件并同步状态。
        /// </summary>
        /// <param name="pause状态">参数值。</param>
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveGame();
            }
        }

        /// <summary>
        /// 响应应用退出事件并同步状态。
        /// </summary>
        protected override void OnApplicationQuit()
        {
            SaveGame();
        }
    }
}
