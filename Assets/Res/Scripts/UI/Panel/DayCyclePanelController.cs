using JN.Client.Manager;
using QFramework;
using TMPro;
using UnityEngine;

namespace JN.Client.UI
{
    public class DayCyclePanelControllerData : UIPanelData
    {
    }

    /// <summary>
    /// 日循环面板：准备阶段展示今日事件等信息。
    /// </summary>
    public class DayCyclePanelController : QFrameworkPanel<DayCyclePanelControllerData>
    {
        private TextMeshProUGUI _txtTitle;
        private TextMeshProUGUI _txtDayLabel;
        private TextMeshProUGUI _txtEventName;
        private TextMeshProUGUI _txtEventHint;
        private TextMeshProUGUI _txtFlowMultiplier;
        private GameObject _groupContent;

        /// <summary>
        /// 面板初始化时绑定控件。
        /// </summary>
        protected override void OnPanelInit()
        {
            _groupContent = transform.Find("group_Content")?.gameObject;
            _txtTitle = transform.Find("group_Content/txt_Title")?.GetComponent<TextMeshProUGUI>();
            _txtDayLabel = transform.Find("group_Content/group_EventInfo/txt_DayLabel")?.GetComponent<TextMeshProUGUI>();
            _txtEventName = transform.Find("group_Content/group_EventInfo/txt_EventName")?.GetComponent<TextMeshProUGUI>();
            _txtEventHint = transform.Find("group_Content/group_EventInfo/txt_EventHint")?.GetComponent<TextMeshProUGUI>();
            _txtFlowMultiplier = transform.Find("group_Content/group_EventInfo/txt_FlowMultiplier")?.GetComponent<TextMeshProUGUI>();

            if (_txtTitle != null)
            {
                _txtTitle.text = "📅 今日";
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
            var dayMgr = TavernDayManager.Instance;
            if (dayMgr == null)
            {
                return;
            }

            var showInPrep = dayMgr.Phase == DayPhase.Preparation;
            if (_groupContent != null && _groupContent.activeSelf != showInPrep)
            {
                _groupContent.SetActive(showInPrep);
            }

            if (!showInPrep)
            {
                return;
            }

            RefreshPreparationContent();
        }

        private void RefreshPreparationContent()
        {
            var dayMgr = TavernDayManager.Instance;
            var dayData = dayMgr.CurrentDay;
            if (dayData == null)
            {
                return;
            }

            if (_txtDayLabel != null)
            {
                _txtDayLabel.text = $"📅 第{dayData.DayNumber}天 / 10";
            }

            var evtId = EventSystemManager.Instance.GetTodaysEventId(dayData.DayNumber);
            var evt = EventSystemManager.Instance.GetEventById(evtId);
            if (evt != null)
            {
                if (_txtEventName != null)
                {
                    _txtEventName.text = $"<color=yellow>今日事件: {evt.EventName}</color>";
                }

                if (_txtEventHint != null)
                {
                    _txtEventHint.text = $"策略提示: {evt.StrategicHint}";
                }

                if (_txtFlowMultiplier != null)
                {
                    _txtFlowMultiplier.text = $"客流倍率: x{dayData.GuestFlowMultiplier:F1}";
                }
            }
            else
            {
                if (_txtEventName != null)
                {
                    _txtEventName.text = "今日事件: 平常一天";
                }

                if (_txtEventHint != null)
                {
                    _txtEventHint.text = string.Empty;
                }

                if (_txtFlowMultiplier != null)
                {
                    _txtFlowMultiplier.text = "客流倍率: x1.0";
                }
            }
        }
    }
}
