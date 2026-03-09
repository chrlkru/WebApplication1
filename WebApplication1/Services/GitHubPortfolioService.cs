using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using WebApplication1.Models;

namespace WebApplication1.Services;

public sealed class GitHubPortfolioService : IGitHubPortfolioService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubPortfolioService> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public GitHubPortfolioService(
        IHttpClientFactory httpClientFactory,
        ILogger<GitHubPortfolioService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<GitHubPortfolioResult> GetRepositoriesAsync(
        string username,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return new GitHubPortfolioResult([], "GitHub username is empty.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("GitHub");
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"users/{Uri.EscapeDataString(username)}/repos?sort=updated&per_page=100");

            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await client.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new GitHubPortfolioResult([], $"GitHub user \"{username}\" was not found.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                var remaining = response.Headers.TryGetValues("X-RateLimit-Remaining", out var remainingValues)
                    ? remainingValues.FirstOrDefault()
                    : null;
                var resetEpochRaw = response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues)
                    ? resetValues.FirstOrDefault()
                    : null;

                if (remaining == "0")
                {
                    var resetAtText = ParseResetTime(resetEpochRaw);
                    return new GitHubPortfolioResult(
                        [],
                        $"GitHub API rate limit exceeded. Reset at {resetAtText}. Add GitHub token in admin panel.");
                }

                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                return new GitHubPortfolioResult(
                    [],
                    $"GitHub returned 403 Forbidden. {ExtractMessageFromBody(responseText)}");
            }

            if (!response.IsSuccessStatusCode)
            {
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                return new GitHubPortfolioResult(
                    [],
                    $"GitHub request failed: {(int)response.StatusCode} {response.StatusCode}. {ExtractMessageFromBody(responseText)}");
            }

            await using var jsonStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var apiRepositories = await JsonSerializer.DeserializeAsync<List<GitHubApiRepository>>(
                                      jsonStream,
                                      SerializerOptions,
                                      cancellationToken) ??
                                  [];

            var mappedRepositories = apiRepositories
                .Where(repo => !repo.Fork)
                .Select(repo => new GitHubRepositoryItem(
                    Name: repo.Name,
                    Description: string.IsNullOrWhiteSpace(repo.Description)
                        ? "No description provided."
                        : repo.Description.Trim(),
                    Language: string.IsNullOrWhiteSpace(repo.Language)
                        ? "Not specified"
                        : repo.Language,
                    Stars: repo.StargazersCount,
                    Forks: repo.ForksCount,
                    HtmlUrl: repo.HtmlUrl,
                    Homepage: repo.Homepage,
                    UpdatedAt: repo.UpdatedAt))
                .ToList();

            return new GitHubPortfolioResult(mappedRepositories, null);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "GitHub API request failed for username {Username}", username);
            return new GitHubPortfolioResult([], "GitHub API is unavailable right now.");
        }
    }

    private static string ParseResetTime(string? resetEpochRaw)
    {
        if (!long.TryParse(resetEpochRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var resetEpoch))
        {
            return "unknown time";
        }

        return DateTimeOffset.FromUnixTimeSeconds(resetEpoch).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz");
    }

    private static string ExtractMessageFromBody(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        try
        {
            using var json = JsonDocument.Parse(responseText);
            if (json.RootElement.TryGetProperty("message", out var messageElement))
            {
                return messageElement.GetString() ?? string.Empty;
            }
        }
        catch
        {
            // ignored
        }

        return string.Empty;
    }
}

internal sealed class GitHubApiRepository
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("fork")]
    public bool Fork { get; set; }

    [JsonPropertyName("stargazers_count")]
    public int StargazersCount { get; set; }

    [JsonPropertyName("forks_count")]
    public int ForksCount { get; set; }

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
