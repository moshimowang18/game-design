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
        private float floatMoney;

        private void Start()
        {
            DataManager.Instance.Init();
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

            GUILayout.Label($"第 {dayData.DayNumber} 天  |  银两: {player.Money}  |  酒楼等级: {player.TavernLevel}", headerStyle);
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

            var evtId = EventSystemManager.Instance.GetTodaysEventId(dayData.DayNumber);
            var evt = EventSystemManager.Instance.GetEventById(evtId);
            if (evt != null)
            {
                GUILayout.Label($"今日事件: {evt.EventName}", bodyStyle);
                GUILayout.Label(evt.Description, bodyStyle);
                GUILayout.Label($"策略提示: {evt.StrategicHint}", bodyStyle);
                GUILayout.Label($"客流倍率: x{evt.GuestFlowModifier}    贵客概率: +{evt.VipProbModifier}", bodyStyle);
            }

            GUILayout.Space(16f * uiScale);
            GUILayout.Label($"已解锁菜品数: {player.UnlockedDishes.Count}", bodyStyle);
            GUILayout.Label($"员工数: {player.Employees.Count}", bodyStyle);
            GUILayout.Label($"客流倍率: x{dayData.GuestFlowMultiplier}", bodyStyle);

            GUILayout.Space(28f * uiScale);
            if (GUILayout.Button("开始营业！", buttonStyle, GUILayout.Height(88f * uiScale)))
            {
                TavernDayManager.Instance.EnterOperationPhase();
            }

            GUILayout.Space(20f * uiScale);
            var upgradeMgr = TavernUpgradeManager.Instance;
            var nextLevel = upgradeMgr.GetNextLevelData();
            if (nextLevel != null)
            {
                GUILayout.Label($"下一级: {nextLevel.Name} (费用: {nextLevel.UpgradeCost})", bodyStyle);
                if (upgradeMgr.CanUpgrade())
                {
                    if (GUILayout.Button($"扩建 ({nextLevel.UpgradeCost} 银两)", buttonStyle, GUILayout.Height(72f * uiScale)))
                    {
                        upgradeMgr.Upgrade();
                    }
                }
                else
                {
                    GUILayout.Label("银两不足", bodyStyle);
                }
            }
            else
            {
                GUILayout.Label("已满级", bodyStyle);
            }
        }

        private void DrawOperationPhase()
        {
            var opMgr = OperationManager.Instance;

            GUILayout.Label("【营业阶段】", titleStyle);
            GUILayout.Space(12f * uiScale);
            GUILayout.Label($"剩余时间: {Mathf.CeilToInt(opMgr.TimeRemaining)} 秒", bodyStyle);
            GUILayout.Label($"当前收入: {Mathf.RoundToInt(opMgr.CurrentRevenue)} 银两", bodyStyle);
            GUILayout.Label($"客人: {opMgr.TotalCustomers} 人 (满意: {opMgr.SatisfiedCustomers})", bodyStyle);
            GUILayout.Label($"负面事件: {opMgr.NegativeEventCount}", bodyStyle);

            if (floatMoney > 0f)
            {
                GUILayout.Label($"<color=yellow>+{Mathf.RoundToInt(floatMoney)} 银两!</color>", highlightStyle);
                floatMoney -= Time.deltaTime * 20f;
            }

            if (opMgr.LastErrorTimer > 0f)
            {
                GUILayout.Label($"<color=red>{opMgr.LastErrorMessage}</color>", highlightStyle);
            }

            GUILayout.Space(20f * uiScale);

            if (GUILayout.Button("收钱", buttonStyle, GUILayout.Height(80f * uiScale)))
            {
                floatMoney = UnityEngine.Random.Range(8f, 20f);
            }

            GUILayout.Space(16f * uiScale);

            var player = DataManager.Instance.PlayerData;
            GUILayout.Label("员工:", bodyStyle);
            for (int i = 0; i < player.Employees.Count; i++)
            {
                var emp = player.Employees[i];
                string staminaBar = new string('■', emp.CurrentStamina) + new string('□', emp.MaxStamina - emp.CurrentStamina);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{emp.Name} [{staminaBar}] {(emp.IsLounging ? "偷懒!" : "工作中")}", bodyStyle, GUILayout.ExpandWidth(true));
                if (emp.IsLounging && GUILayout.Button("踢!", smallButtonStyle, GUILayout.Width(100f * uiScale), GUILayout.Height(56f * uiScale)))
                {
                    emp.KickBackToWork();
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(8f * uiScale);
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

            if (player.Money <= 0 && player.coinNum <= 0)
            {
                player.Money = 5000;
                player.coinNum = 5000;
            }

            if (player.UnlockedDishes.Count == 0)
            {
                player.UnlockedDishes.Add("dish_01");
                player.UnlockedDishes.Add("dish_02");
                player.UnlockedDishes.Add("dish_03");
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
