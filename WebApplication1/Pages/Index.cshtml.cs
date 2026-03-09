using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Pages;

public class IndexModel : PageModel
{
    private readonly IPortfolioSettingsStore _settingsStore;
    private readonly IGitHubPortfolioService _gitHubPortfolioService;

    public IndexModel(
        IPortfolioSettingsStore settingsStore,
        IGitHubPortfolioService gitHubPortfolioService)
    {
        _settingsStore = settingsStore;
        _gitHubPortfolioService = gitHubPortfolioService;
    }

    public PortfolioSettings Settings { get; private set; } = new();
    public string GitHubUsername => Settings.GitHubUsername;
    public string ProfileUrl => $"https://github.com/{Settings.GitHubUsername}";
    public IReadOnlyList<GitHubRepositoryItem> Projects { get; private set; } = [];
    public IReadOnlyDictionary<string, ProjectPresentationSettings> ProjectPagesByRepository { get; private set; } =
        new Dictionary<string, ProjectPresentationSettings>(StringComparer.OrdinalIgnoreCase);
    public string? ErrorMessage { get; private set; }
    public string? InfoMessage { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Settings = await _settingsStore.GetAsync(cancellationToken);
        ProjectPagesByRepository = Settings.ProjectPages
            .Where(page => page.EnableProjectPage)
            .Where(page => Settings.SelectedRepositories.Contains(page.RepositoryName, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(page => page.RepositoryName, page => page, StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(Settings.GitHubUsername))
        {
            ErrorMessage = "GitHub username is empty. Open hidden admin panel and set it.";
            return;
        }

        var repositoriesResult = await _gitHubPortfolioService.GetRepositoriesAsync(
            Settings.GitHubUsername,
            Settings.GitHubToken,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(repositoriesResult.ErrorMessage))
        {
            ErrorMessage = repositoriesResult.ErrorMessage;
            return;
        }

        if (Settings.SelectedRepositories.Count == 0)
        {
            InfoMessage = "No repositories selected yet. Open hidden admin panel and pick projects.";
            return;
        }

        var selectionOrder = Settings.SelectedRepositories
            .Select((name, index) => new { Name = name, Index = index })
            .ToDictionary(item => item.Name, item => item.Index, StringComparer.OrdinalIgnoreCase);

        Projects = repositoriesResult.Repositories
            .Where(repo => selectionOrder.ContainsKey(repo.Name))
            .OrderBy(repo => selectionOrder[repo.Name])
            .ToList();

        if (Projects.Count == 0)
        {
            InfoMessage = "Selected repositories are not available. Check names in hidden admin panel.";
            return;
        }

        var loadedRepositoryNames = repositoriesResult.Repositories
            .Select(repo => repo.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingSelections = Settings.SelectedRepositories
            .Where(name => !loadedRepositoryNames.Contains(name))
            .ToList();

        if (missingSelections.Count > 0)
        {
            InfoMessage = $"Some selected repositories are missing: {string.Join(", ", missingSelections)}";
        }
    }

    public string? GetProjectPageLink(string repositoryName)
    {
        if (!ProjectPagesByRepository.TryGetValue(repositoryName, out var page))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(page.Slug)
            ? null
            : $"/projects/{page.Slug}";
    }
}
