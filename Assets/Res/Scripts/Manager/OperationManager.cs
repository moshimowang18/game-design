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
            _operationTimeRemaining = DefaultOperationDuration;
            _currentRevenue = 0f;
            _satisfiedCustomers = 0;
            _dissatisfiedCustomers = 0;
            _totalCustomers = 0;
            _negativeEventCount = 0;
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

                employee.StaminaConsumeTimer = 0f;
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
            _satisfiedCustomers = Mathf.Max(0, _satisfiedCustomers - 1);
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
                _totalCustomers += partySize;

                float income = partySize * UnityEngine.Random.Range(8f, 20f);
                _currentRevenue += income;
                _satisfiedCustomers += partySize;

                float dayFlow = TavernDayManager.Instance.CurrentDay?.GuestFlowMultiplier ?? 1f;
                _waveTimer = UnityEngine.Random.Range(5f, 15f) / Mathf.Max(0.1f, dayFlow);
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

                if (emp.IsLounging)
                {
                    continue;
                }

                emp.StaminaConsumeTimer += deltaTime;
                if (emp.StaminaConsumeTimer >= 10f)
                {
                    emp.StaminaConsumeTimer = 0f;
                    bool success = emp.TryWork();
                    if (!success)
                    {
                        OnEmployeeMistake(emp);
                    }
                }

                emp.StaminaRecoveryTimer += deltaTime;
                if (emp.StaminaRecoveryTimer >= 30f)
                {
                    emp.StaminaRecoveryTimer = 0f;
                    emp.RecoverStamina();
                }

                emp.LoungingTimer += deltaTime;
                if (emp.LoungingTimer >= 20f)
                {
                    emp.LoungingTimer = 0f;
                    if (UnityEngine.Random.value < 0.2f)
                    {
                        emp.IsLounging = true;
                    }
                }
            }
        }
    }
}
