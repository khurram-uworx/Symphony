using NUnit.Framework;
using Symphony.Config;
using Symphony.Data;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Config;

[TestFixture, Category(TestCatory.Unit)]
class ServiceConfigFactoryTests
{
    const string EnvVar = "LINEAR_API_KEY";

    [Test]
    public void Build_LinearTrackerBlankSavedKey_FallsBackToEnvironmentKey()
    {
        using var env = new EnvironmentKey(EnvVar, "env-linear-key");

        var config = Build(new UserConfiguration
        {
            TrackerKind = "linear",
            TrackerApiKey = string.Empty
        });

        Assert.That(config.Tracker.ApiKey, Is.EqualTo("env-linear-key"));
    }

    [Test]
    public void Build_LinearTrackerSavedKey_UsesSavedKeyOverEnvironment()
    {
        using var env = new EnvironmentKey(EnvVar, "env-linear-key");

        var config = Build(new UserConfiguration
        {
            TrackerKind = "linear",
            TrackerApiKey = "saved-linear-key"
        });

        Assert.That(config.Tracker.ApiKey, Is.EqualTo("saved-linear-key"));
    }

    [Test]
    public void Build_LinearTrackerEnvReference_ResolvesReferencedVariable()
    {
        using var env = new EnvironmentKey(EnvVar, "referenced-linear-key");

        var config = Build(new UserConfiguration
        {
            TrackerKind = "linear",
            TrackerApiKey = $"${EnvVar}"
        });

        Assert.That(config.Tracker.ApiKey, Is.EqualTo("referenced-linear-key"));
    }

    [Test]
    public void Build_NonLinearTracker_IgnoresLinearEnvironmentKey()
    {
        using var env = new EnvironmentKey(EnvVar, "env-linear-key");

        var config = Build(new UserConfiguration
        {
            TrackerKind = TrackerConfig.None,
            TrackerApiKey = string.Empty
        });

        Assert.That(config.Tracker.ApiKey, Is.Empty);
    }

    static ServiceConfig Build(UserConfiguration user) => ServiceConfigFactory.Build(user, string.Empty);

    sealed class EnvironmentKey : IDisposable
    {
        readonly string? original;

        internal EnvironmentKey(string name, string value)
        {
            original = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(EnvVar, original);
    }
}
