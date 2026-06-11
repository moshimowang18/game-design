using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using JN.Client.Scene;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class TableOrderButtonUI : MonoBehaviour
{
    private const string CheckoutCoinIconPath = "Assets/Res/Resources/Textures/UI/Icons 1/coin.png";
    private const float ButtonScaleMultiplier = 1.5f;

    public enum State
    {
        WaitingForOrder,
        InProgress,
        ReadyToClaim,
        WarningSkipFee
    }

    [Header("Screen Clamping")]
    [SerializeField] private float minX = -417f;
    [SerializeField] private float maxX = 418f;
    [SerializeField] private float minY = -850f;
    [SerializeField] private float maxY = 850f;

    [Header("Skip Fee Visuals")]
    [SerializeField] private Sprite warningIcon;
    [SerializeField] private Sprite catchingIcon;
    [SerializeField] private Sprite catchResultIcon;
    [SerializeField] private Sprite checkoutCoinIcon;
    [SerializeField] private GameObject warning;
    [SerializeField] private TMP_Text timerText;

    [Header("Refs")]
    [SerializeField] private Image progressImage;
    [SerializeField] private Image glowImage;
    [SerializeField] private Image icon;
    [SerializeField] private Button button;

    public State CurrentState => state;

    private TableArea boundTable;
    private State state = State.WaitingForOrder;
    private Coroutine progressRoutine;
    private Tween pulseTween;
    private Tween iconTween;
    private Sprite defaultIcon;

    /// <summary>
    /// 初始化组件引用和运行时状态。
    /// </summary>
    private void Awake()
    {
        if (icon != null)
        {
            defaultIcon = icon.sprite;
        }

        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }

        ResetVisuals();
    }

    /// <summary>
    /// 销毁时释放监听、协程和运行时缓存。
    /// </summary>
    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }

        KillTweens();
    }

    /// <summary>
    /// 在帧末同步跟随 界面 和场景表现位置。
    /// </summary>
    private void LateUpdate()
    {
        var rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return;
        }

        var anchoredPosition = rectTransform.anchoredPosition;
        anchoredPosition.x = Mathf.Clamp(anchoredPosition.x, minX, maxX);
        anchoredPosition.y = Mathf.Clamp(anchoredPosition.y, minY, maxY);
        rectTransform.anchoredPosition = anchoredPosition;
    }

    /// <summary>
    /// 初始化模块依赖和默认状态。
    /// </summary>
    /// <param name="table">桌位对象。</param>
    public void Init(TableArea table)
    {
        boundTable = table;
        transform.localScale = Vector3.one * ButtonScaleMultiplier;
    }

    /// <summary>
    /// 显示等待点单状态。
    /// </summary>
    /// <param name="productIcon">参数值。</param>
    /// <param name="canServe">参数值。</param>
    public void ShowWaitingForOrder(Sprite productIcon, bool canServe)
    {
        state = State.WaitingForOrder;
        ResetVisuals();
        SetIcon(canServe ? productIcon : warningIcon);

        if (!canServe)
        {
            if (warning != null)
            {
                warning.SetActive(true);
            }

            if (timerText != null)
            {
                timerText.gameObject.SetActive(true);
                timerText.text = "缺菜";
            }

            return;
        }

        StartGlowLoop();
    }

    /// <summary>
    /// 显示顾客用餐中的按钮状态。
    /// </summary>
    /// <param name="duration">持续时间。</param>
    /// <param name="productIcon">参数值。</param>
    public void ShowDining(float duration, Sprite productIcon)
    {
        if (state == State.InProgress)
        {
            return;
        }

        state = State.InProgress;
        ResetVisuals();
        SetIcon(productIcon);

        if (progressImage != null && progressImage.transform.parent != null)
        {
            progressImage.transform.parent.gameObject.SetActive(true);
            progressImage.fillAmount = 0f;
        }

        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
            timerText.text = string.Empty;
        }

        progressRoutine = StartCoroutine(DiningProgressRoutine(duration));
    }

    /// <summary>
    /// 显示可领取收益状态。
    /// </summary>
    public void ShowReadyToClaim()
    {
        state = State.ReadyToClaim;
        ResetVisuals();
        SetIcon(GetCheckoutCoinIcon());
        StartPulseLoop();
    }

    /// <summary>
    /// 播放缺菜警告提示。
    /// </summary>
    public void FlashNoDishWarning()
    {
        state = State.WarningSkipFee;
        ResetVisuals();
        SetIcon(warningIcon != null ? warningIcon : catchingIcon);

        if (warning != null)
        {
            warning.SetActive(true);
        }

        if (timerText != null)
        {
            timerText.gameObject.SetActive(true);
            timerText.text = "缺菜";
        }

        transform.DOPunchScale(new Vector3(0.12f, 0.12f, 0.12f), 0.3f, 6, 0.4f);
    }

    /// <summary>
    /// 重置按钮图标、文字和特效显示。
    /// </summary>
    public void ResetVisuals()
    {
        if (progressRoutine != null)
        {
            StopCoroutine(progressRoutine);
            progressRoutine = null;
        }

        KillTweens();

        if (warning != null)
        {
            warning.SetActive(false);
        }

        if (timerText != null)
        {
            timerText.gameObject.SetActive(false);
            timerText.text = string.Empty;
        }

        if (glowImage != null)
        {
            glowImage.gameObject.SetActive(true);
            glowImage.transform.localScale = Vector3.one;
        }

        if (progressImage != null)
        {
            progressImage.fillAmount = 0f;
            if (progressImage.transform.parent != null)
            {
                progressImage.transform.parent.gameObject.SetActive(false);
            }
        }

        var background = GetComponent<Image>();
        if (background != null)
        {
            background.enabled = true;
        }

        transform.localScale = Vector3.one * ButtonScaleMultiplier;
        ResetIconTransform();
    }

    /// <summary>
    /// 处理用餐进度协程相关逻辑。
    /// </summary>
    /// <param name="duration">持续时间。</param>
    /// <returns>协程迭代器。</returns>
    private IEnumerator DiningProgressRoutine(float duration)
    {
        duration = Mathf.Max(0.1f, duration);
        var remaining = duration;

        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;

            if (progressImage != null)
            {
                progressImage.fillAmount = Mathf.Clamp01(1f - (remaining / duration));
            }

            yield return null;
        }

        progressRoutine = null;
    }

    /// <summary>
    /// 处理按钮点击事件。
    /// </summary>
    private void OnClick()
    {
        if (boundTable == null)
        {
            return;
        }

        switch (state)
        {
            case State.WaitingForOrder:
                if (!boundTable.CanServeNow())
                {
                    FlashNoDishWarning();
                    return;
                }

                boundTable.HandleActionButtonClick();
                break;
            case State.ReadyToClaim:
                boundTable.HandleActionButtonClick();
                break;
        }
    }

    /// <summary>
    /// 设置按钮图标并处理缺省显示。
    /// </summary>
    /// <param name="sprite">参数值。</param>
    private void SetIcon(Sprite sprite)
    {
        if (icon == null)
        {
            return;
        }

        icon.sprite = sprite != null ? sprite : defaultIcon;
    }

    /// <summary>
    /// 获取结账按钮使用的金币图标，避免继续显示打勾图标。
    /// </summary>
    /// <returns>金币图标；读取失败时回退到原有结账图标。</returns>
    private Sprite GetCheckoutCoinIcon()
    {
        if (checkoutCoinIcon != null)
        {
            return checkoutCoinIcon;
        }

        checkoutCoinIcon = GameplayResourceStore.LoadAsset<Sprite>(CheckoutCoinIconPath);
        return checkoutCoinIcon != null ? checkoutCoinIcon : catchResultIcon != null ? catchResultIcon : catchingIcon;
    }

    /// <summary>
    /// 启动发光循环动画。
    /// </summary>
    private void StartGlowLoop()
    {
        if (glowImage != null)
        {
            pulseTween = glowImage.transform
                .DOScale(Vector3.one * 0.94f, 0.45f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        ResetIconTransform();
    }

    /// <summary>
    /// 启动缩放脉冲动画。
    /// </summary>
    private void StartPulseLoop()
    {
        pulseTween = transform
            .DOScale(1.08f, 0.4f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    /// <summary>
    /// 停止当前 界面 上的 动画缓动 动画。
    /// </summary>
    private void KillTweens()
    {
        pulseTween?.Kill();
        iconTween?.Kill();
        pulseTween = null;
        iconTween = null;
        ResetIconTransform();
    }

    /// <summary>
    /// 将按钮图标恢复到预制体标准姿态，避免动画中断后残留倾斜。
    /// </summary>
    private void ResetIconTransform()
    {
        if (icon == null)
        {
            return;
        }

        icon.transform.localRotation = Quaternion.identity;
        icon.transform.localScale = Vector3.one;
    }
}
