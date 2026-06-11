using System;
using System.Collections;
using System.Collections.Generic;
using JN.Client.Manager;
using JN.Client.Model;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
namespace JN.Client.Scene
{
    /// <summary>
    /// 负责酒楼场景相关的运行时逻辑。
    /// </summary>
    public partial class TavernSceneManager
    {
        #region Guide Constants And State
        private const string GuideCounterCarrierPrefabPath = "Assets/Res/Resources/Equipment/Carriers/P_Equipment_CounterCarrier.prefab";
        private const string GuideStoveCarrierPrefabPath = "Assets/Res/Resources/Equipment/Carriers/P_Equipment_GuideStoveCarrier.prefab";
        private const string GuideFurnaceCarrierPrefabPath = "Assets/Res/Resources/Equipment/Carriers/P_Equipment_GuideFurnaceCarrier.prefab";
        private const string GuideWineCabinetCarrierPrefabPath = "Assets/Res/Resources/Equipment/Carriers/P_Equipment_GuideWineCabinetCarrier.prefab";
        private const string GuideCabinetCarrierPrefabPath = "Assets/Res/Resources/Equipment/Carriers/P_Equipment_GuideCabinetCarrier.prefab";
        private const string GuideKitchenTable1CarrierPrefabPath = "Assets/Res/Resources/Equipment/Carriers/P_Equipment_GuideKitchenTable1Carrier.prefab";
        private const string GuideKitchenTable2CarrierPrefabPath = "Assets/Res/Resources/Equipment/Carriers/P_Equipment_GuideKitchenTable2Carrier.prefab";
        private const string GuideBuildingSuccessEffectPrefabPath = "Assets/Res/Resources/Effect/UIEffect_BuildingSuccess.prefab";
        private const string GuideCounterButtonPrefabResourcePath = "UI/Buttons/BuyCounterButton";
        private const string GuideStoveButtonPrefabResourcePath = "UI/Buttons/BuyStoveButton";
        private const string GuideWorldButtonPrefabResourcePath = "UI/Guides/GuideWorldButton";
        private const string GuideWorldLabelPrefabResourcePath = "UI/Guides/GuideWorldLabel";
        private const string CustomerEnterProgressPrefabResourcePath = "UI/Runtime/CustomerEnterProgress";
        private const string GuideShopkeeperVisualKey = "Shopkeeper";
        private const string GuideChefVisualKey = "Chef";
        private const string GuideWaiterVisualKey = "Waiter";
        private const string GuideShopkeeperMarkerName = "P_Character_WaiterF01_Shopkeeper";
        private const string GuideChefMarkerName = "P_Character_Chef03_Chef";
        private const string GuideWaiterMarkerName = "P_Character_Waiter03_Waiter";
        private const string GuideRecruitChefSpritePath = "Assets/Res/Resources/Textures/UI/Panel/TavernScene/img_RecruitChef_Btn.png";
        private const string GuideRecruitShopkeeperSpritePath = "Assets/Res/Resources/Textures/UI/Panel/TavernScene/img_RecruitTavernKeeper_Btn.png";
        private const string GuideRecruitWaiterSpritePath = "Assets/Res/Resources/Textures/UI/Panel/TavernScene/img_RecruitWaiter_Btn.png";
        private static readonly Dictionary<string, GameObject> GuideCarrierPrefabCache = new();
        private static readonly Dictionary<string, Sprite> GuideButtonSpriteCache = new();
        private static GameObject guideBuildingSuccessEffectPrefab;
        private bool guideCounterDeliveryPending;
        private bool guideStoveDeliveryPending;
        #endregion

        #region Scene Cache

        /// <summary>
        /// 把存档里的桌位状态恢复到当前场景。
        /// </summary>
        private void ApplySavedTableStates()
        {
            foreach (var tableEntry in AllTables)
            {
                tableEntry.Value.ApplySaveState(DataManager.Instance.GetTableData(tableEntry.Key));
            }
        }

        /// <summary>
        /// 缓存场景或配置里的顾客模板。
        /// </summary>
        private void CacheCustomerTemplates()
        {
            customerTemplates.Clear();
            if (customerEntryPoint == null)
            {
                CacheCustomerPrefabsFromReferences();
                return;
            }

            foreach (Transform child in customerEntryPoint)
            {
                foreach (Transform grandChild in child)
                {
                    if (!IsCustomerTemplate(grandChild))
                    {
                        continue;
                    }

                    grandChild.gameObject.SetActive(false);
                    customerTemplates.Add(grandChild.gameObject);
                }
            }

            if (customerTemplates.Count == 0)
            {
                CacheCustomerPrefabsFromReferences();
            }
        }

        /// <summary>
        /// 判断节点是否可作为顾客模板使用。
        /// </summary>
        /// <param name="candidate">数据编号。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private static bool IsCustomerTemplate(Transform candidate)
        {
            return candidate != null && candidate.name.Contains("Customer");
        }

        /// <summary>
        /// 缓存桌面菜品表现 预制体。
        /// </summary>
        private void CacheDishPrefabs()
        {
            dishPrefabs.Clear();
            platePrefab = LoadDishPrefab("Assets/Res/Resources/Models/Objects/plate/plate_P.prefab");

            var productPrefab1 = LoadDishPrefab("Assets/Res/Resources/Models/Objects/vegetable/vegetable01_P.prefab");
            var productPrefab2 = LoadDishPrefab("Assets/Res/Resources/Models/Objects/vegetable/vegetable02_P.prefab");
            var productPrefab3 = LoadDishPrefab("Assets/Res/Resources/Models/Objects/vegetable/vegetable03_P.prefab");
            var productPrefab4 = LoadDishPrefab("Assets/Res/Resources/Models/Objects/vegetable/vegetable04_P.prefab");

            if (productPrefab1 != null) dishPrefabs.Add(productPrefab1);
            if (productPrefab2 != null) dishPrefabs.Add(productPrefab2);
            if (productPrefab3 != null) dishPrefabs.Add(productPrefab3);
            if (productPrefab4 != null) dishPrefabs.Add(productPrefab4);
        }

        /// <summary>
        /// 查找顾客入口、出口和物件搬运起点。
        /// </summary>
        private void ResolveSceneAnchors()
        {
            customerEntryPoint = FindSceneTransformByName("PeopleStartPoint");
            customerExitPoint = FindSceneTransformByName("Door") ?? customerEntryPoint;
            objectMovePoint = FindSceneTransformByName("ObjectMovePoint")
                             ?? FindSceneTransformByName("PeopleStartPoint")
                             ?? FindSceneTransformByName("TableMoveCheckPoint");
            sceneObjectsRoot = FindSceneTransformByName("Objects");
        }

        /// <summary>
        /// 查找新手引导阶段的柜台、灶台和厨房物件。
        /// </summary>
        private void ResolveGuideSceneObjects()
        {
            HideGuideSceneCarrier("P_Equipment_CounterCarrier");
            HideGuideSceneCarrier("P_Equipment_StoveCarrier");

            guideCounterObject = FindGuideSceneObject("P_Equipment_Counter") ?? FindGuideTargetObject("P_Equipment_Counter") ?? FindGuideTargetObject("Counter");
            guideCounterBuildBase = FindGuideSceneObject("柜台建造") ?? FindGuideTargetObject("柜台建造");
            foodTableObject = FindGuideSceneObject("FoodTable") ?? FindGuideTargetObject("FoodTable");

            guideStoveSceneObjects.Clear();
            guideKitchenAnchors.Clear();
            AddGuideKitchenAnchor("stove", "灶台", "BigStove", "灶台建造", GuideStoveCarrierPrefabPath);
            AddGuideKitchenAnchor("furnace", "炉子", "SmallStove", "炉子建造", GuideFurnaceCarrierPrefabPath);
            AddGuideKitchenAnchor("wine_cabinet", "酒柜", "酒柜", "酒柜建造", GuideWineCabinetCarrierPrefabPath);
            AddGuideKitchenAnchor("cabinet", "柜子", "柜子", "柜子建造", GuideCabinetCarrierPrefabPath);
            AddGuideKitchenAnchor("kitchen_table_1", "厨房桌子1", "厨房桌子1", "厨房桌子1建造", GuideKitchenTable1CarrierPrefabPath);
            AddGuideKitchenAnchor("kitchen_table_2", "厨房桌子2", "厨房桌子2", "厨房桌子2建造", GuideKitchenTable2CarrierPrefabPath);
            AddGuideSceneObject(guideStoveSceneObjects, "BigStove");
            guideSteamerObject = FindGuideSceneObject("Steamer_1") ?? FindGuideSceneObject("Steamer") ?? FindGuideTargetObject("Steamer_1") ?? FindGuideTargetObject("Steamer");

            guideStoveObject = guideStoveSceneObjects.Count > 0
                ? guideStoveSceneObjects[0]
                : FindGuideTargetObject("BigStove")
                  ?? FindGuideTargetObject("P_Equipment_Stove")
                  ?? FindGuideTargetObject("Stove01_P")
                  ?? FindGuideTargetObject("SmallStove")
                  ?? FindGuideTargetObject("Wok")
                  ?? FindGuideTargetObject("Steamer");
            guideStoveBuildBase = guideKitchenAnchors.Count > 0 ? guideKitchenAnchors[0].buildBase : FindGuideSceneObject("灶台建造") ?? FindGuideTargetObject("灶台建造");
        }

        #endregion

        #region Business And Guide State

        /// <summary>
        /// 响应酒楼营业状态变化并启动或停止顾客流程。
        /// </summary>
        /// <param name="is打开">参数值。</param>
        private void HandleBusinessStateChanged(bool isOpen)
        {
            if (isOpen)
            {
                if (!hasNavMesh)
                {
                    hasNavMesh = TryGetNavMeshPosition(customerEntryPoint != null ? customerEntryPoint.position : Vector3.zero, out _);
                }

                if (!hasNavMesh)
                {
                    Debug.LogWarning("[TavernSceneManager] 当前场景没有可用的 NavMesh，已跳过顾客生成。");
                    return;
                }

                StartBusinessLoop();
            }
            else
            {
                StopBusinessLoop();
            }
        }

        /// <summary>
        /// 刷新引导物件、员工展示和世界按钮显隐。
        /// </summary>
        private void RefreshGuideWorldState()
        {
            var guide = DataManager.Instance.GameplayGuideData;
            var chefCount = DataManager.Instance.GetHiredGuideChefCount();
            var waiterCount = DataManager.Instance.GetHiredGuideWaiterCount();
            EnsureGuideWorldButtons();

            if (guideCounterObject != null)
            {
                guideCounterObject.SetActive(guide.purchasedCounter && !guideCounterDeliveryPending);
            }

            if (guideCounterBuildBase != null)
            {
                guideCounterBuildBase.SetActive(!guide.purchasedCounter && !guideCounterDeliveryPending);
            }

            foreach (var kitchenAnchor in guideKitchenAnchors)
            {
                var isPending = guidePendingKitchenItems.Contains(kitchenAnchor.itemKey);
                var isPurchased = DataManager.Instance.IsGuideKitchenItemPurchased(kitchenAnchor.itemKey);
                var showBuildBase = ShouldShowGuideKitchenButton(kitchenAnchor.itemKey);
                if (kitchenAnchor.sceneObject != null)
                {
                    kitchenAnchor.sceneObject.SetActive(isPurchased && !isPending);
                }

                if (kitchenAnchor.buildBase != null)
                {
                    kitchenAnchor.buildBase.SetActive(showBuildBase && !isPending);
                }
            }

            if (foodTableObject != null)
            {
                var showFoodTable = DataManager.Instance.IsGuideKitchenItemPurchased("stove")
                                    && !guidePendingKitchenItems.Contains("stove");
                foodTableObject.SetActive(showFoodTable);
                if (!showFoodTable)
                {
                    ClearPreparedDishQueue();
                }
            }

            if (guideSteamerObject != null)
            {
                var furnaceReady = DataManager.Instance.IsGuideKitchenItemPurchased("furnace")
                                   && !guidePendingKitchenItems.Contains("furnace");
                guideSteamerObject.SetActive(furnaceReady);
            }

            RefreshKitchenTableLinkedProps("kitchen_table_1", "Crate1_1", "Crate1_2", "CrateOrange");
            RefreshKitchenTableLinkedProps("kitchen_table_2", "pumpkin_P_1", "Crate1");

            HidePreRecruitSceneStaffModels();
            RefreshGuideStaffVisualAtSceneMarker(GuideShopkeeperVisualKey, StaffRole.Waiter, guide.hiredShopkeeper, GuideShopkeeperMarkerName, "WaiterF1", guideCounterObject, new Vector3(0.06f, -0.27f, -0.4f), 1, 180f);
            EnsureGuideStaffVisualCount(GuideChefVisualKey, StaffRole.Chef, chefCount, 4);
            EnsureGuideStaffVisualCount(GuideWaiterVisualKey, StaffRole.Waiter, waiterCount, 5);
            RefreshGuideStaffVisualAtSceneMarker(GuideChefVisualKey, StaffRole.Chef, chefCount > 0, GuideChefMarkerName, "Chef3", guideStoveObject, new Vector3(0.7f, 0f, 0.6f), 4);
            RefreshGuideStaffVisualAtSceneMarker(GuideWaiterVisualKey, StaffRole.Waiter, waiterCount > 0, GuideWaiterMarkerName, "WaiterF1_1", guideCounterObject, new Vector3(6f, -0.27f, 2.37f), 5, 97.5f);
            LayoutAdditionalGuideStaffVisuals(GuideChefVisualKey, GuideChefMarkerName, "Chef3", guideStoveObject != null ? guideStoveObject.transform : null, new Vector3(0.7f, 0f, 0.6f), 0f);
            LayoutAdditionalGuideStaffVisuals(GuideWaiterVisualKey, GuideWaiterMarkerName, "WaiterF1_1", guideCounterObject != null ? guideCounterObject.transform : null, new Vector3(6f, -0.27f, 2.37f), 97.5f);
            RefreshGuideWorldButtons(guide);

            if (guideCounterButton != null && guideCounterButton.rectTransform != null && guideCounterButton.rectTransform.gameObject.activeSelf)
            {
                SetGuideButtonText(guideCounterButton, $"{GetGuideEquipmentCost(0)}");
            }

            foreach (var kitchenAnchor in guideKitchenAnchors)
            {
                if (kitchenAnchor.button != null && kitchenAnchor.button.rectTransform != null && kitchenAnchor.button.rectTransform.gameObject.activeSelf)
                {
                    SetGuideButtonText(kitchenAnchor.button, $"{GetGuideEquipmentCost(3)}");
                }
            }
        }

        /// <summary>
        /// 按当前员工数量重新排布额外招聘出来的厨师和小二。
        /// </summary>
        /// <param name="visualKey">员工表现键。</param>
        /// <param name="markerName">主标记点名称。</param>
        /// <param name="legacyMarkerName">兼容旧标记点名称。</param>
        /// <param name="fallbackAnchor">备用锚点。</param>
        /// <param name="fallbackOffset">备用偏移。</param>
        /// <param name="fallbackYawDegrees">备用额外朝向。</param>
        private void LayoutAdditionalGuideStaffVisuals(string visualKey, string markerName, string legacyMarkerName, Transform fallbackAnchor, Vector3 fallbackOffset, float fallbackYawDegrees)
        {
            var visuals = GetGuideStaffVisuals(visualKey);
            if (visuals == null || visuals.Length <= 1)
            {
                return;
            }

            var marker = FindSceneTransformByName(markerName) ?? FindSceneTransformByName(legacyMarkerName);
            for (var index = 1; index < visuals.Length; index++)
            {
                var visual = visuals[index];
                if (visual == null)
                {
                    continue;
                }

                // 正在播放入场动画的员工不要被位置同步覆盖，否则会闪回到锚点。
                if (staffVisualsBeingAnimated.Contains(visual))
                {
                    continue;
                }

                if (marker != null)
                {
                    visual.transform.position = ResolveGuideStaffMarkerPosition(visualKey, marker, index);
                    visual.transform.rotation = marker.rotation;
                    visual.transform.localScale = ResolveGuideStaffVisualScale(visualKey, marker.lossyScale);
                    continue;
                }

                if (fallbackAnchor != null)
                {
                    var stackOffset = GetGuideStaffStackOffset(visualKey, index);
                    UpdateGuideStaffTransform(visual.transform, fallbackAnchor, fallbackOffset + stackOffset, fallbackYawDegrees);
                }
            }
        }

        /// <summary>
        /// 按厨房桌子的购买状态刷新附属摆件显隐。
        /// </summary>
        /// <param name="itemKey">厨房桌子键值。</param>
        /// <param name="sceneObjectNames">需要一起显隐的场景物件名称。</param>
        private void RefreshKitchenTableLinkedProps(string itemKey, params string[] sceneObjectNames)
        {
            if (sceneObjectNames == null || sceneObjectNames.Length == 0)
            {
                return;
            }

            var isVisible = DataManager.Instance.IsGuideKitchenItemPurchased(itemKey)
                            && !guidePendingKitchenItems.Contains(itemKey);
            for (var index = 0; index < sceneObjectNames.Length; index++)
            {
                var target = FindGuideSceneObject(sceneObjectNames[index]) ?? FindGuideTargetObject(sceneObjectNames[index]);
                if (target != null)
                {
                    target.SetActive(isVisible);
                }
            }
        }

        #endregion
    }
}
