using JN.Client.Manager;
using JN.Client.Model;
using JN.Client.UI;
using QFramework;
using System.Collections;
using UnityEngine;

namespace JN.Client.Scene
{
    public partial class TavernSceneManager
    {
        #region Guide Actions

        /// <summary>
        /// 处理购买柜台并播放搬运表现。
        /// </summary>
        private void HandleBuyCounter()
        {
            if (!DataManager.Instance.TryPurchaseGuideCounter(out _))
            {
                return;
            }

            GameAudioManager.PlayConstruction();

            guideCounterDeliveryPending = true;
            if (!TryPlayGuideDeliveryEffect(
                    guideCounterObject != null ? guideCounterObject.transform : null,
                    ResolveGuideCarrier(GuideCounterCarrierPrefabPath, "P_Equipment_CounterCarrier"),
                    () =>
                    {
                        guideCounterDeliveryPending = false;
                        RefreshGuideWorldState();
                    }))
            {
                guideCounterDeliveryPending = false;
            }

            RefreshGuideWorldState();
            Signals.Get<GameplayGuideProgressSignal>().Dispatch();
        }

        /// <summary>
        /// 处理购买灶台并播放搬运表现。
        /// </summary>
        private void HandleBuyStove()
        {
            if (!DataManager.Instance.TryPurchaseGuideStove(out _))
            {
                return;
            }

            GameAudioManager.PlayConstruction();

            guideStoveDeliveryPending = true;
            if (!TryPlayGuideDeliveryEffect(
                    guideStoveObject != null ? guideStoveObject.transform : null,
                    LoadGuideCarrierPrefab(GuideStoveCarrierPrefabPath),
                    () =>
                    {
                        guideStoveDeliveryPending = false;
                        RefreshGuideWorldState();
                    }))
            {
                guideStoveDeliveryPending = false;
            }

            RefreshGuideWorldState();
            Signals.Get<GameplayGuideProgressSignal>().Dispatch();
        }

        private void HandleBuyKitchenItem(string itemKey)
        {
            if (!DataManager.Instance.TryPurchaseGuideKitchenItem(itemKey, out _))
            {
                return;
            }

            GameAudioManager.PlayConstruction();

            var anchor = guideKitchenAnchors.Find(current => current != null && current.itemKey == itemKey);
            if (anchor != null && anchor.sceneObject != null)
            {
                guidePendingKitchenItems.Add(itemKey);
                if (!TryPlayGuideDeliveryEffect(
                        anchor.buildBase != null ? anchor.buildBase.transform : anchor.sceneObject.transform,
                        LoadGuideCarrierPrefab(anchor.carrierPrefabPath),
                        () =>
                        {
                            guidePendingKitchenItems.Remove(itemKey);
                            RefreshGuideWorldState();
                        }))
                {
                    guidePendingKitchenItems.Remove(itemKey);
                }
            }

            RefreshGuideWorldState();
            Signals.Get<GameplayGuideProgressSignal>().Dispatch();
        }

        /// <summary>
        /// 处理招聘掌柜。
        /// </summary>
        private void HandleHireShopkeeper()
        {
            OpenRecruitPanel("掌柜", "掌柜", 1, StaffRole.Waiter, ConfirmHireShopkeeper);
        }

        /// <summary>
        /// 处理招聘厨师。
        /// </summary>
        private void HandleHireChef()
        {
            OpenRecruitPanel("厨师", "厨师", 4, StaffRole.Chef, ConfirmHireChef);
        }

        /// <summary>
        /// 处理招聘小二。
        /// </summary>
        private void HandleHireWaiter()
        {
            OpenRecruitPanel("小二", "小二", 5, StaffRole.Waiter, ConfirmHireWaiter);
        }

        /// <summary>
        /// 打开招聘人才确认界面。
        /// </summary>
        /// <param name="displayName">展示名称。</param>
        /// <param name="roleText">人员类型。</param>
        /// <param name="staffId">员工编号。</param>
        /// <param name="role">员工角色。</param>
        /// <param name="onConfirm">确认招聘回调。</param>
        private void OpenRecruitPanel(string displayName, string roleText, int staffId, StaffRole role, System.Action onConfirm)
        {
            var staff = DataManager.Instance.GetGuideStaffConfig(staffId, role);
            var cost = DataManager.Instance.GetGuideStaffHireCost(staffId, role);
            TavernRuntimeModalUI.ShowRecruitPanel(staff != null ? staff.displayName : displayName, roleText, staff != null ? staff.icon : null, cost, onConfirm);
        }

        /// <summary>
        /// 确认招聘掌柜。
        /// </summary>
        private void ConfirmHireShopkeeper()
        {
            if (!DataManager.Instance.TryHireGuideShopkeeper(out var message))
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    TavernRuntimeModalUI.ShowFloatingWarning(message);
                }

                return;
            }

            RefreshGuideWorldButtons(DataManager.Instance.GameplayGuideData);
            StartCoroutine(GuideStaffEnterRoutine(GuideShopkeeperVisualKey, GuideShopkeeperMarkerName, "WaiterF1", StaffRole.Waiter, 1));
            Signals.Get<GameplayGuideProgressSignal>().Dispatch();
        }

        /// <summary>
        /// 确认招聘厨师。
        /// </summary>
        private void ConfirmHireChef()
        {
            if (!DataManager.Instance.TryHireGuideChef(out var message))
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    TavernRuntimeModalUI.ShowFloatingWarning(message);
                }

                return;
            }

            RefreshGuideWorldButtons(DataManager.Instance.GameplayGuideData);
            StartCoroutine(GuideStaffEnterRoutine(GuideChefVisualKey, GuideChefMarkerName, "Chef3", StaffRole.Chef, 4));
            Signals.Get<GameplayGuideProgressSignal>().Dispatch();
        }

        /// <summary>
        /// 确认招聘小二。
        /// </summary>
        private void ConfirmHireWaiter()
        {
            if (!DataManager.Instance.TryHireGuideWaiter(out var message))
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    TavernRuntimeModalUI.ShowFloatingWarning(message);
                }

                return;
            }

            RefreshGuideWorldButtons(DataManager.Instance.GameplayGuideData);
            StartCoroutine(GuideStaffEnterRoutine(GuideWaiterVisualKey, GuideWaiterMarkerName, "WaiterF1_1", StaffRole.Waiter, 5));
            Signals.Get<GameplayGuideProgressSignal>().Dispatch();
        }

        /// <summary>
        /// 从底部员工按钮招聘首个厨师后，播放厨师从门口入场到站位的表现。
        /// </summary>
        public void PlayGuideChefEnterFromBottomRecruit()
        {
            RefreshGuideWorldButtons(DataManager.Instance.GameplayGuideData);
            StartCoroutine(GuideStaffEnterRoutine(GuideChefVisualKey, GuideChefMarkerName, "Chef3", StaffRole.Chef, 4, true));
        }

        /// <summary>
        /// 从底部员工按钮招聘首个小二后，播放小二从门口入场到站位的表现。
        /// </summary>
        public void PlayGuideWaiterEnterFromBottomRecruit()
        {
            RefreshGuideWorldButtons(DataManager.Instance.GameplayGuideData);
            StartCoroutine(GuideStaffEnterRoutine(GuideWaiterVisualKey, GuideWaiterMarkerName, "WaiterF1_1", StaffRole.Waiter, 5, true));
        }

        /// <summary>
        /// 招聘完成后让人才从门口走到站位。
        /// </summary>
        /// <param name="visualKey">员工表现键。</param>
        /// <param name="markerName">目标站位名称。</param>
        /// <param name="legacyMarkerName">兼容旧站位名称。</param>
        /// <param name="role">员工角色。</param>
        /// <param name="preferredStaffId">员工配置编号。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator GuideStaffEnterRoutine(string visualKey, string markerName, string legacyMarkerName, StaffRole role, int preferredStaffId, bool forceCreateNew = false)
        {
            var marker = FindSceneTransformByName(markerName) ?? FindSceneTransformByName(legacyMarkerName);
            if (marker == null)
            {
                yield break;
            }

            // 数据先变、信号先广播：调用本协程时 EnsureGuideStaffVisualCount 已经新建好了对应的员工表现。
            // 因此 forceCreateNew=true 不再额外 Instantiate，而是接管最新一位 visual 让它从门口走进来，
            // 否则会出现 N+1 个表现，多余的那一位会被下一次 RefreshGuideWorldState 销毁，
            // 表现上就是“最新招聘的那位”停在门口/锚点不动。
            GameObject visual;
            var existingVisuals = GetGuideStaffVisuals(visualKey);
            var hasGroupedVisuals = existingVisuals != null && existingVisuals.Length > 0;
            // 厨师/小二支持多人：无论入口来自顶部确认还是底部招募，都应优先拿“最后一个”
            // （即本次新增的 visual）做入场动画，避免错误地复用第一个员工导致站位/动画错乱。
            var preferLatestVisual = visualKey == GuideChefVisualKey || visualKey == GuideWaiterVisualKey;
            if ((forceCreateNew || preferLatestVisual) && hasGroupedVisuals)
            {
                visual = existingVisuals[existingVisuals.Length - 1];
            }
            else if (forceCreateNew)
            {
                visual = CreateAdditionalGuideStaffVisual(visualKey, role, preferredStaffId);
            }
            else
            {
                visual = GetOrCreateGuideStaffVisual(visualKey, role, preferredStaffId);
            }

            if (visual == null)
            {
                yield break;
            }

            var visuals = GetGuideStaffVisuals(visualKey);
            var visualIndex = System.Array.IndexOf(visuals, visual);
            var targetPosition = ResolveGuideStaffMarkerPosition(visualKey, marker, Mathf.Max(0, visualIndex));
            var start = customerEntryPoint != null ? customerEntryPoint.position : marker.position;

            // 先打上“正在入场”标记，再做位置/缩放重置，避免和同帧内的 RefreshGuideWorldState 争夺位置。
            staffVisualsBeingAnimated.Add(visual);
            visual.SetActive(false);
            visual.transform.position = start;
            visual.transform.rotation = marker.rotation;
            visual.transform.localScale = ResolveGuideStaffVisualScale(visualKey, marker.lossyScale);
            visual.SetActive(true);

            try
            {
                yield return MoveCharacterAlongNavMesh(visual.transform, targetPosition, 1.15f, true);
            }
            finally
            {
                staffVisualsBeingAnimated.Remove(visual);
            }

            visual.transform.rotation = marker.rotation;
            RefreshGuideWorldState();
        }

        #endregion
    }
}
