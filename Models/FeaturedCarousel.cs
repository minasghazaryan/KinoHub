namespace KinoHub.Web.Models;

/// <summary>
/// Film chosen by admin to appear in the "Подборка" carousel on the Index page.
/// Stored snapshot so the carousel does not call the API on every load.
/// </summary>
public class FeaturedCarousel
{
    public int Id { get; set; }
    public int KinopoiskId { get; set; }
    public int DisplayOrder { get; set; }
    public string? NameRu { get; set; }
    public string? NameEn { get; set; }
    public string? PosterUrl { get; set; }
    public string? ReleaseYear { get; set; }
    public double? Rating { get; set; }
}
