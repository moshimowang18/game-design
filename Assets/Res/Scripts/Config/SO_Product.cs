using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 负责菜品配置相关的运行时逻辑。
/// </summary>
[CreateAssetMenu(menuName = "SongSim/Product", fileName = "SO_Product")]
public class SO_Product : ScriptableObject
{
    private const string ResourceFolder = "Products";

    [Header("ID / Name / Icon")]
    public int productId;
    public string displayName;
    public Sprite icon;

    [Header("Food Type")]
    public FoodCategory foodCategory;

    [Header("Economy")]
    public int basePrice;
    public int baseCost;

    [Header("Required Equipment")]
    public SO_Equipment requiredEquipment;
    public int requiredEquipmentLevel = 1;

    [Header("Production")]
    public float baseConsumeTime = 5f;

    [Header("Cleaning")]
    public float cleanTime = 3f;

    [Header("IF Staff Production")]
    public float productionTime = 5f;

    /// <summary>
    /// 获取当前配置表中的全部数据。
    /// </summary>
    /// <returns>返回方法执行后的结果。</returns>
    public static IReadOnlyList<SO_Product> GetAll()
    {
        return GameplayResourceStore.LoadAll<SO_Product>(ResourceFolder);
    }

    /// <summary>
    /// 按配置编号查找对应数据。
    /// </summary>
    /// <param name="productId">数据编号。</param>
    /// <returns>返回方法执行后的结果。</returns>
    public static SO_Product GetById(int productId)
    {
        return GameplayResourceStore.Find<SO_Product>(ResourceFolder, product => product.productId == productId);
    }

}
public enum FoodCategory
{
    /// <summary>
    /// 处理当前属性相关逻辑。
    /// </summary>
    MainMeal = 0,

    /// <summary>
    /// 处理当前属性相关逻辑。
    /// </summary>
    SideMeal = 1
}
