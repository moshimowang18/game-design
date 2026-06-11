using QFramework;
using LocalizedTextComponent = AkiFramework.UI.LocalizedTMPText;

namespace JN.Client
{
    /// <summary>
    /// 负责安装江南本地化文本。
    /// </summary>
    public static class JiangNanLocalizationInstaller
    {
        private static bool installed;

        /// <summary>
        /// 安装默认本地化文本。
        /// </summary>
        public static void Install()
        {
            if (installed)
            {
                return;
            }

            // 通用组件只声明扩展点，具体文本解析与刷新订阅由业务项目在这里注入。
            LocalizedTextComponent.TextResolver = key => LocalizationManager.Instance.GetLanguage(key);
            LocalizedTextComponent.SubscribeRefresh = listener => Signals.Get<LanguageSwitchSignal>().AddListener(listener);
            LocalizedTextComponent.UnsubscribeRefresh = listener =>
                Signals.Get<LanguageSwitchSignal>().RemoveListener(listener);
            installed = true;
        }
    }
}
