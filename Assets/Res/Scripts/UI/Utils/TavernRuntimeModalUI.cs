using System;
using System.Collections;
using JN.Client.Manager;
using JN.Client.Model;
using JN.Client.Scene;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JN.Client.UI
{
    /// <summary>
    /// 招聘面板当前显示的人员分类。
    /// </summary>
    public enum RecruitPanelRole
    {
        Chef,
        Waiter
    }

    /// <summary>
    /// 负责加载运行时 UI 预制体并绑定数据，避免在代码里临时新建节点。
    /// </summary>
    public static class TavernRuntimeModalUI
    {
        private const string NewFeatureOpenToastPath = "Assets/Res/Resources/UI/Runtime/NewFeatureOpenToast.prefab";
        private const string NewFeatureOpenTableLv2PanelPath = "Assets/Res/Resources/UI/Runtime/NewFeatureOpenTableLV2Panel.prefab";
        private const string RecruitPanelPath = "Assets/Res/Resources/UI/Runtime/RecruitPanel.prefab";
        private const string RecruitConfirmPanelPath = "Assets/Res/Resources/UI/Runtime/RecruitConfirmPanel.prefab";
        private const string TableUpgradePanelPath = "Assets/Res/Resources/UI/Runtime/TableUpgradePanel.prefab";
        private const string RuntimeInfoPanelPath = "Assets/Res/Resources/UI/Runtime/RuntimeInfoPanel.prefab";
        private const string FloatingWarningPath = "Assets/Res/Resources/UI/Runtime/TipsPanel.prefab";
        private const string ChefCookProgressPath = "Assets/Res/Resources/UI/Runtime/ChefCookProgress.prefab";
        private const string RecruitTabNormalSpritePath = "Assets/Res/Resources/Textures/UI/Panel/Recruit/panel_normal.png";
        private const string RecruitTabSelectedSpritePath = "Assets/Res/Resources/Textures/UI/Panel/Recruit/panel_light.png";
        private const int RecruitChefStaffId = 4;
        private const int RecruitWaiterStaffId = 5;
        private const int MaxTableLevel = 3;
        private const int TableUpgradeBaseCost = 800;
        // 不同等级对应的桌子图标，索引从 0 开始（等级 1 对应索引 0）
        private static readonly string[] TableLevelIconPaths =
        {
            "Assets/Res/Resources/Textures/UI/Icons 1/Furnitures/tableLvl1.png",
            "Assets/Res/Resources/Textures/UI/Icons 1/Furnitures/tableLvl2.png",
            "Assets/Res/Resources/Textures/UI/Icons 1/Furnitures/tableLvl3.png"
        };
        private static readonly string[] TableLevelDisplayNames =
        {
            "木桌", "雕花桌", "鎏金桌"
        };
        private static readonly string[] RecruitChefPortraitPaths =
        {
            "Assets/Res/Resources/Textures/UI/Common/halfPic/chushi1.png",
            "Assets/Res/Resources/Textures/UI/Common/halfPic/chushi2.png",
            "Assets/Res/Resources/Textures/UI/Common/halfPic/chushi3.png"
        };

        private static readonly string[] RecruitWaiterPortraitPaths =
        {
            "Assets/Res/Resources/Textures/UI/Common/halfPic/xiaoer1.png",
            "Assets/Res/Resources/Textures/UI/Common/halfPic/xiaoer2.png",
            "Assets/Res/Resources/Textures/UI/Common/halfPic/xiaoer3.png"
        };

        private const string RecruitShopkeeperPortraitPath = "Assets/Res/Resources/Textures/UI/Common/halfPic/zhanggui.png";

        /// <summary>
        /// 显示“新功能开启”的全屏提示。
        /// </summary>
        public static void ShowNewFeatureOpenToast()
        {
            var root = InstantiateOnCanvas(NewFeatureOpenToastPath);
            if (root == null)
            {
                return;
            }

            GetHost(root)?.StartCoroutine(DestroyAfter(root, 2f));
        }

        /// <summary>
        /// 显示“解锁二级桌升级”的全屏提示，两秒后关闭。
        /// </summary>
        /// <param name="onComplete">提示结束后的回调。</param>
        public static void ShowNewFeatureOpenTableLv2Panel(Action onComplete = null)
        {
            var root = InstantiateOnCanvas(NewFeatureOpenTableLv2PanelPath);
            if (root == null)
            {
                onComplete?.Invoke();
                return;
            }

            GetHost(root)?.StartCoroutine(DestroyAfter(root, 2f, onComplete));
        }

        /// <summary>
        /// 显示招聘确认界面。
        /// </summary>
        /// <param name="displayName">人员显示名。</param>
        /// <param name="roleText">人员类型。</param>
        /// <param name="portrait">半身像。</param>
        /// <param name="cost">花费。</param>
        /// <param name="onConfirm">确认招募回调。</param>
        public static void ShowRecruitPanel(string displayName, string roleText, Sprite portrait, int cost, Action onConfirm)
        {
            var root = InstantiateOnCanvas(RecruitConfirmPanelPath);
            if (root == null)
            {
                return;
            }

            SetTextWithFallback(root, "group_Panel/img_TitleBg/txt_Title", "txt_Title", $"招聘{roleText}");
            SetTextWithFallback(root, "group_Panel/img_NameBg/txt_Name", "txt_Name", ToVerticalText(displayName));
            SetTextWithFallback(root, "group_Panel/btn_Confirm/txt_CostCoinNum", "txt_CostCoinNum", cost.ToString());

            var singlePortrait = ResolveSingleRecruitPortrait(roleText, portrait);
            if (singlePortrait != null)
            {
                SetImageByName(root, "img_Portrait", singlePortrait);
            }

            BindButtonByName(root, "btn_Confirm", () =>
            {
                UnityEngine.Object.Destroy(root);
                onConfirm?.Invoke();
            });
            BindButtonByName(root, "btn_Close", () => UnityEngine.Object.Destroy(root));
        }

        /// <summary>
        /// 显示厨师和小二页签式招聘列表。
        /// </summary>
        /// <param name="defaultRole">默认选中的人员分类。</param>
        public static void ShowRecruitListPanel(RecruitPanelRole defaultRole = RecruitPanelRole.Chef)
        {
            var root = InstantiateOnCanvas(RecruitPanelPath);
            if (root == null)
            {
                return;
            }

            SetText(root, "Panel/txt_Title", "招聘员工");
            SetActive(root, "Panel/group_Tabs", true);
            SetActive(root, "Panel/group_List", true);
            BindButton(root, "Panel/btn_Close", () => UnityEngine.Object.Destroy(root));
            BindButton(root, "Panel/group_Tabs/btn_Chef", () => RefreshRecruitList(root, RecruitPanelRole.Chef));
            BindButton(root, "Panel/group_Tabs/btn_Waiter", () => RefreshRecruitList(root, RecruitPanelRole.Waiter));
            RefreshRecruitList(root, defaultRole);
        }

        /// <summary>
        /// 在厨师头顶显示做菜进度。
        /// </summary>
        /// <param name="target">厨师目标。</param>
        /// <param name="duration">做菜时长。</param>
        /// <param name="worldOffset">世界坐标偏移。</param>
        public static void ShowChefCookProgress(Transform target, float duration, Vector3 worldOffset)
        {
            if (target == null)
            {
                return;
            }

            var root = InstantiateOnCanvas(ChefCookProgressPath, false);
            var host = GetHost(root);
            if (root == null || host == null)
            {
                return;
            }

            host.StartCoroutine(ChefProgressRoutine(root, target, duration, worldOffset));
        }

        /// <summary>
        /// 显示桌子升级窗口，绑定当前等级与下一等级的桌子展示，
        /// 满级时显示提示并禁用确认按钮。
        /// </summary>
        /// <param name="table">被点击的桌位。</param>
        /// <param name="onConfirm">确认升级回调，已扣费成功后执行。</param>
        public static void ShowTableUpgradePanel(TableArea table, Action onConfirm)
        {
            if (table == null)
            {
                return;
            }

            var tableData = DataManager.Instance.GetTableData(table.tableId);
            var currentLevel = tableData != null ? Mathf.Clamp(tableData.level, 1, MaxTableLevel) : 1;
            var isMaxLevel = currentLevel >= MaxTableLevel;
            var nextLevel = isMaxLevel ? currentLevel : currentLevel + 1;
            var cost = isMaxLevel ? 0 : GetTableUpgradeCost(nextLevel);

            var root = InstantiateOnCanvas(TableUpgradePanelPath);
            if (root == null)
            {
                return;
            }

            SetText(root, "Panel/img_Title/txt_Title", "桌子升级");
            BindUpgradeTableInfo(root, "Panel/group_CurTableInfo", currentLevel, includeLevelTag: true);
            BindUpgradeTableInfo(root, "Panel/group_NextTableInfo", nextLevel, includeLevelTag: !isMaxLevel, overrideName: isMaxLevel ? "已满级" : null);

            // 升级按钮：满级时显示满级文案并禁用，否则显示具体花费
            SetText(root, "Panel/btn_Confirm/txt_Label", isMaxLevel ? "已满级" : "升级");
            SetText(root, "Panel/btn_Confirm/txt_CostCoinNum", isMaxLevel ? string.Empty : cost.ToString());
            SetActive(root, "Panel/btn_Confirm/img_Coin", !isMaxLevel);
            SetActive(root, "Panel/btn_Confirm/txt_CostCoinNum", !isMaxLevel);
            SetButtonInteractable(root, "Panel/btn_Confirm", !isMaxLevel);

            BindButton(root, "Panel/btn_Close", () => UnityEngine.Object.Destroy(root));
            if (!isMaxLevel)
            {
                BindButton(root, "Panel/btn_Confirm", () =>
                {
                    if (DataManager.Instance.PlayerData.coinNum < cost)
                    {
                        ShowFloatingWarning("金币不足，无法升级桌子");
                        return;
                    }

                    UnityEngine.Object.Destroy(root);
                    onConfirm?.Invoke();
                });
            }
        }

        /// <summary>
        /// 根据桌子等级填充对应组节点（图标 + 名称），用于当前/下一等级两个分组的展示。
        /// </summary>
        /// <param name="root">面板根节点。</param>
        /// <param name="groupPath">组节点路径，例如 Panel/group_CurTableInfo。</param>
        /// <param name="level">展示的桌子等级。</param>
        /// <param name="includeLevelTag">是否在名称里附带 Lv.X 标签。</param>
        /// <param name="overrideName">手动指定的名称，传入后不再走默认拼接。</param>
        private static void BindUpgradeTableInfo(GameObject root, string groupPath, int level, bool includeLevelTag, string overrideName = null)
        {
            var iconSprite = LoadSprite(GetTableIconPath(level));
            if (iconSprite != null)
            {
                SetImage(root, $"{groupPath}/img_TableIcon", iconSprite);
                SetImagePreserveAspect(root, $"{groupPath}/img_TableIcon", true);
            }

            string displayName;
            if (!string.IsNullOrEmpty(overrideName))
            {
                displayName = overrideName;
            }
            else
            {
                var baseName = GetTableLevelDisplayName(level);
                displayName = includeLevelTag ? $"{baseName} Lv.{level}" : baseName;
            }

            SetText(root, $"{groupPath}/txt_TableName", displayName);
        }

        /// <summary>
        /// 计算从当前等级升到目标等级所需的金币。
        /// </summary>
        /// <param name="targetLevel">升级后的目标等级（>=2）。</param>
        /// <returns>升级花费。</returns>
        private static int GetTableUpgradeCost(int targetLevel)
        {
            return TableUpgradeBaseCost * Mathf.Max(2, targetLevel);
        }

        /// <summary>
        /// 根据等级获取桌子图标资源路径，自动夹紧到 1~MaxTableLevel 之间。
        /// </summary>
        /// <param name="level">桌子等级。</param>
        /// <returns>桌子图标资源路径。</returns>
        private static string GetTableIconPath(int level)
        {
            var index = Mathf.Clamp(level, 1, MaxTableLevel) - 1;
            return TableLevelIconPaths[index];
        }

        /// <summary>
        /// 根据等级获取桌子的中文显示名。
        /// </summary>
        /// <param name="level">桌子等级。</param>
        /// <returns>桌子等级对应的展示名称。</returns>
        private static string GetTableLevelDisplayName(int level)
        {
            var index = Mathf.Clamp(level, 1, MaxTableLevel) - 1;
            return TableLevelDisplayNames[index];
        }

        /// <summary>
        /// 显示简单信息面板。
        /// </summary>
        /// <param name="title">标题。</param>
        /// <param name="content">内容。</param>
        public static void ShowInfoPanel(string title, string content)
        {
            var root = InstantiateOnCanvas(RuntimeInfoPanelPath);
            if (root == null)
            {
                return;
            }

            SetText(root, "Panel/txt_Title", title);
            SetText(root, "Panel/txt_Content", content);
            BindButton(root, "Panel/btn_Close", () => UnityEngine.Object.Destroy(root));
        }

        /// <summary>
        /// 在屏幕中央弹出两秒后上浮消失的提示文本。
        /// </summary>
        /// <param name="content">提示内容。</param>
        public static void ShowFloatingWarning(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            var root = InstantiateOnCanvas(FloatingWarningPath, false);
            if (root == null)
            {
                return;
            }

            var host = EnsureHost(root);
            var rect = root.GetComponent<RectTransform>();
            var canvasGroup = root.GetComponent<CanvasGroup>();
            var text = root.transform.Find("txt_Tip").GetComponent<TextMeshProUGUI>();

            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 120f);
                rect.localScale = Vector3.one;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            if (text != null)
            {
                text.text = content;
                text.color = new Color32(255, 245, 204, 255);
            }

            host?.StartCoroutine(FloatingWarningRoutine(root, rect, canvasGroup));
        }

        /// <summary>
        /// 执行厨师进度条动画。
        /// </summary>
        /// <param name="root">进度条根节点。</param>
        /// <param name="target">跟随目标。</param>
        /// <param name="duration">持续时间。</param>
        /// <param name="worldOffset">世界偏移。</param>
        /// <returns>协程迭代器。</returns>
        private static IEnumerator ChefProgressRoutine(GameObject root, Transform target, float duration, Vector3 worldOffset)
        {
            var rect = root.GetComponent<RectTransform>();
            var progressFill = root.transform.Find("img_ProgressBg/img_ProgressFill")?.GetComponent<Image>();
            if (progressFill != null)
            {
                progressFill.fillAmount = 0f;
            }

            duration = Mathf.Max(0.1f, duration);
            var time = 0f;
            while (time < duration && target != null && root != null)
            {
                time += Time.deltaTime;
                var progress = Mathf.Clamp01(time / duration);
                if (progressFill != null)
                {
                    progressFill.fillAmount = progress;
                }

                if (rect != null && Camera.main != null)
                {
                    rect.position = Camera.main.WorldToScreenPoint(target.position + worldOffset);
                }

                yield return null;
            }

            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// 驱动漂浮提示在两秒内向上移动并淡出。
        /// </summary>
        private static IEnumerator FloatingWarningRoutine(GameObject root, RectTransform rect, CanvasGroup canvasGroup)
        {
            const float duration = 2f;
            const float riseDistance = 90f;
            var elapsed = 0f;
            var startPos = rect != null ? rect.anchoredPosition : Vector2.zero;

            while (elapsed < duration && root != null)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);

                if (rect != null)
                {
                    rect.anchoredPosition = startPos + new Vector2(0f, riseDistance * progress);
                }

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = progress < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (progress - 0.6f) / 0.4f);
                }

                yield return null;
            }

            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        /// <summary>
        /// 延迟销毁预制体实例。
        /// </summary>
        /// <param name="target">目标实例。</param>
        /// <param name="seconds">延迟秒数。</param>
        /// <returns>协程迭代器。</returns>
        private static IEnumerator DestroyAfter(GameObject target, float seconds, Action onComplete = null)
        {
            yield return new WaitForSeconds(seconds);
            if (target != null)
            {
                UnityEngine.Object.Destroy(target);
            }

            onComplete?.Invoke();
        }

        /// <summary>
        /// 加载 UI 预制体并挂到当前可用 Canvas 下。
        /// </summary>
        /// <param name="prefabPath">预制体路径。</param>
        /// <returns>实例对象。</returns>
        private static GameObject InstantiateOnCanvas(string prefabPath, bool stretchToCanvas = true)
        {
            var canvas = ResolveRuntimeUiCanvas();
            var prefab = LoadPrefab(prefabPath);
            if (canvas == null || prefab == null)
            {
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
            instance.transform.SetAsLastSibling();
            if (stretchToCanvas)
            {
                StretchToParent(instance.transform as RectTransform);
            }

            return instance;
        }

        /// <summary>
        /// 优先获取 UIKit 面板所在的屏幕 UI 画布，避免弹窗被挂到场景层 Canvas。
        /// </summary>
        /// <returns>运行时 UI 画布。</returns>
        private static Canvas ResolveRuntimeUiCanvas()
        {
            var tavernPanel = UIKit.GetPanel<TavernStatusBarPanelController>();
            var tavernCanvas = GetParentCanvas(tavernPanel != null ? tavernPanel.transform : null);
            if (tavernCanvas != null)
            {
                return tavernCanvas;
            }

            var townPanel = UIKit.GetPanel<TownStatusBarPanelController>();
            var townCanvas = GetParentCanvas(townPanel != null ? townPanel.transform : null);
            if (townCanvas != null)
            {
                return townCanvas;
            }

            return FindBestScreenSpaceCanvas();
        }

        /// <summary>
        /// 获取指定节点上级的画布。
        /// </summary>
        /// <param name="target">目标节点。</param>
        /// <returns>上级画布。</returns>
        private static Canvas GetParentCanvas(Transform target)
        {
            return target != null ? target.GetComponentInParent<Canvas>() : null;
        }

        /// <summary>
        /// 从当前场景中选择最适合作为弹窗父级的屏幕空间画布。
        /// </summary>
        /// <returns>屏幕空间画布。</returns>
        private static Canvas FindBestScreenSpaceCanvas()
        {
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Canvas bestCanvas = null;
            var bestScore = int.MinValue;
            for (var index = 0; index < canvases.Length; index++)
            {
                var canvas = canvases[index];
                if (canvas == null)
                {
                    continue;
                }

                var score = canvas.sortingOrder;
                if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    score += 10000;
                }
                else if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
                {
                    score += 5000;
                }
                else
                {
                    score -= 10000;
                }

                if (canvas.name.Contains("Environment") || canvas.name.Contains("Scene"))
                {
                    score -= 500;
                }

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestCanvas = canvas;
            }

            return bestCanvas;
        }

        /// <summary>
        /// 将弹窗根节点铺满父级画布，保证遮罩和点击区域覆盖整个屏幕。
        /// </summary>
        /// <param name="rectTransform">弹窗根节点。</param>
        private static void StretchToParent(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// 获取预制体实例上的协程组件。
        /// </summary>
        /// <param name="root">实例根节点。</param>
        /// <returns>协程承载组件。</returns>
        private static RuntimePrefabCoroutineHost GetHost(GameObject root)
        {
            return root != null ? root.GetComponent<RuntimePrefabCoroutineHost>() : null;
        }

        /// <summary>
        /// 确保运行时 UI 预制体具备协程承载组件。
        /// </summary>
        private static RuntimePrefabCoroutineHost EnsureHost(GameObject root)
        {
            if (root == null)
            {
                return null;
            }

            var host = root.GetComponent<RuntimePrefabCoroutineHost>();
            return host != null ? host : root.AddComponent<RuntimePrefabCoroutineHost>();
        }

        /// <summary>
        /// 绑定按钮点击事件。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="path">按钮路径。</param>
        /// <param name="callback">回调。</param>
        private static void BindButton(GameObject root, string path, Action callback)
        {
            var button = root.transform.Find(path)?.GetComponent<Button>();
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => callback?.Invoke());
        }

        /// <summary>
        /// 刷新招聘列表页签和三个候选槽位。
        /// </summary>
        /// <param name="root">招聘面板根节点。</param>
        /// <param name="role">当前显示的招聘分类。</param>
        private static void RefreshRecruitList(GameObject root, RecruitPanelRole role)
        {
            if (root == null)
            {
                return;
            }

            var dataManager = DataManager.Instance;
            var isChef = role == RecruitPanelRole.Chef;
            var staffId = isChef ? RecruitChefStaffId : RecruitWaiterStaffId;
            var staffRole = isChef ? StaffRole.Chef : StaffRole.Waiter;
            var roleName = isChef ? "厨师" : "小二";
            var maxCount = isChef ? DataManager.MaxGuideChefCount : DataManager.MaxGuideWaiterCount;
            var hiredCount = isChef ? dataManager.GetHiredGuideChefCount() : dataManager.GetHiredGuideWaiterCount();
            var staff = dataManager.GetGuideStaffConfig(staffId, staffRole);
            var cost = dataManager.GetGuideStaffHireCost(staffId, staffRole);
            var fallbackPortrait = staff != null ? staff.icon : null;

            RefreshRecruitTabs(root, role);

            for (var index = 0; index < maxCount; index++)
            {
                RefreshRecruitRow(root, role, index, hiredCount, roleName, fallbackPortrait, cost);
            }
        }

        /// <summary>
        /// 刷新单个招聘槽位的显示和按钮状态。
        /// </summary>
        /// <param name="root">招聘面板根节点。</param>
        /// <param name="role">当前人员分类。</param>
        /// <param name="index">槽位索引。</param>
        /// <param name="hiredCount">已招聘数量。</param>
        /// <param name="roleName">人员类型名称。</param>
        /// <param name="portrait">人员头像。</param>
        /// <param name="cost">招聘价格。</param>
        private static void RefreshRecruitRow(GameObject root, RecruitPanelRole role, int index, int hiredCount, string roleName, Sprite portrait, int cost)
        {
            var rowPath = $"Panel/group_List/item_{index + 1}";
            var isHired = index < hiredCount;
            SetText(root, $"{rowPath}/txt_Name", $"{roleName}{index + 1}");
            SetText(root, $"{rowPath}/txt_Status", isHired ? "已招募" : "未招募");
            SetText(root, $"{rowPath}/txt_Cost", isHired ? "已入职" : $"招聘价格：{cost}");
            SetText(root, $"{rowPath}/btn_Recruit/txt_Label", isHired ? "已招募" : $"{cost}");
            SetActive(root, $"{rowPath}/btn_Recruit", !isHired);
            SetTextColor(root, $"{rowPath}/txt_Status", isHired ? new Color(0.21f, 0.67f, 0.25f, 1f) : new Color(0.83f, 0.24f, 0.20f, 1f));
            SetImageColor(root, $"{rowPath}/img_Bg", isHired ? new Color(0.92f, 0.84f, 0.68f, 0.95f) : new Color(1f, 0.94f, 0.78f, 0.95f));
            var recruitPortrait = ResolveRecruitListPortrait(role, index, portrait);
            if (recruitPortrait != null)
            {
                SetImage(root, $"{rowPath}/img_Portrait", recruitPortrait);
            }

            BindButton(root, $"{rowPath}/btn_Recruit", () => TryRecruitFromList(root, role));
        }

        /// <summary>
        /// 处理列表中的招聘按钮点击，并在成功后刷新列表。
        /// </summary>
        /// <param name="root">招聘面板根节点。</param>
        /// <param name="role">要招聘的人员分类。</param>
        private static void TryRecruitFromList(GameObject root, RecruitPanelRole role)
        {
            var dataManager = DataManager.Instance;
            if (dataManager == null)
            {
                return;
            }

            var currentCount = role == RecruitPanelRole.Chef ? dataManager.GetHiredGuideChefCount() : dataManager.GetHiredGuideWaiterCount();
            var displayName = role == RecruitPanelRole.Chef ? $"厨师{currentCount + 1}" : $"小二{currentCount + 1}";
            var roleName = role == RecruitPanelRole.Chef ? "厨师" : "小二";
            var portrait = ResolveRecruitListPortrait(role, currentCount, null);
            var staffRole = role == RecruitPanelRole.Chef ? StaffRole.Chef : StaffRole.Waiter;
            var staffId = role == RecruitPanelRole.Chef ? RecruitChefStaffId : RecruitWaiterStaffId;
            var cost = dataManager.GetGuideStaffHireCost(staffId, staffRole);

            ShowRecruitPanel(displayName, roleName, portrait, cost, () =>
            {
                string message;
                var success = role == RecruitPanelRole.Chef
                    ? dataManager.TryHireGuideChef(out message)
                    : dataManager.TryHireGuideWaiter(out message);
                if (!success)
                {
                    if (IsCoinShortageMessage(message))
                    {
                        ShowFloatingWarning(message);
                    }
                    else
                    {
                        ShowInfoPanel("招聘失败", message);
                    }

                    return;
                }

                if (TavernSceneManager.Instance != null)
                {
                    if (role == RecruitPanelRole.Chef)
                    {
                        TavernSceneManager.Instance.PlayGuideChefEnterFromBottomRecruit();
                    }
                    else
                    {
                        TavernSceneManager.Instance.PlayGuideWaiterEnterFromBottomRecruit();
                    }
                }

                RefreshRecruitList(root, role);
            });
        }

        /// <summary>
        /// 刷新招聘页签的选中态。
        /// </summary>
        /// <param name="root">招聘面板根节点。</param>
        /// <param name="selectedRole">当前选中的分类。</param>
        private static void RefreshRecruitTabs(GameObject root, RecruitPanelRole selectedRole)
        {
            var dataManager = DataManager.Instance;
            var chefLabel = dataManager == null ? "厨师" : $"厨师 {dataManager.GetHiredGuideChefCount()}/{DataManager.MaxGuideChefCount}";
            var waiterLabel = dataManager == null ? "小二" : $"小二 {dataManager.GetHiredGuideWaiterCount()}/{DataManager.MaxGuideWaiterCount}";
            ApplyRecruitTabState(root, "Panel/group_Tabs/btn_Chef", chefLabel, selectedRole == RecruitPanelRole.Chef);
            ApplyRecruitTabState(root, "Panel/group_Tabs/btn_Waiter", waiterLabel, selectedRole == RecruitPanelRole.Waiter);
        }

        /// <summary>
        /// 套用招聘页签按钮的选中图片和文本颜色。
        /// </summary>
        /// <param name="root">招聘面板根节点。</param>
        /// <param name="buttonPath">页签按钮路径。</param>
        /// <param name="label">页签文案。</param>
        /// <param name="selected">是否选中。</param>
        private static void ApplyRecruitTabState(GameObject root, string buttonPath, string label, bool selected)
        {
            var spritePath = selected ? RecruitTabSelectedSpritePath : RecruitTabNormalSpritePath;
            var tabSprite = LoadSprite(spritePath);
            if (tabSprite != null)
            {
                SetImage(root, buttonPath, tabSprite);
            }

            SetText(root, $"{buttonPath}/txt_Label", label);
        }

        /// <summary>
        /// 设置文本内容。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="path">文本路径。</param>
        /// <param name="content">内容。</param>
        private static void SetText(GameObject root, string path, string content)
        {
            var text = root.transform.Find(path)?.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = content;
            }
        }

        /// <summary>
        /// 按节点名称设置文本，适配独立确认面板。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="content">文本内容。</param>
        private static void SetTextByName(GameObject root, string nodeName, string content)
        {
            var text = FindChildByName(root != null ? root.transform : null, nodeName)?.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.text = content;
            }
        }

        /// <summary>
        /// 先按路径设置文本，路径不存在时再按节点名兜底，避免重复写值。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="path">优先路径。</param>
        /// <param name="nodeName">兜底节点名。</param>
        /// <param name="content">文本内容。</param>
        private static void SetTextWithFallback(GameObject root, string path, string nodeName, string content)
        {
            if (!SetTextIfExists(root, path, content))
            {
                SetTextByName(root, nodeName, content);
            }
        }

        /// <summary>
        /// 路径存在时写入文本并返回 true，不存在返回 false。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="path">文本路径。</param>
        /// <param name="content">文本内容。</param>
        /// <returns>是否写入成功。</returns>
        private static bool SetTextIfExists(GameObject root, string path, string content)
        {
            var text = root != null ? root.transform.Find(path)?.GetComponent<TextMeshProUGUI>() : null;
            if (text == null)
            {
                return false;
            }

            text.text = content;
            return true;
        }

        /// <summary>
        /// 将短名称转换为竖向文本，适配招聘面板的竖排姓名区域。
        /// </summary>
        /// <param name="content">原始名称。</param>
        /// <returns>逐字换行后的名称。</returns>
        private static string ToVerticalText(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return string.Empty;
            }

            return string.Join("\n", content.ToCharArray());
        }

        /// <summary>
        /// 设置图片。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="path">图片路径。</param>
        /// <param name="sprite">图片。</param>
        private static void SetImage(GameObject root, string path, Sprite sprite)
        {
            var image = root.transform.Find(path)?.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
            }
        }

        /// <summary>
        /// 按节点名称设置图片，适配独立确认面板。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="sprite">图片资源。</param>
        private static void SetImageByName(GameObject root, string nodeName, Sprite sprite)
        {
            var image = FindChildByName(root != null ? root.transform : null, nodeName)?.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
            }
        }

        /// <summary>
        /// 控制 Image 的 PreserveAspect 标记，让按等级切换的图标自适应大小。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="path">图片路径。</param>
        /// <param name="preserveAspect">是否保持原始比例。</param>
        private static void SetImagePreserveAspect(GameObject root, string path, bool preserveAspect)
        {
            var image = root.transform.Find(path)?.GetComponent<Image>();
            if (image != null)
            {
                image.preserveAspect = preserveAspect;
            }
        }

        /// <summary>
        /// 控制按钮是否可交互，用于满级时禁用确认按钮。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="path">按钮路径。</param>
        /// <param name="interactable">是否可交互。</param>
        private static void SetButtonInteractable(GameObject root, string path, bool interactable)
        {
            var button = root.transform.Find(path)?.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        /// <summary>
        /// 判断失败提示是否属于金币不足。
        /// </summary>
        private static bool IsCoinShortageMessage(string message)
        {
            return !string.IsNullOrWhiteSpace(message)
                   && (message.Contains("金币不足") || message.Contains("铜钱不足"));
        }

        /// <summary>
        /// 设置节点显隐。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="path">节点路径。</param>
        /// <param name="visible">是否显示。</param>
        private static void SetActive(GameObject root, string path, bool visible)
        {
            var target = root.transform.Find(path);
            if (target != null)
            {
                target.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// 设置图片颜色，用于页签选中和列表状态。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="path">图片路径。</param>
        /// <param name="color">目标颜色。</param>
        private static void SetImageColor(GameObject root, string path, Color color)
        {
            var image = root.transform.Find(path)?.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        /// <summary>
        /// 设置文本颜色，用于状态和页签选中态。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="path">文本路径。</param>
        /// <param name="color">目标颜色。</param>
        private static void SetTextColor(GameObject root, string path, Color color)
        {
            var text = root.transform.Find(path)?.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = color;
            }
        }

        /// <summary>
        /// 根据招聘职业和序号读取列表里固定使用的半身像。
        /// </summary>
        /// <param name="role">招聘职业页签。</param>
        /// <param name="index">槽位序号。</param>
        /// <param name="fallbackPortrait">找不到固定资源时的回退头像。</param>
        /// <returns>对应槽位头像。</returns>
        private static Sprite ResolveRecruitListPortrait(RecruitPanelRole role, int index, Sprite fallbackPortrait)
        {
            var portraitPaths = role == RecruitPanelRole.Chef ? RecruitChefPortraitPaths : RecruitWaiterPortraitPaths;
            if (index >= 0 && index < portraitPaths.Length)
            {
                var sprite = LoadSprite(portraitPaths[index]);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return fallbackPortrait;
        }

        /// <summary>
        /// 根据单人招聘面板的职业文案选择默认半身像。
        /// </summary>
        /// <param name="roleText">职业文案。</param>
        /// <param name="fallbackPortrait">外部传入的回退头像。</param>
        /// <returns>单人面板头像。</returns>
        private static Sprite ResolveSingleRecruitPortrait(string roleText, Sprite fallbackPortrait)
        {
            if (!string.IsNullOrWhiteSpace(roleText))
            {
                if (roleText.Contains("掌柜"))
                {
                    var shopkeeperPortrait = LoadSprite(RecruitShopkeeperPortraitPath);
                    if (shopkeeperPortrait != null)
                    {
                        return shopkeeperPortrait;
                    }
                }

                if (roleText.Contains("厨师"))
                {
                    var chefPortrait = LoadSprite(RecruitChefPortraitPaths[0]);
                    if (chefPortrait != null)
                    {
                        return chefPortrait;
                    }
                }

                if (roleText.Contains("小二"))
                {
                    var waiterPortrait = LoadSprite(RecruitWaiterPortraitPaths[0]);
                    if (waiterPortrait != null)
                    {
                        return waiterPortrait;
                    }
                }
            }

            return fallbackPortrait;
        }

        /// <summary>
        /// 读取单张 Sprite 资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>图片资源。</returns>
        private static Sprite LoadSprite(string path)
        {
            return GameplayResourceStore.LoadAsset<Sprite>(path);
        }

        /// <summary>
        /// 读取预制体资源。
        /// </summary>
        /// <param name="path">资源路径。</param>
        /// <returns>预制体。</returns>
        private static GameObject LoadPrefab(string path)
        {
            return GameplayResourceStore.LoadAsset<GameObject>(path);
        }

        /// <summary>
        /// 按节点名称绑定按钮点击事件，适配独立确认面板。
        /// </summary>
        /// <param name="root">根节点。</param>
        /// <param name="nodeName">节点名称。</param>
        /// <param name="callback">点击回调。</param>
        private static void BindButtonByName(GameObject root, string nodeName, Action callback)
        {
            var button = FindChildByName(root != null ? root.transform : null, nodeName)?.GetComponent<Button>();
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            if (callback != null)
            {
                button.onClick.AddListener(() => callback());
            }
        }

        /// <summary>
        /// 递归查找指定名称的子节点。
        /// </summary>
        /// <param name="root">起始节点。</param>
        /// <param name="nodeName">目标节点名。</param>
        /// <returns>找到时返回节点，否则返回 null。</returns>
        private static Transform FindChildByName(Transform root, string nodeName)
        {
            if (root == null || string.IsNullOrWhiteSpace(nodeName))
            {
                return null;
            }

            if (root.name == nodeName)
            {
                return root;
            }

            for (var index = 0; index < root.childCount; index++)
            {
                var result = FindChildByName(root.GetChild(index), nodeName);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
