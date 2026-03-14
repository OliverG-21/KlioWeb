using Microsoft.EntityFrameworkCore;
using Projekt42.Core.Models;

namespace Projekt42.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Image> Images => Set<Image>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<DuplicateLog> DuplicateLogs => Set<DuplicateLog>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Image>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OriginalFilename).IsRequired().HasMaxLength(500);
            entity.Property(e => e.StoredFilename).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.ThumbnailPath).HasMaxLength(1000);
            entity.Property(e => e.Hash).IsRequired().HasMaxLength(64);
            entity.Property(e => e.MimeType).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Hash).IsUnique();
            entity.HasOne(e => e.Topic)
                  .WithMany(t => t.Images)
                  .HasForeignKey(e => e.TopicId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Topic>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(2000);
        });

        modelBuilder.Entity<DuplicateLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IncomingFilename).IsRequired().HasMaxLength(500);
            entity.Property(e => e.DuplicatePath).HasMaxLength(1000);
            entity.Property(e => e.ExistingImagePath).HasMaxLength(1000);
        });

        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AdminPasswordHash).IsRequired().HasMaxLength(200);
            entity.Property(e => e.StorageBasePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.InboxPath).IsRequired().HasMaxLength(500);
        });
    }
}
