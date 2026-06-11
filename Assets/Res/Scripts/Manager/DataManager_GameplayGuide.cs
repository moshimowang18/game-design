using JN.Client.Messages;
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
        public const int MaxGuideChefCount = 3;
        public const int MaxGuideWaiterCount = 3;
        private const int GuideChefRepeatHireCost = 1500;
        private const int GuideWaiterRepeatHireCost = 1000;
        private const string GuideKitchenStove = "stove";
        private const string GuideKitchenFurnace = "furnace";
        private const string GuideKitchenWineCabinet = "wine_cabinet";
        private const string GuideKitchenCabinet = "cabinet";
        private const string GuideKitchenTable1 = "kitchen_table_1";
        private const string GuideKitchenTable2 = "kitchen_table_2";

        /// <summary>
        /// 获取玩法引导快照。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        public GameplayGuideSnapshot GetGameplayGuideSnapshot()
        {
            EnsureGameplayDefaults();
            SyncGameplayGuideProgress();

            var guide = SaveData.gameplay.gameplayGuide;
            var snapshot = new GameplayGuideSnapshot
            {
                Stage = guide.currentStage,
                RecruitmentUnlocked = guide.recruitmentUnlocked,
                CanOpenBusiness = guide.openingUnlocked,
                OnboardingCompleted = guide.onboardingCompleted
            };

            if (snapshot.Stage == GameplayGuideStage.Build)
            {
                snapshot.ActiveTasks.Add(new GameplayGuideTaskProgress(GameplayGuideTaskId.BuyCounter, "购买掌柜桌", guide.purchasedCounter ? 1 : 0, 1));
                snapshot.ActiveTasks.Add(new GameplayGuideTaskProgress(GameplayGuideTaskId.BuyCabinet, "购买柜子", guide.purchasedCabinet ? 1 : 0, 1));
                snapshot.ActiveTasks.Add(new GameplayGuideTaskProgress(GameplayGuideTaskId.BuyWineCabinet, "购买酒柜", guide.purchasedWineCabinet ? 1 : 0, 1));
                snapshot.ActiveTasks.Add(new GameplayGuideTaskProgress(GameplayGuideTaskId.BuyTables, "购买四张桌子", Mathf.Clamp(guide.purchasedTableCount, 0, 4), 4));
                snapshot.ActiveTasks.Add(new GameplayGuideTaskProgress(GameplayGuideTaskId.BuyStove, "购买一个灶台", guide.purchasedStove ? 1 : 0, 1));
                snapshot.ActiveTasks.Add(new GameplayGuideTaskProgress(GameplayGuideTaskId.BuyFurnace, "购买炉子", guide.purchasedFurnace ? 1 : 0, 1));
                snapshot.ActiveTasks.Add(new GameplayGuideTaskProgress(GameplayGuideTaskId.BuyKitchenTable1, "购买厨房桌子1", guide.purchasedKitchenTable1 ? 1 : 0, 1));
                snapshot.ActiveTasks.Add(new GameplayGuideTaskProgress(GameplayGuideTaskId.BuyKitchenTable2, "购买厨房桌子2", guide.purchasedKitchenTable2 ? 1 : 0, 1));
            }
            else if (snapshot.Stage == GameplayGuideStage.Recruit)
            {
                snapshot.ActiveTasks.Add(new GameplayGuideTaskProgress(GameplayGuideTaskId.HireShopkeeper, "招聘掌柜", guide.hiredShopkeeper ? 1 : 0, 1));
                snapshot.ActiveTasks.Add(new GameplayGuideTaskProgress(GameplayGuideTaskId.HireChef, "招聘厨师", guide.hiredChef ? 1 : 0, 1));
                snapshot.ActiveTasks.Add(new GameplayGuideTaskProgress(GameplayGuideTaskId.HireWaiter, "招聘小二", guide.hiredWaiter ? 1 : 0, 1));
            }

            return snapshot;
        }

        /// <summary>
        /// 获取当前玩法引导任务。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        public GameplayGuideTaskProgress GetCurrentGameplayGuideTask()
        {
            var snapshot = GetGameplayGuideSnapshot();
            for (var index = 0; index < snapshot.ActiveTasks.Count; index++)
            {
                var task = snapshot.ActiveTasks[index];
                if (task != null && !task.IsCompleted)
                {
                    return task;
                }
            }

            return snapshot.ActiveTasks.Count > 0 ? snapshot.ActiveTasks[^1] : null;
        }

        /// <summary>
        /// 判断是否满足酒楼开业条件。
        /// </summary>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool CanOpenTavernBusiness()
        {
            var snapshot = GetGameplayGuideSnapshot();
            return snapshot.CanOpenBusiness && !SaveData.tavern.isOpen;
        }

        /// <summary>
        /// 判断是否完成开局基础设备购买，完成后才开放桌子购买。
        /// </summary>
        /// <returns>完成基础设备购买时返回 true。</returns>
        public bool CanPurchaseGuideTables()
        {
            EnsureGameplayDefaults();
            return CanPurchaseGuideTables(SaveData.gameplay.gameplayGuide);
        }

        /// <summary>
        /// 判断是否完成四张桌子购买，完成后才开放厨房设备购买。
        /// </summary>
        /// <returns>完成桌子购买时返回 true。</returns>
        public bool CanPurchaseGuideKitchenEquipment()
        {
            EnsureGameplayDefaults();
            return CanPurchaseGuideKitchenEquipment(SaveData.gameplay.gameplayGuide);
        }

        /// <summary>
        /// 判断是否完成招聘前置设备，完成后才开放招聘。
        /// </summary>
        /// <returns>完成厨房设备购买时返回 true。</returns>
        public bool CanRecruitGuideStaff()
        {
            EnsureGameplayDefaults();
            return CanRecruitGuideStaff(SaveData.gameplay.gameplayGuide);
        }

        /// <summary>
        /// 使用已传入的引导数据判断是否开放桌子购买，避免同步进度时递归触发默认数据初始化。
        /// </summary>
        /// <param name="guide">玩法引导数据。</param>
        /// <returns>满足桌子购买前置时返回 true。</returns>
        private static bool CanPurchaseGuideTables(GameplayGuideSaveData guide)
        {
            return guide != null && guide.purchasedCounter && guide.purchasedCabinet && guide.purchasedWineCabinet;
        }

        /// <summary>
        /// 使用已传入的引导数据判断是否开放厨房设备购买。
        /// </summary>
        /// <param name="guide">玩法引导数据。</param>
        /// <returns>满足厨房购买前置时返回 true。</returns>
        private static bool CanPurchaseGuideKitchenEquipment(GameplayGuideSaveData guide)
        {
            return CanPurchaseGuideTables(guide) && guide.purchasedTableCount >= 4;
        }

        /// <summary>
        /// 使用已传入的引导数据判断是否开放招聘。
        /// </summary>
        /// <param name="guide">玩法引导数据。</param>
        /// <returns>满足招聘前置时返回 true。</returns>
        private static bool CanRecruitGuideStaff(GameplayGuideSaveData guide)
        {
            return CanPurchaseGuideKitchenEquipment(guide)
                   && guide.purchasedStove
                   && guide.purchasedFurnace
                   && guide.purchasedKitchenTable1
                   && guide.purchasedKitchenTable2;
        }

        /// <summary>
        /// 判断是否需要显示招聘解锁提示。
        /// </summary>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool ShouldShowRecruitmentUnlockToast()
        {
            var guide = GameplayGuideData;
            return guide.recruitmentUnlocked && !guide.recruitmentUnlockToastShown;
        }

        /// <summary>
        /// 标记招聘解锁提示已显示。
        /// </summary>
        public void MarkRecruitmentUnlockToastShown()
        {
            var guide = GameplayGuideData;
            if (guide.recruitmentUnlockToastShown)
            {
                return;
            }

            guide.recruitmentUnlockToastShown = true;
            Signals.Get<GameplayGuideProgressSignal>().Dispatch();
            SaveGame();
        }

        /// <summary>
        /// 尝试处理购买引导柜台。
        /// </summary>
        /// <param name="message">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool TryPurchaseGuideCounter(out string message)
        {
            return TryPurchaseGuideEquipment(CounterEquipmentId, "掌柜桌", out message);
        }

        /// <summary>
        /// 尝试处理购买引导灶台。
        /// </summary>
        /// <param name="message">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool TryPurchaseGuideStove(out string message)
        {
            return TryPurchaseGuideEquipment(StoveEquipmentId, "灶台", out message);
        }

        /// <summary>
        /// 尝试处理购买引导厨房物件。
        /// </summary>
        /// <param name="itemKey">语言表键值。</param>
        /// <param name="message">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool TryPurchaseGuideKitchenItem(string itemKey, out string message)
        {
            EnsureGameplayDefaults();
            if (string.IsNullOrEmpty(itemKey))
            {
                message = "未找到可购买物件";
                return false;
            }

            if (IsGuideKitchenItemPurchased(itemKey))
            {
                message = $"{GetGuideKitchenDisplayName(itemKey)}已购买";
                return false;
            }

            var cost = GetGuideEquipmentPurchaseCost(StoveEquipmentId);
            if (PlayerData.coinNum < cost)
            {
                message = $"铜钱不足，购买{GetGuideKitchenDisplayName(itemKey)}需要{cost}";
                return false;
            }

            ChangeCoinNum(-cost);
            SetGuideKitchenItemPurchased(itemKey, true);
            SyncGameplayGuideProgress();
            Signals.Get<GameplayGuideProgressSignal>().Dispatch();
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
            SaveGame();
            message = $"已购买{GetGuideKitchenDisplayName(itemKey)}";
            return true;
        }

        /// <summary>
        /// 尝试处理招聘引导掌柜。
        /// </summary>
        /// <param name="message">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool TryHireGuideShopkeeper(out string message)
        {
            return TryHireGuideStaff(ShopkeeperStaffId, StaffRole.Waiter, "掌柜", out message);
        }

        /// <summary>
        /// 尝试处理招聘引导厨师。
        /// </summary>
        /// <param name="message">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool TryHireGuideChef(out string message)
        {
            return TryHireGuideStaff(ChefStaffId, StaffRole.Chef, "厨师", out message, MaxGuideChefCount, true);
        }

        /// <summary>
        /// 尝试招聘引导阶段的小二。
        /// </summary>
        /// <param name="message">返回招聘失败或成功的提示文案。</param>
        /// <returns>招聘成功时返回 true，否则返回 false。</returns>
        public bool TryHireGuideWaiter(out string message)
        {
            return TryHireGuideStaff(WaiterStaffId, StaffRole.Waiter, "小二", out message, MaxGuideWaiterCount, true);
        }

        /// <summary>
        /// 获取当前已招聘的引导厨师数量。
        /// </summary>
        /// <returns>已招聘厨师数量，兼容旧存档中只记录员工编号的情况。</returns>
        public int GetHiredGuideChefCount()
        {
            EnsureGameplayDefaults();
            return CountHiredGuideStaff(ChefStaffId, StaffRole.Chef);
        }

        /// <summary>
        /// 获取当前已招聘的引导小二数量。
        /// </summary>
        /// <returns>已招聘小二数量，兼容旧存档中只记录员工编号的情况。</returns>
        public int GetHiredGuideWaiterCount()
        {
            EnsureGameplayDefaults();
            return CountHiredGuideStaff(WaiterStaffId, StaffRole.Waiter);
        }

        /// <summary>
        /// 判断是否还能继续招聘引导厨师。
        /// </summary>
        /// <returns>未达到厨师上限时返回 true。</returns>
        public bool CanHireMoreGuideChef()
        {
            return GetHiredGuideChefCount() < MaxGuideChefCount;
        }

        /// <summary>
        /// 判断是否还能继续招聘引导小二。
        /// </summary>
        /// <returns>未达到小二上限时返回 true。</returns>
        public bool CanHireMoreGuideWaiter()
        {
            return GetHiredGuideWaiterCount() < MaxGuideWaiterCount;
        }

        /// <summary>
        /// 获取引导招聘员工的铜钱花费。
        /// </summary>
        /// <param name="preferredStaffId">优先员工编号。</param>
        /// <param name="role">员工角色。</param>
        /// <returns>招聘花费，找不到配置时返回 0。</returns>
        public int GetGuideStaffHireCost(int preferredStaffId, StaffRole role)
        {
            EnsureGameplayDefaults();
            var staff = FindGuideStaff(preferredStaffId, role);
            var levelConfig = staff != null ? staff.GetLevelConfig(1) : null;
            var baseCost = levelConfig != null ? Mathf.Max(0, levelConfig.hireUpgradeCost) : 0;
            var nextHireIndex = CountHiredGuideStaff(preferredStaffId, role) + 1;

            if (nextHireIndex >= 2)
            {
                if (preferredStaffId == ChefStaffId && role == StaffRole.Chef)
                {
                    return GuideChefRepeatHireCost;
                }

                if (preferredStaffId == WaiterStaffId && role == StaffRole.Waiter)
                {
                    return GuideWaiterRepeatHireCost;
                }
            }

            return baseCost;
        }

        /// <summary>
        /// 获取引导招聘员工配置。
        /// </summary>
        /// <param name="preferredStaffId">优先员工编号。</param>
        /// <param name="role">员工角色。</param>
        /// <returns>员工配置。</returns>
        public SO_Staff GetGuideStaffConfig(int preferredStaffId, StaffRole role)
        {
            return FindGuideStaff(preferredStaffId, role);
        }

        /// <summary>
        /// 尝试处理购买引导设备。
        /// </summary>
        /// <param name="equipmentId">数据编号。</param>
        /// <param name="displayNameOverride">数据编号。</param>
        /// <param name="message">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private bool TryPurchaseGuideEquipment(int equipmentId, string displayNameOverride, out string message)
        {
            EnsureGameplayDefaults();
            if (HasOwnedEquipment(equipmentId))
            {
                message = $"{displayNameOverride}已购买";
                return false;
            }

            var equipment = SO_Equipment.GetById(equipmentId);
            var levelConfig = equipment != null ? equipment.GetLevelConfig(1) : null;
            var cost = levelConfig != null ? Mathf.Max(0, levelConfig.upgradeCost) : 0;
            if (PlayerData.coinNum < cost)
            {
                message = $"铜钱不足，购买{displayNameOverride}需要 {cost}";
                return false;
            }

            ChangeCoinNum(-cost);
            SaveData.gameplay.ownedEquipment.Add(new LocalEquipmentSaveData
            {
                equipmentId = (byte)equipmentId,
                currentLevel = 1,
                physicalSlotIndex = (byte)SaveData.gameplay.ownedEquipment.Count
            });

            SyncGameplayGuideProgress();
            Signals.Get<GameplayGuideProgressSignal>().Dispatch();
            Signals.Get<TavernRuntimeChangedSignal>().Dispatch();
            SaveGame();
            message = $"已购买{displayNameOverride}";
            return true;
        }

        /// <summary>
        /// 尝试处理招聘引导员工。
        /// </summary>
        /// <param name="preferredStaffId">数据编号。</param>
        /// <param name="role">参数值。</param>
        /// <param name="displayNameOverride">数据编号。</param>
        /// <param name="message">参数值。</param>
        /// <param name="maxCount">允许招聘的最大数量。</param>
        /// <param name="allowDuplicate">是否允许同一种员工重复招聘。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private bool TryHireGuideStaff(int preferredStaffId, StaffRole role, string displayNameOverride, out string message, int maxCount = 1, bool allowDuplicate = false)
        {
            EnsureGameplayDefaults();
            var hiredCount = CountHiredGuideStaff(preferredStaffId, role);
            if (allowDuplicate)
            {
                if (hiredCount >= Mathf.Max(1, maxCount))
                {
                    message = $"{displayNameOverride}已达到招聘上限";
                    return false;
                }
            }
            else if (hiredCount > 0)
            {
                message = $"{displayNameOverride}已招聘";
                return false;
            }

            var staff = FindGuideStaff(preferredStaffId, role);
            if (staff == null)
            {
                message = $"未找到{displayNameOverride}配置";
                return false;
            }

            var cost = GetGuideStaffHireCost(preferredStaffId, role);

            if (PlayerData.coinNum < cost)
            {
                message = $"铜钱不足，招聘{displayNameOverride}需要 {cost}";
                return false;
            }

            ChangeCoinNum(-cost);

            if (int.TryParse(staff.staffId, out var numericStaffId) && !SaveData.gameplay.hiredStaffIds.Contains(numericStaffId))
            {
                SaveData.gameplay.hiredStaffIds.Add(numericStaffId);
            }

            SaveData.gameplay.ownedStaff.Add(new LocalStaffSaveData
            {
                staffId = (byte)Mathf.Max(0, preferredStaffId),
                currentLevel = 1,
                temporary = false,
                remainingHireTime = 0f
            });

            SyncGameplayGuideProgress();
            Signals.Get<GameplayGuideProgressSignal>().Dispatch();
            SaveGame();
            message = $"已招聘{displayNameOverride}";
            return true;
        }

        /// <summary>
        /// 处理同步玩法引导进度相关逻辑。
        /// </summary>
        private void SyncGameplayGuideProgress()
        {
            if (SaveData?.gameplay == null)
            {
                return;
            }

            SaveData.gameplay.gameplayGuide ??= new GameplayGuideSaveData();
            var guide = SaveData.gameplay.gameplayGuide;
            guide.purchasedTableCount = Mathf.Max(guide.purchasedTableCount, Mathf.Min(GetUnlockedTableCount(), 4));
            guide.purchasedCounter = guide.purchasedCounter || HasOwnedEquipment(CounterEquipmentId);
            guide.purchasedStove = guide.purchasedStove || HasOwnedEquipment(StoveEquipmentId);
            guide.hiredShopkeeper = guide.hiredShopkeeper || HasHiredGuideStaff(ShopkeeperStaffId, StaffRole.Waiter);
            guide.hiredChef = guide.hiredChef || HasHiredGuideStaff(ChefStaffId, StaffRole.Chef);
            guide.hiredWaiter = guide.hiredWaiter || HasHiredGuideStaff(WaiterStaffId, StaffRole.Waiter);
            guide.recruitmentUnlocked = CanRecruitGuideStaff(guide);
            guide.openingUnlocked = guide.recruitmentUnlocked && guide.hiredShopkeeper && guide.hiredChef && guide.hiredWaiter;
            guide.onboardingCompleted = guide.onboardingCompleted || SaveData.tavern.isOpen;
            guide.currentStage = SaveData.tavern.isOpen
                ? GameplayGuideStage.Running
                : guide.openingUnlocked
                    ? GameplayGuideStage.ReadyToOpen
                    : guide.recruitmentUnlocked
                        ? GameplayGuideStage.Recruit
                        : GameplayGuideStage.Build;
        }

        /// <summary>
        /// 处理是否拥有设备相关逻辑。
        /// </summary>
        /// <param name="equipmentId">数据编号。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private bool HasOwnedEquipment(int equipmentId)
        {
            var ownedEquipment = SaveData?.gameplay?.ownedEquipment;
            if (ownedEquipment == null)
            {
                return false;
            }

            for (var index = 0; index < ownedEquipment.Count; index++)
            {
                var equipment = ownedEquipment[index];
                if (equipment != null && equipment.equipmentId == equipmentId && equipment.currentLevel > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 处理Is引导厨房物件购买d相关逻辑。
        /// </summary>
        /// <param name="itemKey">语言表键值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public bool IsGuideKitchenItemPurchased(string itemKey)
        {
            var guide = GameplayGuideData;
            return itemKey switch
            {
                GuideKitchenStove => guide.purchasedStove,
                GuideKitchenFurnace => guide.purchasedFurnace,
                GuideKitchenWineCabinet => guide.purchasedWineCabinet,
                GuideKitchenCabinet => guide.purchasedCabinet,
                GuideKitchenTable1 => guide.purchasedKitchenTable1,
                GuideKitchenTable2 => guide.purchasedKitchenTable2,
                _ => false
            };
        }

        /// <summary>
        /// 获取引导厨房显示名称。
        /// </summary>
        /// <param name="itemKey">语言表键值。</param>
        /// <returns>返回方法执行后的结果。</returns>
        public static string GetGuideKitchenDisplayName(string itemKey)
        {
            return itemKey switch
            {
                GuideKitchenStove => "灶台",
                GuideKitchenFurnace => "炉子",
                GuideKitchenWineCabinet => "酒柜",
                GuideKitchenCabinet => "柜子",
                GuideKitchenTable1 => "厨房桌子1",
                GuideKitchenTable2 => "厨房桌子2",
                _ => "厨房物件"
            };
        }

        /// <summary>
        /// 设置引导厨房物件购买d。
        /// </summary>
        /// <param name="itemKey">语言表键值。</param>
        /// <param name="value">参数值。</param>
        private void SetGuideKitchenItemPurchased(string itemKey, bool value)
        {
            var guide = GameplayGuideData;
            switch (itemKey)
            {
                case GuideKitchenStove:
                    guide.purchasedStove = value;
                    break;
                case GuideKitchenFurnace:
                    guide.purchasedFurnace = value;
                    break;
                case GuideKitchenWineCabinet:
                    guide.purchasedWineCabinet = value;
                    break;
                case GuideKitchenCabinet:
                    guide.purchasedCabinet = value;
                    break;
                case GuideKitchenTable1:
                    guide.purchasedKitchenTable1 = value;
                    break;
                case GuideKitchenTable2:
                    guide.purchasedKitchenTable2 = value;
                    break;
            }
        }

        /// <summary>
        /// 判断指定员工角色是否已经招聘。
        /// </summary>
        /// <param name="role">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private bool HasHiredStaffRole(StaffRole role)
        {
            var ownedStaff = SaveData?.gameplay?.ownedStaff;
            if (ownedStaff != null)
            {
                for (var index = 0; index < ownedStaff.Count; index++)
                {
                    var staffData = ownedStaff[index];
                    if (staffData == null || staffData.currentLevel <= 0)
                    {
                        continue;
                    }

                    var staff = FindGuideStaff(staffData.staffId, role, false);
                    if (staff != null && staff.role == role)
                    {
                        return true;
                    }
                }
            }

            var hiredStaffIds = SaveData?.gameplay?.hiredStaffIds;
            if (hiredStaffIds == null)
            {
                return false;
            }

            for (var index = 0; index < hiredStaffIds.Count; index++)
            {
                var staff = FindGuideStaff(hiredStaffIds[index], role, false);
                if (staff != null && staff.role == role)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断指定员工编号是否已经被当前存档招聘。
        /// </summary>
        /// <param name="preferredStaffId">员工配置编号。</param>
        /// <param name="role">员工角色，用于校验配置类型。</param>
        /// <returns>已经招聘该员工时返回 true，否则返回 false。</returns>
        private bool HasHiredGuideStaff(int preferredStaffId, StaffRole role)
        {
            return CountHiredGuideStaff(preferredStaffId, role) > 0;
        }

        /// <summary>
        /// 统计指定引导员工在当前存档中的招聘数量。
        /// </summary>
        /// <param name="preferredStaffId">员工配置编号。</param>
        /// <param name="role">员工角色，用于过滤同编号以外的兼容数据。</param>
        /// <returns>已招聘数量。</returns>
        private int CountHiredGuideStaff(int preferredStaffId, StaffRole role)
        {
            var count = 0;
            var ownedStaff = SaveData?.gameplay?.ownedStaff;
            if (ownedStaff != null)
            {
                for (var index = 0; index < ownedStaff.Count; index++)
                {
                    var staffData = ownedStaff[index];
                    if (staffData == null || staffData.currentLevel <= 0 || staffData.staffId != preferredStaffId)
                    {
                        continue;
                    }

                    var staff = FindGuideStaff(staffData.staffId, role, false);
                    if (staff != null && staff.role == role)
                    {
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                return count;
            }

            // 旧存档可能只写入 hiredStaffIds，至少按 1 个已招聘来兼容。
            var hiredStaffIds = SaveData?.gameplay?.hiredStaffIds;
            if (hiredStaffIds == null)
            {
                return 0;
            }

            for (var index = 0; index < hiredStaffIds.Count; index++)
            {
                if (hiredStaffIds[index] != preferredStaffId)
                {
                    continue;
                }

                var staff = FindGuideStaff(hiredStaffIds[index], role, false);
                if (staff != null && staff.role == role)
                {
                    return 1;
                }
            }

            return 0;
        }

        /// <summary>
        /// 查找引导员工。
        /// </summary>
        /// <param name="preferredStaffId">数据编号。</param>
        /// <param name="role">参数值。</param>
        /// <param name="allowRoleFallback">参数值。</param>
        /// <returns>返回方法执行后的结果。</returns>
        private static SO_Staff FindGuideStaff(int preferredStaffId, StaffRole role, bool allowRoleFallback = true)
        {
            var allStaff = SO_Staff.GetAll();
            SO_Staff fallback = null;
            for (var index = 0; index < allStaff.Count; index++)
            {
                var staff = allStaff[index];
                if (staff == null || staff.role != role)
                {
                    continue;
                }

                fallback ??= staff;

                if (int.TryParse(staff.staffId, out var numericId) && numericId == preferredStaffId)
                {
                    return staff;
                }
            }

            return allowRoleFallback ? fallback : null;
        }

        /// <summary>
        /// 获取引导设备购买花费。
        /// </summary>
        /// <param name="equipmentId">数据编号。</param>
        /// <returns>返回计算后的数值。</returns>
        private static int GetGuideEquipmentPurchaseCost(int equipmentId)
        {
            var equipment = SO_Equipment.GetById(equipmentId);
            var levelConfig = equipment != null ? equipment.GetLevelConfig(1) : null;
            return levelConfig != null ? Mathf.Max(0, levelConfig.upgradeCost) : 0;
        }
    }
}
