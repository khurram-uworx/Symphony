using Microsoft.AspNetCore.DataProtection;
using NUnit.Framework;
using Symphony.Security;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Security;

[TestFixture, Category(TestCatory.Unit)]
class SecretProtectorTests
{
    [Test]
    public void Protect_EmptyInput_ReturnsInput()
    {
        var protector = new SecretProtector(new EphemeralDataProtectionProvider());

        Assert.That(protector.Protect(null), Is.Null);
        Assert.That(protector.Protect(string.Empty), Is.Empty);
    }

    [Test]
    public void Unprotect_EmptyInput_ReturnsInput()
    {
        var protector = new SecretProtector(new EphemeralDataProtectionProvider());

        Assert.That(protector.Unprotect(null), Is.Null);
        Assert.That(protector.Unprotect(string.Empty), Is.Empty);
    }

    [Test]
    public void Protect_RoundTripsPlaintext()
    {
        var protector = new SecretProtector(new EphemeralDataProtectionProvider());
        const string secret = "super-secret-value";

        var ciphertext = protector.Protect(secret);

        Assert.That(ciphertext, Is.Not.EqualTo(secret));
        Assert.That(protector.Unprotect(ciphertext), Is.EqualTo(secret));
    }

    [Test]
    public void Encrypt_Decrypt_RoundTripsPlaintext()
    {
        var protector = new SecretProtector(new EphemeralDataProtectionProvider());
        const string secret = "another-secret";

        var ciphertext = protector.Encrypt(secret);

        Assert.That(ciphertext, Is.Not.EqualTo(secret));
        Assert.That(protector.Decrypt(ciphertext), Is.EqualTo(secret));
    }
}