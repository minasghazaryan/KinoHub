using System.Collections.Generic;

namespace KinoHub.Web.Models;

public class Movie
{
    public int Id { get; set; }
    public string ImdbId { get; set; } = string.Empty;
    public int? KinopoiskId { get; set; }
    public double? Rating { get; set; }
    public string Title { get; set; } = string.Empty;
    public string NameRu { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ReleaseYear { get; set; } = string.Empty;
    public string PosterPath { get; set; } = string.Empty;

    public List<StreamSource> StreamSources { get; set; } = new();
    public List<Genre> Genres { get; set; } = new();
    public List<Country> Countries { get; set; } = new();
}
