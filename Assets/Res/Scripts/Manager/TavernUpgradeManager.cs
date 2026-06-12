using JN.Client.Model;
using QFramework;
using UnityEngine;

namespace JN.Client.Manager
{
    public struct TavernUpgradedEvent
    {
        public int NewLevel;
        public TavernLevelData LevelData;
    }

    /// <summary>
    /// 酒楼扩建升级管理。
    /// </summary>
    [MonoSingletonPath("[Manager]/TavernUpgradeManager")]
    public class TavernUpgradeManager : MonoSingleton<TavernUpgradeManager>
    {
        private const string ConfigResourcePath = "Config/TavernUpgradeConfig";

        [SerializeField] private TavernUpgradeConfig upgradeConfig;

        private TavernUpgradeConfig Config
        {
            get
            {
                if (upgradeConfig == null)
                {
                    upgradeConfig = Resources.Load<TavernUpgradeConfig>(ConfigResourcePath);
                }

                return upgradeConfig;
            }
        }

        public int GetCurrentLevel()
        {
            DataManager.Instance.Init();
            return DataManager.Instance.PlayerData?.TavernLevel ?? 1;
        }

        public TavernLevelData GetCurrentLevelData()
        {
            return GetLevelData(GetCurrentLevel());
        }

        public TavernLevelData GetNextLevelData()
        {
            return GetLevelData(GetCurrentLevel() + 1);
        }

        public void Upgrade()
        {
            var player = DataManager.Instance.PlayerData;
            if (player == null || player.TavernLevel >= 3)
            {
                return;
            }

            player.TavernLevel++;
            EventSystemManager.Instance.UnlockDishesForKitchenLevel(player.TavernLevel, player);

            var levelData = GetCurrentLevelData();
            if (levelData != null)
            {
                player.HasVipRoom = levelData.HasVipRoom;
            }

            DataManager.Instance.SaveGame();
            TypeEventSystem.Global.Send(new TavernUpgradedEvent
            {
                NewLevel = player.TavernLevel,
                LevelData = levelData
            });
        }

        private TavernLevelData GetLevelData(int level)
        {
            var levels = Config?.Levels;
            if (levels == null || levels.Length == 0)
            {
                return null;
            }

            foreach (var levelData in levels)
            {
                if (levelData != null && levelData.Level == level)
                {
                    return levelData;
                }
            }

            return null;
        }
    }
}
