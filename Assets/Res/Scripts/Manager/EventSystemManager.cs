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
        /// 初始化事件表与默认剧本（第 5 批将改为 Luban 驱动）。
        /// </summary>
        public void Initialize()
        {
            eventSchedule.Clear();
            eventDefinitions.Clear();

            RegisterEvent(new DailyEvent
            {
                EventId = "none",
                EventName = "无事件",
                Description = "今日风平浪静。",
                StrategicHint = "熟悉基础操作即可。",
                GuestFlowModifier = 1f
            });

            RegisterEvent(new DailyEvent
            {
                EventId = "small_festival",
                EventName = "小庙会",
                Description = "城里举办小庙会，客流略有增加。",
                StrategicHint = "适当多备菜品。",
                GuestFlowModifier = 1.15f,
                VipProbModifier = 0.05f
            });

            eventSchedule[1] = "none";
            eventSchedule[2] = "small_festival";
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
