using UnityEngine;
using UnityEngine.EventSystems;

namespace JN.Client.UI
{
    public class ButtonPressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float pressedScale = 0.9f; // 按下时缩小比例
        [SerializeField] private float animSpeed = 15f; // 动画速度

        private Vector3 originalScale;
        private Vector3 targetScale;

        /// <summary>
        /// 初始化组件引用和运行时状态。
        /// </summary>
        private void Awake()
        {
            originalScale = transform.localScale;
            targetScale = originalScale;
        }

        /// <summary>
        /// 逐帧处理输入、状态推进或动画刷新。
        /// </summary>
        private void Update()
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                targetScale,
                Time.deltaTime * animSpeed
            );
        }

        /// <summary>
        /// 处理指针按下时的按钮缩放反馈。
        /// </summary>
        /// <param name="eventData">数据。</param>
        public void OnPointerDown(PointerEventData eventData)
        {
            targetScale = originalScale * pressedScale;
        }

        /// <summary>
        /// 处理指针抬起时的按钮恢复反馈。
        /// </summary>
        /// <param name="eventData">数据。</param>
        public void OnPointerUp(PointerEventData eventData)
        {
            targetScale = originalScale;
        }

        /// <summary>
        /// 处理指针移出时的按钮恢复反馈。
        /// </summary>
        /// <param name="eventData">数据。</param>
        public void OnPointerExit(PointerEventData eventData)
        {
            // 手指滑出按钮也恢复
            targetScale = originalScale;
        }
    }
}
