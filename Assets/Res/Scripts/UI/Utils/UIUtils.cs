using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace JN.Client
{
    public class UIUtils
    {
        #region CoinFlyConfig

        [SerializeField] private static float coinsSpawnRadius = 60f;
        [SerializeField] private static float coinsMinFlyDuration = 0.4f;
        [SerializeField] private static float coinsMaxFlyDuration = 0.7f;
        [SerializeField] private static float coinsMaxZRotation = 25f;
        [SerializeField] private static float coinsStartScale = 1f;
        [SerializeField] private static float coinsEndScale = 0.4f;
        [SerializeField] private static float coinsBLENDXOffset = 40f;

        #endregion

        /// <summary>
        /// 加载贴图资源。
        /// </summary>
        /// <param name="address">参数值。</param>
        /// <returns>返回匹配到的对象引用。</returns>
        public static Texture2D LoadTexture(string address)
        {
            return GameplayResourceStore.LoadAsset<Texture2D>(address);
        }

        /// <summary>
        /// 加载图片。
        /// </summary>
        /// <param name="address">参数值。</param>
        /// <returns>返回匹配到的对象引用。</returns>
        public static Sprite LoadSprite(string address)
        {
            return GameplayResourceStore.LoadAsset<Sprite>(address);
        }

        /// <summary>
        /// 设置当前。
        /// </summary>
        /// <param name="go">参数值。</param>
        /// <param name="active">参数值。</param>
        public static void SetActive(GameObject go, bool active)
        {
            if (go == null) return;
            go.SetActive(active);
        }

        /// <summary>
        /// 播放掉落动画。
        /// </summary>
        /// <param name="go">参数值。</param>
        /// <param name="delay">参数值。</param>
        public static void PlayDropAnim(GameObject go, float delay)
        {
            go.SetActive(true);
            go.transform.localScale = Vector3.zero;

            go.transform
                .DOScale(1f, 0.25f)
                .SetDelay(delay)
                .SetEase(Ease.OutBack);
        }

        /// <summary>
        /// 设置当前界面。
        /// </summary>
        /// <param name="go">参数值。</param>
        /// <param name="is显隐">参数值。</param>
        public static void SetActiveUI(GameObject go, bool isVisible)
        {
            if (go == null) return;

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();

            cg.alpha = isVisible ? 1f : 0f;
            cg.interactable = isVisible;
            cg.blocksRaycasts = isVisible;
        }

        public static void SetActiveUISmooth(
            MonoBehaviour owner,
            GameObject go,
            bool isVisible,
            float duration = 0.3f)
        {
            if (go == null || owner == null) return;

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();

            owner.StartCoroutine(FadeCoroutine(cg, isVisible, duration));
        }

        static IEnumerator FadeCoroutine(
            CanvasGroup cg,
            bool isVisible,
            float duration)
        {
            float start = cg.alpha;
            float end = isVisible ? 1f : 0f;

            cg.interactable = true;
            cg.blocksRaycasts = true;

            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                cg.alpha = Mathf.Lerp(start, end, time / duration);
                yield return null;
            }

            cg.alpha = end;
            cg.interactable = isVisible;
            cg.blocksRaycasts = isVisible;
        }

        /// <summary>
        /// 播放铜钱飞行动画。
        /// </summary>
        /// <param name="start">参数值。</param>
        /// <param name="target">目标对象。</param>
        public static void PlayCoinsFly(Transform start, Transform target)
        {
            if (start == null || target == null)
                return;

            int coinCount = 35;
            const string coinPrefabPath = "Assets/Res/Resources/UI/Item/CoinItem.prefab";

            Transform parent = target.GetComponentInParent<Canvas>().transform;

            Vector3 fromScreen = start.position;
            Vector3 toScreen = target.position;

            Vector3 globalDir = (toScreen - fromScreen).normalized;
            Vector3 globalPerp = Vector3.Cross(globalDir, Vector3.forward).normalized;

            const float maxTrailDelay = 0.2f;

            // 统一改为 Resources 入口加载
            var coinPrefab = GameplayResourceStore.LoadAsset<GameObject>(coinPrefabPath);
            if (coinPrefab == null)
            {
                return;
            }

            for (int i = 0; i < coinCount; i++)
            {
                // 使用对象池
                var coinGo = Lyf.ObjectPool.ObjectPool.Instance.Allocate(coinPrefab, parent);

                var rt = coinGo.GetComponent<RectTransform>();

                var cg = coinGo.GetComponent<CanvasGroup>();
                if (cg == null) cg = coinGo.AddComponent<CanvasGroup>();

                cg.alpha = 0f; // 生成时隐藏

                Vector2 spawnOffset = Random.insideUnitCircle * coinsSpawnRadius;
                rt.position = fromScreen + (Vector3)spawnOffset;

                float duration = Random.Range(coinsMinFlyDuration, coinsMaxFlyDuration);
                float catmullDuration = duration * 0.85f;

                float zRot = Random.Range(-coinsMaxZRotation, coinsMaxZRotation);
                rt.localRotation = Quaternion.Euler(0f, 0f, zRot);
                rt.localScale = Vector3.one * coinsStartScale;

                Vector3 startPos = rt.position;
                Vector3 endPos = toScreen;

                float arcMagnitude = coinsBLENDXOffset * 1.8f *
                                     Random.Range(0.8f, 1.2f);

                Vector3 control1 = Vector3.Lerp(startPos, endPos, 0.25f)
                                   + globalPerp * arcMagnitude;

                Vector3 control2 = Vector3.Lerp(startPos, endPos, 0.6f)
                                   - globalPerp * arcMagnitude * 0.5f;

                Vector3 nearTarget = Vector3.Lerp(startPos, endPos, 0.96f);

                Vector3[] path = new Vector3[]
                {
                    startPos,
                    control1,
                    control2,
                    nearTarget
                };

                float t01 = (float)i / (coinCount - 1);
                float startDelay = t01 * maxTrailDelay;

                var seq = DOTween.Sequence();
                seq.PrependInterval(startDelay);

                seq.AppendCallback(() =>
                {
                    cg.alpha = 1f; // 动画开始再显示
                });

                seq.Append(
                    rt.DOPath(path, catmullDuration, PathType.CatmullRom)
                        .SetEase(Ease.OutCubic)
                );

                seq.Join(
                    rt.DOScale(coinsEndScale, duration)
                        .SetEase(Ease.OutQuad)
                );

                seq.Join(
                    cg.DOFade(0f, duration - 0.25f)
                        .SetDelay(0.25f)
                );

                seq.OnComplete(() =>
                {
                    // 回收到对象池
                    Lyf.ObjectPool.ObjectPool.Instance.Recycle(coinGo);
                });
            }
        }
    }
}