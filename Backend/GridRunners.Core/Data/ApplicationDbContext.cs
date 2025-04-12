using GridRunners.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace GridRunners.Core.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<MazeGame> Games => Set<MazeGame>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            
            entity.Property(e => e.Username)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.Property(e => e.DisplayName)
                .HasMaxLength(50);
        });

        modelBuilder.Entity<MazeGame>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(50);

            // Configure WinnerId as foreign key to Users table
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.WinnerId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Configure many-to-many relationship between User and MazeGame
            entity.HasMany(e => e.Players)
                .WithMany(e => e.Games);
        });
    }
} 