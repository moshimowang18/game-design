using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace JN.Client.Tools
{
    public class AnimateTexture : MonoBehaviour
    {
        /// <summary>
        /// 处理每秒帧数相关逻辑。
        /// </summary>
        public float framesPerSecond = 10.0f;

        /// <summary>
        /// 处理图片序列相关逻辑。
        /// </summary>
        public Sprite[] sprites;

        private Image _image;

        /// <summary>
        /// 在场景启动后补齐依赖并刷新初始显示。
        /// </summary>
        private void Start()
        {
            _image = GetComponent<Image>();
            StartCoroutine(PlayAnimation());
        }

        /// <summary>
        /// 启用时注册事件监听并刷新当前状态。
        /// </summary>
        private void OnEnable()
        {
            StartCoroutine(PlayAnimation());
        }

        /// <summary>
        /// 播放动画。
        /// </summary>
        /// <returns>协程迭代器。</returns>
        private IEnumerator PlayAnimation()
        {
            while (true)
            {
                for (var i = 0; i < sprites.Length; i++)
                {
                    if (_image != null)
                    {
                        _image.sprite = sprites[i];
                    }

                    yield return new WaitForSeconds(1f / framesPerSecond);
                }
            }
        }
    }
}
