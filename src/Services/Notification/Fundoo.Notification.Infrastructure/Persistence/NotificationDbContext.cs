using Fundoo.Notification.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fundoo.Notification.Infrastructure.Persistence;

public class NotificationDbContext : DbContext
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<NotificationLog>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Recipient).IsRequired().HasMaxLength(256);
            b.Property(x => x.Subject).IsRequired().HasMaxLength(500);
            b.Property(x => x.Status).IsRequired().HasMaxLength(50);
        });
    }
}
