using JN.Client.Model;
using UnityEngine;

namespace JN.Client.Manager
{
    [DefaultExecutionOrder(100)]
    public class MinimalGameUI : MonoBehaviour
    {
        private const float ReferenceHeight = 1080f;
        private const float ReferenceWidth = 1920f;

        private int lastScreenWidth;
        private int lastScreenHeight;
        private float uiScale = 1f;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle headerStyle;
        private GUIStyle buttonStyle;
        private GUIStyle smallButtonStyle;
        private GUIStyle highlightStyle;
        private bool stylesReady;

        private void Start()
        {
            DataManager.Instance.Init();
            if (DataManager.Instance.PlayerData.coinNum <= 0)
            {
                DataManager.Instance.PlayerData.coinNum = 30;
            }

            EventSystemManager.Instance.Initialize();
            EnsureDemoData();

            TavernDayManager.Instance.Init();
            TavernDayManager.Instance.StartNewDay(1);
        }

        private void OnGUI()
        {
            EnsureStyles();

            float margin = 32f * uiScale;
            var area = new Rect(margin, margin, Screen.width - margin * 2f, Screen.height - margin * 2f);
            var bgColor = new Color(0f, 0f, 0f, 0.55f);
            DrawPanelBackground(area, bgColor);

            GUILayout.BeginArea(area);
            GUILayout.Space(16f * uiScale);

            var dayMgr = TavernDayManager.Instance;
            var player = DataManager.Instance.PlayerData;
            var dayData = dayMgr.CurrentDay;

            GUILayout.Label($"第 {dayData.DayNumber} 天  |  银两: {player.coinNum}  |  酒楼等级: {player.TavernLevel}", headerStyle);
            GUILayout.Space(20f * uiScale);

            if (dayMgr.Phase == DayPhase.Preparation)
            {
                DrawPreparationPhase(dayData, player);
            }
            else if (dayMgr.Phase == DayPhase.Operation)
            {
                DrawOperationPhase();
            }
            else if (dayMgr.Phase == DayPhase.Settlement)
            {
                DrawSettlementPhase(dayData, player);
            }

            GUILayout.EndArea();
        }

        private void DrawPreparationPhase(GameDayData dayData, PlayerModel player)
        {
            GUILayout.Label("【准备阶段】", titleStyle);
            GUILayout.Space(12f * uiScale);

            if (dayData.DayNumber == 1)
            {
                GUILayout.Label("<color=cyan>第1天提示：选菜要花进货费，扩建桌位才能接更多客人，开始营业前看好今日事件！</color>", bodyStyle);
                GUILayout.Space(12f * uiScale);
            }

            var evtId = EventSystemManager.Instance.GetTodaysEventId(dayData.DayNumber);
            var evt = EventSystemManager.Instance.GetEventById(evtId);
            if (evt != null)
            {
                GUILayout.Label($"今日事件: {evt.EventName}", bodyStyle);
                GUILayout.Label($"  策略提示: {evt.StrategicHint}", bodyStyle);
            }

            GUILayout.Space(16f * uiScale);
            DrawDishSelection(player);

            GUILayout.Space(16f * uiScale);
            GUILayout.Label($"桌位: {player.MaxTables}桌 (基础{player.BaseTables} + 扩建{player.PurchasedTables})", bodyStyle);

            int tableCost = player.TablePrice;
            GUILayout.Label($"购买下一桌需要: {tableCost} 银两", bodyStyle);
            if (player.coinNum >= tableCost)
            {
                if (GUILayout.Button($"添置桌位 ({tableCost} 银两)", buttonStyle, GUILayout.Height(72f * uiScale)))
                {
                    player.coinNum -= tableCost;
                    player.PurchasedTables++;
                    DataManager.Instance.SaveGame();
                }
            }
            else
            {
                GUILayout.Label("银两不足", bodyStyle);
            }

            GUILayout.Space(16f * uiScale);
            int kitchenUpgradeCost = player.TavernLevel * 100;
            GUILayout.Label($"厨房等级: {player.TavernLevel}", bodyStyle);
            if (player.TavernLevel < 3)
            {
                GUILayout.Label($"升级厨房需要: {kitchenUpgradeCost} 银两", bodyStyle);
                if (player.coinNum >= kitchenUpgradeCost)
                {
                    if (GUILayout.Button($"扩建厨房 ({kitchenUpgradeCost} 银两)", buttonStyle, GUILayout.Height(72f * uiScale)))
                    {
                        player.coinNum -= kitchenUpgradeCost;
                        TavernUpgradeManager.Instance.Upgrade();
                    }
                }
                else
                {
                    GUILayout.Label("银两不足", bodyStyle);
                }
            }
            else
            {
                GUILayout.Label("厨房已满级", bodyStyle);
            }

            GUILayout.Space(20f * uiScale);
            GUILayout.Label($"银两: {player.coinNum}", bodyStyle);

            GUILayout.Space(16f * uiScale);
            if (GUILayout.Button("开始营业！", buttonStyle, GUILayout.Height(88f * uiScale)))
            {
                TavernDayManager.Instance.EnterOperationPhase();
            }
        }

        private void DrawDishSelection(PlayerModel player)
        {
            var dishManager = EventSystemManager.Instance;
            GUILayout.Label($"明日菜品 (已选 {player.SelectedDishes.Count}/{player.MaxDishSlots} 槽位):", bodyStyle);

            foreach (var dish in dishManager.GetAllDishes())
            {
                if (dish == null)
                {
                    continue;
                }

                int requiredLevel = dishManager.GetRequiredKitchenLevel(dish.DishId);
                bool unlocked = player.UnlockedDishes.Contains(dish.DishId);
                string levelHint = requiredLevel > player.TavernLevel ? $" [{requiredLevel}级厨房]" : string.Empty;
                string eventTag = string.IsNullOrEmpty(dish.EventDishTag) ? string.Empty : $" [{dish.EventDishTag}]";
                string dishInfo = $"{dish.DishName} (售{Mathf.RoundToInt(dish.BasePrice)} 进{dish.IngredientCost} | {Mathf.RoundToInt(dish.CookTime)}秒{eventTag}){levelHint}";

                if (!unlocked)
                {
                    GUILayout.Label($"  ☐ {dishInfo}", bodyStyle);
                    continue;
                }

                bool selected = player.SelectedDishes.Contains(dish.DishId);
                GUILayout.BeginHorizontal();
                GUILayout.Label(selected ? $"  ☑ {dishInfo}" : $"  ☐ {dishInfo}", bodyStyle, GUILayout.ExpandWidth(true));

                if (selected)
                {
                    if (GUILayout.Button("取消", smallButtonStyle, GUILayout.Width(100f * uiScale), GUILayout.Height(56f * uiScale)))
                    {
                        player.coinNum += dish.IngredientCost;
                        player.SelectedDishes.Remove(dish.DishId);
                        DataManager.Instance.SaveGame();
                    }
                }
                else if (player.SelectedDishes.Count < player.MaxDishSlots)
                {
                    GUI.enabled = player.coinNum >= dish.IngredientCost;
                    if (GUILayout.Button("选择", smallButtonStyle, GUILayout.Width(100f * uiScale), GUILayout.Height(56f * uiScale)))
                    {
                        player.coinNum -= dish.IngredientCost;
                        player.SelectedDishes.Add(dish.DishId);
                        DataManager.Instance.SaveGame();
                    }

                    GUI.enabled = true;
                }

                GUILayout.EndHorizontal();
            }
        }

        private void DrawOperationPhase()
        {
            var opMgr = OperationManager.Instance;

            GUILayout.Label("【营业阶段】", titleStyle);
            GUILayout.Space(12f * uiScale);
            GUILayout.Label($"剩余时间: {Mathf.CeilToInt(opMgr.TimeRemaining)} 秒", bodyStyle);
            GUILayout.Label($"已收银: {Mathf.RoundToInt(opMgr.CurrentRevenue)} 银两", bodyStyle);
            GUILayout.Label($"总客流: {opMgr.TotalCustomers} 人 | 满意: {opMgr.SatisfiedCustomers} | 差评: {opMgr.NegativeEventCount}", bodyStyle);

            if (opMgr.LastErrorTimer > 0f)
            {
                GUILayout.Label($"<color=red>{opMgr.LastErrorMessage}</color>", highlightStyle);
            }

            GUILayout.Space(16f * uiScale);

            var player = DataManager.Instance.PlayerData;
            int maxTables = player.MaxTables;
            if (opMgr.UsedTables >= maxTables && opMgr.WaitingGuests > 0)
            {
                GUILayout.Label("桌位已满！", highlightStyle);
            }

            if (opMgr.WaitingGuests > 0)
            {
                GUILayout.Label($"等待中: {opMgr.WaitingGuests} 位客人", bodyStyle);
            }
            else
            {
                GUILayout.Label("暂无新客人", bodyStyle);
            }

            GUILayout.Label($"桌位: {opMgr.UsedTables}/{maxTables}", bodyStyle);

            if (opMgr.ActiveCustomerCount > 0)
            {
                GUILayout.Label($"用餐中: {opMgr.ActiveCustomerCount} 位", bodyStyle);
            }

            if (opMgr.FinishedCustomerCount > 0)
            {
                GUILayout.Label($"{opMgr.FinishedCustomerCount} 位客人吃完了！待收 {Mathf.RoundToInt(opMgr.PendingPayment)} 银两", bodyStyle);
                if (GUILayout.Button("收钱!", buttonStyle, GUILayout.Height(80f * uiScale)))
                {
                    opMgr.CollectMoney();
                }
            }

            GUILayout.Space(16f * uiScale);

            GUILayout.Label("员工:", bodyStyle);
            for (int i = 0; i < player.Employees.Count; i++)
            {
                var emp = player.Employees[i];
                string staminaBar = new string('■', emp.CurrentStamina) + new string('□', emp.MaxStamina - emp.CurrentStamina);
                string status = GetEmployeeStatus(emp);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{emp.Name} [{staminaBar}] {status}", bodyStyle, GUILayout.ExpandWidth(true));
                if (emp.IsLounging && GUILayout.Button("踢!", smallButtonStyle, GUILayout.Width(100f * uiScale), GUILayout.Height(56f * uiScale)))
                {
                    emp.KickBackToWork();
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(8f * uiScale);
            }

            GUILayout.Space(20f * uiScale);
            if (GUILayout.Button("结束营业（调试）", buttonStyle, GUILayout.Height(72f * uiScale)))
            {
                var result = opMgr.EndOperation();
                TavernDayManager.Instance.EnterSettlementPhase(result);
            }
        }

        private void DrawSettlementPhase(GameDayData dayData, PlayerModel player)
        {
            var result = DataManager.Instance.SaveData?.lastOperationResult;

            GUILayout.Label("【今日结算】", titleStyle);
            GUILayout.Space(12f * uiScale);

            if (result != null)
            {
                string stars = new string('*', result.StarRating);
                GUILayout.Label($"评级: {stars} ({result.StarRating} 星)", bodyStyle);
                GUILayout.Label($"总收入: {Mathf.RoundToInt(result.TotalRevenue)} 银两", bodyStyle);
                GUILayout.Label($"菜品满意度: {result.DishSatisfaction:P0}", bodyStyle);
                GUILayout.Label($"服务效率: {result.ServiceEfficiency:P0}", bodyStyle);
                GUILayout.Label($"环境加成: {result.EnvironmentBonus:P0}", bodyStyle);
                GUILayout.Label($"负面事件: {result.NegativeEvents}", bodyStyle);

                GUILayout.Space(16f * uiScale);
                GUILayout.Label("明日预览:", bodyStyle);
                string preview = result.StarRating >= 5 ? "口碑远播！" :
                                 result.StarRating >= 4 ? "声名鹊起" :
                                 result.StarRating >= 3 ? "门庭若市" :
                                 result.StarRating >= 2 ? "生意清淡" : "门可罗雀";
                GUILayout.Label(preview, bodyStyle);
            }

            GUILayout.Space(28f * uiScale);
            if (GUILayout.Button("进入下一天", buttonStyle, GUILayout.Height(88f * uiScale)))
            {
                int nextDay = dayData.DayNumber + 1;
                if (nextDay > 10)
                {
                    nextDay = 10;
                }

                TavernDayManager.Instance.StartNewDay(nextDay);
            }
        }

        private void EnsureStyles()
        {
            if (stylesReady && lastScreenWidth == Screen.width && lastScreenHeight == Screen.height)
            {
                return;
            }

            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            uiScale = Mathf.Clamp(
                Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight),
                1f,
                2.5f);

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(42f * uiScale),
                fontStyle = FontStyle.Bold,
                richText = true,
                wordWrap = true
            };

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(30f * uiScale),
                fontStyle = FontStyle.Bold,
                richText = true,
                wordWrap = true
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(26f * uiScale),
                richText = true,
                wordWrap = true
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(28f * uiScale),
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };

            smallButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = Mathf.RoundToInt(22f * uiScale),
                wordWrap = true
            };

            highlightStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(32f * uiScale),
                fontStyle = FontStyle.Bold,
                richText = true,
                wordWrap = true
            };

            stylesReady = true;
        }

        private static string GetEmployeeStatus(EmployeeData emp)
        {
            if (emp.IsLounging && emp.CurrentStamina <= 0)
            {
                return "😴休息中";
            }

            if (emp.IsLounging && emp.CurrentStamina > 0)
            {
                return "💤摸鱼!";
            }

            if (emp.CurrentStamina <= 1)
            {
                return "😰低体力";
            }

            return "👷工作中";
        }

        private static void DrawPanelBackground(Rect area, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.Box(area, GUIContent.none);
            GUI.color = previous;
        }

        private static void EnsureDemoData()
        {
            var player = DataManager.Instance.PlayerData;
            if (player == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(player.playerName))
            {
                player.playerName = "测试掌柜";
            }

            if (player.coinNum <= 0)
            {
                player.coinNum = 30;
            }

            EventSystemManager.Instance.UnlockDishesForKitchenLevel(player.TavernLevel, player);
            if (player.UnlockedDishes.Count == 0)
            {
                player.UnlockedDishes.Add("rice");
                player.UnlockedDishes.Add("tofu");
            }

            if (player.Employees.Count == 0)
            {
                player.Employees.Add(new EmployeeData
                {
                    EmployeeId = 1,
                    Name = "小二阿福",
                    CurrentStamina = 3,
                    MaxStamina = 3
                });
                player.Employees.Add(new EmployeeData
                {
                    EmployeeId = 2,
                    Name = "厨师老王",
                    CurrentStamina = 2,
                    MaxStamina = 3,
                    IsLounging = true
                });
            }
        }
    }
}
