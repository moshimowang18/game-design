using System.Collections.Generic;
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
    /// 日循环面板：准备阶段展示今日事件、扩建厨房、菜品列表等信息。
    /// </summary>
    public class DayCyclePanelController : QFrameworkPanel<DayCyclePanelControllerData>
    {
        private const int MaxKitchenLevel = 3;
        private const float ContentPadLeft = 16f;
        private const float ContentPadRight = 16f;
        private const float ContentPadTop = 20f;
        private const float ContentPadBottom = 16f;
        private const float SectionSpacing = 12f;
        private const float TitleHeight = 40f;
        private const float EventSectionHeight = 140f;
        private const float KitchenSectionHeight = 104f;
        private const float DishHeaderHeight = 30f;
        private const float DishRowHeight = 50f;
        private const float DishRowSpacing = 4f;
        private static readonly Vector2 PanelSize = new(500f, 800f);
        private static readonly Vector2 PanelPosition = new(20f, 20f);

        private TextMeshProUGUI _txtTitle;
        private TextMeshProUGUI _txtDayLabel;
        private TextMeshProUGUI _txtEventName;
        private TextMeshProUGUI _txtEventHint;
        private TextMeshProUGUI _txtFlowMultiplier;
        private TextMeshProUGUI _txtKitchenLevel;
        private TextMeshProUGUI _txtKitchenCost;
        private Button _btnUpgradeKitchen;
        private TextMeshProUGUI _txtBtnLabel;
        private TextMeshProUGUI _txtDishHeader;
        private Transform _dishListContent;
        private GameObject _dishItemTemplate;
        private readonly List<GameObject> _dishItems = new();
        private GameObject _groupContent;
        private RectTransform _groupEventInfo;
        private RectTransform _groupKitchenUpgrade;
        private RectTransform _groupDishSelection;
        private RectTransform _scrollDishList;
        private int _lastDishListStateHash = int.MinValue;
        private bool _wasShowingPrep;
        private int _panelLayoutFrames;
        /// <summary>
        /// 面板初始化时绑定控件。
        /// </summary>
        protected override void OnPanelInit()
        {
            ApplyPanelLayout();
            _groupContent = transform.Find("group_Content")?.gameObject;
            _groupEventInfo = transform.Find("group_Content/group_EventInfo") as RectTransform;
            _groupKitchenUpgrade = transform.Find("group_Content/group_KitchenUpgrade") as RectTransform;
            _groupDishSelection = transform.Find("group_Content/group_DishSelection") as RectTransform;
            _scrollDishList = transform.Find("group_Content/group_DishSelection/scroll_DishList") as RectTransform;
            _txtTitle = transform.Find("group_Content/txt_Title")?.GetComponent<TextMeshProUGUI>();
            _txtDayLabel = transform.Find("group_Content/group_EventInfo/txt_DayLabel")?.GetComponent<TextMeshProUGUI>();
            _txtEventName = transform.Find("group_Content/group_EventInfo/txt_EventName")?.GetComponent<TextMeshProUGUI>();
            _txtEventHint = transform.Find("group_Content/group_EventInfo/txt_EventHint")?.GetComponent<TextMeshProUGUI>();
            _txtFlowMultiplier = transform.Find("group_Content/group_EventInfo/txt_FlowMultiplier")?.GetComponent<TextMeshProUGUI>();
            _txtKitchenLevel = transform.Find("group_Content/group_KitchenUpgrade/txt_KitchenLevel")?.GetComponent<TextMeshProUGUI>();
            _txtKitchenCost = transform.Find("group_Content/group_KitchenUpgrade/txt_KitchenCost")?.GetComponent<TextMeshProUGUI>();
            _btnUpgradeKitchen = transform.Find("group_Content/group_KitchenUpgrade/btn_UpgradeKitchen")?.GetComponent<Button>();
            _txtBtnLabel = transform.Find("group_Content/group_KitchenUpgrade/btn_UpgradeKitchen/txt_BtnLabel")?.GetComponent<TextMeshProUGUI>();
            _txtDishHeader = transform.Find("group_Content/group_DishSelection/txt_DishHeader")?.GetComponent<TextMeshProUGUI>();
            _dishListContent = transform.Find("group_Content/group_DishSelection/scroll_DishList/Viewport/Content");

            if (_dishListContent != null)
            {
                var template = _dishListContent.Find("DishItemTemplate");
                if (template != null)
                {
                    _dishItemTemplate = template.gameObject;
                    _dishItemTemplate.SetActive(false);
                }
            }

            if (_txtTitle != null)
            {
                _txtTitle.text = "📅 今日";
            }

            if (_btnUpgradeKitchen != null)
            {
                _btnUpgradeKitchen.onClick.AddListener(OnClickUpgradeKitchen);
            }

            DisableAutoLayout();
            ApplyManualPanelLayout();
            _panelLayoutFrames = 3;

            Debug.Log("[DayCyclePanel] OnPanelInit");
        }

        /// <summary>
        /// 面板打开时刷新显示。
        /// </summary>
        /// <param name="data">面板数据。</param>
        protected override void OnPanelOpen(DayCyclePanelControllerData data)
        {
            ApplyPanelLayout();
            _lastDishListStateHash = int.MinValue;
            _panelLayoutFrames = 3;
            ApplyManualPanelLayout();
            Debug.Log("[DayCyclePanel] OnPanelOpen");
        }

        protected override void OnPanelShow()
        {
            ApplyPanelLayout();
            _panelLayoutFrames = 2;
            ApplyManualPanelLayout();
        }

        /// <summary>
        /// UIKit 打开面板时会强制全屏拉伸，这里恢复为左侧固定尺寸（与 IMGUI 一致）。
        /// </summary>
        private void ApplyPanelLayout()
        {
            var rect = transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = PanelPosition;
            rect.sizeDelta = PanelSize;
        }

        private void DisableAutoLayout()
        {
            DisableLayoutOn(_groupContent);
            DisableLayoutOn(_groupEventInfo?.gameObject);
            DisableLayoutOn(_groupKitchenUpgrade?.gameObject);
            DisableLayoutOn(_groupDishSelection?.gameObject);
            DisableLayoutOn(_dishListContent?.gameObject);

            if (_dishItemTemplate != null)
            {
                DisableLayoutOn(_dishItemTemplate);
            }
        }

        private static void DisableLayoutOn(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            foreach (var layout in go.GetComponents<LayoutGroup>())
            {
                layout.enabled = false;
            }

            foreach (var fitter in go.GetComponents<ContentSizeFitter>())
            {
                fitter.enabled = false;
            }
        }

        private void ApplyManualPanelLayout()
        {
            if (_groupContent == null)
            {
                return;
            }

            var contentRect = _groupContent.transform as RectTransform;
            if (contentRect != null)
            {
                contentRect.anchorMin = Vector2.zero;
                contentRect.anchorMax = Vector2.one;
                contentRect.pivot = new Vector2(0.5f, 0.5f);
                contentRect.offsetMin = Vector2.zero;
                contentRect.offsetMax = Vector2.zero;
            }

            var y = ContentPadTop;

            if (_txtTitle != null)
            {
                PlaceTopBand(_txtTitle.rectTransform, y, TitleHeight);
                y += TitleHeight + SectionSpacing;
            }

            if (_groupEventInfo != null)
            {
                PlaceTopBand(_groupEventInfo, y, EventSectionHeight);
                LayoutEventInfoSection();
                y += EventSectionHeight + SectionSpacing;
            }

            if (_groupKitchenUpgrade != null)
            {
                PlaceTopBand(_groupKitchenUpgrade, y, KitchenSectionHeight);
                LayoutKitchenSection();
                y += KitchenSectionHeight + SectionSpacing;
            }

            if (_groupDishSelection != null)
            {
                var dishSectionHeight = PanelSize.y - y - ContentPadBottom;
                PlaceTopBand(_groupDishSelection, y, dishSectionHeight);
                LayoutDishSelectionSection(dishSectionHeight);
            }
        }

        private void LayoutEventInfoSection()
        {
            const float lineHeight = 28f;
            const float lineSpacing = 8f;
            var lineY = 0f;
            PlaceTopBand(_txtDayLabel?.rectTransform, lineY, lineHeight, 0f, 0f, _groupEventInfo);
            lineY += lineHeight + lineSpacing;
            PlaceTopBand(_txtEventName?.rectTransform, lineY, lineHeight, 0f, 0f, _groupEventInfo);
            lineY += lineHeight + lineSpacing;
            PlaceTopBand(_txtEventHint?.rectTransform, lineY, lineHeight, 0f, 0f, _groupEventInfo);
            lineY += lineHeight + lineSpacing;
            PlaceTopBand(_txtFlowMultiplier?.rectTransform, lineY, lineHeight, 0f, 0f, _groupEventInfo);
        }

        private void LayoutKitchenSection()
        {
            const float levelHeight = 28f;
            const float costHeight = 24f;
            const float buttonHeight = 36f;
            const float lineSpacing = 8f;
            PlaceTopBand(_txtKitchenLevel?.rectTransform, 0f, levelHeight, 0f, 0f, _groupKitchenUpgrade);
            PlaceTopBand(_txtKitchenCost?.rectTransform, levelHeight + lineSpacing, costHeight, 0f, 0f, _groupKitchenUpgrade);
            PlaceTopBand(_btnUpgradeKitchen?.transform as RectTransform, levelHeight + lineSpacing + costHeight + lineSpacing, buttonHeight, 0f, 0f, _groupKitchenUpgrade);
        }

        private void LayoutDishSelectionSection(float sectionHeight)
        {
            PlaceTopBand(_txtDishHeader?.rectTransform, 0f, DishHeaderHeight, 0f, 0f, _groupDishSelection);

            if (_scrollDishList == null)
            {
                return;
            }

            var scrollTop = DishHeaderHeight + SectionSpacing;
            var scrollHeight = Mathf.Max(0f, sectionHeight - scrollTop);
            PlaceTopBand(_scrollDishList, scrollTop, scrollHeight, 0f, 0f, _groupDishSelection);

            var viewport = _scrollDishList.Find("Viewport") as RectTransform;
            if (viewport != null)
            {
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = Vector2.zero;
                viewport.offsetMax = new Vector2(-12f, 0f);
            }

            if (_dishListContent is RectTransform contentRect)
            {
                var contentHeight = _dishItems.Count * (DishRowHeight + DishRowSpacing);
                contentRect.anchorMin = new Vector2(0f, 1f);
                contentRect.anchorMax = new Vector2(1f, 1f);
                contentRect.pivot = new Vector2(0.5f, 1f);
                contentRect.anchoredPosition = Vector2.zero;
                contentRect.sizeDelta = new Vector2(0f, Mathf.Max(contentHeight, scrollHeight));
            }
        }

        private static void PlaceTopBand(
            RectTransform rect,
            float yFromTop,
            float height,
            float padLeft = ContentPadLeft,
            float padRight = ContentPadRight,
            RectTransform parent = null)
        {
            if (rect == null)
            {
                return;
            }

            if (parent != null && rect.parent != parent)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -yFromTop);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            rect.offsetMin = new Vector2(padLeft, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-padRight, rect.offsetMax.y);
        }

        private static void LayoutDishItemRow(RectTransform row, float yFromTop)
        {
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -yFromTop);
            row.sizeDelta = new Vector2(0f, DishRowHeight);

            PlaceChildBand(row.Find("txt_DishName") as RectTransform, 0f, 110f, 0f);
            PlaceChildBand(row.Find("txt_DishCost") as RectTransform, 116f, 150f, 0f);
            PlaceChildBand(row.Find("txt_DishStatus") as RectTransform, 272f, 56f, 0f);
            PlaceChildBand(row.Find("btn_Action") as RectTransform, 0f, 72f, 1f);
        }

        private static void PlaceChildBand(RectTransform rect, float x, float width, float anchorX)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(anchorX, 0f);
            rect.anchorMax = new Vector2(anchorX, 1f);
            rect.pivot = new Vector2(anchorX, 0.5f);
            rect.anchoredPosition = new Vector2(anchorX > 0.5f ? -x : x, 0f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.offsetMin = new Vector2(rect.offsetMin.x, 0f);
            rect.offsetMax = new Vector2(rect.offsetMax.x, 0f);
        }

        private void LateUpdate()
        {
            if (_panelLayoutFrames <= 0)
            {
                return;
            }

            ApplyPanelLayout();
            _panelLayoutFrames--;
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
                if (showInPrep)
                {
                    _lastDishListStateHash = int.MinValue;
                    ApplyManualPanelLayout();
                }
            }

            if (!showInPrep)
            {
                _wasShowingPrep = false;
                return;
            }

            if (!_wasShowingPrep)
            {
                _wasShowingPrep = true;
                _lastDishListStateHash = int.MinValue;
                ApplyManualPanelLayout();
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

            var dishHash = ComputeDishListStateHash();
            if (dishHash != _lastDishListStateHash)
            {
                _lastDishListStateHash = dishHash;
                RefreshDishList();
            }
            else if (_txtDishHeader != null)
            {
                var player = DataManager.Instance.PlayerData;
                if (player != null)
                {
                    _txtDishHeader.text = $"备菜库存 (合计 {player.GetTotalDishStock()} 份)";
                }
            }
        }

        private int ComputeDishListStateHash()
        {
            var player = DataManager.Instance.PlayerData;
            if (player == null)
            {
                return 0;
            }

            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + player.coinNum;
                foreach (var dishId in player.UnlockedDishes)
                {
                    hash = (hash * 31) + (dishId?.GetHashCode() ?? 0);
                }

                if (player.DishStock != null)
                {
                    foreach (var kv in player.DishStock)
                    {
                        hash = (hash * 31) + (kv.Key?.GetHashCode() ?? 0);
                        hash = (hash * 31) + kv.Value;
                    }
                }

                return hash;
            }
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

        private void RefreshDishList()
        {
            var player = DataManager.Instance.PlayerData;
            if (player == null || _dishListContent == null || _dishItemTemplate == null)
            {
                return;
            }

            if (_txtDishHeader != null)
            {
                var totalStock = player.GetTotalDishStock();
                _txtDishHeader.text = $"备菜库存 (合计 {totalStock} 份)";
            }

            foreach (var item in _dishItems)
            {
                if (item != null)
                {
                    Destroy(item);
                }
            }

            _dishItems.Clear();

            var allDishes = EventSystemManager.Instance?.GetAllDishes();
            if (allDishes == null)
            {
                return;
            }

            foreach (var dish in allDishes)
            {
                if (dish == null || !player.UnlockedDishes.Contains(dish.DishId))
                {
                    continue;
                }

                var go = Instantiate(_dishItemTemplate, _dishListContent);
                go.SetActive(true);
                go.name = $"DishItem_{dish.DishId}";

                var txtName = go.transform.Find("txt_DishName")?.GetComponent<TextMeshProUGUI>();
                var txtCost = go.transform.Find("txt_DishCost")?.GetComponent<TextMeshProUGUI>();
                var txtStatus = go.transform.Find("txt_DishStatus")?.GetComponent<TextMeshProUGUI>();
                var btnAction = go.transform.Find("btn_Action")?.GetComponent<Button>();
                var btnLabel = go.transform.Find("btn_Action/txt_BtnLabel")?.GetComponent<TextMeshProUGUI>();

                var currentStock = player.GetDishStock(dish.DishId);

                if (txtName != null)
                {
                    txtName.text = dish.DishName;
                }

                if (txtCost != null)
                {
                    txtCost.text = $"进货: {dish.IngredientCost}银两/份";
                }

                if (txtStatus != null)
                {
                    if (currentStock > 0)
                    {
                        txtStatus.text = $"<color=green>库存 {currentStock}</color>";
                    }
                    else
                    {
                        txtStatus.text = "<color=#888>未备</color>";
                    }
                }

                if (btnLabel != null)
                {
                    btnLabel.text = "备菜+1";
                }

                if (btnAction != null)
                {
                    btnAction.interactable = player.coinNum >= dish.IngredientCost;

                    var capturedDishId = dish.DishId;
                    var capturedCost = dish.IngredientCost;
                    btnAction.onClick.RemoveAllListeners();
                    btnAction.onClick.AddListener(() => OnClickStockDish(capturedDishId, capturedCost));
                }

                _dishItems.Add(go);
            }

            for (var i = 0; i < _dishItems.Count; i++)
            {
                if (_dishItems[i] != null && _dishItems[i].transform is RectTransform rowRect)
                {
                    LayoutDishItemRow(rowRect, i * (DishRowHeight + DishRowSpacing));
                }
            }

            ApplyManualPanelLayout();
            _lastDishListStateHash = ComputeDishListStateHash();
        }

        private void OnClickStockDish(string dishId, int cost)
        {
            var player = DataManager.Instance.PlayerData;
            if (player == null)
            {
                return;
            }

            if (player.coinNum < cost)
            {
                Debug.Log($"[DayCyclePanel] 钱不够: 需要{cost}, 当前{player.coinNum}");
                return;
            }

            DataManager.Instance.ChangeCoinNum(-cost);
            player.AddDishStock(dishId, 1);

            Debug.Log($"[DayCyclePanel] 备菜: {dishId} +1, 当前库存={player.GetDishStock(dishId)}");

            RefreshDishList();
        }
    }
}
