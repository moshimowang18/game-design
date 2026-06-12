using System;
using System.Collections.Generic;
using JN.Client.Messages;

namespace JN.Client.Model
{
    [Serializable]
    /// <summary>
    /// 负责游戏存档数据相关的运行时逻辑。
    /// </summary>
    public class GameSaveData
    {
        public int version = 1;
        public string lastSceneName = "Town";
        public long lastSavedUtcTicks;
        public PlayerModel player = new();
        public LocalGameplaySaveData gameplay = new();
        public TownSaveData town = new();
        public TavernSaveData tavern = new();
        public GameDayData gameDay = new();
        public OperationResult lastOperationResult;
    }

    [Serializable]
    /// <summary>
    /// 负责大地图存档数据相关的运行时逻辑。
    /// </summary>
    public class TownSaveData
    {
        public List<BuildingInfo> buildingInfos = new();
    }

    [Serializable]
    /// <summary>
    /// 负责酒楼存档数据相关的运行时逻辑。
    /// </summary>
    public class TavernSaveData
    {
        public bool isOpen;
        public int availableDishes;
        public int totalServedCustomers;
        public int totalIncome;
        public bool tableLv2UpgradeUnlocked;
        public List<TavernTableSaveData> tables = new();
    }

    [Serializable]
    /// <summary>
    /// 负责酒楼桌位存档数据相关的运行时逻辑。
    /// </summary>
    public class TavernTableSaveData
    {
        public int tableId;
        public bool isUnlocked;
        public int level = 1;
        public int runtimeState = (int)TavernTableRuntimeState.Locked;
        public int totalServedCustomers;
        public int totalIncome;
    }

    /// <summary>
    /// 定义酒楼桌位运行时状态可用的枚举类型。
    /// </summary>
    public enum TavernTableRuntimeState
    {
        Locked = 0,
        Idle = 1,
        Reserved = 2,
        WaitingServe = 3,
        Dining = 4,
        Checkout = 5,
        Cleaning = 6,
        WaitingOrder = 7
    }
}
