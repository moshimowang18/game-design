using System;
using JN.Client.Manager;
using UnityEngine;
using UnityCommunity.UnitySingleton;
using JN.Client.Utils;

namespace JN.Client
{
    /// <summary>
    /// 定义游戏语言可用的枚举类型。
    /// </summary>
    public enum GameLanguage
    {
        zh_CN,
        en_US,
    }

    /// <summary>
    /// 负责本地化相关的运行时逻辑。
    /// </summary>
    public class LocalizationManager : MonoSingleton<LocalizationManager>
    {
        [SerializeField, Tooltip("当前语言")] private GameLanguage currentLanguage;

        /// <summary>
        /// 处理当前语言相关逻辑。
        /// </summary>
        public GameLanguage CurrentLanguage => currentLanguage;

        #region Init

        /// <summary>
        /// 初始化模块依赖和默认状态。
        /// </summary>
        public void Init()
        {
        }

        /// <summary>
        /// 响应初始化事件并同步状态。
        /// </summary>
        protected override void OnInitializing()
        {
            // 优先从本地设置里恢复语言偏好，没有配置时由 游戏设置 自身兜底。
            currentLanguage = GameSetting.Instance.Data.curLanguage;

            // 后续如果改成从 配置表 或远端表加载语言数据，可在这里统一初始化。
            // 加载语言数据();
        }

        /// <summary>
        /// 加载语言数据。
        /// </summary>
        private void LoadLanguageData()
        {
            // 待处理：待填充逻辑（例如从 配置表 表中加载数据）
        }

        #endregion


        #region Public Functions

        /// <summary>
        /// 获取语言。
        /// </summary>
        /// <param name="key">语言表键值。</param>
        /// <returns>返回方法执行后的结果。</returns>
        public string GetLanguage(int key)
        {
            switch (currentLanguage)
            {
                case GameLanguage.zh_CN:
                    // 从配置表读取中文文本。
                case GameLanguage.en_US:
                    // 从配置表读取英文文本。
                default:
                    // 当前语言表尚未正式接入时，统一返回空串，避免 界面 出现 空值。
                    return string.Empty;
            }
        }

        /// <summary>
        /// 处理切换语言相关逻辑。
        /// </summary>
        /// <param name="lan">目标语言。</param>
        public void SwitchLanguage(GameLanguage lan)
        {
            currentLanguage = lan;
            // 所有挂载了 本地化文本组件文本 的文本组件都通过这个信号刷新。
            Signals.Get<LanguageSwitchSignal>().Dispatch();
        }

        #endregion
    }
}
