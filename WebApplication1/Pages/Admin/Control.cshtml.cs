using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Pages.Admin;

public class ControlModel : PageModel
{
    private const string AuthCookieName = "__portfolio_admin_auth";
    private static readonly HashSet<string> ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
    private static readonly HashSet<string> VideoExtensions = [".mp4", ".webm", ".mov", ".m4v"];

    private readonly IPortfolioSettingsStore _settingsStore;
    private readonly IGitHubPortfolioService _gitHubPortfolioService;
    private readonly IWebHostEnvironment _environment;

    public ControlModel(
        IPortfolioSettingsStore settingsStore,
        IGitHubPortfolioService gitHubPortfolioService,
        IWebHostEnvironment environment)
    {
        _settingsStore = settingsStore;
        _gitHubPortfolioService = gitHubPortfolioService;
        _environment = environment;
    }

    [BindProperty]
    public string AccessCode { get; set; } = string.Empty;

    [BindProperty]
    public string GitHubUsername { get; set; } = string.Empty;

    [BindProperty]
    public string GitHubToken { get; set; } = string.Empty;

    [BindProperty]
    public List<string> SelectedRepositories { get; set; } = [];

    [BindProperty]
    public List<ProjectPresentationSettings> ProjectPages { get; set; } = [];

    [BindProperty]
    public bool ShowAboutSection { get; set; }

    [BindProperty]
    public string AboutTitle { get; set; } = string.Empty;

    [BindProperty]
    public string AboutText { get; set; } = string.Empty;

    [BindProperty]
    public bool ShowContactsSection { get; set; }

    [BindProperty]
    public string ContactsEmail { get; set; } = string.Empty;

    [BindProperty]
    public string ContactsTelegram { get; set; } = string.Empty;

    [BindProperty]
    public string ContactsWebsite { get; set; } = string.Empty;

    [BindProperty]
    public string AdminAccessCode { get; set; } = string.Empty;

    public bool IsAuthorized { get; private set; }
    public string? LoginError { get; private set; }
    public string? SaveStatus { get; private set; }
    public IReadOnlyList<GitHubRepositoryItem> AvailableRepositories { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.GetAsync(cancellationToken);
        IsAuthorized = IsRequestAuthorized(settings);
        if (!IsAuthorized)
        {
            return;
        }

        await FillEditorAsync(settings, cancellationToken);
    }

    public async Task<IActionResult> OnPostLoginAsync(CancellationToken cancellationToken)
    {
        var settings = await _settingsStore.GetAsync(cancellationToken);

        if (!string.Equals(AccessCode, settings.AdminAccessCode, StringComparison.Ordinal))
        {
            LoginError = "Access code is incorrect.";
            IsAuthorized = false;
            return Page();
        }

        SetAuthCookie(settings.AdminAccessCode);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        var currentSettings = await _settingsStore.GetAsync(cancellationToken);
        if (!IsRequestAuthorized(currentSettings))
        {
            return Forbid();
        }

        currentSettings.GitHubUsername = GitHubUsername;
        currentSettings.GitHubToken = string.IsNullOrWhiteSpace(GitHubToken)
            ? currentSettings.GitHubToken
            : GitHubToken.Trim();
        currentSettings.SelectedRepositories = SelectedRepositories;

        var mergedProjectPages = MergeProjectPages(
            currentSettings.ProjectPages,
            ProjectPages,
            currentSettings.SelectedRepositories);
        var uploadedCount = await AppendUploadedMediaAsync(
            mergedProjectPages,
            ProjectPages ?? [],
            Request.Form.Files,
            cancellationToken);
        currentSettings.ProjectPages = mergedProjectPages;

        currentSettings.ShowAboutSection = ShowAboutSection;
        currentSettings.AboutTitle = AboutTitle;
        currentSettings.AboutText = AboutText;
        currentSettings.ShowContactsSection = ShowContactsSection;
        currentSettings.ContactsEmail = ContactsEmail;
        currentSettings.ContactsTelegram = ContactsTelegram;
        currentSettings.ContactsWebsite = ContactsWebsite;
        currentSettings.AdminAccessCode = string.IsNullOrWhiteSpace(AdminAccessCode)
            ? currentSettings.AdminAccessCode
            : AdminAccessCode;

        await _settingsStore.SaveAsync(currentSettings, cancellationToken);

        SetAuthCookie(currentSettings.AdminAccessCode);
        SaveStatus = uploadedCount > 0
            ? $"Saved. Uploaded files: {uploadedCount}."
            : "Saved.";
        IsAuthorized = true;
        await FillEditorAsync(currentSettings, cancellationToken);
        return Page();
    }

    private async Task FillEditorAsync(PortfolioSettings settings, CancellationToken cancellationToken)
    {
        GitHubUsername = settings.GitHubUsername;
        GitHubToken = settings.GitHubToken;
        SelectedRepositories = settings.SelectedRepositories.ToList();
        ShowAboutSection = settings.ShowAboutSection;
        AboutTitle = settings.AboutTitle;
        AboutText = settings.AboutText;
        ShowContactsSection = settings.ShowContactsSection;
        ContactsEmail = settings.ContactsEmail;
        ContactsTelegram = settings.ContactsTelegram;
        ContactsWebsite = settings.ContactsWebsite;
        AdminAccessCode = settings.AdminAccessCode;

        var settingsByRepository = settings.ProjectPages
            .ToDictionary(page => page.RepositoryName, page => page, StringComparer.OrdinalIgnoreCase);

        ProjectPages = settings.SelectedRepositories
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(repositoryName =>
            {
                if (settingsByRepository.TryGetValue(repositoryName, out var existing))
                {
                    return CloneProjectPage(existing);
                }

                return new ProjectPresentationSettings
                {
                    RepositoryName = repositoryName,
                    Slug = BuildSlug(repositoryName),
                    Title = repositoryName
                };
            })
            .ToList();

        if (string.IsNullOrWhiteSpace(settings.GitHubUsername))
        {
            AvailableRepositories = [];
            SaveStatus = "Set GitHub username first, then repositories will load.";
            return;
        }

        var repositoriesResult = await _gitHubPortfolioService.GetRepositoriesAsync(
            settings.GitHubUsername,
            settings.GitHubToken,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(repositoriesResult.ErrorMessage))
        {
            AvailableRepositories = [];
            SaveStatus = $"GitHub warning: {repositoriesResult.ErrorMessage}";
            return;
        }

        AvailableRepositories = repositoriesResult.Repositories
            .OrderByDescending(repo => repo.Stars)
            .ThenBy(repo => repo.Name)
            .ToList();
    }

    private bool IsRequestAuthorized(PortfolioSettings settings)
    {
        if (!Request.Cookies.TryGetValue(AuthCookieName, out var tokenFromCookie))
        {
            return false;
        }

        return string.Equals(
            tokenFromCookie,
            BuildAuthToken(settings.AdminAccessCode),
            StringComparison.Ordinal);
    }

    private void SetAuthCookie(string accessCode)
    {
        Response.Cookies.Append(
            AuthCookieName,
            BuildAuthToken(accessCode),
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Strict,
                Secure = Request.IsHttps,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });
    }

    private static string BuildAuthToken(string accessCode)
    {
        var payload = Encoding.UTF8.GetBytes(accessCode);
        return Convert.ToHexString(SHA256.HashData(payload));
    }

    private async Task<int> AppendUploadedMediaAsync(
        List<ProjectPresentationSettings> mergedPages,
        IReadOnlyList<ProjectPresentationSettings> incomingPages,
        IFormFileCollection files,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0 || incomingPages.Count == 0)
        {
            return 0;
        }

        var mergedByRepository = mergedPages.ToDictionary(
            page => page.RepositoryName,
            page => page,
            StringComparer.OrdinalIgnoreCase);

        var webRootPath = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
        var uploadsRoot = Path.Combine(webRootPath, "uploads", "projects");
        Directory.CreateDirectory(uploadsRoot);

        var uploadedCount = 0;

        foreach (var file in files)
        {
            if (!TryGetProjectIndex(file.Name, out var projectIndex))
            {
                continue;
            }

            if (projectIndex < 0 || projectIndex >= incomingPages.Count)
            {
                continue;
            }

            var repositoryName = incomingPages[projectIndex].RepositoryName;
            if (string.IsNullOrWhiteSpace(repositoryName))
            {
                continue;
            }

            if (!mergedByRepository.TryGetValue(repositoryName, out var targetPage))
            {
                continue;
            }

            if (file.Length == 0)
            {
                continue;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var mediaType = GetMediaType(extension);
            if (mediaType is null)
            {
                continue;
            }

            var repositoryFolder = BuildSlug(repositoryName);
            var projectFolderPath = Path.Combine(uploadsRoot, repositoryFolder);
            Directory.CreateDirectory(projectFolderPath);

            var fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
            var absolutePath = Path.Combine(projectFolderPath, fileName);
            await using var stream = new FileStream(absolutePath, FileMode.Create);
            await file.CopyToAsync(stream, cancellationToken);

            var nextOrder = targetPage.MediaItems.Count == 0
                ? 10
                : targetPage.MediaItems.Max(item => item.SortOrder) + 10;
            var relativeUrl = $"/uploads/projects/{repositoryFolder}/{fileName}";

            targetPage.MediaItems.Add(new ProjectMediaItem
            {
                Url = relativeUrl,
                MediaType = mediaType,
                Caption = string.Empty,
                SortOrder = nextOrder
            });

            uploadedCount++;
        }

        foreach (var page in mergedPages)
        {
            page.MediaItems = page.MediaItems
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Url, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return uploadedCount;
    }

    private static string? GetMediaType(string extension)
    {
        if (ImageExtensions.Contains(extension))
        {
            return "image";
        }

        if (VideoExtensions.Contains(extension))
        {
            return "video";
        }

        return null;
    }

    private static bool TryGetProjectIndex(string inputName, out int projectIndex)
    {
        const string prefix = "ProjectUpload_";
        projectIndex = -1;
        if (!inputName.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var indexText = inputName[prefix.Length..];
        return int.TryParse(indexText, out projectIndex);
    }

    private static List<ProjectPresentationSettings> MergeProjectPages(
        IEnumerable<ProjectPresentationSettings> existingPages,
        IEnumerable<ProjectPresentationSettings> incomingPages,
        IEnumerable<string> selectedRepositories)
    {
        var selectedSet = (selectedRepositories ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var merged = existingPages
            .Where(page => selectedSet.Contains(page.RepositoryName))
            .ToDictionary(page => page.RepositoryName, CloneProjectPage, StringComparer.OrdinalIgnoreCase);

        foreach (var page in incomingPages ?? [])
        {
            if (string.IsNullOrWhiteSpace(page.RepositoryName))
            {
                continue;
            }

            var repositoryName = page.RepositoryName.Trim();
            if (!selectedSet.Contains(repositoryName))
            {
                continue;
            }

            merged[repositoryName] = new ProjectPresentationSettings
            {
                RepositoryName = repositoryName,
                EnableProjectPage = page.EnableProjectPage,
                Slug = string.IsNullOrWhiteSpace(page.Slug) ? BuildSlug(repositoryName) : BuildSlug(page.Slug),
                Title = (page.Title ?? string.Empty).Trim(),
                Intro = (page.Intro ?? string.Empty).Trim(),
                Problem = (page.Problem ?? string.Empty).Trim(),
                Solution = (page.Solution ?? string.Empty).Trim(),
                Result = (page.Result ?? string.Empty).Trim(),
                TechStack = (page.TechStack ?? string.Empty).Trim(),
                HeroImageUrl = (page.HeroImageUrl ?? string.Empty).Trim(),
                MediaItems = NormalizeIncomingMedia(page.MediaItems)
            };
        }

        return merged.Values
            .OrderBy(page => page.RepositoryName)
            .ToList();
    }

    private static List<ProjectMediaItem> NormalizeIncomingMedia(List<ProjectMediaItem>? mediaItems)
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

    private static ProjectPresentationSettings CloneProjectPage(ProjectPresentationSettings source)
    {
        return new ProjectPresentationSettings
        {
            RepositoryName = source.RepositoryName,
            EnableProjectPage = source.EnableProjectPage,
            Slug = source.Slug,
            Title = source.Title,
            Intro = source.Intro,
            Problem = source.Problem,
            Solution = source.Solution,
            Result = source.Result,
            TechStack = source.TechStack,
            HeroImageUrl = source.HeroImageUrl,
            MediaItems = source.MediaItems
                .Select(item => new ProjectMediaItem
                {
                    Url = item.Url,
                    MediaType = item.MediaType,
                    Caption = item.Caption,
                    SortOrder = item.SortOrder
                })
                .ToList()
        };
    }

    private static string BuildSlug(string source)
    {
        var chars = source
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "project" : slug;
    }
}
