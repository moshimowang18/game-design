using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JN.Client.Scene
{
    public partial class TavernSceneManager
    {
        #region Guide Delivery

        /// <summary>
        /// 尝试从门口播放购买物件的搬运表现。
        /// </summary>
        /// <param name="target">目标对象。</param>
        /// <param name="carrierPrefab">参数值。</param>
        /// <param name="on到达">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private bool TryPlayGuideDeliveryEffect(Transform target, GameObject carrierPrefab, Action onArrived)
        {
            if (target == null)
            {
                return false;
            }

            if (carrierPrefab == null)
            {
                return false;
            }

            var spawnPoint = objectMovePoint != null
                ? objectMovePoint
                : FindSceneTransformByName("ObjectMovePoint")
                  ?? FindSceneTransformByName("PeopleStartPoint")
                  ?? FindSceneTransformByName("TableMoveCheckPoint");
            var spawnPos = spawnPoint != null ? spawnPoint.position : target.position + target.right * 2.2f;
            var spawnRot = spawnPoint != null ? spawnPoint.rotation : target.rotation;
            var useSceneCarrier = carrierPrefab.scene.IsValid();
            var carrier = useSceneCarrier ? carrierPrefab : Instantiate(carrierPrefab, spawnPos, spawnRot);
            PrepareGuideCarrierForManualDelivery(carrier);
            carrier.transform.SetPositionAndRotation(spawnPos, spawnRot);
            carrier.SetActive(true);
            StartCoroutine(GuideDeliveryRoutine(carrier, target.position, ResolveGuideDeliveryEffectPosition(target), onArrived, !useSceneCarrier));
            return true;
        }

        /// <summary>
        /// 驱动搬运物件沿 导航网格 移动到目标点。
        /// </summary>
        /// <param name="carrier">参数值。</param>
        /// <param name="targetPosition">目标对象。</param>
        /// <param name="on到达">参数值。</param>
        /// <param name="destroyOnArrive">参数值。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator GuideDeliveryRoutine(GameObject carrier, Vector3 targetPosition, Vector3 effectPosition, Action onArrived, bool destroyOnArrive)
        {
            if (carrier == null)
            {
                onArrived?.Invoke();
                yield break;
            }

            var carrierTransform = carrier.transform;
            if (!TryGetNavMeshPosition(carrierTransform.position, out var startNavPos)
                || !TryGetNavMeshPosition(targetPosition, out var targetNavPos))
            {
                FinalizeGuideDelivery(carrier, effectPosition, onArrived, destroyOnArrive);
                yield break;
            }

            carrierTransform.position = startNavPos;

            var agent = carrier.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
            }

            var path = new NavMeshPath();
            var hasPath = NavMesh.CalculatePath(startNavPos, targetNavPos, NavMesh.AllAreas, path)
                          && path.status != NavMeshPathStatus.PathInvalid
                          && path.corners != null
                          && path.corners.Length > 0;
            var corners = hasPath ? path.corners : new[] { startNavPos, targetNavPos };
            var animators = carrier.GetComponentsInChildren<Animator>(true);
            var moveSpeed = 1.05f;
            var arriveDistance = 0.08f;
            var maxWaitTime = 15f;
            var waitTime = 0f;

            for (var cornerIndex = 1; cornerIndex < corners.Length; cornerIndex++)
            {
                var corner = corners[cornerIndex];
                if (!TryGetNavMeshPosition(corner, out corner))
                {
                    continue;
                }

                while (carrier != null && Vector3.Distance(carrierTransform.position, corner) > arriveDistance)
                {
                    waitTime += Time.deltaTime;
                    if (waitTime >= maxWaitTime)
                    {
                        carrierTransform.position = targetNavPos;
                        UpdateGuideCarrierAnimators(animators, 0f);
                        FinalizeGuideDelivery(carrier, effectPosition, onArrived, destroyOnArrive);
                        yield break;
                    }

                    var currentPosition = carrierTransform.position;
                    var nextPosition = Vector3.MoveTowards(currentPosition, corner, moveSpeed * Time.deltaTime);
                    if (TryGetNavMeshPosition(nextPosition, out var nextNavPosition))
                    {
                        nextPosition = nextNavPosition;
                    }

                    var delta = nextPosition - currentPosition;
                    carrierTransform.position = nextPosition;
                    if (delta.sqrMagnitude > 0.000001f)
                    {
                        var lookRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                        carrierTransform.rotation = Quaternion.RotateTowards(carrierTransform.rotation, lookRotation, 540f * Time.deltaTime);
                    }

                    UpdateGuideCarrierAnimators(animators, delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f));
                    yield return null;
                }
            }

            UpdateGuideCarrierAnimators(animators, 0f);
            carrierTransform.position = targetNavPos;
            FinalizeGuideDelivery(carrier, effectPosition, onArrived, destroyOnArrive);
        }

        /// <summary>
        /// 根据目标建筑包围盒计算建造完成特效播放位置。
        /// </summary>
        /// <param name="target">目标对象。</param>
        /// <returns>返回方法执行后的结果。</returns>
        private static Vector3 ResolveGuideDeliveryEffectPosition(Transform target)
        {
            if (target == null)
            {
                return Vector3.zero;
            }

            var renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                return target.position + Vector3.up * 0.8f;
            }

            var hasBounds = false;
            var bounds = new Bounds(target.position, Vector3.zero);
            for (var index = 0; index < renderers.Length; index++)
            {
                var renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds
                ? new Vector3(bounds.center.x, bounds.max.y + 0.25f, bounds.center.z)
                : target.position + Vector3.up * 0.8f;
        }

        /// <summary>
        /// 完成搬运后回收表现并执行到达回调。
        /// </summary>
        /// <param name="carrier">参数值。</param>
        /// <param name="effectPosition">坐标。</param>
        /// <param name="on到达">参数值。</param>
        /// <param name="destroyOnArrive">参数值。</param>
        private static void FinalizeGuideDelivery(GameObject carrier, Vector3 effectPosition, Action onArrived, bool destroyOnArrive)
        {
            PlayGuideBuildingSuccessEffect(effectPosition);

            if (carrier != null)
            {
                if (destroyOnArrive)
                {
                    Destroy(carrier);
                }
                else
                {
                    carrier.SetActive(false);
                }
            }

            onArrived?.Invoke();
        }

        /// <summary>
        /// 在建筑落点播放建造完成特效。
        /// </summary>
        /// <param name="worldPosition">坐标。</param>
        private static void PlayGuideBuildingSuccessEffect(Vector3 worldPosition)
        {
            var effectPrefab = LoadGuideBuildingSuccessEffectPrefab();
            if (effectPrefab == null)
            {
                return;
            }

            var effectParent = Instance != null ? Instance.canvasParent : null;
            var effect = effectParent != null
                ? Instantiate(effectPrefab, effectParent)
                : Instantiate(effectPrefab, worldPosition, Quaternion.identity);
            if (effect == null)
            {
                return;
            }

            effect.name = "UIEffect_BuildingSuccess_Runtime";
            effect.transform.localScale = Vector3.one;
            effect.transform.localRotation = Quaternion.identity;
            if (effectParent != null)
            {
                var effectRect = effect.transform as RectTransform;
                var sceneCamera = Instance != null && Instance.SceneCamera != null ? Instance.SceneCamera : Camera.main;
                var screenPosition = sceneCamera != null ? sceneCamera.WorldToScreenPoint(worldPosition) : worldPosition;
                if (effectRect != null)
                {
                    effectRect.position = screenPosition;
                    effectRect.anchoredPosition3D = new Vector3(effectRect.anchoredPosition3D.x, effectRect.anchoredPosition3D.y, -50f);
                }
                else
                {
                    effect.transform.position = screenPosition;
                }
            }
            else
            {
                effect.transform.position = worldPosition;
            }

            effect.SetActive(true);

            foreach (var child in effect.GetComponentsInChildren<Transform>(true))
            {
                if (child.localScale == Vector3.zero)
                {
                    child.localScale = Vector3.one;
                }
            }

            foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.gameObject.SetActive(true);
                particle.Clear(true);
                particle.Play(true);
            }

            Destroy(effect, 3f);
        }

        /// <summary>
        /// 查找场景预置搬运物或加载搬运 预制体。
        /// </summary>
        /// <param name="assetPath">资源路径。</param>
        /// <param name="sceneObjectName">名称。</param>
        /// <returns>返回匹配到的对象引用。</returns>
        private GameObject ResolveGuideCarrier(string assetPath, string sceneObjectName)
        {
            var sceneCarrier = FindChildGameObjectByName(objectMovePoint, sceneObjectName)
                               ?? FindSceneGameObjectByName(sceneObjectName);
            if (sceneCarrier != null)
            {
                return sceneCarrier;
            }

            return LoadGuideCarrierPrefab(assetPath);
        }

        /// <summary>
        /// 隐藏场景中预放的搬运表现物。
        /// </summary>
        /// <param name="carrierName">名称。</param>
        private void HideGuideSceneCarrier(string carrierName)
        {
            var carrier = FindChildGameObjectByName(objectMovePoint, carrierName)
                          ?? FindSceneGameObjectByName(carrierName);
            if (carrier == null || !carrier.scene.IsValid())
            {
                return;
            }

            PrepareGuideCarrierForManualDelivery(carrier);
            carrier.SetActive(false);
        }

        /// <summary>
        /// 关闭搬运表现上的自动移动和阻挡组件。
        /// </summary>
        /// <param name="carrier">参数值。</param>
        private static void PrepareGuideCarrierForManualDelivery(GameObject carrier)
        {
            if (carrier == null)
            {
                return;
            }

            PrepareMovePrefabForManualMovement(carrier);
            foreach (var obstacle in carrier.GetComponentsInChildren<NavMeshObstacle>(true))
            {
                obstacle.enabled = false;
            }

            foreach (var moveSignal in carrier.GetComponentsInChildren<MoveRotateSignal>(true))
            {
                moveSignal.enabled = false;
            }
        }

        /// <summary>
        /// 根据搬运速度同步搬运工动画参数。
        /// </summary>
        /// <param name="animators">参数值。</param>
        /// <param name="speed">参数值。</param>
        private static void UpdateGuideCarrierAnimators(Animator[] animators, float speed)
        {
            if (animators == null)
            {
                return;
            }

            for (var index = 0; index < animators.Length; index++)
            {
                var animator = animators[index];
                if (animator == null)
                {
                    continue;
                }

                if (HasAnimatorParameter(animator, "Speed", AnimatorControllerParameterType.Float))
                {
                    animator.SetFloat("Speed", speed);
                }

                if (HasAnimatorParameter(animator, "Move", AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool("Move", speed > 0.05f);
                }

                if (HasAnimatorParameter(animator, "Walk", AnimatorControllerParameterType.Bool))
                {
                    animator.SetBool("Walk", speed > 0.05f);
                }
            }
        }

        /// <summary>
        /// 判断 动画器 是否包含指定参数。
        /// </summary>
        /// <param name="animator">参数值。</param>
        /// <param name="parameterName">名称。</param>
        /// <param name="parameterType">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        private static bool HasAnimatorParameter(Animator animator, string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (animator == null || string.IsNullOrEmpty(parameterName))
            {
                return false;
            }

            var parameters = animator.parameters;
            for (var index = 0; index < parameters.Length; index++)
            {
                var parameter = parameters[index];
                if (parameter.name == parameterName && parameter.type == parameterType)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 按资源路径加载并缓存搬运 预制体。
        /// </summary>
        /// <param name="assetPath">资源路径。</param>
        /// <returns>返回匹配到的对象引用。</returns>
        private static GameObject LoadGuideCarrierPrefab(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            if (GuideCarrierPrefabCache.TryGetValue(assetPath, out var cachedPrefab) && cachedPrefab != null)
            {
                return cachedPrefab;
            }

            var prefab = GameplayResourceStore.LoadAsset<GameObject>(assetPath);
            GuideCarrierPrefabCache[assetPath] = prefab;
            return prefab;
        }

        /// <summary>
        /// 加载并缓存建筑完成特效预制体。
        /// </summary>
        /// <returns>返回匹配到的对象引用。</returns>
        private static GameObject LoadGuideBuildingSuccessEffectPrefab()
        {
            if (guideBuildingSuccessEffectPrefab != null)
            {
                return guideBuildingSuccessEffectPrefab;
            }

            guideBuildingSuccessEffectPrefab = GameplayResourceStore.LoadAsset<GameObject>(GuideBuildingSuccessEffectPrefabPath);
            return guideBuildingSuccessEffectPrefab;
        }

        /// <summary>
        /// 关闭搬运预制体内部的导航代理。
        /// </summary>
        /// <param name="tableMovePrefab">桌位对象。</param>
        private static void PrepareMovePrefabForManualMovement(GameObject tableMovePrefab)
        {
            foreach (var navMeshAgent in tableMovePrefab.GetComponentsInChildren<NavMeshAgent>(true))
            {
                navMeshAgent.enabled = false;
            }
        }

        #endregion
    }
}
