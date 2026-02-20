namespace KinoHub.Web;

/// <summary>Thrown when Kinopoisk API returns 402 Payment Required (quota exceeded).</summary>
public class KinopoiskQuotaExceededException : Exception
{
    public KinopoiskQuotaExceededException() : base("Kinopoisk API returned 402 Payment Required. Check your API key and quota at https://kinopoiskapiunofficial.tech") { }
}
