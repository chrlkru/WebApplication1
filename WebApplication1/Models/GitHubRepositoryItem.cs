namespace WebApplication1.Models;

public sealed record GitHubRepositoryItem(
    string Name,
    string Description,
    string Language,
    int Stars,
    int Forks,
    string HtmlUrl,
    string? Homepage,
    DateTimeOffset UpdatedAt);
