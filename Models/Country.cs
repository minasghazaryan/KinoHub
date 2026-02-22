using System.Collections.Generic;

namespace KinoHub.Web.Models;

public class Country
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Movie> Movies { get; set; } = new();
}
