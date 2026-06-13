using QFramework;
using TMPro;
using UnityEngine;

namespace JN.Client.UI
{
    public class DayCyclePanelControllerData : UIPanelData
    {
    }

    /// <summary>
    /// 日循环面板占位壳，用于验证 UIKit 加载流程。
    /// </summary>
    public class DayCyclePanelController : QFrameworkPanel<DayCyclePanelControllerData>
    {
        private TextMeshProUGUI _txtTitle;

        /// <summary>
        /// 面板初始化时绑定控件。
        /// </summary>
        protected override void OnPanelInit()
        {
            _txtTitle = transform.Find("group_Content/txt_Title")?.GetComponent<TextMeshProUGUI>();
            if (_txtTitle != null)
            {
                _txtTitle.text = "日循环面板（占位）";
            }

            Debug.Log("[DayCyclePanel] OnPanelInit");
        }

        /// <summary>
        /// 面板打开时刷新显示。
        /// </summary>
        /// <param name="data">面板数据。</param>
        protected override void OnPanelOpen(DayCyclePanelControllerData data)
        {
            Debug.Log("[DayCyclePanel] OnPanelOpen");
        }

        private void Update()
        {
            // 占位，下一批补阶段切换逻辑
        }
    }
}
