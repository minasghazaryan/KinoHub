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
    }
}
