using KinoHub.Web.Data;
using KinoHub.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddDbContext<KinoContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient("RapidApiMovie", client =>
{
    client.BaseAddress = new Uri("https://movie-database-imdb-alternative.p.rapidapi.com/");
});
builder.Services.AddScoped<RapidApiMovieService>();

builder.Services.AddHttpClient("Kinopoisk", (sp, client) =>
{
    client.BaseAddress = new Uri("https://kinopoiskapiunofficial.tech");
    client.DefaultRequestHeaders.Add("X-API-KEY", sp.GetRequiredService<IConfiguration>()["KinopoiskApiKey"] ?? "");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
});
builder.Services.AddScoped<KinopoiskService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// Seed genres and countries from JSON if tables are empty
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<KinoContext>();
    var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
    await GenresCountriesSeeder.SeedAsync(context, env.ContentRootPath);
}

app.Run();
