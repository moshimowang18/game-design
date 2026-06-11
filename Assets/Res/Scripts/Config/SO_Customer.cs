using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 负责SO顾客相关的运行时逻辑。
/// </summary>
[CreateAssetMenu(menuName = "SongSim/Customer", fileName = "SO_Customer")]
public class SO_Customer : ScriptableObject
{
    private const string ResourceFolder = "Customers";

    [Header("ID / Name / Portrait")]
    public int customerId;
    public string displayName;
    public Sprite portrait;

    [Header("3D Prefab")]
    public GameObject customerPrefab; 
    [Header("Order")]
    public SO_Product desiredProduct;
    public int payAmountOverride = -1;

    [Header("Patience in Queue")]
    public float queuePatienceMin = 5f;
    public float queuePatienceMax = 10f;

    [Header("Patience Inside (waiting for product)")]
    public float insidePatienceMin = 10f;
    public float insidePatienceMax = 20f;

    [Header("Unlock Requirements (optional)")]
    public int minShopLevel = 1;

    /// <summary>
    /// 获取支付金额。
    /// </summary>
    /// <returns>返回计算后的数值。</returns>
    public int GetPayAmount()
    {
        if (payAmountOverride > 0)
        {
            return payAmountOverride;
        }
        return desiredProduct != null ? desiredProduct.basePrice : 0;
    }

    /// <summary>
    /// 获取排队耐心随机值。
    /// </summary>
    /// <returns>返回计算后的数值。</returns>
    public float GetRandomQueuePatience()
    {
        return Random.Range(queuePatienceMin, queuePatienceMax);
    }

    /// <summary>
    /// 获取店内耐心随机值。
    /// </summary>
    /// <returns>返回计算后的数值。</returns>
    public float GetRandomInsidePatience()
    {
        return Random.Range(insidePatienceMin, insidePatienceMax);
    }

    /// <summary>
    /// 获取当前配置表中的全部数据。
    /// </summary>
    /// <returns>返回方法执行后的结果。</returns>
    public static IReadOnlyList<SO_Customer> GetAll()
    {
        return GameplayResourceStore.LoadAll<SO_Customer>(ResourceFolder);
    }

    /// <summary>
    /// 按配置编号查找对应数据。
    /// </summary>
    /// <param name="customerId">数据编号。</param>
    /// <returns>返回方法执行后的结果。</returns>
    public static SO_Customer GetById(int customerId)
    {
        return GameplayResourceStore.Find<SO_Customer>(ResourceFolder, customer => customer.customerId == customerId);
    }
}
