namespace KinoHub.Web.Models;

/// <summary>
/// Film chosen by admin to appear in the "Кинопремьеры" section on the Index page.
/// Stored snapshot so the sidebar does not call the API on every load.
/// </summary>
public class FeaturedPremiere
{
    public int Id { get; set; }
    public int KinopoiskId { get; set; }
    public int DisplayOrder { get; set; }
    public string? NameRu { get; set; }
    public string? NameEn { get; set; }
    public string? PosterUrl { get; set; }
    public int? Year { get; set; }
    public string? PremiereRu { get; set; }
}
