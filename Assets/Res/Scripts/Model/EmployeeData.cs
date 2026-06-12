using System;

namespace JN.Client.Model
{
    /// <summary>
    /// 员工数据（第 1 批基础结构，第 2 批将扩展体力与偷懒逻辑）。
    /// </summary>
    [Serializable]
    public class EmployeeData
    {
        public int EmployeeId;
        public string Name = string.Empty;
    }
}
