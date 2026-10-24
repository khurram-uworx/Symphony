using NUnit.Framework;
using Symphony.Config;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Config;

[TestFixture, Category(TestCatory.Unit)]
class CopilotConfigTests
{
    [Test]
    public void IsValid_NoProvider_ReturnsTrue()
    {
        var config = new CopilotConfig(string.Empty, null, null, null, null);
        Assert.That(config.IsValid(out var error), Is.True);
        Assert.That(error, Is.Null);
    }

    [Test]
    public void IsValid_ByomMissingFields_ReturnsError()
    {
        Assert.That(new CopilotConfig("openai", null, "gpt-4o", "key", null).IsValid(out var error1), Is.False);
        Assert.That(error1, Does.Contain("CopilotBaseUrl"));

        Assert.That(new CopilotConfig("openai", "https://api.x.ai/v1", null, "key", null).IsValid(out var error2), Is.False);
        Assert.That(error2, Does.Contain("CopilotModelId"));

        Assert.That(new CopilotConfig("openai", "https://api.x.ai/v1", "gpt-4o", null, null).IsValid(out var error3), Is.False);
        Assert.That(error3, Does.Contain("CopilotApiKey"));

        Assert.That(new CopilotConfig("openai", "https://api.x.ai/v1", "gpt-4o", "key", "grpc").IsValid(out var error4), Is.False);
        Assert.That(error4, Does.Contain("completions"));
    }

    [Test]
    public void IsValid_ValidByom_ReturnsTrue()
    {
        var config = new CopilotConfig("openai", "https://api.x.ai/v1/", "grok-4.20-non-reasoning", "key", "completions");
        Assert.That(config.IsValid(out var error), Is.True);
        Assert.That(error, Is.Null);
    }
}
