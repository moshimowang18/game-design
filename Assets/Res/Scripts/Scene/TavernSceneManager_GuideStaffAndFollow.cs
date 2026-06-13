using System.Collections.Generic;
using JN.Client.Model;
using UnityEngine;
using UnityEngine.AI;

namespace JN.Client.Scene
{
    public partial class TavernSceneManager
    {
        private static readonly Vector3 GuideChefSecondFixedWorldPosition = new(-7.264f, 0, -5.112f);

        #region Guide Staff And Follow

        /// <summary>
        /// 获取指定员工类型当前在场景中的所有引导表现。
        /// </summary>
        /// <param name="visualKey">员工表现键。</param>
        /// <returns>去除空引用后的员工列表。</returns>
        private List<GameObject> GetGuideStaffVisualGroup(string visualKey)
        {
            if (!guideStaffVisualGroups.TryGetValue(visualKey, out var group) || group == null)
            {
                group = new System.Collections.Generic.List<GameObject>();
                guideStaffVisualGroups[visualKey] = group;
            }

            group.RemoveAll(current => current == null);
            return group;
        }

        /// <summary>
        /// 获取指定员工类型当前在场景中的所有有效表现对象。
        /// </summary>
        /// <param name="visualKey">员工表现键。</param>
        /// <returns>员工表现列表。</returns>
        private GameObject[] GetGuideStaffVisuals(string visualKey)
        {
            return GetGuideStaffVisualGroup(visualKey).ToArray();
        }

        /// <summary>
        /// 公开接口：获取场景中所有小二（Waiter）的 3D GameObject 列表，按生成顺序。
        /// 供新系统建立 EmployeeData ↔ 3D 模型映射。
        /// </summary>
        public GameObject[] GetWaiterVisualsPublic()
        {
            return GetGuideStaffVisuals(GuideWaiterVisualKey);
        }

        /// <summary>
        /// 追加创建一个新的员工引导表现，并记录到分组里。
        /// </summary>
        /// <param name="visualKey">员工表现键。</param>
        /// <param name="role">员工角色。</param>
        /// <param name="preferredStaffId">优先员工编号。</param>
        /// <returns>创建后的员工对象。</returns>
        private GameObject CreateAdditionalGuideStaffVisual(string visualKey, StaffRole role, int preferredStaffId)
        {
            var staffPrefab = ResolveGuideStaffPrefab(role, preferredStaffId);
            if (staffPrefab == null)
            {
                return null;
            }

            var visual = Instantiate(staffPrefab);
            var group = GetGuideStaffVisualGroup(visualKey);
            var suffix = group.Count;
            visual.name = suffix <= 0 ? $"{visualKey}_GuideVisual" : $"{visualKey}_GuideVisual_{suffix + 1}";
            foreach (var agent in visual.GetComponentsInChildren<NavMeshAgent>(true))
            {
                agent.enabled = false;
            }

            group.Add(visual);
            if (!guideStaffVisuals.ContainsKey(visualKey) || guideStaffVisuals[visualKey] == null)
            {
                guideStaffVisuals[visualKey] = visual;
            }

            return visual;
        }

        /// <summary>
        /// 销毁指定员工类型的全部引导表现。
        /// </summary>
        /// <param name="visualKey">员工表现键。</param>
        private void DestroyGuideStaffVisuals(string visualKey)
        {
            var group = GetGuideStaffVisualGroup(visualKey);
            for (var index = 0; index < group.Count; index++)
            {
                if (group[index] != null)
                {
                    // 销毁前先把入场动画占位移除，避免后续 HashSet 持有已销毁的引用。
                    staffVisualsBeingAnimated.Remove(group[index]);
                    Destroy(group[index]);
                }
            }

            group.Clear();
            guideStaffVisuals.Remove(visualKey);
        }

        /// <summary>
        /// 根据当前招聘数量同步同类员工表现数量。
        /// </summary>
        /// <param name="visualKey">员工表现键。</param>
        /// <param name="role">员工角色。</param>
        /// <param name="targetCount">目标数量。</param>
        /// <param name="preferredStaffId">优先员工编号。</param>
        private void EnsureGuideStaffVisualCount(string visualKey, StaffRole role, int targetCount, int preferredStaffId)
        {
            var group = GetGuideStaffVisualGroup(visualKey);
            while (group.Count < targetCount)
            {
                if (CreateAdditionalGuideStaffVisual(visualKey, role, preferredStaffId) == null)
                {
                    break;
                }
            }

            while (group.Count > targetCount)
            {
                var lastIndex = group.Count - 1;
                var visual = group[lastIndex];
                group.RemoveAt(lastIndex);
                if (visual != null)
                {
                    Destroy(visual);
                }
            }

            guideStaffVisuals[visualKey] = group.Count > 0 ? group[0] : null;
        }

        /// <summary>
        /// 计算多人员工在同一工作点附近的散开偏移，避免人物完全重叠。
        /// </summary>
        /// <param name="visualKey">员工表现键。</param>
        /// <param name="index">序号。</param>
        /// <returns>对应序号的本地偏移。</returns>
        private static Vector3 GetGuideStaffStackOffset(string visualKey, int index)
        {
            if (index <= 0)
            {
                return Vector3.zero;
            }

            if (visualKey == GuideChefVisualKey)
            {
                // 厨师横向散开：沿锚点 right 方向排布，2 号厨师 x 约为 -7.264。
                return new Vector3(-0.228f * index, 0f, 0f);
            }

            if (visualKey == GuideWaiterVisualKey)
            {
                return new Vector3((index % 2 == 0 ? -1 : 1) * 0.65f, 0f, (index + 1) * 0.45f);
            }

            return new Vector3((index % 2 == 0 ? -1 : 1) * 0.35f, 0f, (index + 1) * 0.15f);
        }

        /// <summary>
        /// 刷新新手引导阶段的员工站位表现。
        /// </summary>
        /// <param name="visualKey">用于区分掌柜、小二和厨师的表现键。</param>
        /// <param name="role">参数值。</param>
        /// <param name="should显示">参数值。</param>
        /// <param name="anchorObject">参数值。</param>
        /// <param name="localOffset">参数值。</param>
        /// <param name="preferredStaffId">数据编号。</param>
        /// <param name="extraYawDegrees">在锚点朝向基础上额外旋转的角度。</param>
        private void RefreshGuideStaffVisual(string visualKey, StaffRole role, bool shouldShow, GameObject anchorObject, Vector3 localOffset, int preferredStaffId, float extraYawDegrees = 0f)
        {
            if (!shouldShow || anchorObject == null || !anchorObject.activeInHierarchy)
            {
                DestroyGuideStaffVisuals(visualKey);
                return;
            }

            if (HasVisibleRuntimeStaffNearAnchor(GetStaffNameKeyword(visualKey, role), anchorObject.transform))
            {
                DestroyGuideStaffVisuals(visualKey);
                return;
            }

            if (guideStaffVisuals.TryGetValue(visualKey, out var existingVisual) && existingVisual != null)
            {
                // 入场动画进行中保留当前位置，等动画播完再交回常规位置同步逻辑。
                if (staffVisualsBeingAnimated.Contains(existingVisual))
                {
                    return;
                }
                UpdateGuideStaffTransform(existingVisual.transform, anchorObject.transform, localOffset, extraYawDegrees);
                return;
            }

            var staffPrefab = ResolveGuideStaffPrefab(role, preferredStaffId);
            if (staffPrefab == null)
            {
                return;
            }

            var visual = Instantiate(staffPrefab);
            visual.name = $"{visualKey}_GuideVisual";
            foreach (var agent in visual.GetComponentsInChildren<NavMeshAgent>(true))
            {
                agent.enabled = false;
            }

            UpdateGuideStaffTransform(visual.transform, anchorObject.transform, localOffset, extraYawDegrees);
            guideStaffVisuals[visualKey] = visual;
        }

        /// <summary>
        /// 使用场景中的预摆放节点作为员工生成标记位。
        /// </summary>
        /// <param name="visualKey">用于区分掌柜、小二和厨师的表现键。</param>
        /// <param name="role">员工角色。</param>
        /// <param name="shouldShow">是否需要显示员工。</param>
        /// <param name="markerName">场景中用于对齐位置和朝向的节点名。</param>
        /// <param name="legacyMarkerName">旧场景节点名，用于兼容尚未改名的场景。</param>
        /// <param name="fallbackAnchor">找不到标记位时使用的锚点。</param>
        /// <param name="fallbackOffset">找不到标记位时使用的本地偏移。</param>
        /// <param name="preferredStaffId">员工配置编号。</param>
        /// <param name="fallbackYawDegrees">找不到标记位时额外旋转的角度。</param>
        private void RefreshGuideStaffVisualAtSceneMarker(
            string visualKey,
            StaffRole role,
            bool shouldShow,
            string markerName,
            string legacyMarkerName,
            GameObject fallbackAnchor,
            Vector3 fallbackOffset,
            int preferredStaffId,
            float fallbackYawDegrees = 0f)
        {
            if (!shouldShow)
            {
                DestroyGuideStaffVisuals(visualKey);
                return;
            }

            var marker = FindSceneTransformByName(markerName) ?? FindSceneTransformByName(legacyMarkerName);
            if (marker == null)
            {
                RefreshGuideStaffVisual(visualKey, role, true, fallbackAnchor, fallbackOffset, preferredStaffId, fallbackYawDegrees);
                return;
            }

            var visual = GetOrCreateGuideStaffVisual(visualKey, role, preferredStaffId);
            if (visual == null)
            {
                return;
            }

            // 入场动画进行中不要瞬移到锚点，否则人会先闪到目的地再被走过来。
            if (staffVisualsBeingAnimated.Contains(visual))
            {
                return;
            }

            visual.transform.position = marker.position;
            visual.transform.rotation = marker.rotation;
            visual.transform.localScale = ResolveGuideStaffVisualScale(visualKey, marker.lossyScale);
        }

        /// <summary>
        /// 根据标记点和序号计算员工目标点。默认使用 marker 的局部轴偏移；
        /// 厨师额外强制按世界 Z 修正，确保 2 号厨师 z 精确到 -5.112。
        /// </summary>
        /// <param name="visualKey">员工表现键。</param>
        /// <param name="marker">标记点。</param>
        /// <param name="index">员工序号（从 0 开始）。</param>
        /// <returns>世界坐标目标点。</returns>
        private static Vector3 ResolveGuideStaffMarkerPosition(string visualKey, Transform marker, int index)
        {
            if (marker == null)
            {
                return Vector3.zero;
            }

            // 用户要求 2 号厨师固定世界坐标，不受锚点朝向旋转影响。
            if (visualKey == GuideChefVisualKey && index == 1)
            {
                return GuideChefSecondFixedWorldPosition;
            }

            var stackOffset = GetGuideStaffStackOffset(visualKey, index);
            var position = marker.position + marker.right * stackOffset.x + marker.up * stackOffset.y + marker.forward * stackOffset.z;
            if (visualKey == GuideChefVisualKey && index > 0)
            {
                // 锚点约 z=-4.959，按世界 Z 每名 -0.153 排列：index=1 -> z=-5.112。
                position.z = marker.position.z - 0.153f * index;
            }

            return position;
        }

        /// <summary>
        /// 获取或创建指定员工的引导表现。
        /// </summary>
        /// <param name="visualKey">用于区分员工表现的键。</param>
        /// <param name="role">员工角色。</param>
        /// <param name="preferredStaffId">员工配置编号。</param>
        /// <returns>创建成功时返回员工表现对象。</returns>
        private GameObject GetOrCreateGuideStaffVisual(string visualKey, StaffRole role, int preferredStaffId)
        {
            if (guideStaffVisuals.TryGetValue(visualKey, out var existingVisual) && existingVisual != null)
            {
                return existingVisual;
            }

            var staffPrefab = ResolveGuideStaffPrefab(role, preferredStaffId);
            if (staffPrefab == null)
            {
                return null;
            }

            var visual = Instantiate(staffPrefab);
            visual.name = $"{visualKey}_GuideVisual";
            foreach (var agent in visual.GetComponentsInChildren<NavMeshAgent>(true))
            {
                agent.enabled = false;
            }

            guideStaffVisuals[visualKey] = visual;
            return visual;
        }

        /// <summary>
        /// 销毁指定角色的引导员工表现。
        /// </summary>
        /// <param name="visualKey">用于区分员工表现的键。</param>
        private void DestroyGuideStaffVisual(string visualKey)
        {
            DestroyGuideStaffVisuals(visualKey);
        }

        /// <summary>
        /// 根据锚点和偏移同步员工表现位置。
        /// </summary>
        /// <param name="visual">参数值。</param>
        /// <param name="anchor">参数值。</param>
        /// <param name="localOffset">参数值。</param>
        /// <param name="extraYawDegrees">在锚点朝向基础上额外旋转的角度。</param>
        private static void UpdateGuideStaffTransform(Transform visual, Transform anchor, Vector3 localOffset, float extraYawDegrees)
        {
            if (visual == null || anchor == null)
            {
                return;
            }

            var worldOffset = anchor.right * localOffset.x + anchor.up * localOffset.y + anchor.forward * localOffset.z;
            visual.position = anchor.position + worldOffset;
            visual.rotation = Quaternion.LookRotation(-anchor.forward, Vector3.up) * Quaternion.Euler(0f, extraYawDegrees, 0f);
        }

        /// <summary>
        /// 根据员工类型修正模型缩放，小二略微缩小以贴合当前场景比例。
        /// </summary>
        /// <param name="visualKey">用于区分员工表现的键。</param>
        /// <param name="sourceScale">场景标记位或锚点提供的原始缩放。</param>
        /// <returns>应用角色修正后的缩放。</returns>
        private static Vector3 ResolveGuideStaffVisualScale(string visualKey, Vector3 sourceScale)
        {
            return visualKey == GuideWaiterVisualKey ? sourceScale * WaiterVisualScaleMultiplier : sourceScale;
        }

        /// <summary>
        /// 判断锚点附近是否已经有真实员工。
        /// </summary>
        /// <param name="matchKeyword">需要匹配的员工根节点关键字。</param>
        /// <param name="anchor">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private static bool HasVisibleRuntimeStaffNearAnchor(string matchKeyword, Transform anchor)
        {
            if (anchor == null || string.IsNullOrEmpty(matchKeyword))
            {
                return false;
            }

            var scene = anchor.gameObject.scene;
            var renderers = UnityEngine.Object.FindObjectsOfType<Renderer>(true);
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer == null || renderer.gameObject.scene != scene || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var targetTransform = renderer.transform.root != null ? renderer.transform.root : renderer.transform;
                var targetName = targetTransform.name;
                if (targetName.Contains("GuideVisual") || !targetName.Contains(matchKeyword))
                {
                    continue;
                }

                if (Vector3.Distance(targetTransform.position, anchor.position) > 2.2f)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 根据引导员工类型获取场景里用于去重的名称关键字。
        /// </summary>
        /// <param name="visualKey">员工表现键。</param>
        /// <param name="role">员工角色。</param>
        /// <returns>可用于匹配场景模型的名称关键字。</returns>
        private static string GetStaffNameKeyword(string visualKey, StaffRole role)
        {
            return role == StaffRole.Chef ? "Chef" : visualKey;
        }

        /// <summary>
        /// 按角色和优先编号解析员工 预制体。
        /// </summary>
        /// <param name="role">参数值。</param>
        /// <param name="preferredStaffId">数据编号。</param>
        /// <returns>返回匹配到的对象引用。</returns>
        private static GameObject ResolveGuideStaffPrefab(StaffRole role, int preferredStaffId)
        {
            var allStaff = SO_Staff.GetAll();
            SO_Staff fallback = null;
            for (var index = 0; index < allStaff.Count; index++)
            {
                var staff = allStaff[index];
                if (staff == null || staff.role != role)
                {
                    continue;
                }

                fallback ??= staff;
                if (!int.TryParse(staff.staffId, out var numericStaffId) || numericStaffId != preferredStaffId)
                {
                    continue;
                }

                var preferredLevel = staff.GetLevelConfig(1);
                if (preferredLevel?.staffPrefab != null)
                {
                    return preferredLevel.staffPrefab;
                }
            }

            var fallbackLevel = fallback?.GetLevelConfig(1);
            return fallbackLevel?.staffPrefab;
        }

        /// <summary>
        /// 在帧末同步跟随 界面 和场景表现位置。
        /// </summary>
        private void LateUpdate()
        {
            CleanupGuideStaffVisuals();

            if (SceneCamera == null)
            {
                return;
            }

            foreach (var data in uiFollowList)
            {
                if (data.uiTransform == null || data.tableUI == null)
                {
                    continue;
                }

                UpdateScreenSpaceElement(data.uiTransform as RectTransform, data.tableUI.GetWorldAnchorPosition());
            }

            foreach (var button in guideWorldButtons)
            {
                if (button == null || button.rectTransform == null || button.target == null || !button.rectTransform.gameObject.activeSelf)
                {
                    continue;
                }

                UpdateScreenSpaceElement(button.rectTransform, button.target.position + button.worldOffset);
            }

            foreach (var label in guideWorldLabels)
            {
                if (label == null || label.rectTransform == null || label.target == null || !label.rectTransform.gameObject.activeSelf)
                {
                    continue;
                }

                UpdateScreenSpaceElement(label.rectTransform, label.target.position + label.worldOffset);
            }
        }

        /// <summary>
        /// 把世界坐标投影到屏幕空间 界面。
        /// </summary>
        /// <param name="rectTransform">参数值。</param>
        /// <param name="worldPosition">坐标。</param>
        private void UpdateScreenSpaceElement(RectTransform rectTransform, Vector3 worldPosition)
        {
            if (rectTransform == null || SceneCamera == null)
            {
                return;
            }

            var screenPosition = SceneCamera.WorldToScreenPoint(worldPosition);
            var isVisible = screenPosition.z > 0f;
            if (rectTransform.gameObject.activeSelf != isVisible)
            {
                rectTransform.gameObject.SetActive(isVisible);
            }

            if (!isVisible)
            {
                return;
            }

            rectTransform.position = screenPosition;
            rectTransform.rotation = Quaternion.identity;
            rectTransform.localScale = ResolveScreenElementScale(rectTransform.transform);
        }

        /// <summary>
        /// 根据当前跟随界面类型返回应该使用的屏幕缩放。
        /// </summary>
        /// <param name="elementTransform">界面节点。</param>
        /// <returns>最终缩放。</returns>
        private Vector3 ResolveScreenElementScale(Transform elementTransform)
        {
            if (elementTransform == null)
            {
                return Vector3.one;
            }

            for (var index = 0; index < guideWorldLabels.Count; index++)
            {
                var label = guideWorldLabels[index];
                if (label?.rectTransform == elementTransform)
                {
                    return label.scale;
                }
            }

            for (var index = 0; index < guideWorldButtons.Count; index++)
            {
                var button = guideWorldButtons[index];
                if (button?.rectTransform == elementTransform)
                {
                    return button.scale;
                }
            }

            return Vector3.one;
        }

        /// <summary>
        /// 清理真实员工出现后残留的引导员工表现。
        /// </summary>
        private void CleanupGuideStaffVisuals()
        {
            if (guideCounterObject != null && HasVisibleRuntimeStaffNearAnchor(GuideShopkeeperVisualKey, guideCounterObject.transform))
            {
                DestroyGuideStaffVisuals(GuideShopkeeperVisualKey);
                DestroyOrphanGuideVisual($"{GuideShopkeeperVisualKey}_GuideVisual");
            }

            if (guideStoveObject != null && HasVisibleRuntimeStaffNearAnchor("Chef", guideStoveObject.transform))
            {
                DestroyGuideStaffVisuals(GuideChefVisualKey);
                DestroyOrphanGuideVisual($"{GuideChefVisualKey}_GuideVisual");
            }

            if (customerEntryPoint != null && HasVisibleRuntimeStaffNearAnchor(GuideWaiterVisualKey, customerEntryPoint))
            {
                DestroyGuideStaffVisuals(GuideWaiterVisualKey);
                DestroyOrphanGuideVisual($"{GuideWaiterVisualKey}_GuideVisual");
            }
        }

        /// <summary>
        /// 隐藏场景里预摆放的员工模型，避免招聘前就出现人物。
        /// </summary>
        private static void HidePreRecruitSceneStaffModels()
        {
            SetPreplacedStaffModelVisible(GuideChefMarkerName, false);
            SetPreplacedStaffModelVisible(GuideShopkeeperMarkerName, false);
            SetPreplacedStaffModelVisible(GuideWaiterMarkerName, false);
            SetPreplacedStaffModelVisible("Chef3", false);
            SetPreplacedStaffModelVisible("WaiterF1", false);
            SetPreplacedStaffModelVisible("WaiterF1_1", false);
        }

        /// <summary>
        /// 按根节点名称设置预摆放员工的显隐。
        /// </summary>
        /// <param name="objectName">场景里预摆放员工的节点名。</param>
        /// <param name="visible">是否显示。</param>
        private static void SetPreplacedStaffModelVisible(string objectName, bool visible)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return;
            }

            var transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var current in transforms)
            {
                if (current == null || current.name != objectName)
                {
                    continue;
                }

                current.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// 销毁未被字典追踪的孤立引导表现。
        /// </summary>
        /// <param name="objectName">名称。</param>
        private static void DestroyOrphanGuideVisual(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return;
            }

            var orphan = GameObject.Find(objectName);
            if (orphan == null)
            {
                return;
            }

            Destroy(orphan);
        }

        #endregion
    }
}
