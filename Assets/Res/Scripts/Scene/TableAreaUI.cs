using System.Collections;
using JN.Client.Manager;
using JN.Client.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JN.Client.Scene
{
    /// <summary>
    /// 负责桌位区域相关的运行时逻辑。
    /// </summary>
    public class TableAreaUI : MonoBehaviour
    {
        private const float PurchasePriceUiScaleMultiplier = 1f;

        [SerializeField] private Vector3 offset = new(0f, 0.72f, 0f);
        [SerializeField] public GameObject group_PayCoinNum;
        [SerializeField] private TextMeshProUGUI payCoinText;
        [SerializeField] private TextMeshProUGUI runtimeStatusText;
        [SerializeField] private TableOrderButtonUI orderButtonInstance;
        [SerializeField] private TableCleanButtonUI cleanButtonInstance;

        private TableArea tableArea;
        private Transform targetTile;
        private RectTransform rectTransform;
        private Coroutine countdownRoutine;

        /// <summary>
        /// 初始化组件引用和运行时状态。
        /// </summary>
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            CacheStaticReferences();

            DisableBaseRaycastTargets();
            HideStatus();
        }

        /// <summary>
        /// 初始化静态绑定引用。
        /// </summary>
        /// <param name="table">桌位对象。</param>
        public void InitBinding(Transform table)
        {
            targetTile = table;
        }

        /// <summary>
        /// 处理绑定桌位相关逻辑。
        /// </summary>
        /// <param name="table">桌位对象。</param>
        public void BindTable(TableArea table)
        {
            tableArea = table;
            targetTile = table != null ? table.transform : targetTile;
        }

        /// <summary>
        /// 设置解锁提示显隐。
        /// </summary>
        /// <param name="visible">参数值。</param>
        /// <param name="cost">价格。</param>
        public void SetUnlockPrompt(bool visible, int cost)
        {
            if (group_PayCoinNum != null)
            {
                group_PayCoinNum.SetActive(visible);
                group_PayCoinNum.transform.localScale = Vector3.one * PurchasePriceUiScaleMultiplier;
            }

            if (payCoinText != null)
            {
                payCoinText.text = cost.ToString();
            }
        }

        /// <summary>
        /// 刷新状态。
        /// </summary>
        /// <param name="state">参数值。</param>
        /// <param name="customText">参数值。</param>
        public void RefreshState(TavernTableRuntimeState state, string customText = null)
        {
            if (state == TavernTableRuntimeState.Locked)
            {
                HideStatus();
                return;
            }

            EnsureRuntimeStatusText();
            if (runtimeStatusText == null)
            {
                return;
            }

            RefreshActionButtons(state);

            if (state == TavernTableRuntimeState.Idle || state == TavernTableRuntimeState.Dining)
            {
                runtimeStatusText.gameObject.SetActive(false);
                StopRuntimeEffects();
                return;
            }

            runtimeStatusText.gameObject.SetActive(true);
            runtimeStatusText.text = customText ?? GetDefaultStateText(state);
            runtimeStatusText.color = GetDefaultStateColor(state);
            ApplyStateAnimation(state);
        }

        /// <summary>
        /// 隐藏状态。
        /// </summary>
        public void HideStatus()
        {
            StopRuntimeEffects();
            HideActionButtons();
            if (runtimeStatusText != null)
            {
                runtimeStatusText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 启动状态倒计时。
        /// </summary>
        /// <param name="state">参数值。</param>
        /// <param name="duration">持续时间。</param>
        /// <param name="prefix">参数值。</param>
        public void StartStateCountdown(TavernTableRuntimeState state, float duration, string prefix = null)
        {
            EnsureRuntimeStatusText();
            if (runtimeStatusText == null)
            {
                return;
            }

            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
            }

            var labelPrefix = string.IsNullOrWhiteSpace(prefix) ? GetDefaultStateText(state) : prefix;
            countdownRoutine = StartCoroutine(StateCountdownRoutine(state, duration, labelPrefix));
        }

        /// <summary>
        /// 停止状态倒计时。
        /// </summary>
        public void StopStateCountdown()
        {
            if (countdownRoutine == null)
            {
                return;
            }

            StopCoroutine(countdownRoutine);
            countdownRoutine = null;
        }

        /// <summary>
        /// 获取场景锚点位置。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        public Vector3 GetWorldAnchorPosition()
        {
            return targetTile == null ? Vector3.zero : targetTile.position + offset;
        }

        /// <summary>
        /// 缓存静态引用。
        /// </summary>
        private void CacheStaticReferences()
        {
            if (group_PayCoinNum == null)
            {
                group_PayCoinNum = transform.Find("group_PayCoinNum")?.gameObject;
            }

            if (payCoinText == null)
            {
                payCoinText = transform.Find("group_PayCoinNum/txt_CoinNum")?.GetComponent<TextMeshProUGUI>();
            }

            if (runtimeStatusText == null)
            {
                runtimeStatusText = transform.Find("txt_RuntimeState")?.GetComponent<TextMeshProUGUI>();
            }

            if (orderButtonInstance == null)
            {
                orderButtonInstance = transform.Find("NewOrderBtn")?.GetComponent<TableOrderButtonUI>();
            }

            if (cleanButtonInstance == null)
            {
                cleanButtonInstance = transform.Find("CleanBtn")?.GetComponent<TableCleanButtonUI>();
            }
        }

        /// <summary>
        /// 确保运行时状态文本。
        /// </summary>
        private void EnsureRuntimeStatusText()
        {
            CacheStaticReferences();
            MoveRuntimeStatusBelowActionButtons();
        }

        /// <summary>
        /// 确保状态文案始终位于点单按钮下方，同时维持较低层级，避免遮挡按钮点击与视觉。
        /// </summary>
        private void MoveRuntimeStatusBelowActionButtons()
        {
            if (runtimeStatusText == null)
            {
                return;
            }

            var runtimeTransform = runtimeStatusText.transform;
            var targetIndex = runtimeTransform.GetSiblingIndex();

            if (orderButtonInstance != null)
            {
                targetIndex = Mathf.Min(targetIndex, orderButtonInstance.transform.GetSiblingIndex());
            }

            if (cleanButtonInstance != null)
            {
                targetIndex = Mathf.Min(targetIndex, cleanButtonInstance.transform.GetSiblingIndex());
            }

            runtimeTransform.SetSiblingIndex(Mathf.Max(0, targetIndex - 1));

            var runtimeRect = runtimeStatusText.rectTransform;
            var orderRect = orderButtonInstance != null ? orderButtonInstance.GetComponent<RectTransform>() : null;
            if (runtimeRect == null || orderRect == null || runtimeRect.parent != orderRect.parent)
            {
                return;
            }

            var orderAnchoredPosition = orderRect.anchoredPosition;
            var verticalSpacing = 16f;
            runtimeRect.anchoredPosition = new Vector2(
                orderAnchoredPosition.x,
                orderAnchoredPosition.y - orderRect.rect.height - verticalSpacing);
        }

        /// <summary>
        /// 刷新操作按钮。
        /// </summary>
        /// <param name="state">参数值。</param>
        private void RefreshActionButtons(TavernTableRuntimeState state)
        {
            if (tableArea == null)
            {
                return;
            }

            switch (state)
            {
                case TavernTableRuntimeState.WaitingOrder:
                    EnsureOrderButton();
                    HideCleanButton();
                    orderButtonInstance.ShowWaitingForOrder(null, true);
                    runtimeStatusText?.gameObject.SetActive(false);
                    break;
                case TavernTableRuntimeState.WaitingServe:
                    HideOrderButton();
                    HideCleanButton();
                    break;
                case TavernTableRuntimeState.Dining:
                    HideOrderButton();
                    HideCleanButton();
                    runtimeStatusText?.gameObject.SetActive(false);
                    break;
                case TavernTableRuntimeState.Checkout:
                    EnsureOrderButton();
                    HideCleanButton();
                    orderButtonInstance.ShowReadyToClaim();
                    runtimeStatusText?.gameObject.SetActive(false);
                    break;
                case TavernTableRuntimeState.Cleaning:
                    HideOrderButton();
                    HideCleanButton();
                    break;
                default:
                    HideActionButtons();
                    break;
            }
        }

        /// <summary>
        /// 确保点单按钮。
        /// </summary>
        private void EnsureOrderButton()
        {
            CacheStaticReferences();
            if (orderButtonInstance != null)
            {
                orderButtonInstance.gameObject.SetActive(true);
                orderButtonInstance.Init(tableArea);
                MoveRuntimeStatusBelowActionButtons();
            }
        }

        /// <summary>
        /// 确保清扫按钮。
        /// </summary>
        private void EnsureCleanButton()
        {
            CacheStaticReferences();
            if (cleanButtonInstance != null)
            {
                cleanButtonInstance.gameObject.SetActive(true);
                MoveRuntimeStatusBelowActionButtons();
            }
        }

        /// <summary>
        /// 隐藏点单按钮。
        /// </summary>
        private void HideOrderButton()
        {
            if (orderButtonInstance == null)
            {
                return;
            }

            orderButtonInstance.ResetVisuals();
            orderButtonInstance.gameObject.SetActive(false);
        }

        /// <summary>
        /// 隐藏清扫按钮。
        /// </summary>
        private void HideCleanButton()
        {
            if (cleanButtonInstance == null)
            {
                return;
            }

            cleanButtonInstance.ResetVisuals();
            cleanButtonInstance.gameObject.SetActive(false);
        }

        /// <summary>
        /// 隐藏操作按钮。
        /// </summary>
        private void HideActionButtons()
        {
            HideOrderButton();
            HideCleanButton();
        }

        /// <summary>
        /// 获取默认点单图标。
        /// </summary>
        /// <returns>返回匹配到的对象引用。</returns>
        private Sprite GetDefaultOrderIcon()
        {
            var products = SO_Product.GetAll();
            if (products != null && products.Count > 0 && products[0] != null)
            {
                return products[0].icon;
            }

            return null;
        }

        /// <summary>
        /// 应用状态动画。
        /// </summary>
        /// <param name="state">参数值。</param>
        private void ApplyStateAnimation(TavernTableRuntimeState state)
        {
            if (runtimeStatusText == null)
            {
                return;
            }

            runtimeStatusText.transform.localScale = Vector3.one;
            MoveRuntimeStatusBelowActionButtons();
        }

        /// <summary>
        /// 按秒刷新桌位状态倒计时。
        /// </summary>
        /// <param name="state">参数值。</param>
        /// <param name="duration">持续时间。</param>
        /// <param name="prefix">参数值。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator StateCountdownRoutine(TavernTableRuntimeState state, float duration, string prefix)
        {
            duration = Mathf.Max(0f, duration);
            runtimeStatusText.text = prefix;
            runtimeStatusText.color = GetDefaultStateColor(state);
            while (duration > 0f)
            {
                yield return null;
                duration -= Time.deltaTime;
            }

            countdownRoutine = null;
            runtimeStatusText.text = GetDefaultStateText(state);
        }

        /// <summary>
        /// 停止运行时特效。
        /// </summary>
        private void StopRuntimeEffects()
        {
            StopStateCountdown();
            if (runtimeStatusText != null)
            {
                runtimeStatusText.transform.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// 禁用时移除事件监听，避免重复回调。
        /// </summary>
        private void OnDisable()
        {
            StopRuntimeEffects();
        }

        /// <summary>
        /// 禁用底层不需要交互的射线目标。
        /// </summary>
        private void DisableBaseRaycastTargets()
        {
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (ShouldKeepRaycast(graphic.transform))
                {
                    continue;
                }

                graphic.raycastTarget = false;
            }

            foreach (var text in GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (ShouldKeepRaycast(text.transform))
                {
                    continue;
                }

                text.raycastTarget = false;
            }
        }

        /// <summary>
        /// 处理是否保留射线相关逻辑。
        /// </summary>
        /// <param name="target">目标对象。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private bool ShouldKeepRaycast(Transform target)
        {
            return (orderButtonInstance != null && target.IsChildOf(orderButtonInstance.transform))
                   || (cleanButtonInstance != null && target.IsChildOf(cleanButtonInstance.transform));
        }

        /// <summary>
        /// 获取默认状态文本。
        /// </summary>
        /// <param name="state">参数值。</param>
        /// <returns>返回方法执行后的结果。</returns>
        private static string GetDefaultStateText(TavernTableRuntimeState state)
        {
            switch (state)
            {
                case TavernTableRuntimeState.Reserved:
                    return "入座中";
                case TavernTableRuntimeState.WaitingOrder:
                    return "点菜";
                case TavernTableRuntimeState.WaitingServe:
                    return "等待上菜";
                case TavernTableRuntimeState.Dining:
                    return "吃饭中";
                case TavernTableRuntimeState.Checkout:
                    return "结账";
                case TavernTableRuntimeState.Cleaning:
                    return "清理中";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 获取默认状态颜色。
        /// </summary>
        /// <param name="state">参数值。</param>
        /// <returns>返回方法执行后的结果。</returns>
        private static Color GetDefaultStateColor(TavernTableRuntimeState state)
        {
            switch (state)
            {
                case TavernTableRuntimeState.WaitingOrder:
                case TavernTableRuntimeState.WaitingServe:
                    return new Color(1f, 0.85f, 0.2f);
                case TavernTableRuntimeState.Checkout:
                    return new Color(0.4f, 1f, 0.4f);
                case TavernTableRuntimeState.Cleaning:
                    return new Color(0.5f, 0.9f, 1f);
                default:
                    return Color.white;
            }
        }
    }
}
