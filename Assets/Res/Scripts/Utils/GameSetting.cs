using System;
using System.IO;
using JN.Client;
using Newtonsoft.Json;
using UnityCommunity.UnitySingleton;
using UnityEngine;

namespace JN.Client.Utils
{
    [Serializable]
    /// <summary>
    /// 负责游戏设置数据相关的运行时逻辑。
    /// </summary>
    public class GameSettingData
    {
        // 音量
        public float masterVolume = 1f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 0.8f;

        // 画质
        public int graphicsQuality = 2; // 0=低 1=中 2=高

        // 语言
        public GameLanguage curLanguage = GameLanguage.en_US;
    }

    /// <summary>
    /// 负责游戏设置相关的运行时逻辑。
    /// </summary>
    public class GameSetting : MonoSingleton<GameSetting>
    {
        public GameSettingData Data;

        private static string SavePath =>
            Path.Combine(Application.persistentDataPath, "gamesetting.json");

        /// <summary>
        /// 初始化组件引用和运行时状态。
        /// </summary>
        protected override void Awake()
        {
            // 单例基类需要先完成自身初始化。
            base.Awake();
            Signals.Get<LanguageSwitchSignal>().AddListener(SwitchLanguage);

            // 只在第一次实例化时创建默认设置对象，后续通过文件覆盖字段值。
            if (Data == null)
                Data = new GameSettingData();

            LoadFromFile();
            Apply();
        }

        /// <summary>
        /// 销毁时释放监听、协程和运行时缓存。
        /// </summary>
        protected void OnDestroy()
        {
            Signals.Get<LanguageSwitchSignal>().RemoveListener(SwitchLanguage);
        }

        /// <summary>
        /// 处理切换语言相关逻辑。
        /// </summary>
        private void SwitchLanguage()
        {
            Data.curLanguage = LocalizationManager.Instance.CurrentLanguage;
        }

        /// <summary>
        /// 从本地文件加载设置。
        /// </summary>
        private void LoadFromFile()
        {
            if (!File.Exists(SavePath))
                return;

            var json = File.ReadAllText(SavePath);

            // 直接覆盖现有实例字段，避免外部持有的 数据 引用失效。
            JsonConvert.PopulateObject(json, Data);
        }

        /// <summary>
        /// 保存游戏设置。
        /// </summary>
        public void Save()
        {
            File.WriteAllText(
                SavePath,
                JsonConvert.SerializeObject(Data, Formatting.Indented)
            );
        }

        /// <summary>
        /// 应用游戏设置。
        /// </summary>
        public void Apply()
        {
            // 目前只接入了全局音量和画质，后续其它设置也可在这里集中落地。
            AudioListener.volume = Data.masterVolume;
            QualitySettings.SetQualityLevel(Data.graphicsQuality, true);
        }

        /// <summary>
        /// 响应应用退出事件并同步状态。
        /// </summary>
        private void OnApplicationQuit() => Save();

        /// <summary>
        /// 响应应用聚焦事件并同步状态。
        /// </summary>
        /// <param name="focus">参数值。</param>
        private void OnApplicationFocus(bool focus)
        {
            if (!focus) Save();
        }
    }
}
