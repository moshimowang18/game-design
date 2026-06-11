using System;
using System.Collections;
using JN.Client.Model;
using JN.Client.UI;
using QFramework;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JN.Client.Manager
{
    /// <summary>
    /// 负责游戏相关的运行时逻辑。
    /// </summary>
    public class GameManager : MonoSingleton<GameManager>
    {
        private bool sceneLoadedSubscribed;

        /// <summary>
        /// 在场景加载前自动初始化模块。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitializeBeforeSceneLoad()
        {
            Instance.Init();
            EnsureSceneHudOpened(SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// 初始化模块依赖和默认状态。
        /// </summary>
        public void Init()
        {
            SO_Product.GetAll();
            SO_Equipment.GetAll();
            SO_Customer.GetAll();
            SO_Staff.GetAll();
            SO_Shop.GetAll();
            SO_Routine.GetAll();

            EnsureSceneHudListener();
            GameAudioManager.Instance.Init();
        }

        /// <summary>
        /// 加载场景异步。
        /// </summary>
        /// <param name="sceneName">名称。</param>
        /// <param name="on加载完成">参数值。</param>
        /// <returns>协程迭代器。</returns>
        public IEnumerator LoadSceneAsync(string sceneName, Action onLoaded = null)
        {
            var operation = SceneManager.LoadSceneAsync(sceneName);
            if (operation == null)
            {
                yield break;
            }

            operation.allowSceneActivation = true;
            yield return operation;

            DataManager.Instance.RecordLastScene(sceneName);
            EnsureSceneHudOpened(sceneName);
            onLoaded?.Invoke();
        }

        /// <summary>
        /// 确保场景状态栏监听器存在。
        /// </summary>
        private void EnsureSceneHudListener()
        {
            if (sceneLoadedSubscribed)
            {
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            sceneLoadedSubscribed = true;
        }

        /// <summary>
        /// 处理场景加载完成。
        /// </summary>
        /// <param name="scene">参数值。</param>
        /// <param name="mode">参数值。</param>
        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            EnsureSceneHudOpened(scene.name);
        }

        /// <summary>
        /// 确保场景状态栏已打开。
        /// </summary>
        /// <param name="sceneName">名称。</param>
        private static void EnsureSceneHudOpened(string sceneName)
        {
            if (sceneName != "GamePlay_Tavern"
                && sceneName != "Tavern_Gameplay"
                && sceneName != "SCN_Tavern_Gameplay")
            {
                return;
            }

            if (UIKit.GetPanel<TownStatusBarPanelController>() != null)
            {
                UIKit.ClosePanel<TownStatusBarPanelController>();
            }

            if (UIKit.GetPanel<TavernStatusBarPanelController>() == null)
            {
                UIKit.OpenPanel<TavernStatusBarPanelController>(UILevel.Common);
            }

            if (UIKit.GetPanel<StartOpeningWindowController>() == null)
            {
                UIKit.OpenPanel<StartOpeningWindowController>(UILevel.PopUI);
            }
        }
    }

    /// <summary>
    /// 负责全局背景音乐、通用音效和按钮点击音效挂接。
    /// </summary>
    public class GameAudioManager : MonoSingleton<GameAudioManager>
    {
        private const string BgmAssetPath = "Assets/Res/Resources/Audios/BackGroundMusic/BGM.mp3";
        private const string CoinsAssetPath = "Assets/Res/Resources/Audios/Effects/Coins.mp3";
        private const string ConstructionAssetPath = "Assets/Res/Resources/Audios/Effects/Construction.mp3";
        private const string TaskAssetPath = "Assets/Res/Resources/Audios/Effects/Task.mp3";
        private const string WipingAssetPath = "Assets/Res/Resources/Audios/Effects/wiping.mp3";
        private const string UiClickAssetPath = "Assets/Res/Resources/Audios/Effects/UI/uiClick.mp3";
        private const float ButtonScanInterval = 0.5f;

        private AudioSource bgmSource;
        private AudioSource sfxSource;
        private float nextButtonScanTime;
        private int lastCompletedGuideTaskCount = -1;

        /// <summary>
        /// 初始化音频源与监听。
        /// </summary>
        public void Init()
        {
            EnsureAudioSources();
            EnsureSceneListener();
            EnsureGuideTaskListener();
            PlaySceneBgm();
            AttachClickSoundHooksInActiveScene();
        }

        /// <summary>
        /// 每帧补挂当前场景中新出现的按钮点击音效组件。
        /// </summary>
        private void Update()
        {
            if (Time.unscaledTime < nextButtonScanTime)
            {
                return;
            }

            nextButtonScanTime = Time.unscaledTime + ButtonScanInterval;
            AttachClickSoundHooksInActiveScene();
        }

        /// <summary>
        /// 播放场景背景音乐。
        /// </summary>
        public static void PlaySceneBgm()
        {
            Instance.PlayLoopingClip(BgmAssetPath, 0.6f);
        }

        /// <summary>
        /// 播放结账金币音效。
        /// </summary>
        public static void PlayCheckoutCoins()
        {
            Instance.PlayOneShot(CoinsAssetPath, 0.9f);
        }

        /// <summary>
        /// 播放建造相关音效。
        /// </summary>
        public static void PlayConstruction()
        {
            Instance.PlayOneShot(ConstructionAssetPath, 0.9f);
        }

        /// <summary>
        /// 播放任务完成音效。
        /// </summary>
        public static void PlayTaskComplete()
        {
            Instance.PlayOneShot(TaskAssetPath, 1f);
        }

        /// <summary>
        /// 播放清扫音效。
        /// </summary>
        public static void PlayWiping()
        {
            Instance.PlayOneShot(WipingAssetPath, 0.85f);
        }

        /// <summary>
        /// 播放普通按钮点击音效。
        /// </summary>
        public static void PlayButtonClick()
        {
            Instance.PlayOneShot(UiClickAssetPath, 0.8f);
        }

        /// <summary>
        /// 处理场景加载完成事件，刷新 BGM 与按钮音效挂接。
        /// </summary>
        /// <param name="scene">已加载场景。</param>
        /// <param name="mode">加载模式。</param>
        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            PlaySceneBgm();
            AttachClickSoundHooksInActiveScene();
            CacheGuideTaskProgress();
        }

        /// <summary>
        /// 在玩法引导任务完成数增加时播放任务完成音效。
        /// </summary>
        private void HandleGuideProgressChanged()
        {
            if (DataManager.Instance == null)
            {
                return;
            }

            var snapshot = DataManager.Instance.GetGameplayGuideSnapshot();
            if (snapshot == null)
            {
                return;
            }

            var completedCount = CountCompletedGuideTasks(snapshot);
            if (lastCompletedGuideTaskCount >= 0 && completedCount > lastCompletedGuideTaskCount)
            {
                PlayTaskComplete();
            }

            lastCompletedGuideTaskCount = completedCount;
        }

        /// <summary>
        /// 缓存当前引导任务完成数量，避免首次进入场景时误播完成音效。
        /// </summary>
        private void CacheGuideTaskProgress()
        {
            if (DataManager.Instance == null)
            {
                lastCompletedGuideTaskCount = -1;
                return;
            }

            var snapshot = DataManager.Instance.GetGameplayGuideSnapshot();
            lastCompletedGuideTaskCount = snapshot != null ? CountCompletedGuideTasks(snapshot) : -1;
        }

        /// <summary>
        /// 统计当前快照中已完成的任务数量。
        /// </summary>
        /// <param name="snapshot">玩法引导快照。</param>
        /// <returns>已完成任务数。</returns>
        private static int CountCompletedGuideTasks(GameplayGuideSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return 0;
            }

            var completedCount = 0;
            for (var index = 0; index < snapshot.ActiveTasks.Count; index++)
            {
                if (snapshot.ActiveTasks[index] != null && snapshot.ActiveTasks[index].IsCompleted)
                {
                    completedCount++;
                }
            }

            return completedCount;
        }

        /// <summary>
        /// 给当前激活场景中的所有按钮补挂点击音效组件。
        /// </summary>
        private void AttachClickSoundHooksInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            var roots = scene.GetRootGameObjects();
            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                var root = roots[rootIndex];
                if (root == null)
                {
                    continue;
                }

                var buttons = root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                for (var buttonIndex = 0; buttonIndex < buttons.Length; buttonIndex++)
                {
                    var button = buttons[buttonIndex];
                    if (button == null || button.gameObject.GetComponent<UIButtonClickSoundHook>() != null)
                    {
                        continue;
                    }

                    button.gameObject.AddComponent<UIButtonClickSoundHook>();
                }
            }
        }

        /// <summary>
        /// 播放循环背景音乐，已在播放同一片段时不重复启动。
        /// </summary>
        /// <param name="assetPath">音频资源路径。</param>
        /// <param name="volume">音量。</param>
        private void PlayLoopingClip(string assetPath, float volume)
        {
            EnsureAudioSources();
            var clip = LoadAudioClip(assetPath);
            if (clip == null)
            {
                return;
            }

            if (bgmSource.clip == clip && bgmSource.isPlaying)
            {
                bgmSource.volume = volume;
                return;
            }

            bgmSource.clip = clip;
            bgmSource.loop = true;
            bgmSource.volume = volume;
            bgmSource.Play();
        }

        /// <summary>
        /// 播放一次性音效。
        /// </summary>
        /// <param name="assetPath">音频资源路径。</param>
        /// <param name="volume">音量。</param>
        private void PlayOneShot(string assetPath, float volume)
        {
            EnsureAudioSources();
            var clip = LoadAudioClip(assetPath);
            if (clip == null)
            {
                return;
            }

            sfxSource.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// 确保背景音乐与音效音源存在。
        /// </summary>
        private void EnsureAudioSources()
        {
            if (bgmSource == null)
            {
                bgmSource = gameObject.GetComponent<AudioSource>();
                if (bgmSource == null)
                {
                    bgmSource = gameObject.AddComponent<AudioSource>();
                }

                bgmSource.playOnAwake = false;
                bgmSource.loop = true;
                bgmSource.spatialBlend = 0f;
            }

            if (sfxSource != null)
            {
                return;
            }

            var child = transform.Find("SfxAudioSource");
            if (child == null)
            {
                var childObject = new GameObject("SfxAudioSource");
                childObject.transform.SetParent(transform, false);
                child = childObject.transform;
            }

            sfxSource = child.GetComponent<AudioSource>();
            if (sfxSource == null)
            {
                sfxSource = child.gameObject.AddComponent<AudioSource>();
            }

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }

        /// <summary>
        /// 确保已注册场景切换监听。
        /// </summary>
        private void EnsureSceneListener()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        /// <summary>
        /// 确保已注册玩法引导进度监听。
        /// </summary>
        private void EnsureGuideTaskListener()
        {
            Signals.Get<GameplayGuideProgressSignal>().RemoveListener(HandleGuideProgressChanged);
            Signals.Get<GameplayGuideProgressSignal>().AddListener(HandleGuideProgressChanged);
            CacheGuideTaskProgress();
        }

        /// <summary>
        /// 按资源路径读取音频片段。
        /// </summary>
        /// <param name="assetPath">Unity 资源路径。</param>
        /// <returns>读取到的音频片段；失败时返回 null。</returns>
        private static AudioClip LoadAudioClip(string assetPath)
        {
            return GameplayResourceStore.LoadAsset<AudioClip>(assetPath);
        }
    }
}
