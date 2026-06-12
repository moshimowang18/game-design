using JN.Client.Model;
using JN.Client.Scene;
using QFramework;
using UnityEngine;

namespace JN.Client.Manager
{
    /// <summary>
    /// 营业阶段运行时管理：倒计时、收入与满意度统计（数据来自老系统 3D 客人事件）。
    /// </summary>
    [MonoSingletonPath("[Manager]/OperationManager")]
    public class OperationManager : MonoSingleton<OperationManager>
    {
        private const float DefaultOperationDuration = 240f;

        private float _operationTimeRemaining;
        private float _currentRevenue;
        private int _satisfiedCustomers;
        private int _dissatisfiedCustomers;
        private int _totalCustomers;
        private int _negativeEventCount;
        private bool _isOperating;
        private bool _signalsRegistered;

        public float OperationTimeRemaining => _operationTimeRemaining;
        public float TimeRemaining => _operationTimeRemaining;
        public float CurrentRevenue => _currentRevenue;
        public int TotalCustomers => _totalCustomers;
        public int SatisfiedCustomers => _satisfiedCustomers;
        public int DissatisfiedCustomers => _dissatisfiedCustomers;
        public int NegativeEventCount => _negativeEventCount;
        public bool IsOperating => _isOperating;
        public string LastErrorMessage { get; private set; } = string.Empty;
        public float LastErrorTimer { get; private set; }

        /// <summary>
        /// 重置营业状态并开始倒计时。
        /// </summary>
        public void StartOperation()
        {
            // === 桥接老系统：开启 3D 客人生成 ===
            var saveData = DataManager.Instance.SaveData;
            var player = DataManager.Instance.PlayerData;

            if (Object.FindObjectOfType<TavernSceneManager>() != null)
            {
                DataManager.Instance.ResetTransientTavernState();
                saveData = DataManager.Instance.SaveData;
            }

            if (saveData?.tavern?.tables != null && player != null)
            {
                for (int i = 0; i < saveData.tavern.tables.Count; i++)
                {
                    saveData.tavern.tables[i].isUnlocked = i < player.MaxTables;
                }
            }

            if (saveData?.tavern != null && player != null)
            {
                saveData.tavern.availableDishes = player.SelectedDishes.Count;
            }

            if (saveData?.tavern != null)
            {
                DataManager.Instance.SetTavernOpen(true);
            }

            // === 订阅老系统3D客人事件 ===
            if (!_signalsRegistered)
            {
                Signals.Get<TavernCustomerCheckoutSignal>().AddListener(OnRealCustomerCheckout);
                Signals.Get<TavernCustomerAngryLeaveSignal>().AddListener(OnRealCustomerAngryLeave);
                _signalsRegistered = true;
            }

            _operationTimeRemaining = DefaultOperationDuration;
            LastErrorMessage = string.Empty;
            LastErrorTimer = 0f;
            _isOperating = true;

            // === 重置统计数据 ===
            _totalCustomers = 0;
            _satisfiedCustomers = 0;
            _dissatisfiedCustomers = 0;
            _negativeEventCount = 0;
            _currentRevenue = 0f;

            var employees = DataManager.Instance.PlayerData?.Employees;
            if (employees == null)
            {
                return;
            }

            foreach (var employee in employees)
            {
                if (employee == null)
                {
                    continue;
                }

                employee.StaminaRecoveryTimer = 0f;
                employee.LoungingTimer = 0f;
            }
        }

        /// <summary>
        /// 客人满意离开，计入收入与满意度。
        /// </summary>
        public void OnCustomerServed(CustomerData customer)
        {
            if (!_isOperating || customer == null)
            {
                return;
            }

            _totalCustomers++;
            _satisfiedCustomers++;

            float payAmount = 10f * Mathf.Max(1, customer.PartySize) * customer.TipMultiplier;
            _currentRevenue += payAmount;
        }

        /// <summary>
        /// 员工犯错，负面事件 +1。
        /// </summary>
        public void OnEmployeeMistake(EmployeeData employee)
        {
            if (!_isOperating || employee == null)
            {
                return;
            }

            _negativeEventCount++;
            LastErrorMessage = $"{employee.Name}犯错了！";
            LastErrorTimer = 2f;
        }

        /// <summary>
        /// 客人抱怨，计入不满。
        /// </summary>
        public void OnCustomerComplain()
        {
            if (!_isOperating)
            {
                return;
            }

            _totalCustomers++;
            _dissatisfiedCustomers++;
        }

        /// <summary>
        /// 收钱入账（仅外部手动调用时使用）。
        /// </summary>
        public void OnMoneyCollected(float amount)
        {
            if (!_isOperating || amount <= 0f)
            {
                return;
            }

            _currentRevenue += amount;
        }

        /// <summary>
        /// 营业结束，计算并返回结算结果。
        /// </summary>
        public OperationResult EndOperation()
        {
            if (_signalsRegistered)
            {
                Signals.Get<TavernCustomerCheckoutSignal>().RemoveListener(OnRealCustomerCheckout);
                Signals.Get<TavernCustomerAngryLeaveSignal>().RemoveListener(OnRealCustomerAngryLeave);
                _signalsRegistered = false;
            }

            // === 桥接老系统：关闭 3D 客人生成 ===
            if (DataManager.Instance.SaveData?.tavern != null)
            {
                DataManager.Instance.SetTavernOpen(false);
            }

            _isOperating = false;

            var activeEvent = EventSystemManager.Instance.GetEventById(
                TavernDayManager.Instance.CurrentDay?.EventId);
            return ScoreCalculator.Calculate(
                _totalCustomers,
                _satisfiedCustomers,
                _currentRevenue,
                _negativeEventCount,
                DataManager.Instance.PlayerData?.TavernLevel * 0.1f ?? 0f,
                activeEvent);
        }

        private void Update()
        {
            if (!_isOperating)
            {
                return;
            }

            float deltaTime = Time.deltaTime;

            if (LastErrorTimer > 0f)
            {
                LastErrorTimer -= deltaTime;
            }

            if (_operationTimeRemaining > 0f)
            {
                _operationTimeRemaining -= deltaTime;
            }
            else
            {
                _operationTimeRemaining = 0f;
                var result = EndOperation();
                TavernDayManager.Instance.EnterSettlementPhase(result);
                return;
            }

            var player = DataManager.Instance.PlayerData;
            if (player?.Employees == null)
            {
                return;
            }

            foreach (var emp in player.Employees)
            {
                if (emp == null)
                {
                    continue;
                }

                emp.ResetEfficiency(deltaTime);

                if (emp.CurrentStamina <= 0 && !emp.IsLounging)
                {
                    emp.IsLounging = true;
                }

                if (emp.IsLounging && emp.CurrentStamina < emp.MaxStamina)
                {
                    emp.StaminaRecoveryTimer += deltaTime;
                    if (emp.StaminaRecoveryTimer >= 8f)
                    {
                        emp.StaminaRecoveryTimer = 0f;
                        emp.RecoverStamina();

                        if (emp.CurrentStamina >= emp.MaxStamina)
                        {
                            emp.IsLounging = false;
                            emp.LoungingTimer = 0f;
                        }
                    }

                    continue;
                }

                if (!emp.IsLounging)
                {
                    emp.StaminaRecoveryTimer += deltaTime;
                    if (emp.StaminaRecoveryTimer >= 15f)
                    {
                        emp.StaminaRecoveryTimer = 0f;
                        emp.RecoverStamina();
                    }

                    emp.LoungingTimer += deltaTime;
                    if (emp.LoungingTimer >= 20f && emp.CurrentStamina >= 2)
                    {
                        emp.LoungingTimer = 0f;
                        if (UnityEngine.Random.value < 0.15f)
                        {
                            emp.IsLounging = true;
                        }
                    }
                }
            }
        }

        private void OnRealCustomerCheckout()
        {
            if (!_isOperating)
            {
                return;
            }

            var sig = Signals.Get<TavernCustomerCheckoutSignal>();
            _totalCustomers += sig.GroupSize;
            _satisfiedCustomers += sig.GroupSize;
            _currentRevenue += sig.Income;
        }

        private void OnRealCustomerAngryLeave()
        {
            if (!_isOperating)
            {
                return;
            }

            var sig = Signals.Get<TavernCustomerAngryLeaveSignal>();
            _totalCustomers += sig.GroupSize;
            _negativeEventCount += sig.GroupSize;
        }
    }
}
