using ChatBridgeService.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatBridgeService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<CreatioInstance> CreatioInstances => Set<CreatioInstance>();
    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MessageLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => x.InstanceId);
            e.Property(x => x.Type).HasMaxLength(50).IsRequired();
            e.Property(x => x.PhoneNumber).HasMaxLength(200);
            e.Property(x => x.Details).HasMaxLength(1000);
            e.HasOne(x => x.Instance).WithMany().HasForeignKey(x => x.InstanceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CreatioInstance>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ApiKey).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.ApiKey).HasMaxLength(100).IsRequired();
            e.Property(x => x.CreatioBaseUrl).HasMaxLength(500).IsRequired();
            e.Property(x => x.CreatioIdentityUrl).HasMaxLength(500).IsRequired();
            e.Property(x => x.CreatioClientId).HasMaxLength(200).IsRequired();
            e.Property(x => x.CreatioClientSecret).HasMaxLength(500).IsRequired();
            e.Property(x => x.WhatsAppProvider).HasMaxLength(50).IsRequired();
            e.Property(x => x.MetaAccessToken).HasMaxLength(1000);
            e.Property(x => x.MetaPhoneNumberId).HasMaxLength(100);
            e.Property(x => x.MetaVerifyToken).HasMaxLength(200);
            e.Property(x => x.KirimDevApiKey).HasMaxLength(1000);
            e.Property(x => x.KirimDevPhoneNumberId).HasMaxLength(100);
            e.Property(x => x.KirimDevWebhookSecret).HasMaxLength(500);
        });
    }
}
