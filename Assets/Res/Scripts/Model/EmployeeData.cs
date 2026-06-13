using System;
using UnityEngine;

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

        /// <summary>
        /// 关联的 3D 小二 GameObject（运行时建立，不参与序列化）。
        /// </summary>
        [NonSerialized]
        public GameObject VisualGO;

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
