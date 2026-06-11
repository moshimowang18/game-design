using JN.Client.Manager;
using JN.Client.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JN.Client.Scene
{
    public partial class TavernSceneManager
    {
        #region Guide Buttons

        private const float RecruitGuideButtonScaleMultiplier = 2f;
        private const float PurchaseGuideButtonScaleMultiplier = 1.5f;
        private const string CustomerEnterQueueFillSpritePath = "Assets/Res/Resources/Textures/UI/Icons 1/customerEnterProgressFillRed.png";

        /// <summary>
        /// 确保场景购买和招聘按钮已经创建。
        /// </summary>
        private void EnsureGuideWorldButtons()
        {
            if (guideCounterButton == null)
            {
                guideCounterButton = CreateGuideWorldButtonFromPrefab(
                    GuideCounterButtonPrefabResourcePath,
                    "BuyCounterButton",
                    guideCounterBuildBase != null ? guideCounterBuildBase.transform : guideCounterObject != null ? guideCounterObject.transform : null,
                    new Vector3(0f, 0.22f, 0f),
                    string.Empty,
                    HandleBuyCounter);
                ScalePurchaseGuideButton(guideCounterButton);
            }

            if (guideStoveButton == null)
            {
                guideStoveButton = CreateGuideWorldButtonFromPrefab(
                    GuideStoveButtonPrefabResourcePath,
                    "BuyStoveButton",
                    guideStoveBuildBase != null ? guideStoveBuildBase.transform : guideStoveObject != null ? guideStoveObject.transform : null,
                    new Vector3(0f, 0.22f, 0f),
                    string.Empty,
                    () => HandleBuyKitchenItem("stove"));
                ScalePurchaseGuideButton(guideStoveButton);

                if (guideKitchenAnchors.Count > 0)
                {
                    guideKitchenAnchors[0].button = guideStoveButton;
                }
            }

            EnsureGuideKitchenButtons();

            if (guideShopkeeperButton == null)
            {
                guideShopkeeperButton = CreateGuideWorldButton(
                    "HireShopkeeperButton",
                    guideCounterObject != null ? guideCounterObject.transform : null,
                    new Vector3(0f, 1.35f, 0f),
                    string.Empty,
                    HandleHireShopkeeper);
                SetGuideButtonSprite(guideShopkeeperButton, GuideRecruitShopkeeperSpritePath);
                ScaleRecruitGuideButton(guideShopkeeperButton);
            }

            if (guideChefButton == null)
            {
                guideChefButton = CreateGuideWorldButton(
                    "HireChefButton",
                    guideStoveObject != null ? guideStoveObject.transform : null,
                    new Vector3(0f, 1.2f, 0f),
                    string.Empty,
                    HandleHireChef);
                SetGuideButtonSprite(guideChefButton, GuideRecruitChefSpritePath);
                ScaleRecruitGuideButton(guideChefButton);
            }

            if (guideWaiterButton == null)
            {
                guideWaiterButton = CreateGuideWorldButton(
                    "HireWaiterButton",
                    guideCounterObject != null ? guideCounterObject.transform : null,
                    new Vector3(-0.85f, 1.2f, -0.95f),
                    string.Empty,
                    HandleHireWaiter);
                SetGuideButtonSprite(guideWaiterButton, GuideRecruitWaiterSpritePath);
                ScaleRecruitGuideButton(guideWaiterButton);
            }

            EnsureGuideBuildBaseColliders();
        }

        /// <summary>
        /// 确保门口顾客倒计时标签已经创建。
        /// </summary>
        private void EnsureGuideWorldLabels()
        {
            if (nextCustomerTimerLabel?.rectTransform != null)
            {
                nextCustomerTimerLabel.rectTransform.gameObject.SetActive(false);
                return;
            }
        }

        /// <summary>
        /// 创建跟随场景目标的引导按钮。
        /// </summary>
        /// <param name="name">名称。</param>
        /// <param name="target">目标对象。</param>
        /// <param name="worldOffset">参数值。</param>
        /// <param name="label">参数值。</param>
        /// <param name="onClick">参数值。</param>
        /// <returns>返回匹配到的对象引用。</returns>
        private GuideWorldButton CreateGuideWorldButton(string name, Transform target, Vector3 worldOffset, string label, UnityEngine.Events.UnityAction onClick)
        {
            return CreateGuideWorldButtonFromPrefab(GuideWorldButtonPrefabResourcePath, name, target, worldOffset, label, onClick);
        }

        /// <summary>
        /// 从 预制体 创建跟随场景目标的引导按钮。
        /// </summary>
        /// <param name="resourcePath">资源路径。</param>
        /// <param name="name">名称。</param>
        /// <param name="target">目标对象。</param>
        /// <param name="worldOffset">参数值。</param>
        /// <param name="label">参数值。</param>
        /// <param name="onClick">参数值。</param>
        /// <returns>返回匹配到的对象引用。</returns>
        private GuideWorldButton CreateGuideWorldButtonFromPrefab(string resourcePath, string name, Transform target, Vector3 worldOffset, string label, UnityEngine.Events.UnityAction onClick)
        {
            if (canvasParent == null || target == null)
            {
                return null;
            }

            if (resourcePath == GuideStoveButtonPrefabResourcePath || resourcePath == GuideCounterButtonPrefabResourcePath)
            {
                label = string.Empty;
            }

            var prefab = GameplayResourceStore.LoadAsset<GameObject>($"Assets/Res/Resources/{resourcePath}.prefab");
            if (prefab == null)
            {
                return CreateGuideWorldButton(name, target, worldOffset, label, onClick);
            }

            var instance = Instantiate(prefab, canvasParent, false);
            if (instance == null)
            {
                return CreateGuideWorldButton(name, target, worldOffset, label, onClick);
            }

            instance.name = name;

            var rectTransform = instance.GetComponent<RectTransform>();
            var button = instance.GetComponent<Button>();
            var image = button != null ? button.GetComponent<Image>() : instance.GetComponent<Image>();
            var tmpText = FindGuideButtonTmpText(instance.transform);
            var text = tmpText == null ? instance.GetComponentInChildren<Text>(true) : null;
            if (rectTransform == null || button == null || (tmpText == null && text == null))
            {
                Destroy(instance);
                return CreateGuideWorldButton(name, target, worldOffset, label, onClick);
            }

            button.onClick.RemoveAllListeners();
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            SetGuideButtonTextInternal(tmpText, text, label);

            var guideButton = new GuideWorldButton
            {
                rectTransform = rectTransform,
                button = button,
                image = image,
                text = text,
                tmpText = tmpText,
                target = target,
                worldOffset = worldOffset,
                scale = Vector3.one
            };

            guideWorldButtons.Add(guideButton);
            return guideButton;
        }

        /// <summary>
        /// 用指定图片资源替换招聘按钮底图。
        /// </summary>
        /// <param name="guideButton">需要替换图片的按钮。</param>
        /// <param name="spritePath">Sprite 资源路径。</param>
        private static void SetGuideButtonSprite(GuideWorldButton guideButton, string spritePath)
        {
            if (guideButton == null || guideButton.image == null || string.IsNullOrEmpty(spritePath))
            {
                return;
            }

            var sprite = LoadGuideButtonSprite(spritePath);
            if (sprite == null)
            {
                return;
            }

            guideButton.image.sprite = sprite;
            guideButton.image.color = Color.white;
            guideButton.image.type = Image.Type.Simple;
            guideButton.image.preserveAspect = true;
        }

        /// <summary>
        /// 在编辑器环境中按路径读取招聘按钮图片。
        /// </summary>
        /// <param name="spritePath">Sprite 资源路径。</param>
        /// <returns>读取成功时返回 Sprite，否则返回 null。</returns>
        private static Sprite LoadGuideButtonSprite(string spritePath)
        {
            if (GuideButtonSpriteCache.TryGetValue(spritePath, out var cachedSprite))
            {
                return cachedSprite;
            }

            var sprite = GameplayResourceStore.LoadAsset<Sprite>(spritePath);
            GuideButtonSpriteCache[spritePath] = sprite;
            return sprite;
        }

        /// <summary>
        /// 将招聘按钮放大一倍，增强场景内点击识别度。
        /// </summary>
        /// <param name="guideButton">需要缩放的招聘按钮。</param>
        private static void ScaleRecruitGuideButton(GuideWorldButton guideButton)
        {
            if (guideButton?.rectTransform == null)
            {
                return;
            }

            guideButton.rectTransform.sizeDelta *= RecruitGuideButtonScaleMultiplier;
            guideButton.scale = Vector3.one * RecruitGuideButtonScaleMultiplier;
        }

        /// <summary>
        /// 将带价格的购买按钮放大，提升价格可读性和点击热区。
        /// </summary>
        private static void ScalePurchaseGuideButton(GuideWorldButton guideButton)
        {
            if (guideButton?.rectTransform == null)
            {
                return;
            }

            guideButton.rectTransform.sizeDelta *= PurchaseGuideButtonScaleMultiplier;
            guideButton.scale = Vector3.one * PurchaseGuideButtonScaleMultiplier;
        }

        /// <summary>
        /// 优先查找按钮里用于显示金额的 文本组件 文本。
        /// </summary>
        /// <param name="root">参数值。</param>
        /// <returns>返回匹配到的对象引用。</returns>
        private static TMP_Text FindGuideButtonTmpText(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            var tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmpText in tmpTexts)
            {
                if (tmpText != null && tmpText.name == "txt_CoinNum")
                {
                    return tmpText;
                }
            }

            return tmpTexts.Length > 0 ? tmpTexts[0] : null;
        }

        /// <summary>
        /// 更新引导按钮显示文本。
        /// </summary>
        /// <param name="guideButton">数据编号。</param>
        /// <param name="content">参数值。</param>
        private static void SetGuideButtonText(GuideWorldButton guideButton, string content)
        {
            if (guideButton == null)
            {
                return;
            }

            SetGuideButtonTextInternal(guideButton.tmpText, guideButton.text, content);
        }

        /// <summary>
        /// 按文本组件类型写入按钮文案。
        /// </summary>
        /// <param name="tmpText">参数值。</param>
        /// <param name="text">参数值。</param>
        /// <param name="content">参数值。</param>
        private static void SetGuideButtonTextInternal(TMP_Text tmpText, Text text, string content)
        {
            if (tmpText != null)
            {
                tmpText.text = content;
                return;
            }

            if (text != null)
            {
                text.text = content;
            }
        }

        /// <summary>
        /// 把引导按钮调整成只显示价格的轻量样式。
        /// </summary>
        /// <param name="guideButton">数据编号。</param>
        /// <param name="size">参数值。</param>
        private static void ApplyPriceOnlyButtonStyle(GuideWorldButton guideButton, Vector2 size)
        {
            if (guideButton == null || guideButton.rectTransform == null || guideButton.text == null || guideButton.button == null)
            {
                return;
            }

            guideButton.rectTransform.sizeDelta = size;

            var image = guideButton.button.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0f, 0f, 0f, 0f);
                image.raycastTarget = true;
            }

            guideButton.text.fontSize = 22;
            guideButton.text.alignment = TextAnchor.MiddleCenter;
            guideButton.text.color = new Color(1f, 0.95f, 0.82f, 1f);
        }

        /// <summary>
        /// 创建跟随场景目标的引导标签。
        /// </summary>
        /// <param name="name">名称。</param>
        /// <param name="target">目标对象。</param>
        /// <param name="worldOffset">参数值。</param>
        /// <param name="label">参数值。</param>
        /// <returns>返回方法执行后的结果。</returns>
        private GuideWorldLabel CreateGuideWorldLabel(string name, Transform target, Vector3 worldOffset, string label)
        {
            if (canvasParent == null || target == null)
            {
                return null;
            }

            var prefab = GameplayResourceStore.LoadAsset<GameObject>($"Assets/Res/Resources/{GuideWorldLabelPrefabResourcePath}.prefab");
            if (prefab == null)
            {
                return null;
            }

            var instance = Instantiate(prefab, canvasParent, false);
            if (instance == null)
            {
                return null;
            }

            instance.name = name;

            var rectTransform = instance.GetComponent<RectTransform>();
            var text = instance.GetComponentInChildren<Text>(true);
            if (rectTransform == null || text == null)
            {
                Destroy(instance);
                return null;
            }

            text.text = label;

            var guideLabel = new GuideWorldLabel
            {
                rectTransform = rectTransform,
                text = text,
                target = target,
                worldOffset = worldOffset
            };

            guideWorldLabels.Add(guideLabel);
            return guideLabel;
        }

        /// <summary>
        /// 创建门口顾客进入倒计时进度表现。
        /// </summary>
        /// <param name="name">节点名称。</param>
        /// <param name="target">跟随目标。</param>
        /// <param name="worldOffset">世界偏移。</param>
        /// <returns>进度表现引用。</returns>
        private GuideWorldLabel CreateCustomerEnterProgressLabel(string name, Transform target, Vector3 worldOffset)
        {
            if (canvasParent == null || target == null)
            {
                return null;
            }

            var prefab = GameplayResourceStore.LoadAsset<GameObject>("Assets/Res/Resources/UI/Runtime/CustomerEnterProgress.prefab");
            if (prefab == null)
            {
                return CreateGuideWorldLabel(name, target, worldOffset, string.Empty);
            }

            var instance = Instantiate(prefab, canvasParent, false);
            if (instance == null)
            {
                return CreateGuideWorldLabel(name, target, worldOffset, string.Empty);
            }

            instance.name = name;
            var rectTransform = instance.GetComponent<RectTransform>();
            var canvasGroup = instance.GetComponent<CanvasGroup>();
            var progressBackground = instance.transform.Find("img_ProgressBg")?.GetComponent<Image>();
            var progressFill = instance.transform.Find("img_ProgressBg/img_ProgressFill")?.GetComponent<Image>();
            var queueBackground = instance.transform.Find("img_QueueBg")?.GetComponent<Image>();
            var tmpText = instance.transform.Find("txt_Time")?.GetComponent<TMP_Text>() ?? instance.GetComponentInChildren<TMP_Text>(true);
            var text = instance.GetComponentInChildren<Text>(true);
            if (rectTransform == null || progressBackground == null || progressFill == null)
            {
                Destroy(instance);
                return CreateGuideWorldLabel(name, target, worldOffset, string.Empty);
            }

            rectTransform.localScale = Vector3.one * 2f;
            progressBackground.gameObject.SetActive(true);
            progressFill.fillAmount = 0f;
            if (queueBackground != null)
            {
                queueBackground.gameObject.SetActive(false);
            }

            if (tmpText != null)
            {
                tmpText.text = "-- s";
                tmpText.gameObject.SetActive(true);
            }

            if (text != null)
            {
                text.text = string.Empty;
                text.gameObject.SetActive(false);
            }

            var guideLabel = new GuideWorldLabel
            {
                rectTransform = rectTransform,
                text = text,
                tmpText = tmpText,
                progressBackground = progressBackground,
                progressFill = progressFill,
                queueBackground = queueBackground,
                canvasGroup = canvasGroup,
                target = target,
                worldOffset = worldOffset,
                scale = Vector3.one * 2f,
                defaultProgressSprite = progressFill.sprite,
                queuedProgressSprite = GameplayResourceStore.LoadAsset<Sprite>(CustomerEnterQueueFillSpritePath)
            };

            guideWorldLabels.Add(guideLabel);
            return guideLabel;
        }

        /// <summary>
        /// 根据新手任务进度刷新按钮显隐和价格。
        /// </summary>
        /// <param name="guide">数据编号。</param>
        private void RefreshGuideWorldButtons(GameplayGuideSaveData guide)
        {
            if (guideCounterButton != null)
            {
                var showCounterButton = !guide.purchasedCounter;
                guideCounterButton.rectTransform.gameObject.SetActive(showCounterButton);
                if (showCounterButton)
                {
                    SetGuideButtonText(guideCounterButton, $"{GetGuideEquipmentCost(0)}");
                }
            }

            if (guideStoveButton != null)
            {
                var showStoveButton = DataManager.Instance.CanPurchaseGuideKitchenEquipment()
                                      && !DataManager.Instance.IsGuideKitchenItemPurchased("stove");
                guideStoveButton.rectTransform.gameObject.SetActive(showStoveButton);
                if (showStoveButton)
                {
                    SetGuideButtonText(guideStoveButton, $"{GetGuideEquipmentCost(3)}");
                }
            }

            for (var i = 1; i < guideKitchenAnchors.Count; i++)
            {
                var stoveButton = guideKitchenAnchors[i].button;
                if (stoveButton == null || stoveButton.rectTransform == null)
                {
                    continue;
                }

                var itemKey = guideKitchenAnchors[i].itemKey;
                var showStoveButton = ShouldShowGuideKitchenButton(itemKey);
                stoveButton.rectTransform.gameObject.SetActive(showStoveButton);
                if (showStoveButton)
                {
                    SetGuideButtonText(stoveButton, $"{GetGuideEquipmentCost(3)}");
                }
            }

            if (guideShopkeeperButton != null)
            {
                var showShopkeeperButton = guide.recruitmentUnlocked && !guide.hiredShopkeeper;
                guideShopkeeperButton.rectTransform.gameObject.SetActive(showShopkeeperButton);
                ClearGuideButtonText(guideShopkeeperButton);
            }

            if (guideChefButton != null)
            {
                var showChefButton = guide.recruitmentUnlocked && !guide.hiredChef;
                guideChefButton.rectTransform.gameObject.SetActive(showChefButton);
                ClearGuideButtonText(guideChefButton);
            }

            if (guideWaiterButton != null)
            {
                var showWaiterButton = guide.recruitmentUnlocked && !guide.hiredWaiter;
                guideWaiterButton.rectTransform.gameObject.SetActive(showWaiterButton);
                ClearGuideButtonText(guideWaiterButton);
            }
        }

        /// <summary>
        /// 判断厨房购买按钮是否应该在当前任务阶段显示。
        /// </summary>
        /// <param name="itemKey">厨房物件键值。</param>
        /// <returns>应该显示购买按钮时返回 true。</returns>
        private bool ShouldShowGuideKitchenButton(string itemKey)
        {
            if (DataManager.Instance.IsGuideKitchenItemPurchased(itemKey))
            {
                return false;
            }

            if (itemKey == "cabinet" || itemKey == "wine_cabinet")
            {
                return true;
            }

            if (itemKey == "stove" || itemKey == "furnace" || itemKey == "kitchen_table_1" || itemKey == "kitchen_table_2")
            {
                return DataManager.Instance.CanPurchaseGuideKitchenEquipment();
            }

            return false;
        }

        /// <summary>
        /// 清空招聘图片按钮上的文字，避免复用按钮预制体时残留价格或描述。
        /// </summary>
        /// <param name="guideButton">招聘按钮。</param>
        private static void ClearGuideButtonText(GuideWorldButton guideButton)
        {
            SetGuideButtonText(guideButton, string.Empty);
            if (guideButton?.rectTransform == null)
            {
                return;
            }

            foreach (var tmpText in guideButton.rectTransform.GetComponentsInChildren<TMP_Text>(true))
            {
                tmpText.text = string.Empty;
            }

            foreach (var text in guideButton.rectTransform.GetComponentsInChildren<Text>(true))
            {
                text.text = string.Empty;
            }
        }

        /// <summary>
        /// 刷新门口下一位顾客倒计时文本。
        /// </summary>
        private void RefreshNextCustomerTimerLabel()
        {
            if (nextCustomerTimerLabel == null || nextCustomerTimerLabel.rectTransform == null)
            {
                return;
            }

            var shouldShow = DataManager.Instance != null && DataManager.Instance.TavernData.isOpen;
            nextCustomerTimerLabel.rectTransform.gameObject.SetActive(shouldShow);
            if (!shouldShow)
            {
                if (nextCustomerTimerLabel.queueBackground != null)
                {
                    nextCustomerTimerLabel.queueBackground.gameObject.SetActive(false);
                }

                if (nextCustomerTimerLabel.progressFill != null)
                {
                    nextCustomerTimerLabel.progressFill.sprite = nextCustomerTimerLabel.defaultProgressSprite;
                    nextCustomerTimerLabel.progressFill.fillAmount = 0f;
                }

                if (nextCustomerTimerLabel.tmpText != null)
                {
                    nextCustomerTimerLabel.tmpText.text = "-- s";
                }

                return;
            }

            var progress = customerSpawnInterval <= 0.01f
                ? 1f
                : 1f - Mathf.Clamp01(nextCustomerSpawnRemaining / customerSpawnInterval);
            var remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(nextCustomerSpawnRemaining));
            var queueCount = GetQueueCustomerCount();
            var hasQueue = queueCount > 0;

            if (nextCustomerTimerLabel.progressBackground != null)
            {
                nextCustomerTimerLabel.progressBackground.gameObject.SetActive(true);
            }

            if (nextCustomerTimerLabel.queueBackground != null)
            {
                nextCustomerTimerLabel.queueBackground.gameObject.SetActive(hasQueue);
            }

            if (nextCustomerTimerLabel.progressFill != null)
            {
                nextCustomerTimerLabel.progressFill.sprite = hasQueue && nextCustomerTimerLabel.queuedProgressSprite != null
                    ? nextCustomerTimerLabel.queuedProgressSprite
                    : nextCustomerTimerLabel.defaultProgressSprite;
                nextCustomerTimerLabel.progressFill.fillAmount = hasQueue ? 1f : progress;
            }

            if (nextCustomerTimerLabel.tmpText != null)
            {
                nextCustomerTimerLabel.tmpText.text = hasQueue ? $"{queueCount}人排队中" : $"{remainingSeconds} s";
                nextCustomerTimerLabel.tmpText.gameObject.SetActive(true);
            }

            if (nextCustomerTimerLabel.text != null)
            {
                nextCustomerTimerLabel.text.text = string.Empty;
                nextCustomerTimerLabel.text.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 为厨房购买点创建对应价格按钮。
        /// </summary>
        private void EnsureGuideKitchenButtons()
        {
            for (var i = 1; i < guideKitchenAnchors.Count; i++)
            {
                var anchor = guideKitchenAnchors[i];
                if (anchor == null || anchor.button != null)
                {
                    continue;
                }

                var button = CreateGuideWorldButtonFromPrefab(
                    GuideStoveButtonPrefabResourcePath,
                    i == 0 ? "BuyStoveButton" : $"BuyStoveButton_{i}",
                    anchor.buildBase != null ? anchor.buildBase.transform : anchor.sceneObject != null ? anchor.sceneObject.transform : null,
                    new Vector3(0f, 0.22f, 0f),
                    string.Empty,
                    () => HandleBuyKitchenItem(anchor.itemKey));

                if (button != null)
                {
                    ScalePurchaseGuideButton(button);
                    anchor.button = button;
                }
            }
        }

        /// <summary>
        /// 为场景中的购买提示底板补齐碰撞体，支持直接点击底板购买。
        /// </summary>
        private void EnsureGuideBuildBaseColliders()
        {
            EnsureGuideBuildBaseCollider(guideCounterBuildBase);
            EnsureGuideBuildBaseCollider(guideStoveBuildBase);

            for (var index = 0; index < guideKitchenAnchors.Count; index++)
            {
                EnsureGuideBuildBaseCollider(guideKitchenAnchors[index]?.buildBase);
            }
        }

        /// <summary>
        /// 为单个购买提示底板补齐碰撞体。
        /// </summary>
        private static void EnsureGuideBuildBaseCollider(GameObject buildBase)
        {
            if (buildBase == null)
            {
                return;
            }

            var renderers = buildBase.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return;
            }

            Bounds? combinedBounds = null;
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                combinedBounds = combinedBounds.HasValue
                    ? EncapsulateBounds(combinedBounds.Value, renderer.bounds)
                    : renderer.bounds;
            }

            if (!combinedBounds.HasValue)
            {
                return;
            }

            var boxCollider = buildBase.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = buildBase.AddComponent<BoxCollider>();
            }

            boxCollider.isTrigger = true;
            boxCollider.center = buildBase.transform.InverseTransformPoint(combinedBounds.Value.center);
            boxCollider.size = new Vector3(
                Mathf.Max(0.2f, combinedBounds.Value.size.x),
                Mathf.Max(0.2f, combinedBounds.Value.size.y),
                Mathf.Max(0.2f, combinedBounds.Value.size.z));
        }

        /// <summary>
        /// 合并两个包围盒。
        /// </summary>
        private static Bounds EncapsulateBounds(Bounds a, Bounds b)
        {
            a.Encapsulate(b.min);
            a.Encapsulate(b.max);
            return a;
        }

        /// <summary>
        /// 命中场景购买底板时执行对应购买。
        /// </summary>
        private bool TryHandleGuideBuildBaseClick(Collider hitCollider)
        {
            if (hitCollider == null)
            {
                return false;
            }

            if (guideCounterBuildBase != null && hitCollider.GetComponentInParent<Transform>() != null
                && hitCollider.transform.IsChildOf(guideCounterBuildBase.transform))
            {
                HandleBuyCounter();
                return true;
            }

            if (guideStoveBuildBase != null && hitCollider.transform.IsChildOf(guideStoveBuildBase.transform))
            {
                HandleBuyKitchenItem("stove");
                return true;
            }

            for (var index = 0; index < guideKitchenAnchors.Count; index++)
            {
                var anchor = guideKitchenAnchors[index];
                if (anchor?.buildBase == null)
                {
                    continue;
                }

                if (!hitCollider.transform.IsChildOf(anchor.buildBase.transform))
                {
                    continue;
                }

                HandleBuyKitchenItem(anchor.itemKey);
                return true;
            }

            return false;
        }

        #endregion
    }
}
