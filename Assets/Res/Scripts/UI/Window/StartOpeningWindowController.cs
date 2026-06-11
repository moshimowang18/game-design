using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using JN.Client.Manager;
using JN.Client.Model;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JN.Client.UI
{
    public class StartOpeningWindowControllerData : UIPanelData
    {
    }

    /// <summary>
    /// 负责开业引导窗口逻辑。
    /// </summary>
    public class StartOpeningWindowController : QFrameworkPanel<StartOpeningWindowControllerData>
    {
        private const int CounterEquipmentId = 0;
        private const int StoveEquipmentId = 3;
        private const int ShopkeeperStaffId = 1;
        private const int ChefStaffId = 4;
        private const int WaiterStaffId = 5;

        [SerializeField] private Button btn_Opening;
        [SerializeField] private TextMeshProUGUI txt_OpeningInfo;
        [SerializeField] private GameObject group_OpeningEffect;

        [SerializeField] private RectTransform guideTaskPanel;
        [SerializeField] private TextMeshProUGUI guideTaskTitle;
        [SerializeField] private List<TextMeshProUGUI> guideTaskTexts = new();
        [SerializeField] private Button guidePrimaryActionButton;
        [SerializeField] private Button guideSecondaryActionButton;
        [SerializeField] private TextMeshProUGUI guidePrimaryActionText;
        [SerializeField] private TextMeshProUGUI guideSecondaryActionText;
        [SerializeField] private CanvasGroup guideToastCanvasGroup;
        [SerializeField] private TextMeshProUGUI guideToastText;

        private Coroutine guideToastRoutine;

        /// <summary>
        /// 面板初始化时绑定控件和事件。
        /// </summary>
        protected override void OnPanelInit()
        {
            EnsureGuideUi();

            // @txt_OpeningInfo 已不再展示开业进度文案，统一交由 GuideTaskPanel 来呈现任务进度。
            if (txt_OpeningInfo != null)
            {
                txt_OpeningInfo.gameObject.SetActive(false);
            }

            if (btn_Opening != null)
            {
                btn_Opening.onClick.AddListener(OnClickBtnOpening);
            }

            Signals.Get<TableNumSignal>().AddListener(RefreshOpeningInfo);
            Signals.Get<TableNumSignal>().AddListener(RefreshGuideUi);
            Signals.Get<TavernRuntimeChangedSignal>().AddListener(RefreshOpeningInfo);
            Signals.Get<TavernRuntimeChangedSignal>().AddListener(RefreshGuideUi);
            Signals.Get<GameplayGuideProgressSignal>().AddListener(RefreshGuideUi);
        }

        /// <summary>
        /// 响应面板显示事件并同步状态。
        /// </summary>
        protected override void OnPanelShow()
        {
            RefreshOpeningInfo();
            RefreshGuideUi();
        }

        /// <summary>
        /// 面板关闭时清理临时状态和监听。
        /// </summary>
        protected override void OnPanelClose()
        {
            if (btn_Opening != null)
            {
                btn_Opening.onClick.RemoveListener(OnClickBtnOpening);
            }

            Signals.Get<TableNumSignal>().RemoveListener(RefreshOpeningInfo);
            Signals.Get<TableNumSignal>().RemoveListener(RefreshGuideUi);
            Signals.Get<TavernRuntimeChangedSignal>().RemoveListener(RefreshOpeningInfo);
            Signals.Get<TavernRuntimeChangedSignal>().RemoveListener(RefreshGuideUi);
            Signals.Get<GameplayGuideProgressSignal>().RemoveListener(RefreshGuideUi);

            if (guideToastRoutine != null)
            {
                StopCoroutine(guideToastRoutine);
                guideToastRoutine = null;
            }
        }

        /// <summary>
        /// 处理开业按钮点击并切换酒楼营业状态。
        /// </summary>
        private void OnClickBtnOpening()
        {
            if (!DataManager.Instance.CanOpenTavernBusiness())
            {
                return;
            }

            DataManager.Instance.ResetTransientTavernState();
            DataManager.Instance.SetTavernOpen(true);

            if (group_OpeningEffect != null)
            {
                group_OpeningEffect.SetActive(true);
            }

            DOVirtual.DelayedCall(3f, () => UIKit.ClosePanel<StartOpeningWindowController>());
        }

        /// <summary>
        /// 刷新开局信息。
        /// 当前需求下顶部不再展示开业引导文本（统一在 GuideTaskPanel 内呈现任务），
        /// 这里仅保留为占位，确保旧字段不会再被赋值，但仍允许将组件隐藏。
        /// </summary>
        private void RefreshOpeningInfo()
        {
            if (txt_OpeningInfo != null && txt_OpeningInfo.gameObject.activeSelf)
            {
                txt_OpeningInfo.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 刷新引导界面。
        /// </summary>
        private void RefreshGuideUi()
        {
            EnsureGuideUi();

            var snapshot = DataManager.Instance.GetGameplayGuideSnapshot();

            if (guideTaskPanel != null)
            {
                guideTaskPanel.gameObject.SetActive(false);
            }

            if (guideTaskTitle != null)
            {
                guideTaskTitle.text = snapshot.Stage == GameplayGuideStage.Recruit ? "主线任务：招聘" : "主线任务：开店";
            }

            for (var index = 0; index < guideTaskTexts.Count; index++)
            {
                if (guideTaskTexts[index] == null)
                {
                    continue;
                }

                if (index >= snapshot.ActiveTasks.Count)
                {
                    guideTaskTexts[index].gameObject.SetActive(false);
                    continue;
                }

                var task = snapshot.ActiveTasks[index];
                guideTaskTexts[index].gameObject.SetActive(true);
                guideTaskTexts[index].text = $"• {task.Title} ({task.Current}/{task.Target})";
                guideTaskTexts[index].color = task.IsCompleted
                    ? new Color(0.56f, 1f, 0.57f, 1f)
                    : new Color(1f, 0.94f, 0.76f, 1f);
            }

            if (btn_Opening != null)
            {
                btn_Opening.gameObject.SetActive(snapshot.CanOpenBusiness && !DataManager.Instance.TavernData.isOpen);
            }

            UpdateGuideActionButtons(snapshot);

            if (DataManager.Instance.ShouldShowRecruitmentUnlockToast())
            {
                DataManager.Instance.MarkRecruitmentUnlockToastShown();
                ShowGuideToast("招聘已开启", 2f);
            }

            if (guideTaskPanel != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(guideTaskPanel);
            }
        }

        /// <summary>
        /// 更新引导操作按钮。
        /// </summary>
        /// <param name="snapshot">参数值。</param>
        private void UpdateGuideActionButtons(GameplayGuideSnapshot snapshot)
        {
            if (DataManager.Instance.TavernData.isOpen)
            {
                BindGuideActionButton(guidePrimaryActionButton, guidePrimaryActionText, false, string.Empty, null);
                BindGuideActionButton(guideSecondaryActionButton, guideSecondaryActionText, false, string.Empty, null);
                return;
            }

            if (snapshot.Stage == GameplayGuideStage.Build)
            {
                var showCounterButton = !DataManager.Instance.GameplayGuideData.purchasedCounter;
                var showStoveButton = !DataManager.Instance.GameplayGuideData.purchasedStove;

                BindGuideActionButton(
                    guidePrimaryActionButton,
                    guidePrimaryActionText,
                    showCounterButton,
                    $"购买掌柜桌\n{GetEquipmentCost(CounterEquipmentId)} 铜钱",
                    HandleBuyCounter);

                BindGuideActionButton(
                    guideSecondaryActionButton,
                    guideSecondaryActionText,
                    showStoveButton,
                    $"购买灶台\n{GetEquipmentCost(StoveEquipmentId)} 铜钱",
                    HandleBuyStove);
                return;
            }

            if (snapshot.Stage == GameplayGuideStage.Recruit)
            {
                var showShopkeeperButton = !DataManager.Instance.GameplayGuideData.hiredShopkeeper;
                var showChefButton = !DataManager.Instance.GameplayGuideData.hiredChef;
                var showWaiterButton = !DataManager.Instance.GameplayGuideData.hiredWaiter;
                var boundButtonCount = 0;

                BindRecruitActionButton(
                    ref boundButtonCount,
                    showShopkeeperButton,
                    $"招聘掌柜\n{GetStaffCost(ShopkeeperStaffId, StaffRole.Waiter)} 铜钱",
                    HandleHireShopkeeper);

                BindRecruitActionButton(
                    ref boundButtonCount,
                    showChefButton,
                    $"招聘厨师\n{GetStaffCost(ChefStaffId, StaffRole.Chef)} 铜钱",
                    HandleHireChef);

                BindRecruitActionButton(
                    ref boundButtonCount,
                    showWaiterButton,
                    $"招聘小二\n{GetStaffCost(WaiterStaffId, StaffRole.Waiter)} 铜钱",
                    HandleHireWaiter);

                if (boundButtonCount == 0)
                {
                    BindGuideActionButton(guidePrimaryActionButton, guidePrimaryActionText, false, string.Empty, null);
                    BindGuideActionButton(guideSecondaryActionButton, guideSecondaryActionText, false, string.Empty, null);
                }
                else if (boundButtonCount == 1)
                {
                    BindGuideActionButton(guideSecondaryActionButton, guideSecondaryActionText, false, string.Empty, null);
                }
                return;
            }

            BindGuideActionButton(guidePrimaryActionButton, guidePrimaryActionText, false, string.Empty, null);
            BindGuideActionButton(guideSecondaryActionButton, guideSecondaryActionText, false, string.Empty, null);
        }

        /// <summary>
        /// 处理购买柜台并播放搬运表现。
        /// </summary>
        private void HandleBuyCounter()
        {
            DataManager.Instance.TryPurchaseGuideCounter(out var message);
            ShowGuideToast(message, 1.6f);
            RefreshOpeningInfo();
            RefreshGuideUi();
        }

        /// <summary>
        /// 处理购买灶台并播放搬运表现。
        /// </summary>
        private void HandleBuyStove()
        {
            DataManager.Instance.TryPurchaseGuideStove(out var message);
            ShowGuideToast(message, 1.6f);
            RefreshOpeningInfo();
            RefreshGuideUi();
        }

        /// <summary>
        /// 处理招聘掌柜。
        /// </summary>
        private void HandleHireShopkeeper()
        {
            DataManager.Instance.TryHireGuideShopkeeper(out var message);
            ShowGuideToast(message, 1.6f);
            RefreshOpeningInfo();
            RefreshGuideUi();
        }

        /// <summary>
        /// 处理招聘厨师。
        /// </summary>
        private void HandleHireChef()
        {
            DataManager.Instance.TryHireGuideChef(out var message);
            ShowGuideToast(message, 1.6f);
            RefreshOpeningInfo();
            RefreshGuideUi();
        }

        /// <summary>
        /// 处理招聘小二。
        /// </summary>
        private void HandleHireWaiter()
        {
            DataManager.Instance.TryHireGuideWaiter(out var message);
            ShowGuideToast(message, 1.6f);
            RefreshOpeningInfo();
            RefreshGuideUi();
        }

        /// <summary>
        /// 确保引导界面。
        /// </summary>
        private void EnsureGuideUi()
        {
            guideTaskPanel ??= transform.Find("GuideTaskPanel") as RectTransform;
            guideTaskTitle ??= guideTaskPanel != null ? guideTaskPanel.Find("GuideTaskTitle")?.GetComponent<TextMeshProUGUI>() : null;

            if (guideTaskTexts.Count == 0 && guideTaskPanel != null)
            {
                for (var index = 0; index < 3; index++)
                {
                    var taskText = guideTaskPanel.Find($"GuideTask_{index}")?.GetComponent<TextMeshProUGUI>();
                    if (taskText != null)
                    {
                        guideTaskTexts.Add(taskText);
                    }
                }
            }

            guidePrimaryActionButton ??= transform.Find("GuidePrimaryActionButton")?.GetComponent<Button>();
            guideSecondaryActionButton ??= transform.Find("GuideSecondaryActionButton")?.GetComponent<Button>();
            guidePrimaryActionText ??= guidePrimaryActionButton != null ? guidePrimaryActionButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            guideSecondaryActionText ??= guideSecondaryActionButton != null ? guideSecondaryActionButton.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            guideToastCanvasGroup ??= transform.Find("GuideToast")?.GetComponent<CanvasGroup>();
            guideToastText ??= guideToastCanvasGroup != null ? guideToastCanvasGroup.GetComponentInChildren<TextMeshProUGUI>(true) : null;

            if (guideTaskPanel == null
                || guideTaskTitle == null
                || guideTaskTexts.Count < 3
                || guidePrimaryActionButton == null
                || guideSecondaryActionButton == null
                || guidePrimaryActionText == null
                || guideSecondaryActionText == null
                || guideToastCanvasGroup == null
                || guideToastText == null)
            {
                Debug.LogWarning("[StartOpeningWindowController] 缺少静态引导 UI 节点，请检查 prefab 配置。");
            }
        }

        /// <summary>
        /// 处理绑定引导操作按钮相关逻辑。
        /// </summary>
        /// <param name="button">按钮对象。</param>
        /// <param name="buttonText">按钮对象。</param>
        /// <param name="visible">参数值。</param>
        /// <param name="label">参数值。</param>
        /// <param name="onClick">参数值。</param>
        private void BindGuideActionButton(Button button, TextMeshProUGUI buttonText, bool visible, string label, UnityEngine.Events.UnityAction onClick)
        {
            if (button == null)
            {
                return;
            }

            button.gameObject.SetActive(visible);
            button.onClick.RemoveAllListeners();
            if (!visible)
            {
                return;
            }

            if (buttonText != null)
            {
                buttonText.text = label;
            }

            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
        }

        /// <summary>
        /// 按顺序把招聘按钮绑定到主按钮或副按钮。
        /// </summary>
        /// <param name="boundButtonCount">当前已经绑定的按钮数量。</param>
        /// <param name="visible">当前招聘项是否需要显示。</param>
        /// <param name="label">按钮文案。</param>
        /// <param name="onClick">点击回调。</param>
        private void BindRecruitActionButton(ref int boundButtonCount, bool visible, string label, UnityEngine.Events.UnityAction onClick)
        {
            if (!visible || boundButtonCount >= 2)
            {
                return;
            }

            if (boundButtonCount == 0)
            {
                BindGuideActionButton(guidePrimaryActionButton, guidePrimaryActionText, true, label, onClick);
                boundButtonCount++;
                return;
            }

            BindGuideActionButton(guideSecondaryActionButton, guideSecondaryActionText, true, label, onClick);
            boundButtonCount++;
        }

        /// <summary>
        /// 显示引导提示。
        /// </summary>
        /// <param name="message">参数值。</param>
        /// <param name="duration">持续时间。</param>
        private void ShowGuideToast(string message, float duration)
        {
            EnsureGuideUi();
            if (guideToastText == null || guideToastCanvasGroup == null)
            {
                return;
            }

            if (guideToastRoutine != null)
            {
                StopCoroutine(guideToastRoutine);
            }

            guideToastText.text = message;
            guideToastRoutine = StartCoroutine(GuideToastRoutine(duration));
        }

        /// <summary>
        /// 按持续时间显示引导提示。
        /// </summary>
        /// <param name="duration">持续时间。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator GuideToastRoutine(float duration)
        {
            guideToastCanvasGroup.alpha = 1f;
            yield return new WaitForSeconds(duration);

            const float fadeDuration = 0.25f;
            var elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                guideToastCanvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                yield return null;
            }

            guideToastCanvasGroup.alpha = 0f;
            guideToastRoutine = null;
        }

        /// <summary>
        /// 获取设备花费。
        /// </summary>
        /// <param name="equipmentId">数据编号。</param>
        /// <returns>返回计算后的数值。</returns>
        private static int GetEquipmentCost(int equipmentId)
        {
            var equipment = SO_Equipment.GetById(equipmentId);
            var levelConfig = equipment != null ? equipment.GetLevelConfig(1) : null;
            return levelConfig != null ? Mathf.Max(0, levelConfig.upgradeCost) : 0;
        }

        /// <summary>
        /// 获取员工花费。
        /// </summary>
        /// <param name="preferredStaffId">数据编号。</param>
        /// <param name="role">参数值。</param>
        /// <returns>返回计算后的数值。</returns>
        private static int GetStaffCost(int preferredStaffId, StaffRole role)
        {
            if (DataManager.Instance != null)
            {
                return DataManager.Instance.GetGuideStaffHireCost(preferredStaffId, role);
            }

            var allStaff = SO_Staff.GetAll();
            for (var index = 0; index < allStaff.Count; index++)
            {
                var staff = allStaff[index];
                if (staff == null || staff.role != role)
                {
                    continue;
                }

                if (!int.TryParse(staff.staffId, out var numericId) || numericId != preferredStaffId)
                {
                    continue;
                }

                var levelConfig = staff.GetLevelConfig(1);
                return levelConfig != null ? Mathf.Max(0, levelConfig.hireUpgradeCost) : 0;
            }

            return 0;
        }
    }
}
