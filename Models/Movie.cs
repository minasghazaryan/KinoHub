using System.Collections.Generic;

namespace KinoHub.Web.Models;

public class Movie
{
    public int Id { get; set; }
    public int TmdbId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReleaseYear { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;

    public List<StreamSource> StreamSources { get; set; } = new();
}
