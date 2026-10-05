using System.Security.Cryptography;
using System.Text;
using Avis.IFactory;
using Xunit;

namespace Avis.Tests.IFactory;

public class PasswordEncryptionTests
{
    [Fact]
    public void Encrypt_RoundTripsWithManualAesDecrypt()
    {
        const string plaintext = "S3cr3t-Password!";
        var encryptedBase64 = PasswordEncryption.Encrypt(plaintext);

        var key = Convert.FromBase64String("YGjDImfP5bf8F7kGbdqWcA==");
        var iv = Convert.FromBase64String("bDA11yRcbUsTC9jl5LnuiQ==");
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();

        var cipherBytes = Convert.FromBase64String(encryptedBase64);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        Assert.Equal(plaintext, Encoding.UTF8.GetString(plainBytes));
    }

    [Fact]
    public void Encrypt_IsDeterministicForTheFixedIv()
    {
        Assert.Equal(PasswordEncryption.Encrypt("password"), PasswordEncryption.Encrypt("password"));
    }

    [Fact]
    public void Encrypt_DiffersForDifferentInput()
    {
        Assert.NotEqual(PasswordEncryption.Encrypt("password1"), PasswordEncryption.Encrypt("password2"));
    }
}
