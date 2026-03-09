using WebApplication1.Models;

namespace WebApplication1.Services;

public interface IGitHubPortfolioService
{
    Task<GitHubPortfolioResult> GetRepositoriesAsync(
        string username,
        string? token = null,
        CancellationToken cancellationToken = default);
}

public sealed record GitHubPortfolioResult(
    IReadOnlyList<GitHubRepositoryItem> Repositories,
    string? ErrorMessage);
