using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Tests;

public sealed class PortfolioSettingsStoreTests : IDisposable
{
    private readonly string _tempRootPath = Path.Combine(
        Path.GetTempPath(),
        "WebApplication1.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task GetAsync_CreatesDefaults_AndReadsSecretsFromEnvironment()
    {
        var originalToken = Environment.GetEnvironmentVariable("PORTFOLIO_GITHUB_TOKEN");
        var originalAccessCode = Environment.GetEnvironmentVariable("PORTFOLIO_ADMIN_ACCESS_CODE");

        try
        {
            Environment.SetEnvironmentVariable("PORTFOLIO_GITHUB_TOKEN", "  env-token  ");
            Environment.SetEnvironmentVariable("PORTFOLIO_ADMIN_ACCESS_CODE", "  env-access  ");

            var environment = new TestWebHostEnvironment(_tempRootPath);
            var store = new PortfolioSettingsStore(environment);

            var settings = await store.GetAsync();

            Assert.Equal("env-token", settings.GitHubToken);
            Assert.Equal("env-access", settings.AdminAccessCode);
            Assert.True(File.Exists(Path.Combine(_tempRootPath, "Data", "portfolio-settings.json")));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PORTFOLIO_GITHUB_TOKEN", originalToken);
            Environment.SetEnvironmentVariable("PORTFOLIO_ADMIN_ACCESS_CODE", originalAccessCode);
        }
    }

    [Fact]
    public async Task SaveAsync_NormalizesSelectedRepositoriesSlugsAndMedia()
    {
        var environment = new TestWebHostEnvironment(_tempRootPath);
        var store = new PortfolioSettingsStore(environment);

        var settings = new PortfolioSettings
        {
            GitHubUsername = "  octocat  ",
            SelectedRepositories = [" RepoOne ", "repoone", "RepoTwo", " "],
            ProjectPages =
            [
                new ProjectPresentationSettings
                {
                    RepositoryName = " RepoOne ",
                    Slug = " Same Slug ",
                    MediaItems =
                    [
                        new ProjectMediaItem
                        {
                            Url = " /uploads/a.jpg ",
                            MediaType = "IMAGE",
                            Caption = "  first  ",
                            SortOrder = 20
                        },
                        new ProjectMediaItem
                        {
                            Url = " ",
                            MediaType = "video",
                            SortOrder = 10
                        }
                    ]
                },
                new ProjectPresentationSettings
                {
                    RepositoryName = "RepoTwo",
                    Slug = "same slug",
                    MediaItems =
                    [
                        new ProjectMediaItem
                        {
                            Url = " /uploads/b.mp4 ",
                            MediaType = "video",
                            Caption = "  second  ",
                            SortOrder = 5
                        },
                        new ProjectMediaItem
                        {
                            Url = "/uploads/remove.jpg",
                            MediaType = "image",
                            Remove = true,
                            SortOrder = 1
                        }
                    ]
                }
            ]
        };

        await store.SaveAsync(settings);
        var normalized = await store.GetAsync();

        Assert.Equal("octocat", normalized.GitHubUsername);
        Assert.Equal(["RepoOne", "RepoTwo"], normalized.SelectedRepositories);

        var pageByRepository = normalized.ProjectPages
            .ToDictionary(page => page.RepositoryName, StringComparer.OrdinalIgnoreCase);

        Assert.Equal("same-slug", pageByRepository["RepoOne"].Slug);
        Assert.Equal("same-slug-2", pageByRepository["RepoTwo"].Slug);

        var repoOneMedia = Assert.Single(pageByRepository["RepoOne"].MediaItems);
        Assert.Equal("/uploads/a.jpg", repoOneMedia.Url);
        Assert.Equal("image", repoOneMedia.MediaType);
        Assert.Equal("first", repoOneMedia.Caption);

        var repoTwoMedia = Assert.Single(pageByRepository["RepoTwo"].MediaItems);
        Assert.Equal("/uploads/b.mp4", repoTwoMedia.Url);
        Assert.Equal("video", repoTwoMedia.MediaType);
        Assert.Equal("second", repoTwoMedia.Caption);
    }

    [Fact]
    public async Task SaveAsync_WhenRepositoryIsDuplicated_KeepsLastRecord()
    {
        var environment = new TestWebHostEnvironment(_tempRootPath);
        var store = new PortfolioSettingsStore(environment);

        var settings = new PortfolioSettings
        {
            ProjectPages =
            [
                new ProjectPresentationSettings
                {
                    RepositoryName = "RepoOne",
                    Title = "First title"
                },
                new ProjectPresentationSettings
                {
                    RepositoryName = "repoone",
                    Title = "Second title"
                }
            ]
        };

        await store.SaveAsync(settings);
        var normalized = await store.GetAsync();

        var singlePage = Assert.Single(normalized.ProjectPages);
        Assert.Equal("Second title", singlePage.Title);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRootPath))
        {
            Directory.Delete(_tempRootPath, recursive: true);
        }
    }
}

internal sealed class TestWebHostEnvironment : IWebHostEnvironment
{
    public TestWebHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
        WebRootPath = Path.Combine(contentRootPath, "wwwroot");
        ContentRootFileProvider = new NullFileProvider();
        WebRootFileProvider = new NullFileProvider();
    }

    public string ApplicationName { get; set; } = "WebApplication1.Tests";
    public IFileProvider WebRootFileProvider { get; set; }
    public string WebRootPath { get; set; }
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
}
