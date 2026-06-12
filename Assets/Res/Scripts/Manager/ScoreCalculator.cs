using JN.Client.Model;
using UnityEngine;

namespace JN.Client.Manager
{
    /// <summary>
    /// 营业结算评分计算。
    /// </summary>
    public static class ScoreCalculator
    {
        public static OperationResult Calculate(
            int totalCustomers,
            int satisfiedCustomers,
            float currentRevenue,
            int negativeEvents,
            float environmentBonus,
            DailyEvent activeEvent)
        {
            var result = new OperationResult();
            result.TotalRevenue = currentRevenue;

            float satisfiedPct = totalCustomers > 0 ? (float)satisfiedCustomers / totalCustomers : 0f;
            result.DishSatisfaction = Mathf.Clamp01(satisfiedPct);
            result.ServiceEfficiency = Mathf.Clamp01(satisfiedPct * 0.9f);
            result.EnvironmentBonus = Mathf.Clamp01(environmentBonus);
            result.NegativeEvents = negativeEvents;

            float baseScore = result.DishSatisfaction * 0.4f
                            + result.ServiceEfficiency * 0.35f
                            + result.EnvironmentBonus * 0.25f;
            float penalty = negativeEvents * 0.05f;

            result.FinalScore = Mathf.Clamp01(baseScore - penalty);
            result.StarRating = ScoreToStars(result.FinalScore);
            return result;
        }

        private static int ScoreToStars(float score)
        {
            if (score >= 0.9f) return 5;
            if (score >= 0.7f) return 4;
            if (score >= 0.5f) return 3;
            if (score >= 0.3f) return 2;
            return 1;
        }
    }
}
