using KinoHub.Web.Data;
using KinoHub.Web.Middleware;
using KinoHub.Web.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;

Directory.CreateDirectory("logs");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Warning()

    // Reduce framework noise
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning)

    .Enrich.FromLogContext()

    .WriteTo.Async(a => a.File(
        path: "logs/app-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate:
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    ))

    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddRazorPages(o =>
{
    // Admin is served at /manage instead of /Admin (separate URL, not in nav)
    o.Conventions.AddFolderRouteModelConvention("/Admin", model =>
    {
        foreach (var selector in model.Selectors)
        {
            if (selector.AttributeRouteModel?.Template != null)
                selector.AttributeRouteModel.Template = selector.AttributeRouteModel.Template.Replace("Admin", "manage");
        }
    });
});
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IApiCacheService, ApiCacheService>();

builder.Services.AddDbContext<KinoContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null);
        }));

builder.Services.AddHttpClient("Kinopoisk", (sp, client) =>
{
    client.BaseAddress = new Uri("https://kinopoiskapiunofficial.tech");
    client.DefaultRequestHeaders.Add("X-API-KEY", sp.GetRequiredService<IConfiguration>()["KinopoiskApiKey"] ?? "");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddScoped<KinopoiskService>();

var vibixBase = builder.Configuration["Vibix:BaseUrl"] ?? "https://vibix.org";
builder.Services.AddHttpClient("Vibix", (sp, client) =>
{
    client.BaseAddress = new Uri(vibixBase.TrimEnd('/'));
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    var token = sp.GetRequiredService<IConfiguration>()["Vibix:BearerToken"];
    if (!string.IsNullOrEmpty(token))
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
});
builder.Services.AddScoped<VibixService>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        if (ex != null || httpContext.Response.StatusCode >= 1000)
            return LogEventLevel.Error;

        if (httpContext.Response.StatusCode >= 700)
            return LogEventLevel.Warning;

        return LogEventLevel.Information;
    };
});
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Seed genres and countries from JSON if tables are empty

try
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<KinoContext>();
        db.Database.Migrate();
    }
    app.Run();
}
catch(Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
