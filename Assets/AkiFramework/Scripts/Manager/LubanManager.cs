using System;
using System.IO;
using cfg;
using SimpleJSON;
using UnityCommunity.UnitySingleton;
using UnityEngine;

namespace JN.Client.Manager
{
    /// <summary>
    /// Luban 配置表访问入口。
    /// 负责按指定目录加载 JSON 表，并对外提供统一的 Tables 实例。
    /// </summary>
    public class LubanManager : Singleton<LubanManager>
    {
        private const string DefaultGameConfDir = "Assets/AkiFramework/QFrameworkData/LubanData";

        private Tables tables;
        private string currentGameConfDir = DefaultGameConfDir;
        private bool isInitialized;

        public void Init()
        {
            if (isInitialized)
            {
                return;
            }

            Reload();
        }

        /// <summary>
        /// 获取当前已加载的配置表集合。
        /// </summary>
        public Tables GetTables()
        {
            if (!isInitialized || tables == null)
            {
                throw new InvalidOperationException("LubanManager is not initialized. Call Init() first.");
            }

            return tables;
        }

        /// <summary>
        /// 重新加载配置表，可选切换到新的配置目录。
        /// </summary>
        public void Reload(string gameConfDir = null)
        {
            if (!string.IsNullOrWhiteSpace(gameConfDir))
            {
                currentGameConfDir = gameConfDir;
            }

            // Tables 构造时会按需回调 LoadJson，因此这里只替换数据源与加载入口。
            tables = new Tables(LoadJson);
            isInitialized = true;
            Debug.Log($"[LubanManager] Initialized with config dir: {currentGameConfDir}");
        }

        public new bool IsInitialized()
        {
            return isInitialized && tables != null;
        }

        private JSONNode LoadJson(string file)
        {
            var filePath = Path.Combine(currentGameConfDir, $"{file}.json");
            if (File.Exists(filePath))
            {
                try
                {
                    return JSON.Parse(File.ReadAllText(filePath));
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LubanManager] Failed to parse config file: {filePath}\n{e}");
                    return new JSONObject();
                }
            }

            try
            {
                var resourceTextAsset = Resources.Load<TextAsset>($"Config/{file}");
                if (resourceTextAsset != null && !string.IsNullOrWhiteSpace(resourceTextAsset.text))
                {
                    return JSON.Parse(resourceTextAsset.text);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LubanManager] Failed to parse config resource: Config/{file}\n{e}");
            }

            Debug.LogWarning($"[LubanManager] Config file not found: {filePath}，且 Resources/Config/{file}.json 也未找到。");
            return new JSONObject();
        }
    }
}
