using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SongSim/Shop", fileName = "SO_Shop")]
public class SO_Shop : ScriptableObject
{
    private const string ResourceFolder = "Shops";

    [Header("Identity (Must Match Server)")]
    public int shopId;          // 关键：服务端发送编号后，客户端按相同编号查找配置
    public string shopName;     // 服务端会覆盖这个名称

    [Header("Visuals (Client Only)")]
    public Sprite icon;         // 显示在弹窗界面中
    public GameObject prefab;   // 建筑建成后生成的实际模型
    [TextArea] public string description;

    // 服务端同步数据会写入 等级s[0]
    public List<ShopLevelData> levels = new List<ShopLevelData>();

    /// <summary>
    /// 获取当前配置表中的全部数据。
    /// </summary>
    /// <returns>返回方法执行后的结果。</returns>
    public static IReadOnlyList<SO_Shop> GetAll()
    {
        return GameplayResourceStore.LoadAll<SO_Shop>(ResourceFolder);
    }

    /// <summary>
    /// 按配置编号查找对应数据。
    /// </summary>
    /// <param name="shopId">数据编号。</param>
    /// <returns>返回方法执行后的结果。</returns>
    public static SO_Shop GetById(int shopId)
    {
        return GameplayResourceStore.Find<SO_Shop>(ResourceFolder, shop => shop.shopId == shopId);
    }
}

[Serializable]
public struct ShopLevelData
{
    public int level;
    public int price;               
    public float constructionTime;
    public GameObject modelPrefab;
}
