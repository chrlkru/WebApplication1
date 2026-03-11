using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace WebApplication1.Tests;

public sealed class SitePlaywrightTests : IClassFixture<PlaywrightAppFixture>
{
    private readonly PlaywrightAppFixture _fixture;

    public SitePlaywrightTests(PlaywrightAppFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HomePage_LoadsAndRendersProjectCard()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 }))
            .ToContainTextAsync($"GitHub portfolio for {PlaywrightAppFixture.GitHubUsernameForTests}");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Project page" }))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task ProjectCard_AllowsNavigationToDetailsAndBack()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync(_fixture.BaseUrl);

        await page.GetByRole(AriaRole.Link, new() { Name = "Project page" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync($"{_fixture.BaseUrl}/projects/{PlaywrightAppFixture.ProjectSlugForTests}");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 }))
            .ToContainTextAsync(PlaywrightAppFixture.ProjectTitleForTests);

        await page.GetByRole(AriaRole.Link, new() { Name = "Back to portfolio" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync($"{_fixture.BaseUrl}/");
    }

    [Fact]
    public async Task AdminLogin_ShowsError_ForInvalidAccessCode()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/__portfolio-admin-7f1d2");

        await page.GetByLabel("Access code").FillAsync("wrong-code");
        await page.GetByRole(AriaRole.Button, new() { Name = "Open panel" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Access code is incorrect.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AdminSave_UpdatesSettingsAndReflectsOnHomePage()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync($"{_fixture.BaseUrl}/__portfolio-admin-7f1d2");

        await page.GetByLabel("Access code").FillAsync(PlaywrightAppFixture.AccessCodeForTests);
        await page.GetByRole(AriaRole.Button, new() { Name = "Open panel" }).ClickAsync();

        const string updatedTitle = "Playwright Updated About";
        const string updatedText = "Playwright updated about text.";

        await page.GetByLabel("About title").FillAsync(updatedTitle);
        await page.GetByLabel("About text").FillAsync(updatedText);
        await page.GetByRole(AriaRole.Button, new() { Name = "Save settings" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Saved.")).ToBeVisibleAsync();

        await page.GotoAsync(_fixture.BaseUrl);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = updatedTitle }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText(updatedText)).ToBeVisibleAsync();

        await using var settingsStream = File.OpenRead(_fixture.SettingsPath);
        var settingsJson = await JsonDocument.ParseAsync(settingsStream);
        Assert.Equal(updatedTitle, settingsJson.RootElement.GetProperty("AboutTitle").GetString());
        Assert.Equal(updatedText, settingsJson.RootElement.GetProperty("AboutText").GetString());
    }
}

public sealed class PlaywrightAppFixture : IAsyncLifetime
{
    public const string AccessCodeForTests = "playwright-e2e-access";
    public const string GitHubUsernameForTests = "playwright-user";
    public const string RepositoryNameForTests = "demo-repo";
    public const string ProjectSlugForTests = "demo-project";
    public const string ProjectTitleForTests = "Demo Project Story";

    private Process? _process;
    private FakeGitHubApiServer? _fakeGitHubApiServer;
    private readonly StringBuilder _capturedOutput = new();

    public string BaseUrl { get; private set; } = string.Empty;
    public string SettingsPath { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var appDllPath = Path.Combine(baseDirectory, "WebApplication1.dll");
        if (!File.Exists(appDllPath))
        {
            throw new FileNotFoundException("Web application dll was not found for e2e test run.", appDllPath);
        }

        _fakeGitHubApiServer = new FakeGitHubApiServer();
        await _fakeGitHubApiServer.StartAsync();

        var dataDirectory = Path.Combine(baseDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);
        SettingsPath = Path.Combine(dataDirectory, "portfolio-settings.json");
        await File.WriteAllTextAsync(SettingsPath, BuildTestSettingsJson());

        var port = GetFreePort();
        BaseUrl = $"http://127.0.0.1:{port}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{appDllPath}\" --urls {BaseUrl}",
            WorkingDirectory = baseDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["PORTFOLIO_ADMIN_ACCESS_CODE"] = AccessCodeForTests;
        startInfo.Environment["GitHub__ApiBaseUrl"] = _fakeGitHubApiServer.BaseUrl;

        _process = new Process { StartInfo = startInfo };
        _process.OutputDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                lock (_capturedOutput)
                {
                    _capturedOutput.AppendLine(args.Data);
                }
            }
        };
        _process.ErrorDataReceived += (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                lock (_capturedOutput)
                {
                    _capturedOutput.AppendLine(args.Data);
                }
            }
        };

        if (!_process.Start())
        {
            throw new InvalidOperationException("Failed to start web application for e2e tests.");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        using var httpClient = new HttpClient();
        for (var attempt = 0; attempt < 80; attempt++)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException(
                    $"Web application process exited during startup. Output:{Environment.NewLine}{GetOutputSnapshot()}");
            }

            try
            {
                using var response = await httpClient.GetAsync(BaseUrl);
                if ((int)response.StatusCode >= 200 && (int)response.StatusCode < 400)
                {
                    return;
                }
            }
            catch
            {
                // wait until host is ready
            }

            await Task.Delay(250);
        }

        throw new TimeoutException(
            $"Web application did not start in time for e2e tests. Output:{Environment.NewLine}{GetOutputSnapshot()}");
    }

    public async Task DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }

        _process?.Dispose();

        if (_fakeGitHubApiServer is not null)
        {
            await _fakeGitHubApiServer.DisposeAsync();
        }
    }

    private string GetOutputSnapshot()
    {
        lock (_capturedOutput)
        {
            return _capturedOutput.ToString();
        }
    }

    private static string BuildTestSettingsJson()
    {
        var model = new
        {
            GitHubUsername = GitHubUsernameForTests,
            GitHubToken = "",
            SelectedRepositories = new[] { RepositoryNameForTests },
            ProjectPages = new[]
            {
                new
                {
                    RepositoryName = RepositoryNameForTests,
                    EnableProjectPage = true,
                    Slug = ProjectSlugForTests,
                    Title = ProjectTitleForTests,
                    Intro = "Project intro for playwright flow.",
                    Problem = "Problem section text.",
                    Solution = "Solution section text.",
                    Result = "Result section text.",
                    TechStack = "C#, ASP.NET Core",
                    HeroImageUrl = "",
                    MediaItems = Array.Empty<object>()
                }
            },
            ShowAboutSection = true,
            AboutTitle = "About me",
            AboutText = "Add your summary in the hidden admin panel.",
            ShowContactsSection = true,
            ContactsEmail = "",
            ContactsTelegram = "",
            ContactsWebsite = "",
            AdminAccessCode = "change-me"
        };

        return JsonSerializer.Serialize(model, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

internal sealed class FakeGitHubApiServer : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _serverLoopTask;

    public string BaseUrl { get; }

    public FakeGitHubApiServer()
    {
        BaseUrl = $"http://127.0.0.1:{GetFreePort()}/";
        _listener.Prefixes.Add(BaseUrl);
    }

    public Task StartAsync()
    {
        _listener.Start();
        _serverLoopTask = Task.Run(() => RunServerLoopAsync(_cancellationTokenSource.Token));
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _cancellationTokenSource.Cancel();

        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        _listener.Close();

        if (_serverLoopTask is not null)
        {
            await _serverLoopTask;
        }

        _cancellationTokenSource.Dispose();
    }

    private async Task RunServerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext? context = null;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
                await ProcessContextAsync(context);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            finally
            {
                context?.Response.OutputStream.Close();
            }
        }
    }

    private static async Task ProcessContextAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? string.Empty;
        if (context.Request.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
            path.Equals($"/users/{PlaywrightAppFixture.GitHubUsernameForTests}/repos", StringComparison.OrdinalIgnoreCase))
        {
            var payload = """
                          [
                            {
                              "name": "demo-repo",
                              "description": "Demo repository from fake API.",
                              "language": "C#",
                              "fork": false,
                              "stargazers_count": 42,
                              "forks_count": 7,
                              "html_url": "https://github.com/playwright-user/demo-repo",
                              "homepage": "https://example.test/demo-repo",
                              "updated_at": "2026-03-10T10:00:00Z"
                            },
                            {
                              "name": "demo-fork",
                              "description": "Forked repository that should be filtered out.",
                              "language": "C#",
                              "fork": true,
                              "stargazers_count": 100,
                              "forks_count": 20,
                              "html_url": "https://github.com/playwright-user/demo-fork",
                              "homepage": "",
                              "updated_at": "2026-03-10T10:00:00Z"
                            }
                          ]
                          """;

            var buffer = Encoding.UTF8.GetBytes(payload);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.ContentType = "application/json";
            context.Response.ContentEncoding = Encoding.UTF8;
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
