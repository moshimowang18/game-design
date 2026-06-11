using System;
using AkiFramework.UI;
using QFramework;
using UnityEngine;

namespace JN.Client.UI
{
    /// <summary>
    /// 负责江南界面启动器相关的运行时逻辑。
    /// </summary>
    public static class JiangNanUIKitBootstrap
    {
        private static bool initialized;

        /// <summary>
        /// 在场景加载前自动初始化模块。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitializeBeforeSceneLoad()
        {
            Initialize();
        }

        /// <summary>
        /// 注入运行时依赖并刷新初始显示。
        /// </summary>
        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }

            // 这里集中定义 界面 分辨率与面板地址解析规则，避免散落在业务代码里。
            AddressablesUIKit.Initialize(new AddressablesUIKitConfig
            {
                ReferenceWidth = 1080,
                ReferenceHeight = 1920,
                MatchWidthOrHeight = 0.5f,
                UseScreenSpaceOverlay = true,
                AddressResolver = panelSearchKeys => !string.IsNullOrWhiteSpace(panelSearchKeys.GameObjName)
                    ? panelSearchKeys.GameObjName
                    : panelSearchKeys.PanelType.Name
            });

            initialized = true;
        }
    }
}
