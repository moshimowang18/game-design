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
        private const float BaseWaveInterval = 12f;
        private const float DefaultPayPerGuest = 10f;

        private float _operationTimeRemaining;
        private float _currentRevenue;
        private int _satisfiedCustomers;
        private int _dissatisfiedCustomers;
        private int _totalCustomers;
        private int _negativeEvents;
        private float _waveTimer;
        private bool _isOperating;

        private readonly Queue<int> _waves = new();

        public float OperationTimeRemaining => _operationTimeRemaining;
        public float CurrentRevenue => _currentRevenue;
        public int TotalCustomers => _totalCustomers;
        public int SatisfiedCustomers => _satisfiedCustomers;
        public int DissatisfiedCustomers => _dissatisfiedCustomers;
        public int NegativeEvents => _negativeEvents;
        public int PendingWaveCount => _waves.Count;
        public bool IsOperating => _isOperating;

        public float EnvironmentBonus => CalculateEnvironmentBonus();

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
            _negativeEvents = 0;
            _waves.Clear();
            _waveTimer = GetWaveInterval();
            _isOperating = true;
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

            float payAmount = DefaultPayPerGuest * Mathf.Max(1, customer.PartySize) * customer.TipMultiplier;
            _currentRevenue += payAmount;
        }

        /// <summary>
        /// 员工犯错，负面事件 +1。
        /// </summary>
        public void OnEmployeeMistake(EmployeeData employee)
        {
            if (!_isOperating)
            {
                return;
            }

            _negativeEvents++;
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
        /// 收钱入账。
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

            var activeEvent = GetActiveDailyEvent();
            return ScoreCalculator.Calculate(
                _totalCustomers,
                _satisfiedCustomers,
                _currentRevenue,
                _negativeEvents,
                EnvironmentBonus,
                activeEvent);
        }

        private void Update()
        {
            if (!_isOperating)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            TickEmployeeEfficiency(deltaTime);
            TickWaveTimer(deltaTime);

            _operationTimeRemaining -= deltaTime;
            if (_operationTimeRemaining > 0f)
            {
                return;
            }

            _operationTimeRemaining = 0f;
            var result = EndOperation();
            TavernDayManager.Instance.EnterSettlementPhase(result);
        }

        private void TickEmployeeEfficiency(float deltaTime)
        {
            var employees = DataManager.Instance.PlayerData?.Employees;
            if (employees == null)
            {
                return;
            }

            foreach (var employee in employees)
            {
                employee?.ResetEfficiency(deltaTime);
            }
        }

        private void TickWaveTimer(float deltaTime)
        {
            _waveTimer -= deltaTime;
            if (_waveTimer > 0f)
            {
                return;
            }

            _waveTimer = GetWaveInterval();
            SpawnWave();
        }

        private void SpawnWave()
        {
            int partySize = Mathf.Max(1, Mathf.RoundToInt(Random.Range(1f, 4f)));
            _waves.Enqueue(partySize);
        }

        private float GetWaveInterval()
        {
            float guestFlow = TavernDayManager.Instance.CurrentDay?.GuestFlowMultiplier ?? 1f;
            return BaseWaveInterval / Mathf.Max(0.1f, guestFlow);
        }

        private DailyEvent GetActiveDailyEvent()
        {
            var eventId = TavernDayManager.Instance.CurrentDay?.EventId;
            return EventSystemManager.Instance.GetEventById(eventId);
        }

        private static float CalculateEnvironmentBonus()
        {
            var player = DataManager.Instance.PlayerData;
            if (player == null)
            {
                return 0.1f;
            }

            float bonus = player.TavernLevel * 0.05f;
            if (player.HasVipRoom)
            {
                bonus += 0.1f;
            }

            return Mathf.Clamp01(bonus);
        }
    }
}
