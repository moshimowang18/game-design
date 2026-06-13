using JN.Client.Manager;
using JN.Client.Model;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JN.Client.UI
{
    public class DayCyclePanelControllerData : UIPanelData
    {
    }

    /// <summary>
    /// 日循环面板：准备阶段展示今日事件、扩建厨房等信息。
    /// </summary>
    public class DayCyclePanelController : QFrameworkPanel<DayCyclePanelControllerData>
    {
        private const int MaxKitchenLevel = 3;

        private TextMeshProUGUI _txtTitle;
        private TextMeshProUGUI _txtDayLabel;
        private TextMeshProUGUI _txtEventName;
        private TextMeshProUGUI _txtEventHint;
        private TextMeshProUGUI _txtFlowMultiplier;
        private TextMeshProUGUI _txtKitchenLevel;
        private TextMeshProUGUI _txtKitchenCost;
        private Button _btnUpgradeKitchen;
        private TextMeshProUGUI _txtBtnLabel;
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
            _txtKitchenLevel = transform.Find("group_Content/group_KitchenUpgrade/txt_KitchenLevel")?.GetComponent<TextMeshProUGUI>();
            _txtKitchenCost = transform.Find("group_Content/group_KitchenUpgrade/txt_KitchenCost")?.GetComponent<TextMeshProUGUI>();
            _btnUpgradeKitchen = transform.Find("group_Content/group_KitchenUpgrade/btn_UpgradeKitchen")?.GetComponent<Button>();
            _txtBtnLabel = transform.Find("group_Content/group_KitchenUpgrade/btn_UpgradeKitchen/txt_BtnLabel")?.GetComponent<TextMeshProUGUI>();

            if (_txtTitle != null)
            {
                _txtTitle.text = "📅 今日";
            }

            if (_btnUpgradeKitchen != null)
            {
                _btnUpgradeKitchen.onClick.AddListener(OnClickUpgradeKitchen);
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

        /// <summary>
        /// 面板关闭时清理监听。
        /// </summary>
        protected override void OnPanelClose()
        {
            if (_btnUpgradeKitchen != null)
            {
                _btnUpgradeKitchen.onClick.RemoveListener(OnClickUpgradeKitchen);
            }
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

            RefreshKitchenUpgrade();
        }

        private void RefreshKitchenUpgrade()
        {
            var player = DataManager.Instance.PlayerData;
            if (player == null)
            {
                return;
            }

            var currentLevel = player.TavernLevel;

            if (_txtKitchenLevel != null)
            {
                _txtKitchenLevel.text = $"厨房等级: Lv.{currentLevel}";
            }

            if (currentLevel >= MaxKitchenLevel)
            {
                if (_txtKitchenCost != null)
                {
                    _txtKitchenCost.text = "已达最高级";
                }

                if (_btnUpgradeKitchen != null)
                {
                    _btnUpgradeKitchen.gameObject.SetActive(false);
                }

                return;
            }

            var upgradeCost = player.TavernLevel * 100;

            if (_txtKitchenCost != null)
            {
                _txtKitchenCost.text = $"扩建需要: {upgradeCost}银两";
            }

            var canAfford = player.coinNum >= upgradeCost;
            if (_btnUpgradeKitchen != null)
            {
                _btnUpgradeKitchen.gameObject.SetActive(true);
                _btnUpgradeKitchen.interactable = canAfford;
            }

            if (_txtBtnLabel != null)
            {
                _txtBtnLabel.text = canAfford
                    ? "扩建厨房"
                    : $"扩建厨房（差{upgradeCost - player.coinNum}银两）";
            }
        }

        private void OnClickUpgradeKitchen()
        {
            var player = DataManager.Instance.PlayerData;
            if (player == null || player.TavernLevel >= MaxKitchenLevel)
            {
                Debug.Log("[DayCyclePanel] 扩建厨房: 失败");
                return;
            }

            var upgradeCost = player.TavernLevel * 100;
            if (player.coinNum < upgradeCost)
            {
                Debug.Log("[DayCyclePanel] 扩建厨房: 失败");
                return;
            }

            player.coinNum -= upgradeCost;
            TavernUpgradeManager.Instance.Upgrade();
            Debug.Log("[DayCyclePanel] 扩建厨房: 成功");
        }
    }
}
