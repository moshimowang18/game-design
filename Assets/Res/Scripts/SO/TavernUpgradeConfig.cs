using System;
using UnityEngine;

namespace JN.Client.Model
{
    [CreateAssetMenu(menuName = "Config/TavernUpgrade")]
    public class TavernUpgradeConfig : ScriptableObject
    {
        public TavernLevelData[] Levels;
    }

    [Serializable]
    public class TavernLevelData
    {
        public int Level;
        public string Name;
        public int UpgradeCost;
        public int MaxEmployees;
        public int UnlockDishSlots;
        public bool HasVipRoom;
        public int VipRoomCount;
        public float EnvironmentBonus;
        public string Description;
    }
}
