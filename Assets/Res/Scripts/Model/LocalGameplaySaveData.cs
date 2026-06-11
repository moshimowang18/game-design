using System;
using System.Collections.Generic;

namespace JN.Client.Model
{
    [Serializable]
    /// <summary>
    /// 负责本地玩法存档数据相关的运行时逻辑。
    /// </summary>
    public class LocalGameplaySaveData
    {
        public ushort localPlayerNumericId = 1;
        public byte playerLevel = 1;
        public byte activeShopId;
        public bool openingLoanClaimed;
        public int loanCount;
        public int pendingLoanAmount;
        public bool waitingForLoanApproval;
        public bool shopOpened;
        public bool waitingForSettlement;
        public bool firstShopEntryPending = true;
        public bool tutorialEnabled = true;
        public float shopOpenDuration;
        public float reopenCooldown;
        public int pendingSettlementIncome;
        public int pendingSettlementCosts;
        public int dailyRevenue;
        public int totalDepositedIncome;
        public float peakTimeRemaining;
        public float peakTimeCooldown;
        public bool inPeakTime;
        public byte remainingPeakCustomers;
        public List<byte> purchasedLandSlots = new();
        public List<int> hiredStaffIds = new();
        public List<LocalStaffSaveData> ownedStaff = new();
        public bool[] unlockedFeatures = { false, false, false, false, false };
        public List<LocalShopSaveData> ownedShops = new();
        public List<LocalEquipmentSaveData> ownedEquipment = new();
        public GameplayGuideSaveData gameplayGuide = new();
    }

    [Serializable]
    /// <summary>
    /// 负责本地店铺存档数据相关的运行时逻辑。
    /// </summary>
    public class LocalShopSaveData
    {
        public byte mapSlotIndex;
        public byte shopLevel = 1;
        public sbyte shopTypeId = -1;
        public float constructionFinishTime;
        public int totalShopValue;
        public int totalShopSpendings;
        public bool openedForCustomers;
        public float nextCustomerInSeconds;
        public List<LocalRuntimeCustomerSaveData> currentCustomers = new();
        public List<LocalStaffSaveData> ownedStaff = new();
    }

    [Serializable]
    /// <summary>
    /// 负责本地设备存档数据相关的运行时逻辑。
    /// </summary>
    public class LocalEquipmentSaveData
    {
        public byte equipmentId;
        public byte currentLevel;
        public byte physicalSlotIndex;
    }

    [Serializable]
    /// <summary>
    /// 负责本地员工存档数据相关的运行时逻辑。
    /// </summary>
    public class LocalStaffSaveData
    {
        public byte staffId;
        public byte currentLevel;
        public bool temporary;
        public float remainingHireTime;
    }

    [Serializable]
    /// <summary>
    /// 负责本地运行时顾客存档数据相关的运行时逻辑。
    /// </summary>
    public class LocalRuntimeCustomerSaveData
    {
        public ushort runtimeId;
        public byte customerTypeId;
        public bool peakCustomer;
        public string sourcePlayerName;
    }
}
