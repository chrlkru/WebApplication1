namespace WebApplication1.Models;

public sealed class ProjectMediaItem
{
    public string Url { get; set; } = string.Empty;
    public string MediaType { get; set; } = "image";
    public string Caption { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool Remove { get; set; }
}
