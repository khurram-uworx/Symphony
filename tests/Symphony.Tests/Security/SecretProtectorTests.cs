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

    [Test]
    public void Protect_DifferentInstancesWithSharedKeyRing_RoundTrips()
    {
        var directory = Path.Combine(Path.GetTempPath(), "symphony-key-ring-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var first = new SecretProtector(directory);
            var second = new SecretProtector(directory);
            const string secret = "shared-secret";

            var ciphertext = first.Protect(secret);

            Assert.That(ciphertext, Is.Not.EqualTo(secret));
            Assert.That(second.Unprotect(ciphertext), Is.EqualTo(secret));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void RedactSecrets_Object_ReplacesSecretLikeFields()
    {
        var payload = new RedactionTestPayload
        {
            ApiKey = "super-secret",
            Token = "token-secret",
            Message = "visible"
        };

        var redacted = Redaction.RedactSecrets(payload);

        Assert.That(redacted, Is.Not.Null);
        Assert.That(redacted!.ApiKey, Is.EqualTo(Redaction.Redacted));
        Assert.That(redacted.Token, Is.EqualTo(Redaction.Redacted));
        Assert.That(redacted.Message, Is.EqualTo("visible"));
    }

    public sealed class RedactionTestPayload
    {
        public string? ApiKey { get; set; }

        public string? Token { get; set; }

        public string? Message { get; set; }
    }
}