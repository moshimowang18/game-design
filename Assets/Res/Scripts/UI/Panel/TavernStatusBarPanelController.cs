using System.Collections;
using JN.Client.Manager;
using JN.Client.Model;
using JN.Client.Scene;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JN.Client.UI
{
    public class TavernStatusBarPanelControllerData : UIPanelData
    {
    }

    /// <summary>
    /// 负责酒楼状态栏相关的运行时逻辑。
    /// </summary>
    public class TavernStatusBarPanelController : QFrameworkPanel<TavernStatusBarPanelControllerData>
    {
        private const string CustomerEnterQueueFillSpritePath = "Assets/Res/Resources/Textures/UI/Icons 1/customerEnterProgressFillRed.png";

        [SerializeField] private TextMeshProUGUI txt_GoldNum;
        [SerializeField] private TextMeshProUGUI txt_PlayerName;
        [SerializeField] private TextMeshProUGUI txt_ChangeGoldNum;
        [SerializeField] private TextMeshProUGUI txt_Task;
        [SerializeField] private TextMeshProUGUI txt_RuntimeInfo;
        [SerializeField] private RectTransform bottomButtonRoot;
        [SerializeField] private RectTransform topBarRoot;
        private Coroutine coinDeltaRoutine;
        private Vector2 coinDeltaBasePosition;
        private bool hasCoinDeltaBasePosition;
        private RectTransform customerEnterProgressRoot;
        private Image customerEnterProgressBackground;
        private Image customerEnterProgressFill;
        private Image customerEnterQueueBackground;
        private TMP_Text customerEnterProgressText;
        private Sprite customerEnterDefaultFillSprite;
        private Sprite customerEnterQueueFillSprite;

        /// <summary>
        /// 面板初始化时绑定控件和事件。
        /// </summary>
        protected override void OnPanelInit()
        {
            GOReferenceManager.Instance.SaveCoinTransform(txt_GoldNum.transform);
            txt_RuntimeInfo ??= transform.Find("group_TopBar/txt_RuntimeInfo")?.GetComponent<TextMeshProUGUI>();
            EnsureChangeGoldText();
            EnsureCustomerEnterProgress();
            EnsureBottomButtons();

            // txt_Task 已不再显示，主线任务文案统一移交给 StartOpeningWindowController.GuideTaskPanel。
            if (txt_Task != null)
            {
                txt_Task.gameObject.SetActive(false);
            }

            Signals.Get<UpdateCoinNumSignal>().AddListener(UpdateCoinNumHandler);
            Signals.Get<TavernRuntimeChangedSignal>().AddListener(RefreshRuntimeInfo);
            Signals.Get<TavernBusinessStateSignal>().AddListener(HandleBusinessStateChanged);
            Signals.Get<TableNumSignal>().AddListener(HandleGuideChanged);
            Signals.Get<GameplayGuideProgressSignal>().AddListener(HandleGuideChanged);
        }

        /// <summary>
        /// 响应面板显示事件并同步状态。
        /// </summary>
        protected override void OnPanelShow()
        {
            EnsureChangeGoldText();
            EnsureCustomerEnterProgress();
            txt_PlayerName.text = DataManager.Instance.PlayerData.playerName;
            txt_GoldNum.text = DataManager.Instance.PlayerData.coinNum.ToString();
            RefreshRuntimeInfo();
            RefreshTaskText();
            RefreshCustomerEnterProgress();
            ShowRecruitmentUnlockToastIfNeeded();
        }

        /// <summary>
        /// 面板关闭时清理临时状态和监听。
        /// </summary>
        protected override void OnPanelClose()
        {
            TavernSceneManager.Instance?.SetWorldCustomerEnterProgressVisible(true);
            Signals.Get<UpdateCoinNumSignal>().RemoveListener(UpdateCoinNumHandler);
            Signals.Get<TavernRuntimeChangedSignal>().RemoveListener(RefreshRuntimeInfo);
            Signals.Get<TavernBusinessStateSignal>().RemoveListener(HandleBusinessStateChanged);
            Signals.Get<TableNumSignal>().RemoveListener(HandleGuideChanged);
            Signals.Get<GameplayGuideProgressSignal>().RemoveListener(HandleGuideChanged);
        }

        private void Update()
        {
            RefreshCustomerEnterProgress();
        }

        /// <summary>
        /// 更新铜钱数量处理器。
        /// </summary>
        /// <param name="change数量">参数值。</param>
        private void UpdateCoinNumHandler(int changeNum)
        {
            txt_GoldNum.text = DataManager.Instance.PlayerData.coinNum.ToString();
            EnsureChangeGoldText();
            if (txt_ChangeGoldNum == null || changeNum == 0)
            {
                return;
            }

            if (changeNum > 0)
            {
                txt_ChangeGoldNum.text = $"+{changeNum}";
                txt_ChangeGoldNum.color = Color.green;
            }
            else if (changeNum < 0)
            {
                txt_ChangeGoldNum.text = changeNum.ToString();
                txt_ChangeGoldNum.color = Color.red;
            }
            else
            {
                txt_ChangeGoldNum.text = string.Empty;
            }

            if (coinDeltaRoutine != null)
            {
                StopCoroutine(coinDeltaRoutine);
            }

            coinDeltaRoutine = StartCoroutine(CoinDeltaAnim(txt_ChangeGoldNum.rectTransform));
        }

        /// <summary>
        /// 确保铜钱变化文本存在，并记录动画的固定起始位置。
        /// </summary>
        private void EnsureChangeGoldText()
        {
            if (txt_ChangeGoldNum == null)
            {
                txt_ChangeGoldNum = transform.Find("group_TopBar/@group_GoldNum/@txt_ChangeGoldNum")?.GetComponent<TextMeshProUGUI>()
                                    ?? transform.Find("group_TopBar/@group_GoldNum/txt_ChangeGoldNum")?.GetComponent<TextMeshProUGUI>()
                                    ?? transform.Find("@group_GoldNum/@txt_ChangeGoldNum")?.GetComponent<TextMeshProUGUI>()
                                    ?? transform.Find("@group_GoldNum/txt_ChangeGoldNum")?.GetComponent<TextMeshProUGUI>();
            }

            if (txt_ChangeGoldNum == null)
            {
                return;
            }

            if (!hasCoinDeltaBasePosition)
            {
                coinDeltaBasePosition = txt_ChangeGoldNum.rectTransform.anchoredPosition;
                hasCoinDeltaBasePosition = true;
            }

            var canvasGroup = txt_ChangeGoldNum.GetComponent<CanvasGroup>() ?? txt_ChangeGoldNum.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
        }

        /// <summary>
        /// 处理引导变化。
        /// </summary>
        private void HandleGuideChanged()
        {
            RefreshRuntimeInfo();
            RefreshTaskText();
            RefreshCustomerEnterProgress();
            ShowRecruitmentUnlockToastIfNeeded();
        }

        /// <summary>
        /// 响应酒楼营业状态变化并启动或停止顾客流程。
        /// </summary>
        /// <param name="is打开">参数值。</param>
        private void HandleBusinessStateChanged(bool isOpen)
        {
            RefreshRuntimeInfo();
            RefreshTaskText();
            RefreshCustomerEnterProgress();
        }

        /// <summary>
        /// 刷新运行时信息。
        /// </summary>
        private void RefreshRuntimeInfo()
        {
            if (txt_RuntimeInfo == null)
            {
                return;
            }

            var tavernData = DataManager.Instance.TavernData;
            var sceneManager = TavernSceneManager.Instance;
            var queueText = sceneManager != null ? $" | 排队 {sceneManager.GetQueueCustomerCount()}" : string.Empty;

            txt_RuntimeInfo.text =
                $"桌子 {DataManager.Instance.GetUnlockedTableCount()}/6 | 菜品 {tavernData.availableDishes} | 接待 {tavernData.totalServedCustomers}{queueText}";
        }

        /// <summary>
        /// 任务文案已迁移至 GuideTaskPanel，状态栏不再展示当前任务，
        /// 这里只确保 txt_Task 始终为隐藏，避免旧 prefab 中误启用导致的重复展示。
        /// </summary>
        private void RefreshTaskText()
        {
            if (txt_Task != null && txt_Task.gameObject.activeSelf)
            {
                txt_Task.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 处理铜钱变化动画相关逻辑。
        /// </summary>
        /// <param name="target">目标对象。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator CoinDeltaAnim(RectTransform target)
        {
            var time = 0f;
            const float duration = 1f;
            var start = coinDeltaBasePosition;
            var end = start + new Vector2(0f, 80f);

            var canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 1f;
            target.gameObject.SetActive(true);
            target.SetAsLastSibling();
            target.anchoredPosition = start;

            while (time < duration)
            {
                time += Time.deltaTime;
                var progress = Mathf.Clamp01(time / duration);
                target.anchoredPosition = Vector2.Lerp(start, end, progress);
                canvasGroup.alpha = 1f - progress;
                yield return null;
            }

            canvasGroup.alpha = 0f;
            target.anchoredPosition = start;
            coinDeltaRoutine = null;
        }

        /// <summary>
        /// 确保右上角顾客进店进度条引用存在，并接管场景中的同类显示。
        /// </summary>
        private void EnsureCustomerEnterProgress()
        {
            topBarRoot ??= transform.Find("group_TopBar") as RectTransform;
            if (topBarRoot == null)
            {
                return;
            }

            if (customerEnterProgressRoot == null)
            {
                customerEnterProgressRoot = topBarRoot.Find("group_CustomerEnterProgress") as RectTransform;
            }

            if (customerEnterProgressRoot == null)
            {
                return;
            }

            customerEnterProgressBackground ??= customerEnterProgressRoot.Find("img_ProgressBg")?.GetComponent<Image>();
            customerEnterProgressFill ??= customerEnterProgressRoot.Find("img_ProgressBg/img_ProgressFill")?.GetComponent<Image>();
            customerEnterQueueBackground ??= customerEnterProgressRoot.Find("img_QueueBg")?.GetComponent<Image>();
            customerEnterProgressText ??= customerEnterProgressRoot.Find("txt_Time")?.GetComponent<TMP_Text>()
                                           ?? customerEnterProgressRoot.GetComponentInChildren<TMP_Text>(true);

            if (customerEnterProgressBackground == null || customerEnterProgressFill == null || customerEnterProgressText == null)
            {
                customerEnterProgressRoot = null;
                return;
            }

            if (customerEnterProgressFill != null && customerEnterDefaultFillSprite == null)
            {
                customerEnterDefaultFillSprite = customerEnterProgressFill.sprite;
            }

            customerEnterQueueFillSprite ??= GameplayResourceStore.LoadAsset<Sprite>(CustomerEnterQueueFillSpritePath);
            TavernSceneManager.Instance?.SetWorldCustomerEnterProgressVisible(false);
        }

        /// <summary>
        /// 刷新右上角顾客进店进度显示。
        /// </summary>
        private void RefreshCustomerEnterProgress()
        {
            EnsureCustomerEnterProgress();
            if (customerEnterProgressRoot == null)
            {
                return;
            }

            var sceneManager = TavernSceneManager.Instance;
            var shouldShow = DataManager.Instance != null && DataManager.Instance.TavernData.isOpen && sceneManager != null;
            customerEnterProgressRoot.gameObject.SetActive(shouldShow);
            if (!shouldShow)
            {
                return;
            }

            sceneManager.SetWorldCustomerEnterProgressVisible(false);
            var remaining = sceneManager.GetNextCustomerSpawnRemaining();
            var interval = sceneManager.GetCustomerSpawnInterval();
            var queueCount = sceneManager.GetQueueCustomerCount();
            var hasQueue = queueCount > 0;
            var progress = interval <= 0.01f ? 1f : 1f - Mathf.Clamp01(remaining / interval);
            var remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(remaining));

            if (customerEnterProgressBackground != null)
            {
                customerEnterProgressBackground.gameObject.SetActive(true);
            }

            if (customerEnterQueueBackground != null)
            {
                customerEnterQueueBackground.gameObject.SetActive(hasQueue);
            }

            if (customerEnterProgressFill != null)
            {
                customerEnterProgressFill.sprite = hasQueue && customerEnterQueueFillSprite != null
                    ? customerEnterQueueFillSprite
                    : customerEnterDefaultFillSprite;
                customerEnterProgressFill.fillAmount = hasQueue ? 1f : progress;
            }

            if (customerEnterProgressText != null)
            {
                customerEnterProgressText.text = hasQueue ? $"{queueCount}人排队中" : $"{remainingSeconds} s";
                customerEnterProgressText.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 确保底部设施、城镇和员工三个按钮存在。
        /// </summary>
        private void EnsureBottomButtons()
        {
            bottomButtonRoot ??= transform.Find("group_BottomButtons")?.GetComponent<RectTransform>();
            if (bottomButtonRoot == null)
            {
                Debug.LogWarning("[TavernStatusBarPanelController] 预制体缺少 group_BottomButtons 节点，请在面板 prefab 内配置底部按钮。", this);
                return;
            }

            BindBottomButton("btn_Facility", OnClickFacilityButton);
            BindBottomButton("btn_Town", OnClickTownButton);
            BindBottomButton("btn_Staff", OnClickStaffButton);
        }

        /// <summary>
        /// 绑定底部按钮点击事件。
        /// </summary>
        /// <param name="buttonName">按钮节点名。</param>
        /// <param name="onClick">点击回调。</param>
        private void BindBottomButton(string buttonName, UnityEngine.Events.UnityAction onClick)
        {
            var button = bottomButtonRoot != null ? bottomButtonRoot.Find(buttonName)?.GetComponent<Button>() : null;
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
        }

        /// <summary>
        /// 打开设施列表信息。
        /// </summary>
        private void OnClickFacilityButton()
        {
            TavernRuntimeModalUI.ShowInfoPanel("设施列表", $"桌子：{DataManager.Instance.GetUnlockedTableCount()}/6\n菜品：{DataManager.Instance.TavernData.availableDishes}\n当前阶段：{DataManager.Instance.GetGameplayGuideSnapshot().Stage}");
        }

        /// <summary>
        /// 返回城镇场景。
        /// </summary>
        private void OnClickTownButton()
        {
            StartCoroutine(GameManager.Instance.LoadSceneAsync("Town", () =>
            {
                if (UIKit.GetPanel<TavernStatusBarPanelController>() != null)
                {
                    UIKit.ClosePanel<TavernStatusBarPanelController>();
                }
            }));
        }

        /// <summary>
        /// 打开底部员工入口，展示厨师和小二的页签招聘列表。
        /// </summary>
        private void OnClickStaffButton()
        {
            if (TavernDayManager.Instance != null && !TavernDayManager.Instance.CanSpendMoney())
            {
                Debug.Log("[StatusBar] 营业中不能招聘员工，请等准备阶段");
                return;
            }

            var dataManager = DataManager.Instance;
            var guide = dataManager.GameplayGuideData;
            var chefCount = dataManager.GetHiredGuideChefCount();
            var waiterCount = dataManager.GetHiredGuideWaiterCount();
            if (!dataManager.CanRecruitGuideStaff())
            {
                TavernRuntimeModalUI.ShowInfoPanel("员工信息", BuildStaffInfoText(guide.hiredShopkeeper, chefCount, waiterCount, "完成前置设备后可招聘厨师和小二"));
                return;
            }

            var defaultRole = chefCount <= waiterCount ? RecruitPanelRole.Chef : RecruitPanelRole.Waiter;
            TavernRuntimeModalUI.ShowRecruitListPanel(defaultRole);
        }

        /// <summary>
        /// 拼接员工信息面板的展示文案。
        /// </summary>
        /// <param name="hasShopkeeper">是否已招聘掌柜。</param>
        /// <param name="chefCount">当前厨师数量。</param>
        /// <param name="waiterCount">当前小二数量。</param>
        /// <param name="tip">补充提示。</param>
        /// <returns>员工信息文本。</returns>
        private static string BuildStaffInfoText(bool hasShopkeeper, int chefCount, int waiterCount, string tip)
        {
            return $"掌柜：{(hasShopkeeper ? "已招聘" : "未招聘")}\n厨师：{chefCount}/{DataManager.MaxGuideChefCount}\n小二：{waiterCount}/{DataManager.MaxGuideWaiterCount}\n{tip}";
        }

        /// <summary>
        /// 招聘阶段首次开启时显示全屏提示。
        /// </summary>
        private void ShowRecruitmentUnlockToastIfNeeded()
        {
            if (!DataManager.Instance.ShouldShowRecruitmentUnlockToast())
            {
                return;
            }

            TavernRuntimeModalUI.ShowNewFeatureOpenToast();
            DataManager.Instance.MarkRecruitmentUnlockToastShown();
        }

    }
}
