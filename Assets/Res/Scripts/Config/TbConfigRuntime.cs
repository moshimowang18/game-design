using cfg;
using SimpleJSON;
using UnityEngine;

namespace JN.Client.Config
{
    /// <summary>
    /// 负责在运行时加载 TbConfig 表并提供统一读取入口。
    /// </summary>
    public static class TbConfigRuntime
    {
        private const string ConfigResourcePath = "Config/tbconfig";
        private static Tables cachedTables;
        private static bool loadAttempted;

        /// <summary>
        /// 顾客刷新时间配置名。
        /// </summary>
        public const string CustomerRefreshTimeKey = "customerRefreshTime";

        /// <summary>
        /// 厨师做菜时间配置名。
        /// </summary>
        public const string ChefCookTimeKey = "chefCookTime";

        /// <summary>
        /// 顾客用餐时间配置名。
        /// </summary>
        public const string CustomerEatTimeKey = "customerEatTime";

        /// <summary>
        /// 桌子清理时间配置名。
        /// </summary>
        public const string TableCleanTimeKey = "tableCleanTime";

        /// <summary>
        /// 读取整型配置值，不存在时返回兜底值。
        /// </summary>
        /// <param name="configName">配置名称。</param>
        /// <param name="defaultValue">兜底数值。</param>
        /// <returns>配置值或兜底值。</returns>
        public static int GetInt(string configName, int defaultValue)
        {
            var configTable = GetConfigTable();
            return configTable == null ? defaultValue : configTable.GetValueOrDefault(configName, defaultValue);
        }

        /// <summary>
        /// 读取浮点配置值，不存在时返回兜底值。
        /// </summary>
        /// <param name="configName">配置名称。</param>
        /// <param name="defaultValue">兜底数值。</param>
        /// <returns>配置值或兜底值。</returns>
        public static float GetFloat(string configName, float defaultValue)
        {
            return GetInt(configName, Mathf.RoundToInt(defaultValue));
        }

        /// <summary>
        /// 读取顾客刷新时间。
        /// </summary>
        public static float GetCustomerRefreshTime(float defaultValue)
        {
            return Mathf.Max(0.5f, GetFloat(CustomerRefreshTimeKey, defaultValue));
        }

        /// <summary>
        /// 读取厨师做菜时间。
        /// </summary>
        public static float GetChefCookTime(float defaultValue)
        {
            return Mathf.Max(0.1f, GetFloat(ChefCookTimeKey, defaultValue));
        }

        /// <summary>
        /// 读取顾客用餐时间。
        /// </summary>
        public static float GetCustomerEatTime(float defaultValue)
        {
            return Mathf.Max(0.1f, GetFloat(CustomerEatTimeKey, defaultValue));
        }

        /// <summary>
        /// 读取桌子清理时间。
        /// </summary>
        public static float GetTableCleanTime(float defaultValue)
        {
            return Mathf.Max(0.1f, GetFloat(TableCleanTimeKey, defaultValue));
        }

        /// <summary>
        /// 获取已加载的 TbConfig 表。
        /// </summary>
        /// <returns>配置表对象。</returns>
        private static TbConfig GetConfigTable()
        {
            EnsureTablesLoaded();
            return cachedTables?.TbConfig;
        }

        /// <summary>
        /// 按需从 Resources 中加载 Luban 配置表。
        /// </summary>
        private static void EnsureTablesLoaded()
        {
            if (loadAttempted)
            {
                return;
            }

            loadAttempted = true;
            var textAsset = Resources.Load<TextAsset>(ConfigResourcePath);
            if (textAsset == null || string.IsNullOrWhiteSpace(textAsset.text))
            {
                Debug.LogWarning($"[TbConfigRuntime] 未找到配置资源 {ConfigResourcePath}，将使用代码默认值。");
                return;
            }

            try
            {
                cachedTables = new Tables(_ => JSON.Parse(textAsset.text));
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[TbConfigRuntime] 读取配置表失败，将使用代码默认值。异常：{exception.Message}");
                cachedTables = null;
            }
        }
    }
}
