using System.Collections.Generic;
using JN.Client.Model;
using QFramework;
using UnityEngine;

namespace JN.Client.Manager
{
    /// <summary>
    /// 营业阶段运行时管理：倒计时、客流波次、收入与满意度统计。
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
        private float _waveTimer;
        private bool _isOperating;

        private int _waitingGuests;
        private readonly List<CustomerData> _activeCustomers = new();
        private readonly List<CustomerData> _finishedCustomers = new();
        private float _pendingPayment;

        public float OperationTimeRemaining => _operationTimeRemaining;
        public float TimeRemaining => _operationTimeRemaining;
        public float CurrentRevenue => _currentRevenue;
        public int TotalCustomers => _totalCustomers;
        public int SatisfiedCustomers => _satisfiedCustomers;
        public int DissatisfiedCustomers => _dissatisfiedCustomers;
        public int NegativeEventCount => _negativeEventCount;
        public bool IsOperating => _isOperating;
        public int WaitingGuests => _waitingGuests;
        public int ActiveCustomerCount => _activeCustomers.Count;
        public int FinishedCustomerCount => _finishedCustomers.Count;
        public float PendingPayment => _pendingPayment;
        public int UsedTables => _activeCustomers.Count + _finishedCustomers.Count;
        public string LastErrorMessage { get; private set; } = string.Empty;
        public float LastErrorTimer { get; private set; }

        /// <summary>
        /// 重置营业状态并开始倒计时。
        /// </summary>
        public void StartOperation()
        {
            _operationTimeRemaining = DefaultOperationDuration;
            _currentRevenue = 0f;
            _satisfiedCustomers = 0;
            _dissatisfiedCustomers = 0;
            _totalCustomers = 0;
            _negativeEventCount = 0;
            _waitingGuests = 0;
            _pendingPayment = 0f;
            _activeCustomers.Clear();
            _finishedCustomers.Clear();
            LastErrorMessage = string.Empty;
            LastErrorTimer = 0f;
            _isOperating = true;

            float dayFlow = TavernDayManager.Instance.CurrentDay?.GuestFlowMultiplier ?? 1f;
            _waveTimer = UnityEngine.Random.Range(5f, 15f) / Mathf.Max(0.1f, dayFlow);

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
        /// 收取已吃完客人的款项。
        /// </summary>
        public void CollectMoney()
        {
            if (_finishedCustomers.Count <= 0)
            {
                return;
            }

            _currentRevenue += _pendingPayment;
            _satisfiedCustomers += _finishedCustomers.Count;
            _finishedCustomers.Clear();
            _pendingPayment = 0f;
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
            _isOperating = false;
            CollectMoney();

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

            _waveTimer -= deltaTime;
            if (_waveTimer <= 0f)
            {
                int partySize = UnityEngine.Random.Range(1, 4);
                _waitingGuests += partySize;
                _totalCustomers += partySize;

                float dayFlow = TavernDayManager.Instance.CurrentDay?.GuestFlowMultiplier ?? 1f;
                _waveTimer = UnityEngine.Random.Range(5f, 15f) / Mathf.Max(0.1f, dayFlow);
            }

            var player = DataManager.Instance.PlayerData;
            if (player?.Employees == null)
            {
                return;
            }

            var availableEmployees = player.Employees.FindAll(e => e != null && !e.IsLounging && e.CurrentStamina > 0);
            int maxTables = player.MaxTables;
            while (_waitingGuests > 0 && availableEmployees.Count > 0 && UsedTables < maxTables)
            {
                var emp = availableEmployees[0];
                availableEmployees.RemoveAt(0);
                _waitingGuests--;

                var customer = GenerateCustomer();
                customer.ServeStartTime = Time.time;
                customer.ServeDuration = UnityEngine.Random.Range(8f, 15f);
                _activeCustomers.Add(customer);

                bool success = emp.TryWork();
                if (!success)
                {
                    _negativeEventCount++;
                    customer.Satisfaction = 0.5f;
                    LastErrorMessage = $"{emp.Name}犯错了！";
                    LastErrorTimer = 2f;
                }
            }

            for (int i = _activeCustomers.Count - 1; i >= 0; i--)
            {
                var customer = _activeCustomers[i];
                if (Time.time - customer.ServeStartTime < customer.ServeDuration)
                {
                    continue;
                }

                float payment = customer.Type switch
                {
                    CustomerType.Vip => UnityEngine.Random.Range(30f, 60f),
                    CustomerType.Regular => UnityEngine.Random.Range(15f, 30f),
                    CustomerType.Special => UnityEngine.Random.Range(25f, 50f),
                    _ => UnityEngine.Random.Range(8f, 20f)
                };
                payment *= customer.Satisfaction;
                payment *= customer.TipMultiplier;

                _pendingPayment += payment;
                _finishedCustomers.Add(customer);
                _activeCustomers.RemoveAt(i);
            }

            foreach (var emp in player.Employees)
            {
                if (emp == null)
                {
                    continue;
                }

                emp.ResetEfficiency(deltaTime);

                if (emp.CurrentStamina <= 0)
                {
                    emp.IsLounging = true;
                }

                if (emp.IsLounging)
                {
                    emp.StaminaRecoveryTimer += deltaTime;
                    if (emp.StaminaRecoveryTimer >= 12f)
                    {
                        emp.StaminaRecoveryTimer = 0f;
                        int staminaBefore = emp.CurrentStamina;
                        emp.RecoverStamina();
                        if (staminaBefore <= 0 && emp.CurrentStamina > 0)
                        {
                            emp.IsLounging = false;
                        }
                    }

                    continue;
                }

                emp.StaminaRecoveryTimer += deltaTime;
                if (emp.StaminaRecoveryTimer >= 12f)
                {
                    emp.StaminaRecoveryTimer = 0f;
                    emp.RecoverStamina();
                }

                if (emp.CurrentStamina >= 2)
                {
                    emp.LoungingTimer += deltaTime;
                    if (emp.LoungingTimer >= 20f)
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

        private static CustomerData GenerateCustomer()
        {
            float vipBonus = TavernDayManager.Instance.CurrentDay?.VipProbabilityBonus ?? 0f;
            float roll = UnityEngine.Random.value;

            CustomerType type;
            if (roll < 0.05f + vipBonus)
            {
                type = CustomerType.Vip;
            }
            else if (roll < 0.15f + vipBonus * 0.5f)
            {
                type = CustomerType.Regular;
            }
            else
            {
                type = CustomerType.Normal;
            }

            return new CustomerData
            {
                Type = type,
                Name = type == CustomerType.Vip ? "贵客" : type == CustomerType.Regular ? "熟客" : "客人",
                Patience = type == CustomerType.Vip ? 120f : type == CustomerType.Regular ? 80f : 60f,
                TipMultiplier = type == CustomerType.Vip ? 2f : type == CustomerType.Regular ? 1.3f : 1f,
                Satisfaction = 1f
            };
        }
    }
}
