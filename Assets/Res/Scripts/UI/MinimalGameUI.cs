using JN.Client.Model;
using UnityEngine;

namespace JN.Client.Manager
{
    [DefaultExecutionOrder(-100)]
    public class MinimalGameUI : MonoBehaviour
    {
        private const string HostObjectName = "[DayCycleUI]";

        private static MinimalGameUI s_instance;

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

            // 所有日循环UI已迁移到 DayCyclePanelController（准备阶段）
            // 顶部HUD（TavernStatusBarPanelController）显示天数+营业倒计时+客流统计
            // 结算弹窗（DaySettlementWindowController）显示评分
            // 此处仅保留少量调试按钮
            GUI.depth = 9999;

            var dayMgr = TavernDayManager.Instance;
            if (dayMgr == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(20, Screen.height - 120, 200, 100));

            if (dayMgr.Phase == DayPhase.Preparation)
            {
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
            else if (dayMgr.Phase == DayPhase.Operation)
            {
                if (GUILayout.Button("结束营业（调试）", GUILayout.Height(30)))
                {
                    var opMgr = OperationManager.Instance;
                    if (opMgr == null)
                    {
                        return;
                    }

                    var result = opMgr.EndOperation();
                    TavernDayManager.Instance.EnterSettlementPhase(result);
                }
            }

            GUILayout.EndArea();
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
