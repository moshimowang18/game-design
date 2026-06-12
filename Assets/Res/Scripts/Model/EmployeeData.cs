using System;
using UnityEngine;

namespace JN.Client.Model
{
    /// <summary>
    /// 员工运行时数据（体力、偷懒、工作效率）。
    /// </summary>
    [Serializable]
    public class EmployeeData
    {
        public int EmployeeId;
        public string Name = string.Empty;
        public int MaxStamina = 3;
        public int CurrentStamina = 3;
        public float SkillLevel = 1f;
        public bool IsLounging;
        public float WorkEfficiency = 1f;
        public float LoungingTimer;
        public float StaminaConsumeTimer;
        public float StaminaRecoveryTimer;

        private float efficiencyResetTimer;

        public bool IsLowStamina => CurrentStamina <= 1;

        public void ConsumeStamina()
        {
            CurrentStamina = Math.Max(0, CurrentStamina - 1);
        }

        public void RecoverStamina()
        {
            if (CurrentStamina < MaxStamina)
            {
                CurrentStamina++;
            }
        }

        public bool TryWork()
        {
            if (IsLounging)
            {
                return false;
            }

            ConsumeStamina();
            if (IsLowStamina)
            {
                float mistakeChance = 0.3f / SkillLevel;
                return UnityEngine.Random.value > mistakeChance;
            }

            return true;
        }

        public void KickBackToWork()
        {
            IsLounging = false;
            WorkEfficiency = 1.5f;
            efficiencyResetTimer = 3f;
        }

        /// <summary>
        /// 由外部每帧调用，检查工作效率加成是否到期。
        /// </summary>
        public void ResetEfficiency(float deltaTime)
        {
            if (efficiencyResetTimer <= 0f)
            {
                return;
            }

            efficiencyResetTimer -= deltaTime;
            if (efficiencyResetTimer <= 0f)
            {
                efficiencyResetTimer = 0f;
                WorkEfficiency = 1f;
            }
        }
    }
}
