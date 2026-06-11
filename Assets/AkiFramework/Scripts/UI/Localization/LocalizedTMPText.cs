using System;
using TMPro;
using UnityEngine;

namespace AkiFramework.UI
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    /// <summary>
    /// 通用本地化文本组件。
    /// 只关心文本 ID 与刷新时机，具体语言解析逻辑由外部安装器注入。
    /// </summary>
    public class LocalizedTMPText : MonoBehaviour
    {
        [Header("Localization")]
        [SerializeField] private int localizationId;

        private TextMeshProUGUI _tmpText;

        /// <summary>
        /// 由业务层注入的文本解析函数。
        /// </summary>
        public static Func<int, string> TextResolver { get; set; }

        /// <summary>
        /// 由业务层注入的刷新订阅函数，例如语言切换信号。
        /// </summary>
        public static Action<Action> SubscribeRefresh { get; set; }

        /// <summary>
        /// 由业务层注入的刷新取消订阅函数。
        /// </summary>
        public static Action<Action> UnsubscribeRefresh { get; set; }

        private void Awake()
        {
            _tmpText = GetComponent<TextMeshProUGUI>();
        }

        private void OnEnable()
        {
            RefreshText();
            SubscribeRefresh?.Invoke(RefreshText);
        }

        private void OnDisable()
        {
            UnsubscribeRefresh?.Invoke(RefreshText);
        }

        public void RefreshText()
        {
            if (_tmpText == null) return;

            if (localizationId == 0)
            {
                Debug.LogWarning($"[LocalizedTMPText] GameObject {gameObject.name} 未设置 Localization Key");
                return;
            }

            _tmpText.text = TextResolver?.Invoke(localizationId) ?? string.Empty;
        }

        private void OnValidate()
        {
            if (_tmpText == null) _tmpText = GetComponent<TextMeshProUGUI>();
        }
    }
}
