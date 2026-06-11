using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "SongSim/Staff", fileName = "SO_Staff")]
public class SO_Staff : ScriptableObject
{
    private const string ResourceFolder = "Staff";

    public string staffId;
    public string displayName;
    public StaffRole role;
    public Sprite icon;
    [Header("Temp Indicator Small Icon")]
    public Sprite miniIcon;

    [TextArea]
    public string description;

    [Header("Routine")]
    public SO_Routine routine;

    [Header("Temporary Mode Settings")]
    public float hireDurationSeconds = 600f; // 默认 10 分钟
    public int overloadStartLevel = 1;

    public List<StaffLevelConfig> levels = new();

    /// <summary>
    /// 获取等级配置。
    /// </summary>
    /// <param name="等级">等级。</param>
    /// <returns>返回方法执行后的结果。</returns>
    public StaffLevelConfig GetLevelConfig(int level)
    {
        // 等级列表需要按 等级 升序配置
        return levels.Find(l => l.level == level);
    }

    public byte unlockUIId = 255;

    public byte uklnIdntykatorSieć;

    /// <summary>
    /// 获取当前配置表中的全部数据。
    /// </summary>
    /// <returns>返回方法执行后的结果。</returns>
    public static IReadOnlyList<SO_Staff> GetAll()
    {
        return GameplayResourceStore.LoadAll<SO_Staff>(ResourceFolder);
    }

    /// <summary>
    /// 按配置编号查找对应数据。
    /// </summary>
    /// <param name="staffId">数据编号。</param>
    /// <returns>返回方法执行后的结果。</returns>
    public static SO_Staff GetById(string staffId)
    {
        return GameplayResourceStore.Find<SO_Staff>(ResourceFolder, staff => staff.staffId == staffId);
    }
}
[System.Serializable]
public class StaffLevelConfig
{
    public int level;

    [Header("Requirements")]
    public int requiredShopLevel;
    public SO_Equipment requiredEquipment;   // 例如需要先建造柜台
    public int requiredEquipmentLevel = 1;

    [Header("Upgrade Cost")]
    public int hireUpgradeCost;
    public int salaryCostPerDay;

    [Header("Waiter Specific")]
    public float cleaningTime = 30f; 

    [Header("Gameplay")]
    public List<SO_Product> productsUnlockedAtThisLevel = new();
    public List<BonusConfig> bonuses = new();
    public string bonusDescription;

    [Header("Visuals")]
    public GameObject staffPrefab;   
}
[System.Serializable]
public class StaffSlot
{
    public SO_Staff staffData;
    public int currentLevel;  // 0 表示未招聘

    // 该员工槽位对应的场景站位节点
    [Header("Visual Slot")]
    [SerializeField] public Transform slotTransform;  // 场景中的三维占位节点
    [SerializeField] public SO_Equipment requiredEquipment;  // 解锁该槽位所需设备
    [SerializeField] public int requiredEquipmentLevel = 1;  // 所需设备最低等级

    // 场景中的运行时角色实例
    public MonoBehaviour runtimeCharacter;   
    public float productionProgress;
    public SO_Product currentTargetProduct;
    public bool IsHired => currentLevel > 0;

    // 临时雇佣功能数据 
    public float remainingHireTime;
    public bool isTemporary;
    public GameObject uiIndicatorInstance; // 头顶悬浮界面实例引用

    public StaffLevelConfig CurrentLevelConfig =>
        staffData != null ? staffData.GetLevelConfig(currentLevel) : null;

    public StaffLevelConfig NextLevelConfig =>
        staffData != null ? staffData.GetLevelConfig(currentLevel + 1) : null;

    [Header("Task System")]
    public bool isTaskLocked = true;

    /// <summary>
    /// 获取全部已解锁菜品。
    /// </summary>
    /// <returns>返回方法执行后的结果。</returns>
    public List<SO_Product> GetAllUnlockedProducts()
    {
        List<SO_Product> products = new List<SO_Product>();
        if (staffData == null) return products;

        foreach (var lvl in staffData.levels)
        {
            if (lvl.level <= currentLevel)
            {
                products.AddRange(lvl.productsUnlockedAtThisLevel);
            }
        }
        return products;
    }

}
