using System;

namespace JN.Client.Model
{
    /// <summary>
    /// 菜品运行时数据。
    /// </summary>
    [Serializable]
    public class DishData
    {
        public string DishId = string.Empty;
        public string DishName = string.Empty;
        public float BasePrice;
        public float CookTime;
        public string TargetGuestType = string.Empty;
        public bool IsUnlocked;
        public int UnlockCost;
        public int IngredientCost;
        public string EventDishTag = string.Empty;
    }
}
