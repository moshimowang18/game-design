using System;
using System.Security.Cryptography;
using System.Text;

namespace JN.Client.Utils
{
    public static class SecurityStorage
    {
        private const int Iterations = 1000;

        private static readonly string InternalPepper = "ZOOBIE";

        /// <summary>
        /// 处理密码哈希相关逻辑。
        /// </summary>
        /// <param name="password">参数值。</param>
        /// <returns>返回方法执行后的结果。</returns>
        public static string HashPassword(string password)
        {
            // 结合设备 ID 作为动态盐值，让存档无法在不同设备间通用
            string saltString = InternalPepper + UnityEngine.SystemInfo.deviceUniqueIdentifier;
            byte[] salt = Encoding.UTF8.GetBytes(saltString);

            // 使用基于密码的密钥派生算法生成哈希。
            using (var rfc2898 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                byte[] hash = rfc2898.GetBytes(32); // 生成 256 位 (32 字节) 的哈希
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// 校验密码是否匹配。
        /// </summary>
        /// <param name="input密码">参数值。</param>
        /// <param name="saved哈希">参数值。</param>
        /// <returns>满足条件时返回 真，否则返回 假。</returns>
        public static bool VerifyPassword(string inputPassword, string savedHash)
        {
            string newHash = HashPassword(inputPassword);
            return newHash == savedHash;
        }
    }
}
