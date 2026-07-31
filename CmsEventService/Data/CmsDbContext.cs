using CmsEventService.Domain;
using Microsoft.EntityFrameworkCore;

namespace CmsEventService.Data;

public sealed class CmsDbContext(DbContextOptions<CmsDbContext> options) : DbContext(options)
{
    public DbSet<CmsEntity> Entities => Set<CmsEntity>();

    public DbSet<CmsEventLog> EventLogs => Set<CmsEventLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CmsEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasMaxLength(128);
            entity.Property(x => x.PayloadJson).IsRequired();
            entity.Property(x => x.LastEventType).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => new { x.IsCmsPublished, x.IsLocallyDisabled });
        });

        modelBuilder.Entity<CmsEventLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntityId).HasMaxLength(128);
            entity.Property(x => x.Type).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1024).IsRequired();
            entity.HasIndex(x => new { x.EntityId, x.Timestamp });
        });
    }
}
