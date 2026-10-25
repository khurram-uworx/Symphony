using Microsoft.AspNetCore.DataProtection;
using Symphony.Data;

namespace Symphony.Security;

public interface ISecretProtector
{
    string? Protect(string? plaintext);

    string? Unprotect(string? ciphertext);

    string? Encrypt(string? plaintext) => Protect(plaintext);

    string? Decrypt(string? ciphertext) => Unprotect(ciphertext);
}

public static class SecretProtectionPurposes
{
    public const string Application = "Symphony";

    public const string Secrets = "Secrets";

    public const string Version = "v1";
}

public sealed class SecretProtector : ISecretProtector
{
    public const string Purpose = "Symphony.Secrets.v1";

    readonly IDataProtector protector;

    public SecretProtector(IDataProtectionProvider provider)
        : this((provider ?? throw new ArgumentNullException(nameof(provider))).CreateProtector(
            SecretProtectionPurposes.Application,
            SecretProtectionPurposes.Secrets,
            SecretProtectionPurposes.Version))
    {
    }

    public SecretProtector(IDataProtector protector)
    {
        this.protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public SecretProtector(IServiceProvider services)
        : this((services ?? throw new ArgumentNullException(nameof(services))).GetRequiredService<IDataProtectionProvider>())
    {
    }

    public SecretProtector()
        : this(DataProtectionProvider.Create(Path.Combine(Path.GetTempPath(), "symphony-secret-protection")))
    {
    }

    public SecretProtector(string keyRingPath)
        : this(CreateProvider(keyRingPath))
    {
    }

    public SecretProtector(DirectoryInfo keyRingDirectory)
        : this(keyRingDirectory is null
            ? throw new ArgumentNullException(nameof(keyRingDirectory))
            : DataProtectionProvider.Create(keyRingDirectory))
    {
    }

    static IDataProtectionProvider CreateProvider(string keyRingPath)
    {
        if (string.IsNullOrWhiteSpace(keyRingPath))
            throw new ArgumentException("A key ring path is required.", nameof(keyRingPath));

        var directory = Path.IsPathRooted(keyRingPath)
            ? keyRingPath
            : Path.GetFullPath(keyRingPath);

        Directory.CreateDirectory(directory);
        return DataProtectionProvider.Create(directory);
    }

    public string? Protect(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
            return plaintext;

        return protector.Protect(plaintext);
    }

    public string? Unprotect(string? ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext))
            return ciphertext;

        return protector.Unprotect(ciphertext);
    }

    public string? Encrypt(string? plaintext) => Protect(plaintext);

    public string? Decrypt(string? ciphertext) => Unprotect(ciphertext);
}

public static class SecretProtectionRegistration
{
    public static IServiceCollection AddSymphonySecretProtection(this IServiceCollection services) =>
        AddSymphonySecretProtection(services, configuration: null, contentRoot: null);

    public static IServiceCollection AddSymphonySecretProtection(
        this IServiceCollection services,
        IConfiguration configuration) =>
        AddSymphonySecretProtection(services, configuration, contentRoot: null);

    public static IServiceCollection AddSymphonySecretProtection(
        this IServiceCollection services,
        string? contentRoot) =>
        AddSymphonySecretProtection(services, configuration: null, contentRoot: contentRoot);

    public static IServiceCollection AddSymphonySecretProtection(
        this IServiceCollection services,
        IConfiguration? configuration,
        string? contentRoot) =>
        AddSecretProtectorCore(services, configuration, contentRoot);

    public static IServiceCollection AddSecretProtector(this IServiceCollection services) =>
        AddSymphonySecretProtection(services);

    public static IServiceCollection AddSecretProtector(
        this IServiceCollection services,
        IConfiguration configuration) =>
        AddSymphonySecretProtection(services, configuration);

    public static IServiceCollection AddSecretProtector(
        this IServiceCollection services,
        string? contentRoot) =>
        AddSymphonySecretProtection(services, contentRoot);

    public static IServiceCollection AddSecretProtector(
        this IServiceCollection services,
        IConfiguration? configuration,
        string? contentRoot) =>
        AddSymphonySecretProtection(services, configuration, contentRoot);

    static IServiceCollection AddSecretProtectorCore(
        IServiceCollection services,
        IConfiguration? configuration,
        string? contentRoot)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(ISecretProtector)))
            return services;

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IDataProtectionProvider)))
            services.AddSymphonyDataProtection(configuration, contentRoot);

        services.AddSingleton<ISecretProtector>(serviceProvider =>
        {
            try
            {
                return new SecretProtector(serviceProvider);
            }
            catch (InvalidOperationException)
            {
                return new SecretProtector();
            }
        });
        return services;
    }
}