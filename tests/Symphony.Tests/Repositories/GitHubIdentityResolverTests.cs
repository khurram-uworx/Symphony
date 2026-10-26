using System.Net;
using System.Net.Http.Headers;
using NUnit.Framework;
using Symphony.Repositories;
using static Symphony.Tests.NUnitConstants;

namespace Symphony.Tests.Repositories;

[TestFixture, Category(TestCatory.Unit)]
class GitHubIdentityResolverTests
{
    [Test]
    public async Task ResolveAsync_WithoutToken_ReturnsFallbackIdentity()
    {
        var resolver = new GitHubIdentityResolver(new HttpClient(new StubHandler(_ => throw new InvalidOperationException("no request expected"))));

        var identity = await resolver.ResolveAsync(null, "khurram-uworx");

        Assert.That(identity.Name, Is.EqualTo("khurram-uworx"));
        Assert.That(identity.Email, Is.EqualTo("khurram-uworx@users.noreply.github.com"));
    }

    [Test]
    public async Task ResolveAsync_ApiReturnsUser_ReturnsLoginIdentityAndSendsBearerToken()
    {
        AuthenticationHeaderValue? authorization = null;
        var resolver = new GitHubIdentityResolver(new HttpClient(new StubHandler(request =>
        {
            authorization = request.Headers.Authorization;
            return json(HttpStatusCode.OK, """{"login":"octocat","name":"The Octocat"}""");
        })));

        var identity = await resolver.ResolveAsync("ghp_resolverSuccessToken", "fallback");

        Assert.That(identity.Name, Is.EqualTo("The Octocat"));
        Assert.That(identity.Email, Is.EqualTo("octocat@users.noreply.github.com"));
        Assert.That(authorization?.Scheme, Is.EqualTo("Bearer"));
        Assert.That(authorization?.Parameter, Is.EqualTo("ghp_resolverSuccessToken"));
    }

    [Test]
    public async Task ResolveAsync_ApiRejectsToken_ReturnsFallbackIdentity()
    {
        var resolver = new GitHubIdentityResolver(new HttpClient(new StubHandler(_ => json(HttpStatusCode.Unauthorized, """{"message":"Bad credentials"}"""))));

        var identity = await resolver.ResolveAsync("ghp_resolverRejectedToken", "khurram-uworx");

        Assert.That(identity.Name, Is.EqualTo("khurram-uworx"));
        Assert.That(identity.Email, Is.EqualTo("khurram-uworx@users.noreply.github.com"));
    }

    [Test]
    public async Task ResolveAsync_TransportFailure_ReturnsFallbackIdentity()
    {
        var resolver = new GitHubIdentityResolver(new HttpClient(new StubHandler(_ => throw new HttpRequestException("offline"))));

        var identity = await resolver.ResolveAsync("ghp_resolverOfflineToken", null);

        Assert.That(identity.Name, Is.EqualTo(GitHubIdentity.FallbackName));
        Assert.That(identity.Email, Is.EqualTo("symphonyagent@users.noreply.github.com"));
    }

    static HttpResponseMessage json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
