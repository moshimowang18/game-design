using System.Collections;
using DG.Tweening;
using UnityEngine;

namespace AkiFramework.UI
{
    public static class UIUtils
    {
        public static Texture2D LoadTexture(string address)
        {
            var resourcePath = ToResourcesPath(address);
            return string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Texture2D>(resourcePath);
        }

        public static Sprite LoadSprite(string address)
        {
            var resourcePath = ToResourcesPath(address);
            return string.IsNullOrEmpty(resourcePath) ? null : Resources.Load<Sprite>(resourcePath);
        }

        public static void SetActive(GameObject go, bool active)
        {
            if (go == null) return;
            go.SetActive(active);
        }

        public static void PlayDropAnim(GameObject go, float delay)
        {
            go.SetActive(true);
            go.transform.localScale = Vector3.zero;

            go.transform
                .DOScale(1f, 0.25f)
                .SetDelay(delay)
                .SetEase(Ease.OutBack);
        }

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

        private static string ToResourcesPath(string pathOrKey)
        {
            if (string.IsNullOrWhiteSpace(pathOrKey))
            {
                return null;
            }

            var normalized = pathOrKey.Replace('\\', '/');
            const string marker = "/Resources/";
            var markerIndex = normalized.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                var resourcePath = normalized[(markerIndex + marker.Length)..];
                var extensionIndex = resourcePath.LastIndexOf('.');
                return extensionIndex > 0 ? resourcePath[..extensionIndex] : resourcePath;
            }

            // 兼容直接传 Resources 相对路径的调用方
            return normalized;
        }
    }
}