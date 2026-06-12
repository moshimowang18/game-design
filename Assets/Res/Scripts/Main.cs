using System;
using System.Collections;
using JN.Client.Manager;
using JN.Client.UI;
using QFramework;
using UnityEngine;
namespace JN.Client
{
    /// <summary>
    /// 负责入口相关的运行时逻辑。
    /// </summary>
    public class Main : MonoBehaviour
    {
        

        /// <summary>
        /// 初始化组件引用和运行时状态。
        /// </summary>
        protected void Awake()
        {
            // 统一锁定目标帧率为 60，避免不同设备默认帧率策略不一致。
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;

            // 按依赖顺序初始化基础系统，避免后续 界面 打开时访问到未准备好的数据。
            LocalizationManager.Instance.Init();
            LubanManager.Instance.Init();
            GameManager.Instance.Init();
            DataManager.Instance.Init();
            EventSystemManager.Instance.Initialize();
            TavernDayManager.Instance.Init();
            GOReferenceManager.Instance.Init();

            // 安装业务侧对通用框架的适配逻辑。
            JiangNanLocalizationInstaller.Install();
            JiangNanUIKitBootstrap.Initialize();


           

            UIKit.OpenPanel<CreatePlayerPanelController>(UILevel.Common);
        }

        

        /// <summary>
        /// 打开场景状态栏。
        /// </summary>
        /// <param name="sceneName">名称。</param>
        private static void OpenSceneHud(string sceneName)
        {
            switch (sceneName)
            {
                case "GamePlay_Tavern":
                case "Tavern_Gameplay":
                case "SCN_Tavern_Gameplay":
                    if (UIKit.GetPanel<TownStatusBarPanelController>() != null)
                    {
                        UIKit.ClosePanel<TownStatusBarPanelController>();
                    }

                    UIKit.OpenPanel<TavernStatusBarPanelController>(UILevel.Common);
                    UIKit.OpenPanel<StartOpeningWindowController>(UILevel.PopUI);
                    break;
                case "Town":
                case "Town_Main":
                case "SCN_Town_Main":
                default:
                    UIKit.OpenPanel<TownStatusBarPanelController>(UILevel.Common);
                    break;
            }
        }
    }
}