using System;

namespace JN.Client.Model
{
    /// <summary>
    /// 员工运行时数据（体力、休息状态）。
    /// </summary>
    [Serializable]
    public class EmployeeData
    {
        public string Name = string.Empty;
        public int Stamina = 3;
        public bool IsResting;
        public bool KickedFromRest;

        public EmployeeData()
        {
            Name = string.Empty;
            Stamina = 3;
            IsResting = false;
            KickedFromRest = false;
        }

        public override string ToString()
        {
            return $"{Name}(体力{Stamina},休息={IsResting})";
        }
    }
}
