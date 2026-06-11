using System;
using JN.Client.Manager;
using JN.Client.Scene;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace JN.Client.UI
{
    public class NewBuildingWindowControllerData : UIPanelData
    {
        /// <summary>
        /// 保存目标地块编号。
        /// </summary>
        public int tileId;

        /// <summary>
        /// 保存确认操作回调。
        /// </summary>
        public Action confirmAction;
    }

    /// <summary>
    /// 负责新建建筑窗口逻辑。
    /// </summary>
    public class NewBuildingWindowController : QFrameworkPanel<NewBuildingWindowControllerData>
    {
        private int SelfPlayerId => ResolveSelfPlayerId();

        [SerializeField] private TextMeshProUGUI txt_Title;
        [SerializeField] private Button btn_Close;
        [SerializeField] private Button btn_SelectBuilding_1;
        [SerializeField] private Button btn_SelectBuilding_2;
        [SerializeField] private Button btn_SelectBuilding_3;

        /// <summary>
        /// 面板初始化时绑定控件和事件。
        /// </summary>
        protected override void OnPanelInit()
        {
            btn_Close.onClick.AddListener(OnClickClose);
            btn_SelectBuilding_1.onClick.AddListener(OnClickSelectBuilding1);
            btn_SelectBuilding_2.onClick.AddListener(OnClickSelectBuilding2);
            btn_SelectBuilding_3.onClick.AddListener(OnClickSelectBuilding3);
        }

        /// <summary>
        /// 面板打开时读取数据并刷新显示。
        /// </summary>
        /// <param name="data">数据。</param>
        protected override void OnPanelOpen(NewBuildingWindowControllerData data)
        {
            RefreshView();
        }

        /// <summary>
        /// 面板关闭时清理临时状态和监听。
        /// </summary>
        protected override void OnPanelClose()
        {
            btn_Close.onClick.RemoveListener(OnClickClose);
            btn_SelectBuilding_1.onClick.RemoveListener(OnClickSelectBuilding1);
            btn_SelectBuilding_2.onClick.RemoveListener(OnClickSelectBuilding2);
            btn_SelectBuilding_3.onClick.RemoveListener(OnClickSelectBuilding3);
        }

        /// <summary>
        /// 刷新窗口显示。
        /// </summary>
        private void RefreshView()
        {
            var buildingInfo = DataManager.Instance.GetTownBuildingInfos().Find(info => info.tileId == Data.tileId);
            var canBuildOnThisLand = buildingInfo != null
                                     && buildingInfo.playerId == SelfPlayerId
                                     && buildingInfo.buildingLevel <= 0
                                     && buildingInfo.status == 0;

            btn_SelectBuilding_1.interactable = canBuildOnThisLand;
            btn_SelectBuilding_2.interactable = canBuildOnThisLand;
            btn_SelectBuilding_3.interactable = canBuildOnThisLand;

            if (!canBuildOnThisLand)
            {
                txt_Title.text = "请先购买自己的地块";
                return;
            }

            if (Data.tileId != 0)
            {
               // txt_Title.text = $"地块：{Data.tileId} 选择新建建筑";
               txt_Title.text = $"我要经营";
            }
        }

        /// <summary>
        /// 处理关闭点击事件。
        /// </summary>
        private void OnClickClose()
        {
            CloseSelf();
        }

        /// <summary>
        /// 解析当前玩家编号。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        private int ResolveSelfPlayerId()
        {
            return DataManager.Instance != null ? DataManager.Instance.GetLocalPlayerNumericId() : 0;
        }

        /// <summary>
        /// 处理选择 1 级建筑按钮点击。
        /// </summary>
        private void OnClickSelectBuilding1()
        {
            ConfirmSelection(-3000, 1);
        }

        /// <summary>
        /// 处理选择 2 级建筑按钮点击。
        /// </summary>
        private void OnClickSelectBuilding2()
        {
            ConfirmSelection(-4000, 2);
        }

        /// <summary>
        /// 处理选择 3 级建筑按钮点击。
        /// </summary>
        private void OnClickSelectBuilding3()
        {
            ConfirmSelection(-5000, 3);
        }

        /// <summary>
        /// 处理确认选择建筑。
        /// </summary>
        /// <param name="coinChange">参数值。</param>
        /// <param name="buildingLevel">等级。</param>
        private void ConfirmSelection(int coinChange, int buildingLevel)
        {
            if (!DataManager.Instance.TryStartTownBuilding(Data.tileId, buildingLevel, coinChange, 3, out var message))
            {
                txt_Title.text = message;
                return;
            }

            GameAudioManager.PlayConstruction();
            Data.confirmAction?.Invoke();
            var buildingInfo = DataManager.Instance.GetTownBuildingInfos().Find(info => info.tileId == Data.tileId);
            TileManager.Instance.UpdateTile(Data.tileId, buildingInfo);
            TileManager.Instance.RefreshAllTileViews();
            CloseSelf();
        }
    }
}
