using JN.Client.Manager;
using JN.Client.Model;
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
        private TextMeshProUGUI _txtDayInfo;
        private TextMeshProUGUI _txtRating;
        private TextMeshProUGUI _txtRevenue;
        private TextMeshProUGUI _txtSatisfaction;
        private TextMeshProUGUI _txtService;
        private TextMeshProUGUI _txtEnvironment;
        private TextMeshProUGUI _txtNegative;
        private TextMeshProUGUI _txtTomorrowPreview;
        private Button _btnNextDay;

        protected override void OnPanelInit()
        {
            _txtTitle = transform.Find("group_Content/txt_Title")?.GetComponent<TextMeshProUGUI>();
            _txtDayInfo = transform.Find("group_Content/txt_DayInfo")?.GetComponent<TextMeshProUGUI>();
            _txtRating = transform.Find("group_Content/txt_Rating")?.GetComponent<TextMeshProUGUI>();
            _txtRevenue = transform.Find("group_Content/group_Stats/txt_Revenue")?.GetComponent<TextMeshProUGUI>();
            _txtSatisfaction = transform.Find("group_Content/group_Stats/txt_Satisfaction")?.GetComponent<TextMeshProUGUI>();
            _txtService = transform.Find("group_Content/group_Stats/txt_Service")?.GetComponent<TextMeshProUGUI>();
            _txtEnvironment = transform.Find("group_Content/group_Stats/txt_Environment")?.GetComponent<TextMeshProUGUI>();
            _txtNegative = transform.Find("group_Content/group_Stats/txt_Negative")?.GetComponent<TextMeshProUGUI>();
            _txtTomorrowPreview = transform.Find("group_Content/txt_TomorrowPreview")?.GetComponent<TextMeshProUGUI>();
            _btnNextDay = transform.Find("group_Content/btn_NextDay")?.GetComponent<Button>();

            if (_txtTitle != null)
            {
                _txtTitle.text = "今日结算";
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
            RefreshSettlement();
        }

        protected override void OnPanelClose()
        {
            if (_btnNextDay != null)
            {
                _btnNextDay.onClick.RemoveListener(OnClickNextDay);
            }
        }

        private void RefreshSettlement()
        {
            var dayMgr = TavernDayManager.Instance;
            var dayData = dayMgr?.CurrentDay;
            if (dayData == null)
            {
                return;
            }

            var evtId = EventSystemManager.Instance.GetTodaysEventId(dayData.DayNumber);
            var evt = EventSystemManager.Instance.GetEventById(evtId);
            var eventName = evt != null ? evt.EventName : "平常一天";
            if (_txtDayInfo != null)
            {
                _txtDayInfo.text = $"第{dayData.DayNumber}天 · {eventName}";
            }

            var result = DataManager.Instance.SaveData?.lastOperationResult;
            if (result == null)
            {
                return;
            }

            var stars = result.StarRating;
            var starDisplay = new string('★', stars) + new string('☆', 5 - stars);
            if (_txtRating != null)
            {
                _txtRating.text = $"<color=yellow>评级: {starDisplay} ({stars} 星)</color>";
            }

            if (_txtRevenue != null)
            {
                _txtRevenue.text = $"总收入: {Mathf.RoundToInt(result.TotalRevenue)} 银两";
            }

            if (_txtSatisfaction != null)
            {
                _txtSatisfaction.text = $"菜品满意度: {result.DishSatisfaction:P0}";
            }

            if (_txtService != null)
            {
                _txtService.text = $"服务效率: {result.ServiceEfficiency:P0}";
            }

            if (_txtEnvironment != null)
            {
                _txtEnvironment.text = $"环境加成: {result.EnvironmentBonus:P0}";
            }

            if (_txtNegative != null)
            {
                _txtNegative.text = $"负面事件: {result.NegativeEvents}";
            }

            if (_txtTomorrowPreview != null)
            {
                var nextDay = dayData.DayNumber + 1;
                if (nextDay > 10)
                {
                    _txtTomorrowPreview.text = "<color=yellow>10天剧本完成！</color>";
                }
                else
                {
                    var preview = result.StarRating >= 5 ? "口碑远播！" :
                        result.StarRating >= 4 ? "声名鹊起" :
                        result.StarRating >= 3 ? "门庭若市" :
                        result.StarRating >= 2 ? "生意清淡" : "门可罗雀";
                    _txtTomorrowPreview.text = $"明日预览: {preview}";
                }
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
