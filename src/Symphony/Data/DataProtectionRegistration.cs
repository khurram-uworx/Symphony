using Microsoft.AspNetCore.DataProtection;

namespace Symphony.Data;

public static class DataProtectionRegistration
{
    public static IDataProtectionBuilder AddSymphonyDataProtection(this IServiceCollection services) =>
        AddSymphonyDataProtection(services, configuration: null, contentRoot: null);

    public static IDataProtectionBuilder AddSymphonyDataProtection(
        this IServiceCollection services,
        IConfiguration configuration) =>
        AddSymphonyDataProtection(services, configuration, contentRoot: null);

    public static IDataProtectionBuilder AddSymphonyDataProtection(
        this IServiceCollection services,
        string? contentRoot) =>
        AddSymphonyDataProtection(services, configuration: null, contentRoot: contentRoot);

    public static IDataProtectionBuilder AddSymphonyDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        string? contentRoot) =>
        AddSymphonyDataProtectionCore(services, configuration, contentRoot);



    public static IDataProtectionBuilder AddDataProtectionPersistence(
        this IServiceCollection services) =>
        AddSymphonyDataProtection(services);

    public static IDataProtectionBuilder AddDataProtectionPersistence(
        this IServiceCollection services,
        IConfiguration configuration) =>
        AddSymphonyDataProtection(services, configuration);

    public static IDataProtectionBuilder AddDataProtectionPersistence(
        this IServiceCollection services,
        string? contentRoot) =>
        AddSymphonyDataProtection(services, contentRoot);

    public static IDataProtectionBuilder AddDataProtectionPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        string? contentRoot) =>
        AddSymphonyDataProtection(services, configuration, contentRoot);

    public static IDataProtectionBuilder ConfigureDataProtection(
        this IServiceCollection services) =>
        AddSymphonyDataProtection(services);

    public static IDataProtectionBuilder ConfigureDataProtection(
        this IServiceCollection services,
        IConfiguration configuration) =>
        AddSymphonyDataProtection(services, configuration);

    public static IDataProtectionBuilder ConfigureDataProtection(
        this IServiceCollection services,
        string? contentRoot) =>
        AddSymphonyDataProtection(services, contentRoot);

    public static IDataProtectionBuilder ConfigureDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        string? contentRoot) =>
        AddSymphonyDataProtection(services, configuration, contentRoot);

    static IDataProtectionBuilder AddSymphonyDataProtectionCore(
        IServiceCollection services,
        IConfiguration? configuration,
        string? contentRoot)
    {
        var configured = FirstNonEmpty(configuration,
            "DataProtection:KeysPath",
            "DataProtection:KeyPath",
            "DataProtection:KeysDirectory",
            "DataProtection:KeyDirectory",
            "DataProtection:Path",
            "DataProtection:KeysLocation");

        var root = string.IsNullOrWhiteSpace(contentRoot)
            ? Directory.GetCurrentDirectory()
            : contentRoot;

        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(root, "App_Data", "DataProtection-Keys")
            : Path.IsPathRooted(configured)
                ? configured!
                : Path.GetFullPath(Path.Combine(root, configured!));

        Directory.CreateDirectory(path);

        return services.AddDataProtection()
            .SetApplicationName("Symphony")
            .PersistKeysToFileSystem(new DirectoryInfo(path));
    }

    static string? FirstNonEmpty(IConfiguration? configuration, params string[] keys)
    {
        if (configuration is null)
            return null;

        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}