using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace JN.Client.Scene
{
    /// <summary>
    /// 负责酒楼顾客运行时相关的运行时逻辑。
    /// </summary>
    public class TavernCustomerRuntimeController : MonoBehaviour
    {
        private const float NavMeshSampleDistance = 2f;
        private const float StuckVelocityThreshold = 0.01f;
        private const float RepathDelay = 0.75f;
        private const float GroundOffset = -0.1f;
        private const float SitBlendDelay = 0.2f;
        private const float StandBlendDelay = 0.2f;
        private const float SeatTowardTableOffset = 0.08f;
        private const float StuckSideStepDistance = 0.4f;
        private const float EatLoopRetriggerInterval = 1.1f;
        private const string OrderBubblePrefabResourcePath = "UI/Guides/CustomerOrderBubble";
        private const string OrderWaitProgressPrefabResourcePath = "UI/Runtime/CustomerEnterProgress";
        private const string OrderWaitProgressFillRedResourcePath = "Textures/UI/Icons 1/customerEnterProgressFillRed";
        private const float OrderWaitHeadOffsetY = 1.28f;
        private static readonly Vector3 OrderWaitProgressScale = new(0.008f, 0.008f, 0.008f);
        private static readonly Vector3 OrderBubbleWorldBaseOffset = new(0f, 1.02f, -0.05f);
        private static readonly Color AngryBubbleColor = new(1f, 0.35f, 0.35f);
        private const float OrderBubbleRightSideOffsetDistance = 0.2f;
        private const float OrderBubbleLeftSideOffsetDistance = 0.12f;
        private const float DiningSpeechChance = 0.6f;
        private static readonly string[] DiningSpeechTexts =
        {
            "不错哦",
            "好吃好吃",
            "一般般",
            "大厨水平不错"
        };

        /// <summary>
        /// 定义顾客状态可用的枚举类型。
        /// </summary>
        private enum CustomerState
        {
            None,
            Queueing,
            MovingToTable,
            Dining,
            Leaving
        }

        private TavernSceneManager owner;
        private NavMeshAgent agent;
        private Animator animator;
        private Vector3 exitPosition;
        private Vector3 currentDestination;
        private CustomerState state;
        private int speedHash = -1;
        private bool hasSpeedParam;
        private float stuckTimer;
        private bool hasSitDownTrigger;
        private bool hasStandUpTrigger;
        private bool hasStartEatTrigger;
        private bool hasStopEatTrigger;
        private bool hasIsSittingBool;
        private bool hasIsEatingBool;
        private readonly List<GameObject> activeOrderBubbles = new();
        private GameObject orderWaitProgressRoot;
        private Image orderWaitProgressFill;
        private Sprite orderWaitProgressDefaultSprite;
        private Sprite orderWaitProgressAngrySprite;
        private Coroutine orderWaitVisualRoutine;

        public int TableId { get; private set; }
        public bool IsOrderWaitActive => orderWaitVisualRoutine != null;
        public int SeatIndex { get; private set; }
        public bool IsSeated { get; private set; }
        public bool IsReadyCheckout { get; private set; }

        /// <summary>
        /// 注入运行时依赖并刷新初始显示。
        /// </summary>
        /// <param name="tavernSceneManager">参数值。</param>
        /// <param name="startPosition">坐标。</param>
        /// <param name="targetExitPosition">目标对象。</param>
        public void Initialize(TavernSceneManager tavernSceneManager, Vector3 startPosition, Vector3 targetExitPosition)
        {
            owner = tavernSceneManager;
            exitPosition = targetExitPosition;
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            EnsureAnimationEventRelay();
            TableId = -1;
            SeatIndex = 0;
            IsSeated = false;
            IsReadyCheckout = false;
            currentDestination = startPosition;
            stuckTimer = 0f;

            // 动画参数是否存在取决于具体模型控制器，因此初始化时先做一次能力探测。
            CacheAnimatorState();
            PrepareAgentForSpawn(startPosition);
        }

        /// <summary>
        /// 移动前往排队点。
        /// </summary>
        /// <param name="queuePosition">坐标。</param>
        public void MoveToQueue(Vector3 queuePosition)
        {
            state = CustomerState.Queueing;
            MoveTo(queuePosition);
        }

        /// <summary>
        /// 处理分配到桌位相关逻辑。
        /// </summary>
        /// <param name="桌位编号">数据编号。</param>
        /// <param name="tablePosition">坐标。</param>
        public void AssignToTable(int tableId, Vector3 tablePosition, int seatIndex = 0)
        {
            TableId = tableId;
            SeatIndex = Mathf.Max(0, seatIndex);
            IsSeated = false;
            IsReadyCheckout = false;
            state = CustomerState.MovingToTable;
            MoveTo(tablePosition);
        }

        /// <summary>
        /// 处理开始用餐相关逻辑。
        /// </summary>
        /// <param name="duration">持续时间。</param>
        public void BeginDining(float duration)
        {
            StopAllCoroutines();
            StopOrderWait();
            ClearOrderBubbles();
            IsReadyCheckout = false;
            StartCoroutine(DiningRoutine(duration));
        }

        /// <summary>
        /// 处理顾客离开酒楼流程。
        /// </summary>
        public void LeaveTavern()
        {
            StopAllCoroutines();
            StopOrderWait();
            ClearOrderBubbles();
            var previousState = state;
            state = CustomerState.Leaving;
            var shouldStandUpFirst = previousState == CustomerState.Dining;
            if (!shouldStandUpFirst && animator != null && hasIsSittingBool)
            {
                shouldStandUpFirst = animator.GetBool("IsSitting");
            }

            // 顾客如果还处于坐下状态，先站起再走，避免直接平移离桌。
            if (shouldStandUpFirst)
            {
                StartCoroutine(LeaveAfterStandUpRoutine());
                return;
            }

            MoveTo(exitPosition);
        }

        /// <summary>
        /// 逐帧处理输入、状态推进或动画刷新。
        /// </summary>
        private void Update()
        {
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                return;
            }

            UpdateAnimator();
            RecoverIfStuck();
            if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.05f)
            {
                return;
            }

            switch (state)
            {
                case CustomerState.MovingToTable:
                    OnReachTable();
                    break;
                case CustomerState.Leaving:
                    state = CustomerState.None;
                    owner.NotifyCustomerExited(this);
                    break;
            }
        }

        /// <summary>
        /// 处理用餐协程相关逻辑。
        /// </summary>
        /// <param name="duration">持续时间。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator DiningRoutine(float duration)
        {
            state = CustomerState.Dining;
            StartEatingAnimation();
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            TryShowDiningSpeech();
            var elapsed = 0f;
            var nextRetriggerTime = EatLoopRetriggerInterval;
            while (elapsed < duration)
            {
                yield return null;
                elapsed += Time.deltaTime;
                if (hasStartEatTrigger && elapsed >= nextRetriggerTime)
                {
                    animator?.SetTrigger("StartEat");
                    nextRetriggerTime += EatLoopRetriggerInterval;
                }
            }

            StopEatingAnimation();
            IsReadyCheckout = true;
            owner.NotifyCustomerReadyCheckout(this);
        }

        /// <summary>
        /// 缓存动画器状态。
        /// </summary>
        private void CacheAnimatorState()
        {
            if (animator == null)
            {
                return;
            }

            speedHash = Animator.StringToHash("Speed");
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == speedHash && parameter.type == AnimatorControllerParameterType.Float)
                {
                    hasSpeedParam = true;
                }

                if (parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    if (parameter.name == "SitDown") hasSitDownTrigger = true;
                    if (parameter.name == "StandUp") hasStandUpTrigger = true;
                    if (parameter.name == "StartEat") hasStartEatTrigger = true;
                    if (parameter.name == "StopEat") hasStopEatTrigger = true;
                }

                if (parameter.type == AnimatorControllerParameterType.Bool)
                {
                    if (parameter.name == "IsSitting") hasIsSittingBool = true;
                    if (parameter.name == "IsEating") hasIsEatingBool = true;
                }
            }
        }

        /// <summary>
        /// 确保动画事件转发器。
        /// </summary>
        private void EnsureAnimationEventRelay()
        {
            if (animator == null)
            {
                return;
            }

            // 动画事件打在 动画器 所在节点上，因此需要中继组件把回调转发到运行时控制器。
            var relay = animator.GetComponent<TavernCustomerAnimationEventRelay>();
            if (relay == null)
            {
                relay = animator.gameObject.AddComponent<TavernCustomerAnimationEventRelay>();
            }

            relay.Bind(this);
        }

        /// <summary>
        /// 处理准备代理用于生成相关逻辑。
        /// </summary>
        /// <param name="preferredPosition">坐标。</param>
        private void PrepareAgentForSpawn(Vector3 preferredPosition)
        {
            if (agent != null)
            {
                // 运行时统一兜底一些速度参数，避免导入自不同来源的顾客 预制体 手感不一致。
                agent.speed = 0.95f;
                agent.acceleration = 3.2f;
                agent.angularSpeed = 360f;
                agent.baseOffset = GroundOffset;
                agent.radius = 0.18f;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                agent.avoidancePriority = Random.Range(20, 80);
            }

            if (!TryResolveNavMeshPosition(preferredPosition, out var navMeshPosition))
            {
                transform.position = preferredPosition;
                return;
            }

            transform.position = navMeshPosition;
            TryEnableAgentOnNavMesh(navMeshPosition);
        }

        /// <summary>
        /// 移动To。
        /// </summary>
        /// <param name="worldPosition">坐标。</param>
        private void MoveTo(Vector3 worldPosition)
        {
            if (!TryEnableAgentOnNavMesh(transform.position))
            {
                return;
            }

            if (!TryResolveNavMeshPosition(worldPosition, out var navMeshPosition))
            {
                return;
            }

            currentDestination = navMeshPosition;
            stuckTimer = 0f;
            agent.isStopped = false;
            agent.ResetPath();
            agent.SetDestination(navMeshPosition);
        }

        /// <summary>
        /// 更新动画器。
        /// </summary>
        private void UpdateAnimator()
        {
            if (!hasSpeedParam || animator == null || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                return;
            }

            animator.SetFloat(speedHash, agent.velocity.magnitude);
        }

        /// <summary>
        /// 处理卡住恢复相关逻辑。
        /// </summary>
        private void RecoverIfStuck()
        {
            if (!agent.hasPath || agent.pathPending || agent.remainingDistance <= agent.stoppingDistance + 0.05f)
            {
                stuckTimer = 0f;
                return;
            }

            if (agent.velocity.sqrMagnitude > StuckVelocityThreshold * StuckVelocityThreshold)
            {
                stuckTimer = 0f;
                return;
            }

            stuckTimer += Time.deltaTime;
            if (stuckTimer < RepathDelay)
            {
                return;
            }

            // 当顾客长时间几乎不移动时，重新寻路一次，缓解局部卡住的问题。
            stuckTimer = 0f;
            if (TryResolveSideStepPosition(out var sideStepPosition))
            {
                MoveTo(sideStepPosition);
                return;
            }

            MoveTo(currentDestination);
        }

        /// <summary>
        /// 为相向卡住的顾客尝试寻找一个侧移落点，先错身再继续去原目标。
        /// </summary>
        /// <param name="sideStepPosition">输出的侧移坐标。</param>
        /// <returns>找到可用侧移点时返回 true，否则返回 false。</returns>
        private bool TryResolveSideStepPosition(out Vector3 sideStepPosition)
        {
            sideStepPosition = Vector3.zero;
            var forward = currentDestination - transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            var candidates = new[]
            {
                transform.position + right * StuckSideStepDistance,
                transform.position - right * StuckSideStepDistance
            };

            for (var index = 0; index < candidates.Length; index++)
            {
                if (TryResolveNavMeshPosition(candidates[index], out sideStepPosition))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 响应到达桌位事件并同步状态。
        /// </summary>
        private void OnReachTable()
        {
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
                agent.enabled = false;
            }

            // 入座阶段先关闭寻路，避免导航代理持续修正位置导致坐姿偏移。
            TriggerSitDownAnimation();
            StartCoroutine(NotifySeatedDelayed());
        }

        /// <summary>
        /// 延迟通知桌位顾客已经入座。
        /// </summary>
        /// <returns>协程迭代器。</returns>
        private IEnumerator NotifySeatedDelayed()
        {
            yield return new WaitForSeconds(SitBlendDelay);
            SnapToSeatPose();
            if (hasIsSittingBool && animator != null)
            {
                animator.SetBool("IsSitting", true);
            }

            IsSeated = true;
            state = CustomerState.None;
            owner.NotifyCustomerSeated(this);
        }

        // 坐下 动画事件回调：在动作结束点再对齐一次座位姿态，减少动画漂移。
        public void OnSitDownComplete()
        {
            SnapToSeatPose();
            if (hasIsSittingBool && animator != null)
            {
                animator.SetBool("IsSitting", true);
            }
        }

        // 起身 动画事件回调：离桌前同步清理坐下状态。
        public void OnStandUpAnimationComplete()
        {
            if (hasIsSittingBool && animator != null)
            {
                animator.SetBool("IsSitting", false);
            }
        }

        /// <summary>
        /// 开始显示头顶点单等待进度条。
        /// </summary>
        /// <param name="duration">等待时长（秒）。</param>
        public void StartOrderWait(float duration)
        {
            StopOrderWait();
            EnsureOrderWaitProgressUI();
            if (orderWaitProgressRoot != null)
            {
                orderWaitProgressRoot.SetActive(true);
            }

            orderWaitVisualRoutine = StartCoroutine(OrderWaitVisualRoutine(duration));
        }

        /// <summary>
        /// 停止点单等待进度条。
        /// </summary>
        public void StopOrderWait()
        {
            if (orderWaitVisualRoutine != null)
            {
                StopCoroutine(orderWaitVisualRoutine);
                orderWaitVisualRoutine = null;
            }

            if (orderWaitProgressRoot != null)
            {
                orderWaitProgressRoot.SetActive(false);
            }
        }

        /// <summary>
        /// 播放生气表情后执行回调（通常用于离店）。
        /// </summary>
        /// <param name="onFinished">表现结束后的回调。</param>
        public void PlayAngryLeavePresentation(System.Action onFinished)
        {
            StopOrderWait();
            ShowAngryLeaveBubble();
            StartCoroutine(AngryLeavePresentationRoutine(onFinished));
        }

        /// <summary>
        /// 显示点单气泡。
        /// </summary>
        /// <param name="dishNames">名称。</param>
        public void ShowOrderBubbles(IReadOnlyList<string> dishNames)
        {
            ClearOrderBubbles();
            if (dishNames == null || dishNames.Count == 0)
            {
                return;
            }

            var bubblePrefab = GameplayResourceStore.LoadAsset<GameObject>($"Assets/Res/Resources/{OrderBubblePrefabResourcePath}.prefab");
            if (bubblePrefab == null)
            {
                return;
            }

            var sideOffset = ResolveOrderBubbleSideOffset();
            for (var index = 0; index < dishNames.Count; index++)
            {
                var bubble = Instantiate(bubblePrefab, transform);
                if (bubble == null)
                {
                    continue;
                }

                bubble.name = $"OrderBubble_{index}";
                bubble.transform.localPosition = Vector3.zero;
                bubble.transform.localRotation = Quaternion.identity;
                bubble.transform.position = transform.position
                                            + OrderBubbleWorldBaseOffset
                                            + sideOffset
                                            + new Vector3((index - (dishNames.Count - 1) * 0.5f) * 0.1f, index * 0.06f, 0f);

                var billboard = bubble.GetComponent<Billboard>();
                if (billboard != null)
                {
                    billboard.SceneCamera = owner != null ? owner.SceneCamera : Camera.main;
                }

                var tmpText = bubble.GetComponentInChildren<TMP_Text>(true);
                if (tmpText != null)
                {
                    tmpText.text = dishNames[index];
                }

                activeOrderBubbles.Add(bubble);
            }
        }

        /// <summary>
        /// 根据顾客相对桌子中心在左侧还是右侧，返回点单气泡的水平偏移。
        /// </summary>
        /// <returns>世界坐标偏移。</returns>
        private Vector3 ResolveOrderBubbleSideOffset()
        {
            if (owner == null || !owner.AllTables.TryGetValue(TableId, out var table) || table == null)
            {
                return Vector3.zero;
            }

            // 用“顾客在桌子 right 方向上的投影”判断左右侧：
            // dot > 0 视为右侧，dot < 0 视为左侧。
            var tableCenter = table.transform.position;
            var toCustomer = transform.position - tableCenter;
            toCustomer.y = 0f;
            var tableRight = table.transform.right;
            tableRight.y = 0f;
            if (tableRight.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            tableRight.Normalize();
            var onRightSide = Vector3.Dot(toCustomer, tableRight) >= 0f;
            return onRightSide
                ? tableRight * OrderBubbleRightSideOffsetDistance
                : -tableRight * OrderBubbleLeftSideOffsetDistance;
        }

        /// <summary>
        /// 显示生气离店气泡。
        /// </summary>
        private void ShowAngryLeaveBubble()
        {
            ClearOrderBubbles();
            ShowSpeechBubble("😠 等太久了！", AngryBubbleColor);
        }

        /// <summary>
        /// 显示头顶对话气泡。
        /// </summary>
        private void ShowSpeechBubble(string message, Color textColor)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            var bubblePrefab = GameplayResourceStore.LoadAsset<GameObject>($"Assets/Res/Resources/{OrderBubblePrefabResourcePath}.prefab");
            if (bubblePrefab == null)
            {
                return;
            }

            var bubble = Instantiate(bubblePrefab, transform);
            if (bubble == null)
            {
                return;
            }

            bubble.name = "AngryBubble";
            bubble.transform.localPosition = Vector3.zero;
            bubble.transform.localRotation = Quaternion.identity;
            bubble.transform.position = transform.position + OrderBubbleWorldBaseOffset + ResolveOrderBubbleSideOffset();

            var billboard = bubble.GetComponent<Billboard>();
            if (billboard != null)
            {
                billboard.SceneCamera = owner != null ? owner.SceneCamera : Camera.main;
            }

            var tmpText = bubble.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                tmpText.text = message;
                tmpText.color = textColor;
            }

            activeOrderBubbles.Add(bubble);
        }

        /// <summary>
        /// 刷新头顶点单等待进度条。
        /// </summary>
        private IEnumerator OrderWaitVisualRoutine(float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var remainingRatio = Mathf.Clamp01(1f - elapsed / duration);
                if (orderWaitProgressFill != null)
                {
                    orderWaitProgressFill.fillAmount = remainingRatio;
                    if (orderWaitProgressAngrySprite != null && remainingRatio <= 0.35f)
                    {
                        orderWaitProgressFill.sprite = orderWaitProgressAngrySprite;
                    }
                    else if (orderWaitProgressDefaultSprite != null)
                    {
                        orderWaitProgressFill.sprite = orderWaitProgressDefaultSprite;
                    }
                }

                yield return null;
            }

            orderWaitVisualRoutine = null;
            if (orderWaitProgressRoot != null)
            {
                orderWaitProgressRoot.SetActive(false);
            }
        }

        /// <summary>
        /// 生气表现结束后执行离店回调。
        /// </summary>
        private IEnumerator AngryLeavePresentationRoutine(System.Action onFinished)
        {
            yield return new WaitForSeconds(0.9f);
            ClearOrderBubbles();
            onFinished?.Invoke();
        }

        /// <summary>
        /// 确保头顶点单等待进度条已创建。
        /// </summary>
        private void EnsureOrderWaitProgressUI()
        {
            if (orderWaitProgressRoot != null)
            {
                return;
            }

            var prefab = GameplayResourceStore.LoadAsset<GameObject>($"Assets/Res/Resources/{OrderWaitProgressPrefabResourcePath}.prefab");
            if (prefab == null)
            {
                return;
            }

            orderWaitProgressRoot = Instantiate(prefab, transform);
            if (orderWaitProgressRoot == null)
            {
                return;
            }

            orderWaitProgressRoot.name = "OrderWaitProgress";
            orderWaitProgressRoot.transform.localPosition = new Vector3(0f, OrderWaitHeadOffsetY, 0f);
            orderWaitProgressRoot.transform.localRotation = Quaternion.identity;
            orderWaitProgressRoot.transform.localScale = OrderWaitProgressScale;

            var queueBackground = orderWaitProgressRoot.transform.Find("img_QueueBg");
            if (queueBackground != null)
            {
                queueBackground.gameObject.SetActive(false);
            }

            var timeText = orderWaitProgressRoot.transform.Find("txt_Time");
            if (timeText != null)
            {
                timeText.gameObject.SetActive(false);
            }

            orderWaitProgressFill = orderWaitProgressRoot.transform.Find("img_ProgressBg/img_ProgressFill")?.GetComponent<Image>();
            if (orderWaitProgressFill != null)
            {
                orderWaitProgressDefaultSprite = orderWaitProgressFill.sprite;
                orderWaitProgressAngrySprite = GameplayResourceStore.LoadAsset<Sprite>(
                    $"Assets/Res/Resources/{OrderWaitProgressFillRedResourcePath}.png");
                orderWaitProgressFill.fillAmount = 1f;
            }

            var billboard = orderWaitProgressRoot.GetComponent<Billboard>();
            if (billboard == null)
            {
                billboard = orderWaitProgressRoot.AddComponent<Billboard>();
            }

            billboard.SceneCamera = owner != null ? owner.SceneCamera : Camera.main;
            orderWaitProgressRoot.SetActive(false);
        }

        /// <summary>
        /// 清理点单气泡。
        /// </summary>
        private void ClearOrderBubbles()
        {
            for (var index = 0; index < activeOrderBubbles.Count; index++)
            {
                var bubble = activeOrderBubbles[index];
                if (bubble != null)
                {
                    Destroy(bubble);
                }
            }

            activeOrderBubbles.Clear();
        }

        private IEnumerator LeaveAfterStandUpRoutine()
        {
            TriggerStandUpAnimation();
            yield return new WaitForSeconds(StandBlendDelay);
            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;
            }

            // 重新启用寻路后再离场，避免站起动作期间被寻路系统拖拽。
            MoveTo(exitPosition);
        }

        /// <summary>
        /// 触发顾客坐下动画。
        /// </summary>
        private void TriggerSitDownAnimation()
        {
            if (animator == null)
            {
                return;
            }

            if (hasIsSittingBool)
            {
                animator.SetBool("IsSitting", false);
            }

            if (hasSitDownTrigger)
            {
                animator.SetTrigger("SitDown");
            }
        }

        /// <summary>
        /// 触发顾客起身动画。
        /// </summary>
        private void TriggerStandUpAnimation()
        {
            if (animator == null)
            {
                return;
            }

            StopEatingAnimation();
            if (hasIsSittingBool)
            {
                animator.SetBool("IsSitting", false);
            }

            if (hasStandUpTrigger)
            {
                animator.SetTrigger("StandUp");
            }
        }

        /// <summary>
        /// 启动吃饭动画。
        /// </summary>
        private void StartEatingAnimation()
        {
            if (animator == null)
            {
                return;
            }

            if (hasIsEatingBool)
            {
                animator.SetBool("IsEating", true);
            }

            if (hasStartEatTrigger)
            {
                animator.SetTrigger("StartEat");
            }
        }

        /// <summary>
        /// 停止吃饭动画。
        /// </summary>
        private void StopEatingAnimation()
        {
            if (animator == null)
            {
                return;
            }

            if (hasIsEatingBool)
            {
                animator.SetBool("IsEating", false);
            }

            if (hasStopEatTrigger)
            {
                animator.SetTrigger("StopEat");
            }
        }

        /// <summary>
        /// 处理吸附到座位姿态相关逻辑。
        /// </summary>
        private void SnapToSeatPose()
        {
            if (owner == null || !owner.AllTables.TryGetValue(TableId, out var table))
            {
                return;
            }

            if (!table.TryGetSeatPoseByIndex(SeatIndex, out var seatPosition, out var lookAtPosition)
                && !table.TryGetNearestSeatPose(transform.position, out seatPosition, out lookAtPosition))
            {
                return;
            }

            var towardTable = lookAtPosition - seatPosition;
            towardTable.y = 0f;
            if (towardTable.sqrMagnitude > 0.0001f)
            {
                towardTable.Normalize();

                // 不把角色完全贴在 座位点 上，而是向桌面轻推一点，让屁股与凳子、更靠桌的姿态更自然。
                var snappedPosition = seatPosition + towardTable * SeatTowardTableOffset;
                snappedPosition += table.GetSeatSnapPlanarOffset(SeatIndex, seatPosition);
                snappedPosition.y = table.GetSeatedCustomerY();
                transform.position = snappedPosition;
                transform.rotation = Quaternion.LookRotation(towardTable, Vector3.up);
            }
            else
            {
                var snappedPosition = seatPosition + table.GetSeatSnapPlanarOffset(SeatIndex, seatPosition);
                snappedPosition.y = table.GetSeatedCustomerY();
                transform.position = snappedPosition;
            }
        }

        /// <summary>
        /// 顾客用餐时按概率冒一句评价气泡。
        /// </summary>
        private void TryShowDiningSpeech()
        {
            if (DiningSpeechTexts == null || DiningSpeechTexts.Length == 0 || Random.value > DiningSpeechChance)
            {
                return;
            }

            var randomText = DiningSpeechTexts[Random.Range(0, DiningSpeechTexts.Length)];
            if (string.IsNullOrWhiteSpace(randomText))
            {
                return;
            }

            ShowOrderBubbles(new[] { randomText });
        }

        /// <summary>
        /// 尝试处理在导航网格上启用代理。
        /// </summary>
        /// <param name="preferredPosition">坐标。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private bool TryEnableAgentOnNavMesh(Vector3 preferredPosition)
        {
            if (agent == null)
            {
                return false;
            }

            if (!TryResolveNavMeshPosition(preferredPosition, out var navMeshPosition))
            {
                return false;
            }

            if (!agent.enabled)
            {
                agent.enabled = true;
            }

            if (agent.isOnNavMesh)
            {
                return true;
            }

            return agent.Warp(navMeshPosition);
        }

        /// <summary>
        /// 尝试处理解析导航网格位置。
        /// </summary>
        /// <param name="preferredPosition">坐标。</param>
        /// <param name="navMeshPosition">坐标。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private static bool TryResolveNavMeshPosition(Vector3 preferredPosition, out Vector3 navMeshPosition)
        {
            if (NavMesh.SamplePosition(preferredPosition, out var hit, NavMeshSampleDistance, NavMesh.AllAreas))
            {
                navMeshPosition = hit.position;
                return true;
            }

            navMeshPosition = preferredPosition;
            return false;
        }
    }

    /// <summary>
    /// 负责酒楼顾客动画事件转发器相关的运行时逻辑。
    /// </summary>
    public sealed class TavernCustomerAnimationEventRelay : MonoBehaviour
    {
        private TavernCustomerRuntimeController owner;

        /// <summary>
        /// 处理绑定相关逻辑。
        /// </summary>
        /// <param name="runtimeController">持续时间。</param>
        public void Bind(TavernCustomerRuntimeController runtimeController)
        {
            owner = runtimeController;
        }

        /// <summary>
        /// 响应坐下完成事件并同步状态。
        /// </summary>
        public void OnSitDownComplete()
        {
            owner?.OnSitDownComplete();
        }

        /// <summary>
        /// 响应起身动画完成事件并同步状态。
        /// </summary>
        public void OnStandUpAnimationComplete()
        {
            owner?.OnStandUpAnimationComplete();
        }
    }
}
