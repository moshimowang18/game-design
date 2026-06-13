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
        public const int MaxEmployeeCount = 3;

        public string playerId;
        public string playerName;
        public int coinNum;
        public int buildId;
        public long createdAtUtcTicks;

        public int CurrentDay;
        public List<EmployeeData> Employees = new();
        public List<string> UnlockedDishes = new();
        public List<string> SelectedDishes = new();
        public int TavernLevel = 1;
        public bool HasVipRoom;
        public int PurchasedTables;

        /// <summary>
        /// 菜品库存（备菜系统）。Key=DishId, Value=库存份数。
        /// 准备阶段花钱备菜+1，营业阶段每卖出一份-1。
        /// 营业结束后清零（隔夜变质）。
        /// </summary>
        public Dictionary<string, int> DishStock = new Dictionary<string, int>();

        public float EnvironmentBonus => TavernLevel * 0.1f;
        public int BaseTables => 2;
        public int MaxTables => BaseTables + PurchasedTables;
        public int TablePrice => 50 + PurchasedTables * 30;
        public int MaxDishSlots => TavernLevel + 1;

        public PlayerModel()
        {
            playerId = string.Empty;
            playerName = string.Empty;
            coinNum = 0;
            createdAtUtcTicks = 0;
            buildId = 0;
            CurrentDay = 0;
            Employees = new List<EmployeeData>();
            UnlockedDishes = new List<string>();
            SelectedDishes = new List<string>();
            TavernLevel = 1;
            HasVipRoom = false;
            PurchasedTables = 0;
            DishStock = new Dictionary<string, int>();
        }

        /// <summary>
        /// 获取某道菜的当前库存。
        /// </summary>
        public int GetDishStock(string dishId)
        {
            if (DishStock == null)
            {
                DishStock = new Dictionary<string, int>();
            }

            return DishStock.TryGetValue(dishId, out var v) ? v : 0;
        }

        /// <summary>
        /// 增加某道菜的库存（备菜）。
        /// </summary>
        public void AddDishStock(string dishId, int amount = 1)
        {
            if (DishStock == null)
            {
                DishStock = new Dictionary<string, int>();
            }

            if (DishStock.ContainsKey(dishId))
            {
                DishStock[dishId] += amount;
            }
            else
            {
                DishStock[dishId] = amount;
            }
        }

        /// <summary>
        /// 消耗某道菜的库存（卖出）。返回是否消耗成功。
        /// </summary>
        public bool ConsumeDishStock(string dishId, int amount = 1)
        {
            if (DishStock == null || !DishStock.ContainsKey(dishId))
            {
                return false;
            }

            if (DishStock[dishId] < amount)
            {
                return false;
            }

            DishStock[dishId] -= amount;
            if (DishStock[dishId] <= 0)
            {
                DishStock.Remove(dishId);
            }

            return true;
        }

        /// <summary>
        /// 获取所有库存总数（用于同步给老系统 availableDishes）。
        /// </summary>
        public int GetTotalDishStock()
        {
            if (DishStock == null)
            {
                return 0;
            }

            var total = 0;
            foreach (var kv in DishStock)
            {
                total += kv.Value;
            }

            return total;
        }

        /// <summary>
        /// 清零库存（隔夜变质）。
        /// </summary>
        public void ClearDishStock()
        {
            if (DishStock != null)
            {
                DishStock.Clear();
            }
        }
    }
}
