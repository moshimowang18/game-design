using JN.Client.Manager;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JN.Client.UI
{
    public class DaySettlementWindowControllerData : UIPanelData
    {
    }

    /// <summary>
    /// 日结算弹窗（PopUI）。
    /// </summary>
    public class DaySettlementWindowController : QFrameworkPanel<DaySettlementWindowControllerData>
    {
        private TextMeshProUGUI _txtTitle;
        private Button _btnNextDay;

        protected override void OnPanelInit()
        {
            _txtTitle = transform.Find("group_Content/txt_Title")?.GetComponent<TextMeshProUGUI>();
            _btnNextDay = transform.Find("group_Content/btn_NextDay")?.GetComponent<Button>();

            if (_txtTitle != null)
            {
                _txtTitle.text = "今日结算（占位）";
            }

            if (_btnNextDay != null)
            {
                _btnNextDay.onClick.AddListener(OnClickNextDay);
            }

            Debug.Log("[DaySettlement] OnPanelInit");
        }

        protected override void OnPanelOpen(DaySettlementWindowControllerData data)
        {
            Debug.Log("[DaySettlement] OnPanelOpen");
        }

        protected override void OnPanelClose()
        {
            if (_btnNextDay != null)
            {
                _btnNextDay.onClick.RemoveListener(OnClickNextDay);
            }
        }

        private void OnClickNextDay()
        {
            UIKit.ClosePanel<DaySettlementWindowController>();

            var dayMgr = TavernDayManager.Instance;
            var dayData = dayMgr?.CurrentDay;
            if (dayData == null)
            {
                return;
            }

            var nextDay = dayData.DayNumber + 1;
            if (nextDay > 10)
            {
                nextDay = 10;
            }

            dayMgr.StartNewDay(nextDay);
        }
    }
}
