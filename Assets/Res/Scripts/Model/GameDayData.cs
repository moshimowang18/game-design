using System;

namespace JN.Client.Model
{
    /// <summary>
    /// 当前游戏日的运行时数据。
    /// </summary>
    [Serializable]
    public class GameDayData
    {
        public int DayNumber;
        public DayPhase CurrentPhase;
        public string EventId;
        public float OperationTimeLimit;
        public float GuestFlowMultiplier = 1f;
        public float VipProbabilityBonus;

        public GameDayData()
        {
            DayNumber = 1;
            CurrentPhase = DayPhase.Preparation;
            EventId = string.Empty;
            OperationTimeLimit = 120f;
            GuestFlowMultiplier = 1f;
            VipProbabilityBonus = 0f;
        }
    }
}
