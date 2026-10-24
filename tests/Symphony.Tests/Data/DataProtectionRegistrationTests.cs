using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using Symphony.Data;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests;

[TestFixture, Category(TestCatory.Unit)]
class DataProtectionRegistrationTests
{
    string root = null!;

    [SetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "symphony-dp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    [Test]
    public void AddSymphonyDataProtection_UsesConfiguredRelativePath()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = "keys"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSymphonyDataProtection(configuration, root);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        var repository = options.XmlRepository as FileSystemXmlRepository;

        Assert.That(repository, Is.Not.Null);
        Assert.That(repository!.Directory.FullName, Is.EqualTo(Path.GetFullPath(Path.Combine(root, "keys"))));
        Assert.That(Directory.Exists(repository.Directory.FullName), Is.True);
    }

    [Test]
    public void AddSymphonyDataProtection_UsesAbsoluteConfiguredPath()
    {
        var keys = Path.Combine(root, "absolute-keys");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:KeysPath"] = keys
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSymphonyDataProtection(configuration, root);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        var repository = options.XmlRepository as FileSystemXmlRepository;

        Assert.That(repository, Is.Not.Null);
        Assert.That(repository!.Directory.FullName, Is.EqualTo(Path.GetFullPath(keys)));
    }

    [Test]
    public void AddSymphonyDataProtection_DefaultsToAppDataPath()
    {
        var services = new ServiceCollection();
        services.AddSymphonyDataProtection(root);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
        var repository = options.XmlRepository as FileSystemXmlRepository;

        Assert.That(repository, Is.Not.Null);
        Assert.That(repository!.Directory.FullName, Is.EqualTo(Path.GetFullPath(Path.Combine(root, "App_Data", "DataProtection-Keys"))));
    }
}