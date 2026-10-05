using System.Security.Cryptography;
using System.Text;

namespace Avis.IFactory;

/// <summary>
/// AES-128-CBC/PKCS7 password obfuscation for GET /api/tokens. Key and IV are
/// fixed and taken verbatim from the StackTest Gateway API doc's C#/Java sample
/// code - not a per-site secret, every StackTest Gateway instance uses the same
/// pair.
/// </summary>
public static class PasswordEncryption
{
    private static readonly byte[] Key = Convert.FromBase64String("YGjDImfP5bf8F7kGbdqWcA==");
    private static readonly byte[] Iv = Convert.FromBase64String("bDA11yRcbUsTC9jl5LnuiQ==");

    public static string Encrypt(string password)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = Iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(password);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        return Convert.ToBase64String(cipherBytes);
    }
}
