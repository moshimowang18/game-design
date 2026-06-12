using System;

namespace JN.Client.Model
{
    /// <summary>
    /// 单日营业结算结果。
    /// </summary>
    [Serializable]
    public class OperationResult
    {
        public float TotalRevenue;
        public float DishSatisfaction;
        public float ServiceEfficiency;
        public float EnvironmentBonus;
        public int NegativeEvents;
        public float FinalScore;
        public int StarRating;
    }
}
