using System.Text.Json;
using WebApplication1.Models;

namespace WebApplication1.Services;

public sealed class PortfolioSettingsStore : IPortfolioSettingsStore
{
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true
    };
    private readonly string _settingsPath;

    public PortfolioSettingsStore(IWebHostEnvironment environment)
    {
        _settingsPath = Path.Combine(environment.ContentRootPath, "Data", "portfolio-settings.json");
    }

    public async Task<PortfolioSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_settingsPath))
            {
                var defaults = Normalize(new PortfolioSettings());
                await SaveInternalAsync(defaults, cancellationToken);
                return defaults;
            }

            await using var fileStream = File.OpenRead(_settingsPath);
            var settings = await JsonSerializer.DeserializeAsync<PortfolioSettings>(
                fileStream,
                _serializerOptions,
                cancellationToken);

            return Normalize(settings ?? new PortfolioSettings());
        }
        finally
        {
            _syncLock.Release();
        }
    }

    public async Task SaveAsync(PortfolioSettings settings, CancellationToken cancellationToken = default)
    {
        await _syncLock.WaitAsync(cancellationToken);
        try
        {
            await SaveInternalAsync(Normalize(settings), cancellationToken);
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task SaveInternalAsync(PortfolioSettings settings, CancellationToken cancellationToken)
    {
        var directoryPath = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await using var fileStream = File.Create(_settingsPath);
        await JsonSerializer.SerializeAsync(fileStream, settings, _serializerOptions, cancellationToken);
    }

    private static PortfolioSettings Normalize(PortfolioSettings settings)
    {
        settings.GitHubUsername = (settings.GitHubUsername ?? string.Empty).Trim();
        settings.GitHubToken = ResolveSecret(
            currentValue: settings.GitHubToken,
            environmentName: "PORTFOLIO_GITHUB_TOKEN",
            fallbackValue: string.Empty);
        settings.AboutTitle = (settings.AboutTitle ?? string.Empty).Trim();
        settings.AboutText = (settings.AboutText ?? string.Empty).Trim();
        settings.ContactsEmail = (settings.ContactsEmail ?? string.Empty).Trim();
        settings.ContactsTelegram = (settings.ContactsTelegram ?? string.Empty).Trim();
        settings.ContactsWebsite = (settings.ContactsWebsite ?? string.Empty).Trim();
        settings.AdminAccessCode = ResolveSecret(
            currentValue: settings.AdminAccessCode,
            environmentName: "PORTFOLIO_ADMIN_ACCESS_CODE",
            fallbackValue: "change-me");

        settings.SelectedRepositories = (settings.SelectedRepositories ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        settings.ProjectPages = NormalizeProjectPages(settings.ProjectPages);

        return settings;
    }

    private static List<ProjectPresentationSettings> NormalizeProjectPages(
        List<ProjectPresentationSettings>? projectPages)
    {
        var normalized = (projectPages ?? [])
            .Where(page => !string.IsNullOrWhiteSpace(page.RepositoryName))
            .Select(page => new ProjectPresentationSettings
            {
                RepositoryName = page.RepositoryName.Trim(),
                EnableProjectPage = page.EnableProjectPage,
                Slug = NormalizeSlug(page.Slug, page.RepositoryName),
                Title = (page.Title ?? string.Empty).Trim(),
                Intro = (page.Intro ?? string.Empty).Trim(),
                Problem = (page.Problem ?? string.Empty).Trim(),
                Solution = (page.Solution ?? string.Empty).Trim(),
                Result = (page.Result ?? string.Empty).Trim(),
                TechStack = (page.TechStack ?? string.Empty).Trim(),
                HeroImageUrl = (page.HeroImageUrl ?? string.Empty).Trim(),
                MediaItems = NormalizeMediaItems(page.MediaItems)
            })
            .GroupBy(page => page.RepositoryName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();

        var slugUsage = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var page in normalized)
        {
            var baseSlug = string.IsNullOrWhiteSpace(page.Slug)
                ? NormalizeSlug(page.RepositoryName, page.RepositoryName)
                : page.Slug;

            var slug = baseSlug;
            var suffix = 2;
            while (!slugUsage.Add(slug))
            {
                slug = $"{baseSlug}-{suffix}";
                suffix++;
            }

            page.Slug = slug;
        }

        return normalized;
    }

    private static string NormalizeSlug(string? slug, string fallbackSource)
    {
        var source = string.IsNullOrWhiteSpace(slug) ? fallbackSource : slug;
        var chars = source
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var rawSlug = new string(chars);
        while (rawSlug.Contains("--", StringComparison.Ordinal))
        {
            rawSlug = rawSlug.Replace("--", "-", StringComparison.Ordinal);
        }

        rawSlug = rawSlug.Trim('-');
        return string.IsNullOrWhiteSpace(rawSlug) ? "project" : rawSlug;
    }

    private static List<ProjectMediaItem> NormalizeMediaItems(List<ProjectMediaItem>? mediaItems)
    {
        return (mediaItems ?? [])
            .Where(item => !item.Remove)
            .Where(item => !string.IsNullOrWhiteSpace(item.Url))
            .Select(item => new ProjectMediaItem
            {
                Url = item.Url.Trim(),
                MediaType = string.Equals(item.MediaType, "video", StringComparison.OrdinalIgnoreCase)
                    ? "video"
                    : "image",
                Caption = (item.Caption ?? string.Empty).Trim(),
                SortOrder = item.SortOrder
            })
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Url, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveSecret(string? currentValue, string environmentName, string fallbackValue)
    {
        var envValue = Environment.GetEnvironmentVariable(environmentName);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue.Trim();
        }

        return string.IsNullOrWhiteSpace(currentValue)
            ? fallbackValue
            : currentValue.Trim();
    }
}
