using UnityEngine;

namespace JN.Client.SO
{
    /// <summary>
    /// 负责资源相关的运行时逻辑。
    /// </summary>
    [CreateAssetMenu(fileName = "ResourceSO", menuName = "ScriptableObject/ResourceSO")]
    public class ResourceSO : ScriptableObject
    {
        /// <summary>
        /// 保存初始金币数量。
        /// </summary>
        public int goldNum;
    }
}
