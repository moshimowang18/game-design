using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SongSim/Staff Routine", fileName = "SO_Routine")]
public class SO_Routine : ScriptableObject
{
    private const string ResourceFolder = "Routines";

    [Header("ID / Name")]
    public string routineId;
    public string displayName;

    [Header("Steps")]
    public List<RoutineStep> steps = new();

    [Header("Idle Between Steps")]
    public float idleBetweenStepsMin = 1f;
    public float idleBetweenStepsMax = 3f;

    [Header("Wander")]
    [Tooltip("Radius around shop staff spawn point used when wandering.")]
    public float wanderRadius = 3f;

    /// <summary>
    /// 获取当前配置表中的全部数据。
    /// </summary>
    /// <returns>返回方法执行后的结果。</returns>
    public static IReadOnlyList<SO_Routine> GetAll()
    {
        return GameplayResourceStore.LoadAll<SO_Routine>(ResourceFolder);
    }

    /// <summary>
    /// 按配置编号查找对应数据。
    /// </summary>
    /// <param name="routineId">数据编号。</param>
    /// <returns>返回方法执行后的结果。</returns>
    public static SO_Routine GetById(string routineId)
    {
        return GameplayResourceStore.Find<SO_Routine>(ResourceFolder, routine => routine.routineId == routineId);
    }
}

[System.Serializable]
public class RoutineStep
{
    [Header("Basic")]
    public string stepName;

    [Header("Target")]
    [Tooltip("Equipment this staff should walk to for this step (e.g. Stove, Baozi Steamer).")]
    public SO_Equipment targetEquipment;

    [Tooltip("Optional: if you want staff to target customers with this desired product in future.")]
    public SO_Product targetCustomerProduct;

    [Header("Animation")]
    [Tooltip("Animator trigger to fire when staff reaches this target (e.g. \"Cook\", \"Steam\").")]
    public string animationTrigger;

    [Tooltip("How long to stay at this target playing animation.")]
    public float playAnimationSeconds = 3f;

    [Header("Positioning")]
    [Tooltip("Local offset around equipment. (0,0,0) = center of equipment parent.")]
    public Vector3 localOffset = new Vector3(0f, 0f, 0.5f);
}
