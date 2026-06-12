using JN.Client.Model;
using UnityEngine;

namespace JN.Client.Manager
{
    [DefaultExecutionOrder(100)]
    public class MinimalGameUI : MonoBehaviour
    {
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
            GUILayout.BeginArea(new Rect(20, 20, 500, 800));

            var dayMgr = TavernDayManager.Instance;
            var player = DataManager.Instance.PlayerData;
            var dayData = dayMgr.CurrentDay;

            GUILayout.Label($"<b>第 {dayData.DayNumber} 天</b>  |  银两: {player.Money}  |  酒楼等级: {player.TavernLevel}");
            GUILayout.Space(10);

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
            GUILayout.Label("【准备阶段】", new GUIStyle(GUI.skin.label) { fontSize = 20 });

            var evtId = EventSystemManager.Instance.GetTodaysEventId(dayData.DayNumber);
            var evt = EventSystemManager.Instance.GetEventById(evtId);
            if (evt != null)
            {
                GUILayout.Label($"今日事件: {evt.EventName}");
                GUILayout.Label($"  {evt.Description}");
                GUILayout.Label($"  策略提示: {evt.StrategicHint}");
                GUILayout.Label($"  客流倍率: x{evt.GuestFlowModifier}  贵客概率: +{evt.VipProbModifier}");
            }

            GUILayout.Space(10);
            GUILayout.Label($"已解锁菜品数: {player.UnlockedDishes.Count}");
            GUILayout.Label($"员工数: {player.Employees.Count}");
            GUILayout.Label($"客流倍率: x{dayData.GuestFlowMultiplier}");

            GUILayout.Space(20);
            if (GUILayout.Button("开始营业！", GUILayout.Height(50)))
            {
                TavernDayManager.Instance.EnterOperationPhase();
            }

            GUILayout.Space(10);
            var upgradeMgr = TavernUpgradeManager.Instance;
            var nextLevel = upgradeMgr.GetNextLevelData();
            if (nextLevel != null)
            {
                GUILayout.Label($"下一级: {nextLevel.Name} (费用: {nextLevel.UpgradeCost})");
                if (upgradeMgr.CanUpgrade())
                {
                    if (GUILayout.Button($"扩建 ({nextLevel.UpgradeCost}银两)"))
                    {
                        upgradeMgr.Upgrade();
                    }
                }
                else
                {
                    GUILayout.Label("银两不足");
                }
            }
            else
            {
                GUILayout.Label("已满级");
            }
        }

        private void DrawOperationPhase()
        {
            var opMgr = OperationManager.Instance;

            GUILayout.Label("【营业阶段】", new GUIStyle(GUI.skin.label) { fontSize = 20 });
            GUILayout.Label($"剩余时间: {Mathf.CeilToInt(opMgr.TimeRemaining)}秒");
            GUILayout.Label($"当前收入: {Mathf.RoundToInt(opMgr.CurrentRevenue)}银两");
            GUILayout.Label($"客人: {opMgr.TotalCustomers}人 (满意: {opMgr.SatisfiedCustomers})");
            GUILayout.Label($"负面事件: {opMgr.NegativeEventCount}");
            GUILayout.Label($"待接波次: {opMgr.PendingWaveCount}");

            GUILayout.Space(10);

            if (GUILayout.Button("收钱", GUILayout.Height(40)))
            {
                opMgr.OnMoneyCollected(10f);
                opMgr.OnCustomerServed(new CustomerData { PartySize = 1, TipMultiplier = 1f });
            }

            GUILayout.Space(5);

            var player = DataManager.Instance.PlayerData;
            GUILayout.Label("员工状态:");
            for (int i = 0; i < player.Employees.Count; i++)
            {
                var emp = player.Employees[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"  {emp.Name} 体力:{emp.CurrentStamina}/{emp.MaxStamina} {(emp.IsLounging ? "偷懒" : "工作中")}");
                if (emp.IsLounging && GUILayout.Button("踢!", GUILayout.Width(50)))
                {
                    emp.KickBackToWork();
                }

                if (GUILayout.Button("犯错", GUILayout.Width(50)))
                {
                    if (!emp.TryWork())
                    {
                        opMgr.OnEmployeeMistake(emp);
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("结束营业（调试）"))
            {
                opMgr.EndOperation();
            }
        }

        private void DrawSettlementPhase(GameDayData dayData, PlayerModel player)
        {
            var result = DataManager.Instance.SaveData?.lastOperationResult;

            GUILayout.Label("【今日结算】", new GUIStyle(GUI.skin.label) { fontSize = 20 });

            if (result != null)
            {
                string stars = new string('*', result.StarRating);
                GUILayout.Label($"评级: {stars} ({result.StarRating}星)");
                GUILayout.Label($"总收入: {Mathf.RoundToInt(result.TotalRevenue)}银两");
                GUILayout.Label($"菜品满意度: {result.DishSatisfaction:P0}");
                GUILayout.Label($"服务效率: {result.ServiceEfficiency:P0}");
                GUILayout.Label($"环境加成: {result.EnvironmentBonus:P0}");
                GUILayout.Label($"负面事件: {result.NegativeEvents}");

                GUILayout.Space(10);
                GUILayout.Label("明日预览:");
                string preview = result.StarRating >= 5 ? "口碑远播！" :
                                 result.StarRating >= 4 ? "声名鹊起" :
                                 result.StarRating >= 3 ? "门庭若市" :
                                 result.StarRating >= 2 ? "生意清淡" : "门可罗雀";
                GUILayout.Label($"  {preview}");
            }

            GUILayout.Space(20);
            if (GUILayout.Button("进入下一天", GUILayout.Height(50)))
            {
                int nextDay = dayData.DayNumber + 1;
                if (nextDay > 10)
                {
                    nextDay = 10;
                }

                TavernDayManager.Instance.StartNewDay(nextDay);
            }
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
