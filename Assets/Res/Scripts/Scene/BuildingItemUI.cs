using System.Collections;
using JN.Client.Manager;
using JN.Client.Messages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JN.Client.Scene
{
    /// <summary>
    /// 负责建筑物件相关的运行时逻辑。
    /// </summary>
    public class BuildingItemUI : MonoBehaviour
    {
        private const string LandPricePrefabPath = "Assets/Res/Resources/UI/Runtime/LandPrice.prefab";

        private static int ResolveSelfPlayerId()
        {
            return DataManager.Instance != null ? DataManager.Instance.GetLocalPlayerNumericId() : 0;
        }

        [Header("UI Nodes")]
        [SerializeField] private GameObject userObj;
        [SerializeField] private GameObject timeObj;
        [SerializeField] private Button openingBtn;
        [SerializeField] private Button enterBtn;
        [SerializeField] private Button userBtn;
        [SerializeField] private TMP_Text unameText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private GameObject functionObj;
        [SerializeField] private GameObject dushEffect;
        [SerializeField] private GameObject hammerEffect;
        [SerializeField] private GameObject fireworkEffect;
        [SerializeField] private GameObject openingSuccess;
        [SerializeField] private GameObject openingNew;
        [SerializeField] private GameObject landPriceObj;
        [SerializeField] private TMP_Text landPriceText;
        [SerializeField] private Button landPriceButton;
        [SerializeField] private GameObject landPriceRecommendObj;
        [Header("位置设置")]
        [SerializeField] private Vector3 level1Offset = new(0f, 15f, 0f);
        [SerializeField] private Vector3 level2Offset = new(0f, 18f, 0f);
        [SerializeField] private Vector3 level3Offset = new(0f, 21f, 0f);
        [SerializeField] private Vector3 uiScale = new(1.35f, 1.35f, 1f);
        [SerializeField] private float buildEffectScaleMultiplier = 1.25f;

        private RectTransform rectTransform;
        private Tile targetTile;
        private BuildingInfo buildingInfo;
        private float countdownTime;
        private Sprite openingNewDefaultSprite;
        private Vector3 dushEffectDefaultScale = Vector3.one;
        private Vector3 hammerEffectDefaultScale = Vector3.one;

        /// <summary>
        /// 初始化组件引用和运行时状态。
        /// </summary>
        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.localScale = uiScale;

            BindButton(openingBtn, HandlePrimaryAction);
            BindButton(userBtn, HandlePrimaryAction);
            BindButton(enterBtn, HandleEnterTavern);
            BindButton(openingNew != null ? openingNew.GetComponent<Button>() : null, HandlePrimaryAction);

            openingNewDefaultSprite = openingNew != null ? openingNew.GetComponent<Image>()?.sprite : null;
            dushEffectDefaultScale = dushEffect != null ? dushEffect.transform.localScale : Vector3.one;
            hammerEffectDefaultScale = hammerEffect != null ? hammerEffect.transform.localScale : Vector3.one;
            ResolveLandPriceReferences();
        }

        /// <summary>
        /// 处理绑定相关逻辑。
        /// </summary>
        /// <param name="tile">参数值。</param>
        public void Bind(Tile tile)
        {
            targetTile = tile;
        }

        /// <summary>
        /// 获取场景锚点位置。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        public Vector3 GetWorldAnchorPosition()
        {
            if (targetTile == null)
            {
                return Vector3.zero;
            }

            return targetTile.transform.position + GetOffsetByBuildingLevel();
        }

        /// <summary>
        /// 设置锚点ed位置。
        /// </summary>
        /// <param name="anchoredPosition">坐标。</param>
        public void SetAnchoredPosition(Vector2 anchoredPosition)
        {
            rectTransform.anchoredPosition = anchoredPosition;
        }

        /// <summary>
        /// 设置显隐。
        /// </summary>
        /// <param name="visible">参数值。</param>
        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// 设置数据。
        /// </summary>
        /// <param name="info">参数值。</param>
        public void SetData(BuildingInfo info)
        {
            buildingInfo = info;
            StopAllCoroutines();
            ResetUI();

            if (info == null)
            {
                return;
            }

            if (info.playerId == 0)
            {
                RefreshLandPrice();
                return;
            }

            if (info.playerId == ResolveSelfPlayerId() && info.buildingLevel <= 0)
            {
                SetOpeningNewSprite(openingNewDefaultSprite);
                SetNodeActive(openingNew, true);
                return;
            }

            if (info.playerId > 0)
            {
                SetNodeActive(userObj, true);
            }
            
            if (unameText != null)
            {
                unameText.text = info.name;
            }

            if (info.buildingTime > 0)
            {
                countdownTime = info.buildingTime;
                SetBuildEffectsActive(true);
                SetNodeActive(timeObj, true);
                StartCoroutine(Countdown());
            }

            if (info.status == 1)
            {
                SetNodeActive(timeObj, true);
                SetBuildEffectsActive(true);
            }

            if (info.status == 2)
            {
                SetNodeActive(functionObj, true);
                if (info.playerId == ResolveSelfPlayerId())
                {
                    SetNodeActive(userObj, false);
                    SetNodeActive(enterBtn, true);
                }
            }

            if (info.celebrationTime > 0)
            {
                SetNodeActive(fireworkEffect, true);
            }

            if (info.playerId == ResolveSelfPlayerId() && info.buildingTime > 0 && unameText != null)
            {
                unameText.text = "建造中";
            }

            RefreshLandPrice();
        }

        /// <summary>
        /// 重置界面。
        /// </summary>
        public void ResetUI()
        {
            SetNodeActive(openingBtn, false);
            SetNodeActive(enterBtn, false);
            SetNodeActive(openingNew, false);
            SetNodeActive(timeObj, false);
            SetNodeActive(userObj, false);
            SetNodeActive(dushEffect, false);
            SetNodeActive(hammerEffect, false);
            SetNodeActive(fireworkEffect, false);
            SetNodeActive(openingSuccess, false);
            SetNodeActive(functionObj, false);
            SetLandPriceActive(false);
        }

        /// <summary>
        /// 处理倒计时显示。
        /// </summary>
        /// <returns>协程迭代器。</returns>
        private IEnumerator Countdown()
        {
            while (countdownTime > 0f)
            {
                countdownTime -= Time.deltaTime;
                var displaySeconds = Mathf.CeilToInt(countdownTime);
                if (timeText != null)
                {
                    timeText.text = $"00:{displaySeconds:D2}";
                }

                yield return null;
            }

            if (timeText != null)
            {
                timeText.text = "00:00";
            }

            SetNodeActive(timeObj, false);
            if (buildingInfo == null)
            {
                yield break;
            }

            buildingInfo.buildingTime = 0;
            buildingInfo.status = 2;
            if (buildingInfo.playerId == ResolveSelfPlayerId())
            {
                JN.Client.Manager.DataManager.Instance.SetActiveOwnedBuilding(buildingInfo.tileId, buildingInfo.buildingLevel);
            }

            TileManager.Instance.UpdateTile(buildingInfo.tileId, buildingInfo);
        }

        /// <summary>
        /// 处理主要操作。
        /// </summary>
        private void HandlePrimaryAction()
        {
            targetTile?.HandlePrimaryActionFromUI();
        }

        /// <summary>
        /// 处理进入酒楼操作。
        /// </summary>
        private void HandleEnterTavern()
        {
            targetTile?.EnterTavernFromUI();
        }

        /// <summary>
        /// 处理绑定按钮相关逻辑。
        /// </summary>
        /// <param name="button">按钮对象。</param>
        /// <param name="callback">回调函数。</param>
        private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
        }

        /// <summary>
        /// 设置节点显隐状态。
        /// </summary>
        /// <param name="target">目标对象。</param>
        /// <param name="active">参数值。</param>
        private static void SetNodeActive(Object target, bool active)
        {
            switch (target)
            {
                case null:
                    return;
                case Component component:
                    component.gameObject.SetActive(active);
                    break;
                case GameObject gameObject:
                    gameObject.SetActive(active);
                    break;
            }
        }

        /// <summary>
        /// 切换地块购买或建筑建造按钮的图标。
        /// </summary>
        /// <param name="sprite">需要显示的按钮图标。</param>
        private void SetOpeningNewSprite(Sprite sprite)
        {
            if (openingNew == null || sprite == null)
            {
                return;
            }

            var image = openingNew.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }

        /// <summary>
        /// 刷新地块购买价格显示。
        /// </summary>
        private void RefreshLandPrice()
        {
            var canShow = CanShowLandPrice();
            SetLandPriceActive(canShow);
            if (!canShow)
            {
                return;
            }

            ResolveLandPriceReferences();
            if (landPriceText != null)
            {
                landPriceText.text = DataManager.Instance.GetTownLandPurchaseCost(targetTile != null ? targetTile.tileId : 0).ToString();
            }

            if (landPriceRecommendObj != null)
            {
                var tileId = targetTile != null ? targetTile.tileId : 0;
                landPriceRecommendObj.SetActive(tileId == 2 || tileId == 3);
            }
        }

        /// <summary>
        /// 当前地块是否需要显示购买价格。
        /// </summary>
        /// <returns>显示时返回 true。</returns>
        private bool CanShowLandPrice()
        {
            if (targetTile == null || buildingInfo != null && buildingInfo.playerId != 0)
            {
                return false;
            }

            var selfPlayerId = ResolveSelfPlayerId();
            return !DataManager.Instance.HasOwnedTownLand(selfPlayerId);
        }

        /// <summary>
        /// 显示或隐藏地块价格 UI。
        /// </summary>
        /// <param name="active">是否显示。</param>
        private void SetLandPriceActive(bool active)
        {
            ResolveLandPriceReferences();
            if (landPriceObj != null)
            {
                landPriceObj.SetActive(active);
            }
        }

        /// <summary>
        /// 只负责解析并绑定 LandPrice 引用，不在代码中调整任何布局参数。
        /// </summary>
        private void ResolveLandPriceReferences()
        {
            var loadedFromResources = landPriceObj != null && landPriceObj.name == "LandPrice";
            if (!loadedFromResources)
            {
                if (landPriceObj != null)
                {
                    Destroy(landPriceObj);
                    landPriceObj = null;
                    landPriceText = null;
                    landPriceButton = null;
                    landPriceRecommendObj = null;
                }

                var existing = transform.Find("LandPrice");
                if (existing != null)
                {
                    Destroy(existing.gameObject);
                }
            }

            if (landPriceObj == null)
            {
                var prefab = GameplayResourceStore.LoadAsset<GameObject>(LandPricePrefabPath);
                if (prefab != null)
                {
                    landPriceObj = Instantiate(prefab, transform, false);
                    landPriceObj.name = "LandPrice";
                }
            }

            if (landPriceObj == null)
            {
                return;
            }

            landPriceText ??= landPriceObj.GetComponentInChildren<TMP_Text>(true);
            landPriceButton ??= landPriceObj.GetComponent<Button>();
            if (landPriceRecommendObj == null)
            {
                var recommend = landPriceObj.transform.Find("img_Recommend");
                landPriceRecommendObj = recommend != null ? recommend.gameObject : null;
            }

            BindButton(landPriceButton, HandlePrimaryAction);
        }

        /// <summary>
        /// 显示或隐藏建造中的烟雾与锤子特效，并统一放大显示。
        /// </summary>
        /// <param name="active">是否显示建造特效。</param>
        private void SetBuildEffectsActive(bool active)
        {
            SetScaledEffectActive(dushEffect, dushEffectDefaultScale, active);
            SetScaledEffectActive(hammerEffect, hammerEffectDefaultScale, active);
        }

        /// <summary>
        /// 按初始缩放倍率显示建造特效。
        /// </summary>
        /// <param name="effect">特效节点。</param>
        /// <param name="defaultScale">预制体中的原始缩放。</param>
        /// <param name="active">是否显示。</param>
        private void SetScaledEffectActive(GameObject effect, Vector3 defaultScale, bool active)
        {
            if (effect == null)
            {
                return;
            }

            effect.transform.localScale = defaultScale * buildEffectScaleMultiplier;
            effect.SetActive(active);
        }

        /// <summary>
        /// 获取按等级偏移建筑等级。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        private Vector3 GetOffsetByBuildingLevel()
        {
            var level = buildingInfo != null ? buildingInfo.buildingLevel : 0;
            return level switch
            {
                1 => level1Offset,
                2 => level2Offset,
                3 => level3Offset,
                _ => level1Offset
            };
        }
    }
}
