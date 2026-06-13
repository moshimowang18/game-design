using JN.Client.Model;
using UnityEngine;
namespace JN.Client.Manager
{
    [DefaultExecutionOrder(-100)]
    public class MinimalGameUI : MonoBehaviour
    {
        private const float ReferenceHeight = 1080f;
        private const float ReferenceWidth = 1920f;
        private const string HostObjectName = "[DayCycleUI]";

        private static MinimalGameUI s_instance;

        private int lastScreenWidth;
        private int lastScreenHeight;
        private float uiScale = 1f;
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle headerStyle;
        private GUIStyle buttonStyle;
        private bool stylesReady;
        /// <summary>
        /// 挂在场景相机上的实例会迁移到常驻 Host，避免切场景后 IMGUI 消失。
        /// </summary>
        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(this);
                return;
            }

            if (gameObject.name != HostObjectName)
            {
                var host = new GameObject(HostObjectName);
                DontDestroyOnLoad(host);
                host.AddComponent<MinimalGameUI>();
                Destroy(this);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (s_instance != this)
            {
                return;
            }

            DataManager.Instance.Init();

            var player = DataManager.Instance.PlayerData;
            if (player.CurrentDay <= 0)
            {
                player.coinNum = 30;
                player.CurrentDay = 1;
                player.UnlockedDishes.Clear();
                player.UnlockedDishes.Add("rice");
                player.UnlockedDishes.Add("tofu");
                DataManager.Instance.SaveGame();
            }

            EventSystemManager.Instance.Initialize();
            EnsureDemoData();

            // 早于 TavernSceneManager.Start，避免存档 isOpen=true 时客人抢先进场
            if (DataManager.Instance.SaveData?.tavern != null && DataManager.Instance.SaveData.tavern.isOpen)
            {
                DataManager.Instance.SetTavernOpen(false);
            }

            TavernDayManager.Instance.Init();
            TavernDayManager.Instance.StartNewDay(1);
        }

        private void OnGUI()
        {
            if (s_instance != null && s_instance != this)
            {
                return;
            }

            EnsureStyles();
            GUI.depth = 9999;

            var dayMgr = TavernDayManager.Instance;
            if (dayMgr == null)
            {
                return;
            }

            const float panelWidth = 500f;
            const float panelHeight = 800f;
            var panelRect = new Rect(20f, 20f, panelWidth, panelHeight);

            GUILayout.BeginArea(panelRect);
            var boxBg = new Color(0f, 0f, 0f, 0.55f);
            var previousColor = GUI.color;
            GUI.color = boxBg;
            GUI.Box(new Rect(0f, 0f, panelWidth, panelHeight), GUIContent.none);
            GUI.color = previousColor;

            GUILayout.Space(12f * uiScale);

            var player = DataManager.Instance.PlayerData;
            var dayData = dayMgr.CurrentDay;

            GUILayout.Label($"<b>📅 第{dayData.DayNumber}天/10</b>", headerStyle);
            GUILayout.Space(12f * uiScale);

            if (dayMgr.Phase == DayPhase.Preparation)
            {
                DrawPreparationPhase();
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

        private void DrawPreparationPhase()
        {
            GUILayout.Label("【准备阶段】UI在左侧黑色面板", new GUIStyle(GUI.skin.label) { fontSize = 14 });
            GUILayout.Space(10);
            if (GUILayout.Button("重置游戏（调试）", GUILayout.Height(30)))
            {
                var player = DataManager.Instance.PlayerData;
                if (player == null)
                {
                    return;
                }

                player.coinNum = 30;
                player.CurrentDay = 1;
                player.TavernLevel = 1;
                player.PurchasedTables = 0;
                player.ClearDishStock();
                player.UnlockedDishes.Clear();
                player.UnlockedDishes.Add("rice");
                player.UnlockedDishes.Add("tofu");
                player.Employees.Clear();
                DataManager.Instance.SaveGame();
                EnsureDemoData();
                TavernDayManager.Instance.StartNewDay(1);
            }
        }

        private void DrawOperationPhase()
        {
            var opMgr = OperationManager.Instance;

            GUILayout.Label("【营业阶段】", new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold });
            GUILayout.Label($"⏱ 剩余: {Mathf.CeilToInt(opMgr.TimeRemaining)}秒", bodyStyle);
            GUILayout.Label($"📊 客流: {opMgr.TotalCustomers}人 (满意{opMgr.SatisfiedCustomers}/生气{opMgr.NegativeEventCount})", bodyStyle);
            GUILayout.Label("→ 在3D场景里点击桌位完成结账", bodyStyle);

            GUILayout.Space(20f);
            if (GUILayout.Button("结束营业（调试）", buttonStyle, GUILayout.Height(48f)))
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

            stylesReady = true;
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
