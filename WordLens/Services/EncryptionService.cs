using System;
using System.Text;

namespace WordLens.Services
{
    /// <summary>
    /// API Key加密服务接口
    /// </summary>
    public interface IEncryptionService
    {
        /// <summary>
        /// 加密明文字符串
        /// </summary>
        /// <param name="plainText">明文</param>
        /// <returns>加密后的字符串，格式：ENC::Base64String</returns>
        string Encrypt(string plainText);

        /// <summary>
        /// 解密密文字符串
        /// </summary>
        /// <param name="cipherText">密文（ENC::开头）或明文（向后兼容）</param>
        /// <returns>解密后的明文</returns>
        string Decrypt(string cipherText);

        /// <summary>
        /// 检查字符串是否已加密
        /// </summary>
        /// <param name="text">待检查的字符串</param>
        /// <returns>true表示已加密</returns>
        bool IsEncrypted(string text);
    }

    /// <summary>
    /// 加密服务实现
    /// 使用XOR + Base64简单混淆方案
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private const string EncryptionPrefix = "ENC::";
        private const string EncryptionKey = "WordLens-Secret-Key-2024-Avalonia-MVVM";

        /// <inheritdoc />
        public string Encrypt(string plainText)
        {
            // 空字符串或null直接返回
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            // 如果已经加密，直接返回
            if (IsEncrypted(plainText))
                return plainText;

            try
            {
                // 1. 转换为字节数组
                var bytes = Encoding.UTF8.GetBytes(plainText);
                
                // 2. 获取密钥字节
                var keyBytes = Encoding.UTF8.GetBytes(EncryptionKey);

                // 3. XOR加密
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] ^= keyBytes[i % keyBytes.Length];
                }

                // 4. Base64编码
                var base64 = Convert.ToBase64String(bytes);

                // 5. 添加前缀标识
                return $"{EncryptionPrefix}{base64}";
            }
            catch (Exception)
            {
                // 加密失败返回原文（降级处理）
                return plainText;
            }
        }

        /// <inheritdoc />
        public string Decrypt(string cipherText)
        {
            // 空字符串或null直接返回
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            // 检查是否加密，如果没有加密标识，说明是明文（向后兼容）
            if (!IsEncrypted(cipherText))
                return cipherText;

            try
            {
                // 1. 移除前缀
                var base64 = cipherText.Substring(EncryptionPrefix.Length);

                // 2. Base64解码
                var bytes = Convert.FromBase64String(base64);

                // 3. 获取密钥字节
                var keyBytes = Encoding.UTF8.GetBytes(EncryptionKey);

                // 4. XOR解密（XOR的特性：加密和解密使用相同操作）
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] ^= keyBytes[i % keyBytes.Length];
                }

                // 5. 转换回字符串
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception)
            {
                // 解密失败返回原文（降级处理）
                return cipherText;
            }
        }

        /// <inheritdoc />
        public bool IsEncrypted(string text)
        {
            return !string.IsNullOrEmpty(text) && 
                   text.StartsWith(EncryptionPrefix, StringComparison.Ordinal);
        }
    }
}