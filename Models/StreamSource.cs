namespace KinoHub.Web.Models;

public class StreamSource
{
    public int Id { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    public int MovieId { get; set; }
    public Movie? Movie { get; set; }
}
