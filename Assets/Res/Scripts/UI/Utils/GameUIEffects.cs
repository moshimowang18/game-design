using DG.Tweening;
using JN.Client.Manager;
using UnityEngine;
using UnityEngine.UI;

namespace JN.Client
{
    /// <summary>
    /// 负责游戏特效相关的运行时逻辑。
    /// </summary>
    public static class GameUIEffects
    {
        private const float CoinsSpawnRadius = 60f;
        private const float CoinsMinFlyDuration = 0.4f;
        private const float CoinsMaxFlyDuration = 0.7f;
        private const float CoinsMaxZRotation = 25f;
        private const float CoinsStartScale = 1f;
        private const float CoinsEndScale = 0.4f;
        private const float CoinsBlendXOffset = 40f;
        private const float MaxTrailDelay = 0.2f;
        private const int CoinCount = 35;
        private const string CoinPrefabPath = "Assets/Res/Resources/UI/Item/CoinItem.prefab";

        private static GameObject coinPrefab;

        /// <summary>
        /// 播放铜钱飞行动画。
        /// </summary>
        /// <param name="start">参数值。</param>
        /// <param name="target">目标对象。</param>
        public static void PlayCoinsFly(Transform start, Transform target)
        {
            if (start == null || target == null)
            {
                return;
            }

            var targetCanvas = target.GetComponentInParent<Canvas>();
            if (targetCanvas == null)
            {
                return;
            }

            var parent = targetCanvas.transform;
            var fromScreen = start.position;
            var toScreen = target.position;
            var globalDir = (toScreen - fromScreen).normalized;
            var globalPerp = Vector3.Cross(globalDir, Vector3.forward).normalized;

            EnsureCoinPrefabLoaded();
            if (coinPrefab == null || parent == null)
            {
                return;
            }

            // 使用一组带随机控制点的曲线路径，让金币群飞的效果更有层次。
            for (var i = 0; i < CoinCount; i++)
            {
                var coinGo = Lyf.ObjectPool.ObjectPool.Instance.Allocate(coinPrefab, parent);
                if (coinGo == null)
                {
                    continue;
                }

                var rt = coinGo.GetComponent<RectTransform>();
                if (rt == null)
                {
                    Lyf.ObjectPool.ObjectPool.Instance.Recycle(coinGo);
                    continue;
                }

                if (!coinGo.TryGetComponent<CanvasGroup>(out var cg))
                {
                    cg = coinGo.AddComponent<CanvasGroup>();
                }

                cg.alpha = 0f;
                rt.position = fromScreen + (Vector3)(Random.insideUnitCircle * CoinsSpawnRadius);
                rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-CoinsMaxZRotation, CoinsMaxZRotation));
                rt.localScale = Vector3.one * CoinsStartScale;

                var duration = Random.Range(CoinsMinFlyDuration, CoinsMaxFlyDuration);
                var catmullDuration = duration * 0.85f;
                var startPos = rt.position;
                var endPos = toScreen;

                var arcMagnitude = CoinsBlendXOffset * 1.8f * Random.Range(0.8f, 1.2f);
                var control1 = Vector3.Lerp(startPos, endPos, 0.25f) + globalPerp * arcMagnitude;
                var control2 = Vector3.Lerp(startPos, endPos, 0.6f) - globalPerp * arcMagnitude * 0.5f;
                var nearTarget = Vector3.Lerp(startPos, endPos, 0.96f);
                var path = new[] { startPos, control1, control2, nearTarget };

                var t01 = (float)i / (CoinCount - 1);
                var startDelay = t01 * MaxTrailDelay;

                var seq = DOTween.Sequence();
                seq.PrependInterval(startDelay);
                seq.AppendCallback(() => cg.alpha = 1f);
                seq.Append(rt.DOPath(path, catmullDuration, PathType.CatmullRom).SetEase(Ease.OutCubic));
                seq.Join(rt.DOScale(CoinsEndScale, duration).SetEase(Ease.OutQuad));
                seq.Join(cg.DOFade(0f, duration - 0.25f).SetDelay(0.25f));
                seq.OnComplete(() =>
                {
                    if (coinGo != null)
                    {
                        Lyf.ObjectPool.ObjectPool.Instance.Recycle(coinGo);
                    }
                });
            }
        }

        /// <summary>
        /// 确保铜钱预制体加载完成。
        /// </summary>
        /// <returns>返回方法执行后的结果。</returns>
        private static void EnsureCoinPrefabLoaded()
        {
            if (coinPrefab != null)
            {
                return;
            }

            // 特效 预制体 只首次加载一次，后续由对象池复用实例。
            coinPrefab = GameplayResourceStore.LoadAsset<GameObject>(CoinPrefabPath);
        }
    }
}

namespace JN.Client.UI
{
    /// <summary>
    /// 负责为单个按钮补挂通用点击音效。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonClickSoundHook : MonoBehaviour
    {
        private Button button;

        /// <summary>
        /// 缓存按钮组件。
        /// </summary>
        private void Awake()
        {
            button = GetComponent<Button>();
        }

        /// <summary>
        /// 激活时绑定点击音效。
        /// </summary>
        private void OnEnable()
        {
            button ??= GetComponent<Button>();
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        /// <summary>
        /// 失活时移除点击音效监听，避免重复绑定。
        /// </summary>
        private void OnDisable()
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(HandleClick);
        }

        /// <summary>
        /// 处理按钮点击音效播放。
        /// </summary>
        private static void HandleClick()
        {
            GameAudioManager.PlayButtonClick();
        }
    }
}
