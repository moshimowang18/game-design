using System;
using System.Collections.Generic;

namespace JN.Client.Model
{
    [Serializable]
    /// <summary>
    /// 负责玩家模型相关的运行时逻辑。
    /// </summary>
    public class PlayerModel
    {
        public string playerId;
        public string playerName;
        public int coinNum;
        public int buildId;
        public long createdAtUtcTicks;

        public int CurrentDay = 1;
        public int Money;
        public List<EmployeeData> Employees = new();
        public List<string> UnlockedDishes = new();
        public int TavernLevel = 1;
        public bool HasVipRoom;

        public float EnvironmentBonus => TavernLevel * 0.1f;

        public PlayerModel()
        {
            playerId = string.Empty;
            playerName = string.Empty;
            coinNum = 0;
            createdAtUtcTicks = 0;
            buildId = 0;
            CurrentDay = 1;
            Money = 0;
            Employees = new List<EmployeeData>();
            UnlockedDishes = new List<string>();
            TavernLevel = 1;
            HasVipRoom = false;
        }
    }
}
