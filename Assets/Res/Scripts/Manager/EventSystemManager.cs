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

        /// <summary>
        /// 初始化 10 日固定事件剧本。
        /// </summary>
        public void Initialize()
        {
            eventSchedule.Clear();
            eventDefinitions.Clear();

            RegisterEvent(new DailyEvent
            {
                EventId = "none",
                EventName = "风平浪静",
                Description = "今日风平浪静",
                StrategicHint = "熟悉基础操作即可",
                GuestFlowModifier = 1.0f,
                VipProbModifier = 0f,
                CustomerPatienceMod = 1.0f,
                DishPriceModifier = 1.0f
            });

            RegisterEvent(new DailyEvent
            {
                EventId = "small_festival",
                EventName = "小庙会",
                Description = "城里小庙会客流略增",
                StrategicHint = "适当多备菜品",
                GuestFlowModifier = 1.15f,
                VipProbModifier = 0.05f,
                CustomerPatienceMod = 1.0f,
                DishPriceModifier = 1.0f
            });

            RegisterEvent(new DailyEvent
            {
                EventId = "rainstorm",
                EventName = "暴雨",
                Description = "大雨倾盆客人稀少但贵客多",
                StrategicHint = "留雅间给贵客砍大众菜",
                GuestFlowModifier = 0.7f,
                VipProbModifier = 0.3f,
                CustomerPatienceMod = 0.8f,
                DishPriceModifier = 1.1f,
                SpecialDishTag = "热汤"
            });

            RegisterEvent(new DailyEvent
            {
                EventId = "storyteller",
                EventName = "说书人来",
                Description = "说书先生驻场客人愿意等",
                StrategicHint = "可以上慢菜和高价菜",
                GuestFlowModifier = 1.0f,
                VipProbModifier = 0.1f,
                CustomerPatienceMod = 2.0f,
                DishPriceModifier = 1.0f
            });

            RegisterEvent(new DailyEvent
            {
                EventId = "inspection",
                EventName = "官府巡查",
                Description = "官差巡视需打点",
                StrategicHint = "留足银两或备好酒菜",
                GuestFlowModifier = 0.9f,
                VipProbModifier = 0.15f,
                CustomerPatienceMod = 0.9f,
                DishPriceModifier = 1.0f
            });

            RegisterEvent(new DailyEvent
            {
                EventId = "caravan",
                EventName = "商队过境",
                Description = "异域商队带来稀罕食材",
                StrategicHint = "抢购食材推新菜",
                GuestFlowModifier = 1.1f,
                VipProbModifier = 0.1f,
                CustomerPatienceMod = 1.0f,
                DishPriceModifier = 0.9f,
                SpecialDishTag = "异域"
            });

            RegisterEvent(new DailyEvent
            {
                EventId = "grand_festival",
                EventName = "大庙会",
                Description = "全城庙会客流暴增",
                StrategicHint = "多备快菜少上慢菜",
                GuestFlowModifier = 1.5f,
                VipProbModifier = 0.1f,
                CustomerPatienceMod = 0.7f,
                DishPriceModifier = 1.0f
            });

            RegisterEvent(new DailyEvent
            {
                EventId = "plague_rumor",
                EventName = "瘟疫传闻",
                Description = "瘟疫传闻四起药膳需求暴增",
                StrategicHint = "转做药膳应对",
                GuestFlowModifier = 0.5f,
                VipProbModifier = 0.05f,
                CustomerPatienceMod = 1.0f,
                DishPriceModifier = 1.3f,
                SpecialDishTag = "药膳"
            });

            RegisterEvent(new DailyEvent
            {
                EventId = "vip_visit",
                EventName = "贵客驾到",
                Description = "有贵客慕名而来",
                StrategicHint = "确保雅间空置备好高档菜",
                GuestFlowModifier = 0.9f,
                VipProbModifier = 0.6f,
                CustomerPatienceMod = 1.0f,
                DishPriceModifier = 1.0f,
                SpecialDishTag = "高档"
            });

            RegisterEvent(new DailyEvent
            {
                EventId = "city_feast",
                EventName = "全城宴席",
                Description = "城中庆典酒楼人满为患",
                StrategicHint = "全力备菜这是最终考验",
                GuestFlowModifier = 1.8f,
                VipProbModifier = 0.2f,
                CustomerPatienceMod = 0.6f,
                DishPriceModifier = 1.0f
            });

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
    }
}
