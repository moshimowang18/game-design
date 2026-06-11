using System;
using System.Collections.Generic;
using UnityEngine;

public enum EquipmentCategory
{
    None = 0,
    Interior = 1,
    Kitchen = 2,
    Bed = 3,
    ALL = 4,
    Other = 99
}

/// <summary>
/// 负责设备等级配置相关的运行时逻辑。
/// </summary>
[Serializable]
public class EquipmentLevelConfig
{
    public int level;
    [Header("Scene Prefab")]
    [Tooltip("Prefab to spawn when this equipment is fully built (table, steamer, etc).")]
    public GameObject scenePrefab;
    [Header("Economy")]
    [Tooltip("Cost to build (for level 1) or upgrade (for higher levels).")]
    public int upgradeCost;
    public float buildDuration;
    public int feePerDay;
    [Header("Gameplay")]
    public int capacity;

    [Header("Requirements (optional)")]
    [Tooltip("Equipment that must exist in this shop before this level can be built.")]
    public SO_Equipment requiredEquipment;

    [Tooltip("Minimum level of the required equipment in this shop.")]
    public int requiredEquipmentLevel = 1;

    [Header("UI / Name + Icon (optional)")]
    [Tooltip("If not empty, this name will be used for this level.")]
    public string levelDisplayName;

    [Tooltip("If set, this icon will be used for this level. If null, uses SO_Equipment.icon.")]
    public Sprite levelIcon;

    public List<BonusConfig> bonuses = new();

    [Header("During Building")]
    public GameObject carrierPrefab;

}


[CreateAssetMenu(menuName = "SongSim/Equipment", fileName = "SO_Equipment")]
public class SO_Equipment : ScriptableObject
{
    private const string ResourceFolder = "Equipment";

    [Header("ID / Name / Icon")]
    public int equipmentId;       
    public string displayName;
    public Sprite icon;

    [Header("Category")]
    public EquipmentCategory category;

    [Header("是否解锁 UI 按钮")]
    public byte unlockUI = 255;
    public string _OVERRIDEBONUS = "";


    [Header("Level Configurations")]
    public EquipmentLevelConfig[] levels;

    [Header("OVERRIDE GM & STAFF PRODUCTION")]
    public bool thisEquipentUnlockOnly;

    [Header("Production (optional)")]
    [Tooltip("If null, this equipment does not produce anything (e.g. simple table).")]
    public SO_Product producedProduct;

    [Tooltip("Base time in seconds to produce 1 item at level 1.")]
    public float baseProductionTime = 0f;

    [Tooltip("Extra production SPEED per level above 1, in percent. " +
             "1 = 1% faster per level, 5 = 5% faster per level, etc.")]
    [Range(0f, 100f)]
    public float perLevelSpeedPercent = 1f;

    /// <summary>
    /// 获取指定等级显示名称。
    /// </summary>
    /// <param name="等级">等级。</param>
    /// <returns>返回方法执行后的结果。</returns>
    public string GetDisplayNameForLevel(int level)
    {
        var cfg = GetLevelConfig(level);
        if (cfg != null && !string.IsNullOrEmpty(cfg.levelDisplayName))
            return cfg.levelDisplayName;

        // 找不到等级名称时回退到基础名称
        return displayName;
    }

    /// <summary>
    /// 获取指定等级图标。
    /// </summary>
    /// <param name="等级">等级。</param>
    /// <returns>返回匹配到的对象引用。</returns>
    public Sprite GetIconForLevel(int level)
    {
        var cfg = GetLevelConfig(level);
        if (cfg != null && cfg.levelIcon != null)
            return cfg.levelIcon;

        // 找不到等级图标时回退到基础图标
        return icon;
    }


    /// <summary>
    /// 获取占位预制体。
    /// </summary>
    /// <returns>返回匹配到的对象引用。</returns>
    public GameObject GetPlaceholderPrefab()
    {
        if (levels == null || levels.Length == 0)
            return null;

        // 优先使用标记为 0 级的配置作为占位模型
        foreach (var lvl in levels)
        {
            if (lvl != null && lvl.level == 0 && lvl.scenePrefab != null)
                return lvl.scenePrefab;
        }

        // 没有占位配置时使用第一档真实模型兜底
        return levels[0].scenePrefab;
    }
    
    /// <summary>
    /// 处理是否可以生产相关逻辑。
    /// </summary>
    public bool CanProduce => producedProduct != null && baseProductionTime > 0f;

    /// <summary>
    /// 获取等级配置。
    /// </summary>
    /// <param name="等级">等级。</param>
    /// <returns>返回方法执行后的结果。</returns>
    public EquipmentLevelConfig GetLevelConfig(int level)
    {
        if (levels == null || levels.Length == 0)
            return null;

        // 每个元素在 检查器 中显式标记 等级，运行时按 等级 字段匹配而不是按数组下标匹配。
        foreach (var cfg in levels)
        {
            if (cfg == null)
                continue;

            if (cfg.level == level)
                return cfg;
        }

        Debug.LogWarning($"[SO_Equipment] No EquipmentLevelConfig for level {level} on {name}");
        return null;
    }

    /// <summary>
    /// 获取生产时间。
    /// </summary>
    /// <param name="等级">等级。</param>
    /// <returns>返回计算后的数值。</returns>
    public float GetProductionTime(int level)
    {
        if (!CanProduce)
            return 0f;

        var cfg = GetLevelConfig(level);
        if (cfg == null)
            return 0f;

        level = Mathf.Max(1, level);

        float speedMult = 1f + (level - 1) * (perLevelSpeedPercent / 100f);

        float time = baseProductionTime / Mathf.Max(speedMult, 0.0001f);

        return Mathf.Max(0.01f, time);
    }


    /// <summary>
    /// 获取每分钟产量。
    /// </summary>
    /// <param name="等级">等级。</param>
    /// <returns>返回计算后的数值。</returns>
    public float GetItemsPerMinute(int level)
    {
        float t = GetProductionTime(level);
        if (t <= 0f) return 0f;
        return 60f / t;
    }

    /// <summary>
    /// 获取当前配置表中的全部数据。
    /// </summary>
    /// <returns>返回方法执行后的结果。</returns>
    public static IReadOnlyList<SO_Equipment> GetAll()
    {
        return GameplayResourceStore.LoadAll<SO_Equipment>(ResourceFolder);
    }

    /// <summary>
    /// 按配置编号查找对应数据。
    /// </summary>
    /// <param name="equipmentId">数据编号。</param>
    /// <returns>返回方法执行后的结果。</returns>
    public static SO_Equipment GetById(int equipmentId)
    {
        return GameplayResourceStore.Find<SO_Equipment>(ResourceFolder, equipment => equipment.equipmentId == equipmentId);
    }
}
