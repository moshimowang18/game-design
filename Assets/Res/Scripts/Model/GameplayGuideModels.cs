using System;
using System.Collections.Generic;

namespace JN.Client.Model
{
    /// <summary>
    /// 定义玩法引导阶段枚举。
    /// </summary>
    public enum GameplayGuideStage
    {
        Build = 0,
        Recruit = 1,
        ReadyToOpen = 2,
        Running = 3
    }

    /// <summary>
    /// 定义玩法引导任务Id可用的枚举类型。
    /// </summary>
    public enum GameplayGuideTaskId
    {
        BuyCounter = 0,
        BuyTables = 1,
        BuyStove = 2,
        HireShopkeeper = 3,
        HireChef = 4,
        HireWaiter = 5,
        BuyCabinet = 6,
        BuyWineCabinet = 7,
        BuyKitchenTable1 = 8,
        BuyKitchenTable2 = 9,
        BuyFurnace = 10
    }

    /// <summary>
    /// 负责玩法引导存档数据相关的运行时逻辑。
    /// </summary>
    [Serializable]
    public class GameplayGuideSaveData
    {
        public GameplayGuideStage currentStage;
        public bool purchasedCounter;
        public int purchasedTableCount;
        public bool purchasedStove;
        public bool purchasedFurnace;
        public bool purchasedWineCabinet;
        public bool purchasedCabinet;
        public bool purchasedKitchenTable1;
        public bool purchasedKitchenTable2;
        public bool recruitmentUnlocked;
        public bool recruitmentUnlockToastShown;
        public bool hiredShopkeeper;
        public bool hiredChef;
        public bool hiredWaiter;
        public bool openingUnlocked;
        public bool onboardingCompleted;
    }

    /// <summary>
    /// 负责玩法引导任务进度相关的运行时逻辑。
    /// </summary>
    public sealed class GameplayGuideTaskProgress
    {
        public GameplayGuideTaskProgress(GameplayGuideTaskId taskId, string title, int current, int target)
        {
            TaskId = taskId;
            Title = title;
            Current = current;
            Target = target;
        }

        public GameplayGuideTaskId TaskId { get; }
        public string Title { get; }
        public int Current { get; }
        public int Target { get; }
        public bool IsCompleted => Current >= Target;
    }

    /// <summary>
    /// 负责玩法引导快照相关的运行时逻辑。
    /// </summary>
    public sealed class GameplayGuideSnapshot
    {
        public GameplayGuideStage Stage { get; set; }
        public bool RecruitmentUnlocked { get; set; }
        public bool CanOpenBusiness { get; set; }
        public bool OnboardingCompleted { get; set; }
        public List<GameplayGuideTaskProgress> ActiveTasks { get; set; } = new();
    }
}
