using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using WebApplication1.Services;

namespace WebApplication1.Tests;

public sealed class GitHubPortfolioServiceTests
{
    [Fact]
    public async Task GetRepositoriesAsync_ReturnsValidationError_WhenUsernameIsEmpty()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[]", Encoding.UTF8, "application/json")
        });

        var result = await service.GetRepositoriesAsync(" ");

        Assert.Equal("GitHub username is empty.", result.ErrorMessage);
        Assert.Empty(result.Repositories);
    }

    [Fact]
    public async Task GetRepositoriesAsync_ReturnsNotFoundError_WhenUserMissing()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await service.GetRepositoriesAsync("missing-user");

        Assert.Contains("was not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Repositories);
    }

    [Fact]
    public async Task GetRepositoriesAsync_UsesBearerToken_AndHandlesRateLimitExceeded()
    {
        AuthenticationHeaderValue? capturedAuthorization = null;
        var service = CreateService(request =>
        {
            capturedAuthorization = request.Headers.Authorization;
            var response = new HttpResponseMessage(HttpStatusCode.Forbidden);
            response.Headers.Add("X-RateLimit-Remaining", "0");
            response.Headers.Add("X-RateLimit-Reset", "1735689600");
            return response;
        });

        var result = await service.GetRepositoriesAsync("octocat", "secret-token");

        Assert.Equal("Bearer", capturedAuthorization?.Scheme);
        Assert.Equal("secret-token", capturedAuthorization?.Parameter);
        Assert.Contains("rate limit exceeded", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Repositories);
    }

    [Fact]
    public async Task GetRepositoriesAsync_MapsResponse_AndSkipsForks()
    {
        const string payload = """
                               [
                                 {
                                   "name": "portfolio",
                                   "description": null,
                                   "language": null,
                                   "fork": false,
                                   "stargazers_count": 5,
                                   "forks_count": 2,
                                   "html_url": "https://github.com/octocat/portfolio",
                                   "homepage": "https://portfolio.example.com",
                                   "updated_at": "2026-02-10T10:00:00Z"
                                 },
                                 {
                                   "name": "forked-repo",
                                   "description": "Fork",
                                   "language": "C#",
                                   "fork": true,
                                   "stargazers_count": 100,
                                   "forks_count": 10,
                                   "html_url": "https://github.com/octocat/forked-repo",
                                   "homepage": null,
                                   "updated_at": "2026-02-10T10:00:00Z"
                                 }
                               ]
                               """;

        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });

        var result = await service.GetRepositoriesAsync("octocat");

        Assert.Null(result.ErrorMessage);
        var repository = Assert.Single(result.Repositories);
        Assert.Equal("portfolio", repository.Name);
        Assert.Equal("No description provided.", repository.Description);
        Assert.Equal("Not specified", repository.Language);
        Assert.Equal(5, repository.Stars);
        Assert.Equal(2, repository.Forks);
        Assert.Equal("https://github.com/octocat/portfolio", repository.HtmlUrl);
    }

    private static GitHubPortfolioService CreateService(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var client = new HttpClient(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://api.github.com/")
        };

        var factory = new StubHttpClientFactory(client);
        return new GitHubPortfolioService(factory, NullLogger<GitHubPortfolioService>.Instance);
    }
}

internal sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}

internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(handler(request));
    }
}
