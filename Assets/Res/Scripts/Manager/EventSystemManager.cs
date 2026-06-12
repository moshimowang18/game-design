using System;
using System.Collections.Generic;
using JN.Client.Model;
using QFramework;
using UnityEngine;

namespace JN.Client.Manager
{
    /// <summary>
    /// 每日事件配置数据。
    /// </summary>
    [Serializable]
    public class DailyEvent
    {
        public string EventId;
        public string EventName;
        public string Description;
        public string StrategicHint;
        public float GuestFlowModifier = 1f;
        public float VipProbModifier;
        public float CustomerPatienceMod = 1f;
        public float DishPriceModifier = 1f;
        public string SpecialDishTag = string.Empty;
    }

    /// <summary>
    /// 负责日事件调度与效果应用。
    /// </summary>
    [MonoSingletonPath("[Manager]/EventSystemManager")]
    public class EventSystemManager : MonoSingleton<EventSystemManager>
    {
        private readonly Dictionary<int, string> eventSchedule = new();
        private readonly Dictionary<string, DailyEvent> eventDefinitions = new();
        private readonly Dictionary<string, DishData> dishDefinitions = new();

        private static readonly string[] DishOrder =
        {
            "rice", "tofu", "fish", "herb_soup", "birdnest", "exotic_meat"
        };

        /// <summary>
        /// 初始化 10 日固定事件剧本。
        /// </summary>
        public void Initialize()
        {
            eventSchedule.Clear();
            eventDefinitions.Clear();
            dishDefinitions.Clear();

            RegisterDish(new DishData { DishId = "rice", DishName = "白米饭", BasePrice = 5f, CookTime = 3f, TargetGuestType = "all", IsUnlocked = true, UnlockCost = 0, IngredientCost = 2, EventDishTag = "快菜" });
            RegisterDish(new DishData { DishId = "tofu", DishName = "麻婆豆腐", BasePrice = 12f, CookTime = 8f, TargetGuestType = "all", IsUnlocked = true, UnlockCost = 0, IngredientCost = 5, EventDishTag = "" });
            RegisterDish(new DishData { DishId = "fish", DishName = "清蒸鲈鱼", BasePrice = 25f, CookTime = 12f, TargetGuestType = "all", IsUnlocked = false, UnlockCost = 0, IngredientCost = 12, EventDishTag = "" });
            RegisterDish(new DishData { DishId = "herb_soup", DishName = "药膳鸡汤", BasePrice = 35f, CookTime = 14f, TargetGuestType = "all", IsUnlocked = false, UnlockCost = 0, IngredientCost = 18, EventDishTag = "药膳" });
            RegisterDish(new DishData { DishId = "birdnest", DishName = "燕窝羹", BasePrice = 50f, CookTime = 18f, TargetGuestType = "vip", IsUnlocked = false, UnlockCost = 0, IngredientCost = 25, EventDishTag = "高档" });
            RegisterDish(new DishData { DishId = "exotic_meat", DishName = "西域烤羊腿", BasePrice = 45f, CookTime = 16f, TargetGuestType = "all", IsUnlocked = false, UnlockCost = 0, IngredientCost = 22, EventDishTag = "异域" });

            RegisterEvent(new DailyEvent { EventId = "none", EventName = "风平浪静", Description = "今日风平浪静", StrategicHint = "熟悉基础操作即可", GuestFlowModifier = 1f, VipProbModifier = 0f, CustomerPatienceMod = 1f, DishPriceModifier = 1f, SpecialDishTag = "" });
            RegisterEvent(new DailyEvent { EventId = "small_festival", EventName = "小庙会", Description = "城里小庙会客流略增", StrategicHint = "适当多备菜品", GuestFlowModifier = 1.15f, VipProbModifier = 0.05f, CustomerPatienceMod = 1f, DishPriceModifier = 1f, SpecialDishTag = "" });
            RegisterEvent(new DailyEvent { EventId = "rainstorm", EventName = "暴雨", Description = "大雨倾盆客人稀少但贵客多", StrategicHint = "留雅间给贵客砍大众菜", GuestFlowModifier = 0.7f, VipProbModifier = 0.3f, CustomerPatienceMod = 0.8f, DishPriceModifier = 1.1f, SpecialDishTag = "热汤" });
            RegisterEvent(new DailyEvent { EventId = "storyteller", EventName = "说书人来", Description = "说书先生驻场客人愿意等", StrategicHint = "可以上慢菜和高价菜", GuestFlowModifier = 1f, VipProbModifier = 0.1f, CustomerPatienceMod = 2f, DishPriceModifier = 1f, SpecialDishTag = "" });
            RegisterEvent(new DailyEvent { EventId = "inspection", EventName = "官府巡查", Description = "官差巡视需打点", StrategicHint = "留足银两或备好酒菜", GuestFlowModifier = 0.9f, VipProbModifier = 0.15f, CustomerPatienceMod = 0.9f, DishPriceModifier = 1f, SpecialDishTag = "" });
            RegisterEvent(new DailyEvent { EventId = "caravan", EventName = "商队过境", Description = "异域商队带来稀罕食材", StrategicHint = "抢购食材推新菜", GuestFlowModifier = 1.1f, VipProbModifier = 0.1f, CustomerPatienceMod = 1f, DishPriceModifier = 0.9f, SpecialDishTag = "异域" });
            RegisterEvent(new DailyEvent { EventId = "grand_festival", EventName = "大庙会", Description = "全城庙会客流暴增", StrategicHint = "多备快菜少上慢菜", GuestFlowModifier = 1.5f, VipProbModifier = 0.1f, CustomerPatienceMod = 0.7f, DishPriceModifier = 1f, SpecialDishTag = "" });
            RegisterEvent(new DailyEvent { EventId = "plague_rumor", EventName = "瘟疫传闻", Description = "瘟疫传闻四起药膳需求暴增", StrategicHint = "转做药膳应对", GuestFlowModifier = 0.5f, VipProbModifier = 0.05f, CustomerPatienceMod = 1f, DishPriceModifier = 1.3f, SpecialDishTag = "药膳" });
            RegisterEvent(new DailyEvent { EventId = "vip_visit", EventName = "贵客驾到", Description = "有贵客慕名而来", StrategicHint = "确保雅间空置备好高档菜", GuestFlowModifier = 0.9f, VipProbModifier = 0.6f, CustomerPatienceMod = 1f, DishPriceModifier = 1f, SpecialDishTag = "高档" });
            RegisterEvent(new DailyEvent { EventId = "city_feast", EventName = "全城宴席", Description = "城中庆典酒楼人满为患", StrategicHint = "全力备菜这是最终考验", GuestFlowModifier = 1.8f, VipProbModifier = 0.2f, CustomerPatienceMod = 0.6f, DishPriceModifier = 1f, SpecialDishTag = "" });

            eventSchedule[1] = "none";
            eventSchedule[2] = "small_festival";
            eventSchedule[3] = "rainstorm";
            eventSchedule[4] = "storyteller";
            eventSchedule[5] = "inspection";
            eventSchedule[6] = "caravan";
            eventSchedule[7] = "grand_festival";
            eventSchedule[8] = "plague_rumor";
            eventSchedule[9] = "vip_visit";
            eventSchedule[10] = "city_feast";
        }

        public IReadOnlyList<DishData> GetAllDishes()
        {
            var dishes = new List<DishData>();
            foreach (var dishId in DishOrder)
            {
                if (dishDefinitions.TryGetValue(dishId, out var dish))
                {
                    dishes.Add(dish);
                }
            }

            return dishes;
        }

        public DishData GetDishById(string dishId)
        {
            if (!string.IsNullOrWhiteSpace(dishId) && dishDefinitions.TryGetValue(dishId, out var dish))
            {
                return dish;
            }

            return null;
        }

        public int GetRequiredKitchenLevel(string dishId)
        {
            return dishId switch
            {
                "rice" or "tofu" => 1,
                "fish" or "herb_soup" => 2,
                "birdnest" or "exotic_meat" => 3,
                _ => 99
            };
        }

        public void UnlockDishesForKitchenLevel(int kitchenLevel, PlayerModel player)
        {
            if (player == null)
            {
                return;
            }

            foreach (var dish in dishDefinitions.Values)
            {
                bool unlocked = kitchenLevel >= GetRequiredKitchenLevel(dish.DishId);
                dish.IsUnlocked = unlocked;
                if (unlocked && !player.UnlockedDishes.Contains(dish.DishId))
                {
                    player.UnlockedDishes.Add(dish.DishId);
                }
            }

            player.UnlockedDishes.RemoveAll(id => GetRequiredKitchenLevel(id) > kitchenLevel);
            player.SelectedDishes.RemoveAll(id => !player.UnlockedDishes.Contains(id));
        }

        /// <summary>
        /// 获取指定天数的事件 ID。
        /// </summary>
        public string GetTodaysEventId(int dayNumber)
        {
            if (eventSchedule.TryGetValue(dayNumber, out var eventId) && !string.IsNullOrWhiteSpace(eventId))
            {
                return eventId;
            }

            return "none";
        }

        /// <summary>
        /// 按 ID 获取事件配置。
        /// </summary>
        public DailyEvent GetEventById(string id)
        {
            if (!string.IsNullOrWhiteSpace(id) && eventDefinitions.TryGetValue(id, out var dailyEvent))
            {
                return dailyEvent;
            }

            return eventDefinitions.TryGetValue("none", out var fallback) ? fallback : null;
        }

        /// <summary>
        /// 将事件效果写入当日数据。
        /// </summary>
        public void ApplyEventEffects(DailyEvent evt, GameDayData dayData)
        {
            if (evt == null || dayData == null)
            {
                return;
            }

            dayData.EventId = evt.EventId;
            dayData.GuestFlowMultiplier = Mathf.Max(0.1f, evt.GuestFlowModifier);
            dayData.VipProbabilityBonus = evt.VipProbModifier;
        }

        private void RegisterEvent(DailyEvent dailyEvent)
        {
            if (dailyEvent == null || string.IsNullOrWhiteSpace(dailyEvent.EventId))
            {
                return;
            }

            eventDefinitions[dailyEvent.EventId] = dailyEvent;
        }

        private void RegisterDish(DishData dish)
        {
            if (dish == null || string.IsNullOrWhiteSpace(dish.DishId))
            {
                return;
            }

            dishDefinitions[dish.DishId] = dish;
        }
    }
}
