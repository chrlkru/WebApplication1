using System.Net.Http.Headers;
using WebApplication1.Services;

LoadDotEnv(Path.Combine(Directory.GetCurrentDirectory(), ".env"));
LoadDotEnv(Path.Combine(Directory.GetCurrentDirectory(), "..", ".env"));

var builder = WebApplication.CreateBuilder(args);
var gitHubApiBaseUrl = builder.Configuration["GitHub:ApiBaseUrl"] ?? "https://api.github.com/";

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddHttpClient("GitHub", client =>
{
    client.BaseAddress = new Uri(gitHubApiBaseUrl);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WebApplication1-GitHubPortfolio");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    client.Timeout = TimeSpan.FromSeconds(12);
});
builder.Services.AddSingleton<IPortfolioSettingsStore, PortfolioSettingsStore>();
builder.Services.AddScoped<IGitHubPortfolioService, GitHubPortfolioService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

static void LoadDotEnv(string path)
{
    if (!File.Exists(path))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            continue;
        }

        var separator = line.IndexOf('=');
        if (separator <= 0)
        {
            continue;
        }

        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim();
        if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
        {
            value = value[1..^1];
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}
