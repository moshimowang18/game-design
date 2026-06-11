using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace JN.Client.Scene
{
    public class CameraController : MonoBehaviour
    {
        public static CameraController Instance { get; private set; }

        [Header("Drag Settings")]
        [SerializeField] private float dragSensitivity = 0.02f;  // 每像素拖拽对应的相机移动速度
        [SerializeField] private float smoothSpeed = 10f;        // 数值越大拖拽越灵敏

        [Header("Axis Movement Locks")]
        [SerializeField] private bool lockX = false;
        [SerializeField] private bool lockZ = false;

        [Header("Axis Bounds")]
        [SerializeField] private bool useXBounds = false;
        [SerializeField] private float minX = -20f;
        [SerializeField] private float maxX = 20f;

        [SerializeField] private bool useZBounds = false;
        [SerializeField] private float minZ = -20f;
        [SerializeField] private float maxZ = 20f;

        [Header("Input Filters")]
        [Tooltip("Layer ID that will block camera drag when raycast hits it (default 5 = UI)")]
        [SerializeField] private int blockLayerId = 5;

        [Header("Editor / PC Support")]
        [SerializeField] private bool allowMouseDragInEditor = true;

        [Header("Tap Detection")]
        [Tooltip("Max squared distance (in screen pixels^2) between down & up to still count as a tap")]
        [SerializeField] private float tapMaxMovementSqr = 25f;

        private Vector3 _targetPosition;
        private float _fixedHeightY;
        private bool _isDragging;
        private Vector2 _lastPointerPos;
        private Vector3 _lastFramePosition;
        private Vector2 _pointerDownPos;
        private bool _pointerDownValid;

        /// <summary>
        /// 初始化组件引用和运行时状态。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>
        /// 在场景启动后补齐依赖并刷新初始显示。
        /// </summary>
        private void Start()
        {
            _targetPosition = transform.position;
            _fixedHeightY = transform.position.y;

            if (useXBounds)
                _targetPosition.x = Mathf.Clamp(_targetPosition.x, minX, maxX);
            if (useZBounds)
                _targetPosition.z = Mathf.Clamp(_targetPosition.z, minZ, maxZ);

            _targetPosition.y = _fixedHeightY;
            transform.position = _targetPosition;
            _lastFramePosition = transform.position;
        }

        /// <summary>
        /// 逐帧处理输入、状态推进或动画刷新。
        /// </summary>
        private void Update()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (allowMouseDragInEditor)
                HandleMouseDrag();
            else
                HandleTouchDrag();
#else
            HandleTouchDrag();
#endif

            float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, _targetPosition, t);
        }

        /// <summary>
        /// 处理触摸拖拽。
        /// </summary>
        private void HandleTouchDrag()
        {
            if (Input.touchCount == 0)
            {
                _isDragging = false;
                _pointerDownValid = false;
                return;
            }

            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                if (IsPointerBlocked(touch.position))
                {
                    _isDragging = false;
                    _pointerDownValid = false;
                    return;
                }

                _isDragging = true;
                _lastPointerPos = touch.position;
                _pointerDownPos = touch.position;
                _pointerDownValid = true;
            }
            else if (touch.phase == TouchPhase.Moved && _isDragging)
            {
                Vector2 delta = touch.position - _lastPointerPos;
                _lastPointerPos = touch.position;

                if (_pointerDownValid && (touch.position - _pointerDownPos).sqrMagnitude > tapMaxMovementSqr)
                {
                    _pointerDownValid = false;
                }

                ApplyDrag(delta);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                if (_pointerDownValid &&
                    (touch.position - _pointerDownPos).sqrMagnitude <= tapMaxMovementSqr &&
                    !IsPointerBlocked(touch.position))
                {
                    if (!Tile.TryHandlePointerClick(touch.position))
                    {
                        if (!TableArea.TryHandlePointerClick(touch.position))
                        {
                            TavernSceneManager.TryHandlePurchasePointerClick(touch.position);
                        }
                    }
                }

                _isDragging = false;
                _pointerDownValid = false;
            }
        }

        /// <summary>
        /// 处理鼠标拖拽。
        /// </summary>
        private void HandleMouseDrag()
        {
            if (Input.GetMouseButtonDown(0))
            {
                Vector2 pos = Input.mousePosition;

                if (IsPointerBlocked(pos))
                {
                    _isDragging = false;
                    _pointerDownValid = false;
                    return;
                }

                _isDragging = true;
                _lastPointerPos = pos;
                _pointerDownPos = pos;
                _pointerDownValid = true;
            }
            else if (Input.GetMouseButton(0) && _isDragging)
            {
                Vector2 currentPos = Input.mousePosition;
                Vector2 delta = currentPos - _lastPointerPos;
                _lastPointerPos = currentPos;

                if (_pointerDownValid && (currentPos - _pointerDownPos).sqrMagnitude > tapMaxMovementSqr)
                {
                    _pointerDownValid = false;
                }

                ApplyDrag(delta);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                Vector2 upPos = Input.mousePosition;

                if (_pointerDownValid &&
                    (upPos - _pointerDownPos).sqrMagnitude <= tapMaxMovementSqr &&
                    !IsPointerBlocked(upPos))
                {
                    if (!Tile.TryHandlePointerClick(upPos))
                    {
                        if (!TableArea.TryHandlePointerClick(upPos))
                        {
                            TavernSceneManager.TryHandlePurchasePointerClick(upPos);
                        }
                    }
                }

                _isDragging = false;
                _pointerDownValid = false;
            }
        }

        /// <summary>
        /// 应用拖拽。
        /// </summary>
        /// <param name="screenDelta">参数值。</param>
        private void ApplyDrag(Vector2 screenDelta)
        {
            if (screenDelta.sqrMagnitude < 0.01f)
                return;

            Vector3 right = transform.right;
            right.y = 0f;
            right.Normalize();

            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 move = (-screenDelta.x * right + -screenDelta.y * forward) * dragSensitivity;

            if (lockX) move.x = 0f;
            if (lockZ) move.z = 0f;

            _targetPosition += move;
            _targetPosition.y = _fixedHeightY;

            if (useXBounds && !lockX)
                _targetPosition.x = Mathf.Clamp(_targetPosition.x, minX, maxX);

            if (useZBounds && !lockZ)
                _targetPosition.z = Mathf.Clamp(_targetPosition.z, minZ, maxZ);
        }

        /// <summary>
        /// 在帧末同步跟随 界面 和场景表现位置。
        /// </summary>
        private void LateUpdate()
        {
            if (Vector3.SqrMagnitude(transform.position - _lastFramePosition) > 0.00001f)
            {
                Camera cam = Camera.main;
                if (cam == null) return;
                _lastFramePosition = transform.position;
            }
        }

        /// <summary>
        /// 设置目标位置。
        /// </summary>
        /// <param name="worldPos">参数值。</param>
        public void SetTargetPosition(Vector3 worldPos)
        {
            if (lockX) worldPos.x = _targetPosition.x;
            if (lockZ) worldPos.z = _targetPosition.z;

            worldPos.y = _fixedHeightY;
            _targetPosition = worldPos;

            if (useXBounds && !lockX)
                _targetPosition.x = Mathf.Clamp(_targetPosition.x, minX, maxX);

            if (useZBounds && !lockZ)
                _targetPosition.z = Mathf.Clamp(_targetPosition.z, minZ, maxZ);
        }

        /// <summary>
        /// 处理指针是否被阻挡相关逻辑。
        /// </summary>
        /// <param name="screenPos">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private bool IsPointerBlocked(Vector2 screenPos)
        {
            if (IsPointerOverUI(screenPos))
                return true;

            if (Camera.main != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(screenPos);
                int mask = 1 << blockLayerId;

                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, mask))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 处理指针是否悬停在界面相关逻辑。
        /// </summary>
        /// <param name="screenPosition">坐标。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null)
                return false;

            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            return results.Count > 0;
        }

        /// <summary>
        /// 处理点击。
        /// </summary>
        /// <param name="screenPos">参数值。</param>
        private void HandleTap(Vector2 screenPos)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f))
                return;

            Transform t = hit.transform;
            GameObject slotGO = null;

            while (t != null)
            {
                if (t.CompareTag("Slot"))
                {
                    slotGO = t.gameObject;
                    break;
                }
                t = t.parent;
            }

            if (slotGO == null)
                return;

            int slotIndex = -1;
            int slotLevel = 0;
            bool isBuilt = false;

            Debug.Log($"Tapped on slot: {slotIndex} lvl: {slotLevel}, isBuilt: {isBuilt}");

            if (slotIndex == -1)
            {
                Debug.LogWarning("[CameraController] HandleTap: no equipment slot uses this Slot GameObject as sceneParentPosition.");
                return;
            }
        }
    }
}
