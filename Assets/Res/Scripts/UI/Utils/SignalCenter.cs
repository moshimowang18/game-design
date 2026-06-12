namespace JN.Client
{
    /// <summary>
    /// 定义语言切换信号。
    /// </summary>
    public class LanguageSwitchSignal : ASignal
    {
    }

    #region UI

    /// <summary>
    /// 定义金币数量刷新信号。
    /// </summary>
    public class UpdateCoinNumSignal : ASignal<int>
    {
    }

    /// <summary>
    /// 定义建筑开始建造信号。
    /// </summary>
    public class StartBuildingSignal : ASignal<int>
    {
    }

    /// <summary>
    /// 负责到达桌位信号相关的运行时逻辑。
    /// </summary>
    public class ArrivedTableSignal : ASignal<int>
    {
    }

    /// <summary>
    /// 负责桌位数量信号相关的运行时逻辑。
    /// </summary>
    public class TableNumSignal : ASignal
    {
    }

    /// <summary>
    /// 负责酒楼营业状态信号相关的运行时逻辑。
    /// </summary>
    public class TavernBusinessStateSignal : ASignal<bool>
    {
    }

    /// <summary>
    /// 负责酒楼运行时变化信号相关的运行时逻辑。
    /// </summary>
    public class TavernRuntimeChangedSignal : ASignal
    {
    }

    /// <summary>
    /// 负责玩法引导进度信号相关的运行时逻辑。
    /// </summary>
    public class GameplayGuideProgressSignal : ASignal
    {
    }

    /// <summary>
    /// 3D客人结账成功时触发（来自老系统 CompleteCheckout）。
    /// 用于新系统评分统计：满意客人数、真实收入。
    /// </summary>
    public class TavernCustomerCheckoutSignal : ASignal
    {
        public int TableId;
        public int GroupSize;
        public int Income;

        public TavernCustomerCheckoutSignal Set(int tableId, int groupSize, int income)
        {
            TableId = tableId;
            GroupSize = groupSize;
            Income = income;
            return this;
        }
    }

    /// <summary>
    /// 3D客人点单超时生气离店时触发（来自老系统 HandleTableOrderTimeout）。
    /// 用于新系统评分统计：差评/负面事件。
    /// </summary>
    public class TavernCustomerAngryLeaveSignal : ASignal
    {
        public int TableId;
        public int GroupSize;

        public TavernCustomerAngryLeaveSignal Set(int tableId, int groupSize)
        {
            TableId = tableId;
            GroupSize = groupSize;
            return this;
        }
    }

    #endregion
}
