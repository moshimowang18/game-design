using System;

namespace JN.Client.Model
{
    public enum CustomerType
    {
        Normal,
        Regular,
        Vip,
        Special
    }

    /// <summary>
    /// 客人运行时数据。
    /// </summary>
    [Serializable]
    public class CustomerData
    {
        public CustomerType Type;
        public string Name = string.Empty;
        public float Patience;
        public int PartySize = 1;
        public string PreferredDish = string.Empty;
        public float TipMultiplier = 1f;
        public bool NeedsVipRoom;
        public bool GotVipRoom;
    }
}
