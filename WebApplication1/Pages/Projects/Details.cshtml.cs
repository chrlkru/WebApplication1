using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Pages.Projects;

public class DetailsModel : PageModel
{
    private readonly IPortfolioSettingsStore _settingsStore;
    private readonly IGitHubPortfolioService _gitHubPortfolioService;

    public DetailsModel(
        IPortfolioSettingsStore settingsStore,
        IGitHubPortfolioService gitHubPortfolioService)
    {
        _settingsStore = settingsStore;
        _gitHubPortfolioService = gitHubPortfolioService;
    }

    public GitHubRepositoryItem? Repository { get; private set; }
    public ProjectPresentationSettings? Presentation { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string Title =>
        string.IsNullOrWhiteSpace(Presentation?.Title)
            ? Repository?.Name ?? "Project"
            : Presentation.Title;

    public IReadOnlyList<string> TechStackItems =>
        string.IsNullOrWhiteSpace(Presentation?.TechStack)
            ? []
            : Presentation.TechStack
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public IReadOnlyList<ProjectMediaItem> CarouselItems =>
        Presentation?.MediaItems
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Url, StringComparer.OrdinalIgnoreCase)
            .ToList() ??
        [];

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.GetAsync(cancellationToken);

        var presentation = settings.ProjectPages.FirstOrDefault(page =>
            page.EnableProjectPage &&
            page.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

        if (presentation is null)
        {
            return NotFound();
        }

        if (!settings.SelectedRepositories.Contains(presentation.RepositoryName, StringComparer.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var repositoriesResult = await _gitHubPortfolioService.GetRepositoriesAsync(
            settings.GitHubUsername,
            settings.GitHubToken,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(repositoriesResult.ErrorMessage))
        {
            ErrorMessage = repositoriesResult.ErrorMessage;
            Presentation = presentation;
            return Page();
        }

        Repository = repositoriesResult.Repositories.FirstOrDefault(repo =>
            repo.Name.Equals(presentation.RepositoryName, StringComparison.OrdinalIgnoreCase));
        Presentation = presentation;

        if (Repository is null)
        {
            ErrorMessage = "Repository is not available on GitHub.";
        }

        return Page();
    }
}
