using KinoHub.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace KinoHub.Web.Data;

public class KinoContext : DbContext
{
    public KinoContext(DbContextOptions<KinoContext> options)
        : base(options)
    {
    }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<StreamSource> StreamSources => Set<StreamSource>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<FeaturedPremiere> FeaturedPremieres => Set<FeaturedPremiere>();
    public DbSet<FeaturedCarousel> FeaturedCarousels => Set<FeaturedCarousel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Movie>()
            .HasIndex(m => m.ImdbId)
            .IsUnique();
        modelBuilder.Entity<Movie>()
            .HasMany(m => m.StreamSources)
            .WithOne(s => s.Movie!)
            .HasForeignKey(s => s.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Movie>()
            .HasMany(m => m.Genres)
            .WithMany(g => g.Movies)
            .UsingEntity(j => j.ToTable("MovieGenres"));

        modelBuilder.Entity<Movie>()
            .HasMany(m => m.Countries)
            .WithMany(c => c.Movies)
            .UsingEntity(j => j.ToTable("MovieCountries"));

        modelBuilder.Entity<Genre>(e =>
        {
            e.HasKey(g => g.Id);
            e.Property(g => g.Id).ValueGeneratedNever();
            e.Property(g => g.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<Country>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
            e.Property(c => c.Name).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<FeaturedPremiere>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.NameRu).HasMaxLength(500);
            e.Property(f => f.NameEn).HasMaxLength(500);
            e.Property(f => f.PosterUrl).HasMaxLength(1000);
            e.Property(f => f.PremiereRu).HasMaxLength(20);
        });

        modelBuilder.Entity<FeaturedCarousel>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.NameRu).HasMaxLength(500);
            e.Property(f => f.NameEn).HasMaxLength(500);
            e.Property(f => f.PosterUrl).HasMaxLength(1000);
            e.Property(f => f.ReleaseYear).HasMaxLength(20);
        });
    }
}
