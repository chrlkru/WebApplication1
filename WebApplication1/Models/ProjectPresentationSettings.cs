namespace WebApplication1.Models;

public sealed class ProjectPresentationSettings
{
    public string RepositoryName { get; set; } = string.Empty;
    public bool EnableProjectPage { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Intro { get; set; } = string.Empty;
    public string Problem { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string TechStack { get; set; } = string.Empty;
    public string HeroImageUrl { get; set; } = string.Empty;
    public List<ProjectMediaItem> MediaItems { get; set; } = [];
}
