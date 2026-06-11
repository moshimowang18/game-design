using System;

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

        public PlayerModel()
        {
            playerId = string.Empty;
            playerName = string.Empty;
            coinNum = 0;
            createdAtUtcTicks = 0;
            buildId = 0;
        }
    }
}
