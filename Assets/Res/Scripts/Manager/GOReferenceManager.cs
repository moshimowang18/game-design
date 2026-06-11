using UnityCommunity.UnitySingleton;
using UnityEngine;

namespace JN.Client.Manager
{
    /// <summary>
    /// 负责对象引用相关的运行时逻辑。
    /// </summary>
    public class GOReferenceManager : MonoSingleton<GOReferenceManager>
    {
        private Transform coinTransform;

        private GameObject TownBuilding;

        /// <summary>
        /// 初始化模块依赖和默认状态。
        /// </summary>
        public void Init()
        {
        }

        /// <summary>
        /// 保存铜钱节点。
        /// </summary>
        /// <param name="coin">参数值。</param>
        public void SaveCoinTransform(Transform coin)
        {
            coinTransform = coin;
        }

        /// <summary>
        /// 获取铜钱节点。
        /// </summary>
        /// <returns>返回匹配到的对象引用。</returns>
        public Transform GetCoinTransform()
        {
            return coinTransform;
        }

        /// <summary>
        /// 保存大地图建筑。
        /// </summary>
        /// <param name="obj">参数值。</param>
        public void SaveTownBuilding(GameObject obj)
        {
            TownBuilding = obj;
            if (TownBuilding == null)
            {
                Debug.LogWarning("GOReferenceManager.SaveTownBuilding received null. Town building cache is not available yet.");
                return;
            }

            // 城镇里这栋建筑默认先隐藏，等业务逻辑决定显示时再启用。
            TownBuilding.SetActive(false);
        }

        /// <summary>
        /// 获取大地图建筑。
        /// </summary>
        /// <returns>返回匹配到的对象引用。</returns>
        public GameObject GetTownBuilding()
        {
            return TownBuilding;
        }
    }
}
