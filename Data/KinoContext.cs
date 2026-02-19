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
    }
}
