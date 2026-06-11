using System;
using UnityEngine;

/// <summary>
/// 定义员工角色可用的枚举类型。
/// </summary>
public enum StaffRole
{
    Chef,
    Waiter
}

/// <summary>
/// 定义加成类型可用的枚举类型。
/// </summary>
public enum BonusType
{
    IncomeAdd,
    CustomerFlowPercent,
    ProductProductionSpeed
}

/// <summary>
/// 定义加成范围可用的枚举类型。
/// </summary>
public enum BonusScope
{
    Global,
    TableOnly,
    ProductOnly
}

[Serializable]
/// <summary>
/// 负责加成配置相关的运行时逻辑。
/// </summary>
public class BonusConfig
{
    public BonusType type;
    public BonusScope scope = BonusScope.Global;
    public float value;
    public SO_Product productFilter;
}

[Serializable]
/// <summary>
/// 保存顾客生成条目所需的数据字段。
/// </summary>
public struct CustomerSpawnEntry
{
    public SO_Customer customer;
    [Tooltip("权重越高，被抽中的概率越大。")]
    public float spawnChance;
}

/// <summary>
/// 定义网络家具数据可用的枚举类型。
/// </summary>
public enum NET_MEBEL
{
    RECEPCJA = 0,
    PAROWNIK = 1,
    STOL = 2,
    PIEC = 3,
    WINO = 5
}

/// <summary>
/// 定义网络员工数据可用的枚举类型。
/// </summary>
public enum NET_PRACOWNIK
{
    KELNER = 0,
    KUCHARZ = 1
}

[Serializable]
/// <summary>
/// 保存店铺传输数据所需的数据字段。
/// </summary>
public struct ShopDTO
{
    public sbyte shopId;
    public byte level;
    public int price;
    public float buildTime;
    public byte pozycjaNaMapie;
    public bool sklepOtwarty;
}

[Serializable]
/// <summary>
/// 保存家具传输数据所需的数据字段。
/// </summary>
public struct MebelDTO
{
    public NET_MEBEL numerek;
    public byte położenie;
    public byte poziomek;
}

[Serializable]
/// <summary>
/// 保存员工传输数据所需的数据字段。
/// </summary>
public struct RobolDTO
{
    public NET_PRACOWNIK numerek;
    public byte poziomek;
}
