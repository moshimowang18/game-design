using System;
using System.Collections.Generic;

namespace cfg
{
    /// <summary>
    /// 为配置表补充按配置名快速读取数值的能力。
    /// </summary>
    public partial class TbConfig
    {
        private Dictionary<string, Config> configMap;

        /// <summary>
        /// 尝试按配置名读取对应配置行。
        /// </summary>
        /// <param name="configName">配置名称。</param>
        /// <param name="config">读取到的配置行。</param>
        /// <returns>存在对应配置时返回 true。</returns>
        public bool TryGet(string configName, out Config config)
        {
            EnsureConfigMap();
            return configMap.TryGetValue(configName ?? string.Empty, out config);
        }

        /// <summary>
        /// 按配置名读取整型数值，不存在时返回兜底值。
        /// </summary>
        /// <param name="configName">配置名称。</param>
        /// <param name="defaultValue">兜底数值。</param>
        /// <returns>配置值或兜底值。</returns>
        public int GetValueOrDefault(string configName, int defaultValue)
        {
            return TryGet(configName, out var config) ? config.Value : defaultValue;
        }

        /// <summary>
        /// 初始化配置名到配置行的映射缓存。
        /// </summary>
        private void EnsureConfigMap()
        {
            if (configMap != null)
            {
                return;
            }

            configMap = new Dictionary<string, Config>(StringComparer.OrdinalIgnoreCase);
            if (_dataList == null)
            {
                return;
            }

            for (var index = 0; index < _dataList.Count; index++)
            {
                var config = _dataList[index];
                if (config == null || string.IsNullOrWhiteSpace(config.ConfigName))
                {
                    continue;
                }

                configMap[config.ConfigName] = config;
            }
        }
    }
}
