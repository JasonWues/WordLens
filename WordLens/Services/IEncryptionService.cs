namespace WordLens.Services;

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