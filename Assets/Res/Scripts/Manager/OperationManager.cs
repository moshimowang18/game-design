using JN.Client;
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

        private const float StaminaRecoveryInterval = 20f;

        private float _staminaRecoveryTimer;

        private float _operationTimeRemaining;

        private float _currentRevenue;

        private int _satisfiedCustomers;

        private int _dissatisfiedCustomers;

        private int _totalCustomers;

        private int _negativeEventCount;

        private bool _isOperating;

        private bool _signalsRegistered;

        private bool _employeeSignalRegistered;



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

        private void Awake()
        {
            if (!_employeeSignalRegistered)
            {
                Signals.Get<GameplayGuideProgressSignal>().AddListener(OnGameplayGuideProgress);
                _employeeSignalRegistered = true;
            }
        }

        private void OnDestroy()
        {
            if (_employeeSignalRegistered)
            {
                Signals.Get<GameplayGuideProgressSignal>().RemoveListener(OnGameplayGuideProgress);
                _employeeSignalRegistered = false;
            }
        }

        private void OnGameplayGuideProgress()
        {
            SyncEmployeesFromOldSystem();
        }

        /// <summary>
        /// 同步员工数据到老系统招聘的小二数量。
        /// 老系统 ownedStaff 中 staffId=5 的小二数量为权威。
        /// </summary>
        public void SyncEmployeesFromOldSystem()
        {
            var player = DataManager.Instance.PlayerData;
            if (player == null)
            {
                return;
            }

            var oldSystemWaiterCount = DataManager.Instance.GetHiredGuideWaiterCount();

            while (player.Employees.Count < oldSystemWaiterCount && player.Employees.Count < PlayerModel.MaxEmployeeCount)
            {
                var emp = new EmployeeData
                {
                    Name = $"小二{(char)('A' + player.Employees.Count)}",
                    Stamina = 3,
                    IsResting = false,
                    KickedFromRest = false
                };
                player.Employees.Add(emp);
                Debug.Log($"[Employee] 同步:新增{emp.Name}");
            }

            while (player.Employees.Count > oldSystemWaiterCount)
            {
                player.Employees.RemoveAt(player.Employees.Count - 1);
            }
        }

        /// <summary>
        /// 获取当前在岗（非休息）员工数量，给老系统调度用。
        /// </summary>
        public int GetActiveWaiterCount()
        {
            var player = DataManager.Instance.PlayerData;
            if (player == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var emp in player.Employees)
            {
                if (emp != null && !emp.IsResting)
                {
                    count++;
                }
            }

            return count;
        }



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



            if (saveData?.tavern != null && player != null)

            {

                var totalStock = player.GetTotalDishStock();
                saveData.tavern.availableDishes = 0;
                Debug.Log($"[OpMgr] 营业开始,备菜食材上桌: {totalStock}份,成品菜待厨师开火");

                var sceneMgr = Object.FindObjectOfType<TavernSceneManager>();
                if (sceneMgr != null && totalStock > 0)
                {
                    sceneMgr.StageIngredientStockFromPlayer(player);
                }

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

                Signals.Get<TavernDishServedSignal>().AddListener(OnDishServed);

                _signalsRegistered = true;

            }



            if (player != null && player.Employees != null)

            {

                foreach (var emp in player.Employees)

                {

                    if (emp == null)

                    {

                        continue;

                    }

                    emp.Stamina = 3;

                    emp.IsResting = false;

                    emp.KickedFromRest = false;

                }

            }



            ResetStaminaRecoveryTimer();



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

            SyncEmployeesFromOldSystem();
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

                Signals.Get<TavernDishServedSignal>().RemoveListener(OnDishServed);

                _signalsRegistered = false;

            }



            // === 桥接老系统：关闭 3D 客人生成 ===

            if (DataManager.Instance.SaveData?.tavern != null)

            {

                DataManager.Instance.SetTavernOpen(false);

            }



            _isOperating = false;

            var player = DataManager.Instance.PlayerData;
            var saveData = DataManager.Instance.SaveData;
            if (player != null)
            {
                if (player.Employees != null)
                {
                    foreach (var emp in player.Employees)
                    {
                        if (emp == null)
                        {
                            continue;
                        }

                        emp.Stamina = 3;
                        emp.IsResting = false;
                        emp.KickedFromRest = false;
                    }
                }

                var waste = player.GetTotalDishStock();
                if (waste > 0)
                {
                    Debug.Log($"[OpMgr] 营业结束，剩余 {waste} 份菜品变质丢弃");
                }

                player.ClearDishStock();
                if (saveData?.tavern != null)
                {
                    saveData.tavern.availableDishes = 0;
                }
            }

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



            if (TavernDayManager.Instance != null && TavernDayManager.Instance.Phase == DayPhase.Operation)

            {

                UpdateStaminaRecovery();

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



        private void OnDishServed()

        {

            if (!_isOperating)

            {

                return;

            }



            var player = DataManager.Instance.PlayerData;

            if (player == null || player.Employees == null || player.Employees.Count == 0)

            {

                return;

            }



            EmployeeData target = null;

            var maxStamina = -1;

            foreach (var emp in player.Employees)

            {

                if (emp == null || emp.IsResting)

                {

                    continue;

                }

                if (emp.Stamina > maxStamina)

                {

                    maxStamina = emp.Stamina;

                    target = emp;

                }

            }



            if (target == null)

            {

                return;

            }



            target.Stamina = Mathf.Max(0, target.Stamina - 1);



            if (target.Stamina == 0)

            {

                target.IsResting = true;

                Debug.Log($"[Employee] {target.Name} 体力耗尽，自动休息");

            }

            else

            {

                Debug.Log($"[Employee] {target.Name} 上菜消耗，剩余体力{target.Stamina}");

            }

        }



        /// <summary>

        /// 重置体力恢复计时器（营业开始时调用）。

        /// </summary>

        public void ResetStaminaRecoveryTimer()

        {

            _staminaRecoveryTimer = 0f;

        }



        private void UpdateStaminaRecovery()

        {

            var player = DataManager.Instance.PlayerData;

            if (player == null || player.Employees == null)

            {

                return;

            }



            _staminaRecoveryTimer += Time.deltaTime;

            if (_staminaRecoveryTimer < StaminaRecoveryInterval)

            {

                return;

            }

            _staminaRecoveryTimer = 0f;



            foreach (var emp in player.Employees)

            {

                if (emp == null || !emp.IsResting)

                {

                    continue;

                }

                emp.Stamina = Mathf.Min(3, emp.Stamina + 1);

                Debug.Log($"[Employee] {emp.Name} 休息恢复，当前体力{emp.Stamina}");

            }

        }

    }

}


