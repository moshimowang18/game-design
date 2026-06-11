using UnityEngine;
using TMPro;

namespace JN.Client
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedTMPText : MonoBehaviour
    {
        [Header("本地化配置")]
        [SerializeField] private int localizationId;
    
        private TextMeshProUGUI _tmpText;

        /// <summary>
        /// 初始化组件引用和运行时状态。
        /// </summary>
        private void Awake()
        {
            _tmpText = GetComponent<TextMeshProUGUI>();
        }

        /// <summary>
        /// 启用时注册事件监听并刷新当前状态。
        /// </summary>
        private void OnEnable()
        {
            RefreshText();
            Signals.Get<LanguageSwitchSignal>().AddListener(RefreshText);
        }

        /// <summary>
        /// 禁用时移除事件监听，避免重复回调。
        /// </summary>
        private void OnDisable()
        {
            if (Signals.Get<LanguageSwitchSignal>() != null)
            {
                Signals.Get<LanguageSwitchSignal>().RemoveListener(RefreshText);
            }
        }

        /// <summary>
        /// 刷新文本。
        /// </summary>
        public void RefreshText()
        {
            if (_tmpText == null) return;

            if (localizationId == 0)
            {
                Debug.LogWarning($"[LocalizedTMPText] GameObject {gameObject.name} 未设置 Localization Key");
                return;
            }

            string translatedText = LocalizationManager.Instance.GetLanguage(localizationId);
            _tmpText.text = translatedText;
        }

        /// <summary>
        /// 编辑器参数变化时同步预览和默认配置。
        /// </summary>
        private void OnValidate()
        {
            if (_tmpText == null) _tmpText = GetComponent<TextMeshProUGUI>();
        }
    }
}
