namespace WebApplication1.Models;

public sealed class PortfolioSettings
{
    public string GitHubUsername { get; set; } = "your-github-username";
    public string GitHubToken { get; set; } = string.Empty;
    public List<string> SelectedRepositories { get; set; } = [];
    public List<ProjectPresentationSettings> ProjectPages { get; set; } = [];

    public bool ShowAboutSection { get; set; } = true;
    public string AboutTitle { get; set; } = "About me";
    public string AboutText { get; set; } = "Add your summary in the hidden admin panel.";

    public bool ShowContactsSection { get; set; } = true;
    public string ContactsEmail { get; set; } = string.Empty;
    public string ContactsTelegram { get; set; } = string.Empty;
    public string ContactsWebsite { get; set; } = string.Empty;

    public string AdminAccessCode { get; set; } = "change-me";
}
