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

    #endregion
}
