using DG.Tweening;
using JN.Client;
using JN.Client.Manager;
using JN.Client.Messages;
using JN.Client.Model;
using QFramework;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JN.Client.UI
{
    public class TownStatusBarPanelControllerData : UIPanelData
    {
    }

    /// <summary>
    /// 负责大地图状态栏相关的运行时逻辑。
    /// </summary>
    public class TownStatusBarPanelController : QFrameworkPanel<TownStatusBarPanelControllerData>
    {
        private const string ChangeGoldTextPrefabPath = "Assets/Res/Resources/UI/Runtime/ChangeGoldText.prefab";

        [SerializeField] public Transform group_GoldNum;
        [SerializeField] private TextMeshProUGUI txt_GoldNum;
        [SerializeField] private RectTransform group_BottomBar;
        [SerializeField] private Button btn_Enter;

        [SerializeField] private GameObject warningPrefab;
        

        [SerializeField] private TextMeshProUGUI txt_ChangeGoldNum;

        private Coroutine coinDeltaRoutine;
        private Vector2 coinDeltaBasePosition;
        private bool hasCoinDeltaBasePosition;

        /// <summary>
        /// 面板初始化时绑定控件和事件。
        /// </summary>
        protected override void OnPanelInit()
        {
            // 其他系统会复用这个缓存坐标作为金币飞行动画的目标点。
            if (group_GoldNum != null)
            {
                GOReferenceManager.Instance.SaveCoinTransform(group_GoldNum.transform);
            }
            else
            {
                Debug.LogWarning(
                    "TownStatusBarPanelController.group_GoldNum is null. Coin target transform was not cached.", this);
            }
            
            Signals.Get<UpdateCoinNumSignal>().AddListener(UpdateCoinNumHandler);
            Signals.Get<GameplayGuideProgressSignal>().AddListener(RefreshEnterButtonState);
            Signals.Get<TavernBusinessStateSignal>().AddListener(HandleTavernBusinessStateChanged);
        }

        /// <summary>
        /// 响应面板显示事件并同步状态。
        /// </summary>
        protected override void OnPanelShow()
        {
            EnsureChangeGoldText();
            EnsureEnterButton();
            txt_GoldNum.text = DataManager.Instance.PlayerData.coinNum.ToString();
            RefreshEnterButtonState();
        }

        /// <summary>
        /// 面板关闭时清理临时状态和监听。
        /// </summary>
        protected override void OnPanelClose()
        {
            Signals.Get<UpdateCoinNumSignal>().RemoveListener(UpdateCoinNumHandler);
            Signals.Get<GameplayGuideProgressSignal>().RemoveListener(RefreshEnterButtonState);
            Signals.Get<TavernBusinessStateSignal>().RemoveListener(HandleTavernBusinessStateChanged);
        }

        private void Update()
        {
            RefreshEnterButtonState();
        }

        /// <summary>
        /// 更新铜钱数量处理器。
        /// </summary>
        /// <param name="change数量">参数值。</param>
        private void UpdateCoinNumHandler(int changeNum)
        {
            txt_GoldNum.text = DataManager.Instance.PlayerData.coinNum.ToString();
            PlayCoinDelta(changeNum);
        }

        /// <summary>
        /// 刷新右下角进店按钮显隐。
        /// </summary>
        private void RefreshEnterButtonState()
        {
            EnsureEnterButton();
            if (btn_Enter == null)
            {
                return;
            }

            btn_Enter.gameObject.SetActive(GetCompletedOwnedBuilding() != null);
        }

        /// <summary>
        /// 开业状态切换时同步刷新进店按钮。
        /// </summary>
        /// <param name="isOpen">是否开业。</param>
        private void HandleTavernBusinessStateChanged(bool isOpen)
        {
            RefreshEnterButtonState();
        }

        /// <summary>
        /// 确保进店按钮引用与点击回调就绪。
        /// </summary>
        private void EnsureEnterButton()
        {
            if (group_BottomBar == null)
            {
                group_BottomBar = transform.Find("group_BottomBar") as RectTransform;
            }

            if (btn_Enter == null && group_BottomBar != null)
            {
                btn_Enter = group_BottomBar.Find("btn_Enter")?.GetComponent<Button>();
            }

            if (btn_Enter == null)
            {
                return;
            }

            btn_Enter.onClick.RemoveAllListeners();
            btn_Enter.onClick.AddListener(HandleEnterTavern);
        }

        /// <summary>
        /// 点击右下角按钮进店。
        /// </summary>
        private void HandleEnterTavern()
        {
            var ownedBuilding = GetCompletedOwnedBuilding();
            if (ownedBuilding == null || GameManager.Instance == null)
            {
                return;
            }

            DataManager.Instance.SetActiveOwnedBuilding(ownedBuilding.tileId, ownedBuilding.buildingLevel);
            StartCoroutine(GameManager.Instance.LoadSceneAsync("GamePlay_Tavern", () =>
            {
                if (UIKit.GetPanel<TownStatusBarPanelController>() != null)
                {
                    UIKit.ClosePanel<TownStatusBarPanelController>();
                }

                UIKit.OpenPanel<TavernStatusBarPanelController>(UILevel.Common);
                UIKit.OpenPanel<StartOpeningWindowController>(UILevel.PopUI);
            }));
        }

        /// <summary>
        /// 获取当前玩家已建成、可进店的建筑。
        /// </summary>
        private static BuildingInfo GetCompletedOwnedBuilding()
        {
            if (DataManager.Instance?.PlayerData == null
                || !int.TryParse(DataManager.Instance.PlayerData.playerId, out var playerId))
            {
                return null;
            }

            var buildingInfos = DataManager.Instance.GetTownBuildingInfos();
            return buildingInfos.Find(info => info != null
                                              && info.playerId == playerId
                                              && info.status == 2
                                              && info.buildingLevel > 0);
        }

        
        private void SpawnWarning(string text, Transform parent, GameObject obj = null, bool isRed = true)
        {
            var warning = Instantiate(warningPrefab, parent);
            warning.GetComponent<TMP_Text>().text = text;

            if (!isRed)
            {
                warning.GetComponent<TMP_Text>().color = Color.white;
            }
            var currentPos = warning.transform.position;
            if (obj == null)
            {
                warning.transform.position = new Vector3(currentPos.x + 25, currentPos.y + 45, currentPos.z);
            }
            else
            {
                var pos = Camera.main.WorldToScreenPoint(obj.transform.position);
                warning.transform.position = new Vector3(pos.x + 25, pos.y + 45, pos.z);
            }
            var newPos = warning.transform.position;

            warning.transform.DOMove(new Vector3(newPos.x + 45f, newPos.y + 65f, newPos.z), 1.5f).SetEase(Ease.InQuad);
            var canvas = warning.GetComponent<CanvasGroup>();
            canvas.DOFade(0f, 1f).SetDelay(.5f);
            Destroy(warning.gameObject, 2f);
        }

        /// <summary>
        /// 播放右上角铜钱增减的上浮渐淡动画。
        /// </summary>
        /// <param name="changeNum">铜钱变化量。</param>
        private void PlayCoinDelta(int changeNum)
        {
            EnsureChangeGoldText();
            if (txt_ChangeGoldNum == null || changeNum == 0)
            {
                return;
            }

            txt_ChangeGoldNum.text = changeNum > 0 ? $"+{changeNum}" : changeNum.ToString();
            txt_ChangeGoldNum.color = changeNum > 0 ? Color.green : Color.red;
            if (coinDeltaRoutine != null)
            {
                StopCoroutine(coinDeltaRoutine);
            }

            coinDeltaRoutine = StartCoroutine(CoinDeltaAnim(txt_ChangeGoldNum.rectTransform));
        }

        /// <summary>
        /// 找不到面板内变化文本时，从预制体加载一个跟随金币数字的文本。
        /// </summary>
        private void EnsureChangeGoldText()
        {
            if (txt_ChangeGoldNum != null || txt_GoldNum == null)
            {
                if (txt_ChangeGoldNum != null)
                {
                    if (!hasCoinDeltaBasePosition)
                    {
                        coinDeltaBasePosition = txt_ChangeGoldNum.rectTransform.anchoredPosition;
                        hasCoinDeltaBasePosition = true;
                    }

                    var existingCanvasGroup = txt_ChangeGoldNum.GetComponent<CanvasGroup>() ?? txt_ChangeGoldNum.gameObject.AddComponent<CanvasGroup>();
                    existingCanvasGroup.alpha = 0f;
                }

                return;
            }

            var prefab = LoadChangeGoldTextPrefab();
            if (prefab == null)
            {
                Debug.LogWarning($"[TownStatusBarPanelController] 缺少金币变化文本预制体：{ChangeGoldTextPrefabPath}");
                return;
            }

            var node = Instantiate(prefab);
            node.transform.SetParent(txt_GoldNum.transform.parent, false);
            var rect = node.GetComponent<RectTransform>();
            rect.anchorMin = txt_GoldNum.rectTransform.anchorMin;
            rect.anchorMax = txt_GoldNum.rectTransform.anchorMax;
            rect.pivot = txt_GoldNum.rectTransform.pivot;
            rect.anchoredPosition = txt_GoldNum.rectTransform.anchoredPosition + new Vector2(0f, -28f);
            rect.sizeDelta = txt_GoldNum.rectTransform.sizeDelta;

            txt_ChangeGoldNum = node.GetComponent<TextMeshProUGUI>();
            if (txt_ChangeGoldNum == null)
            {
                Debug.LogWarning($"[TownStatusBarPanelController] 金币变化文本预制体缺少 TextMeshProUGUI：{ChangeGoldTextPrefabPath}");
                Destroy(node);
                return;
            }

            txt_ChangeGoldNum.font = txt_GoldNum.font;
            txt_ChangeGoldNum.fontSize = txt_GoldNum.fontSize;
            txt_ChangeGoldNum.alignment = TextAlignmentOptions.Center;
            txt_ChangeGoldNum.raycastTarget = false;
            txt_ChangeGoldNum.text = string.Empty;
            coinDeltaBasePosition = rect.anchoredPosition;
            hasCoinDeltaBasePosition = true;
        }

        /// <summary>
        /// 按上浮和透明度变化表现铜钱增减。
        /// </summary>
        /// <param name="target">变化文本节点。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator CoinDeltaAnim(RectTransform target)
        {
            var time = 0f;
            const float duration = 1f;
            var start = coinDeltaBasePosition;
            var end = start + new Vector2(0f, 80f);
            var canvasGroup = target.GetComponent<CanvasGroup>() ?? target.gameObject.AddComponent<CanvasGroup>();

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
        /// 读取金币变化文本预制体。
        /// </summary>
        /// <returns>读取成功返回预制体，否则返回 null。</returns>
        private static GameObject LoadChangeGoldTextPrefab()
        {
            return GameplayResourceStore.LoadAsset<GameObject>(ChangeGoldTextPrefabPath);
        }
    }
}
