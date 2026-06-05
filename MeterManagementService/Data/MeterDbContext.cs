using MeterManagementService.Models;
using Microsoft.EntityFrameworkCore;

namespace MeterManagementService.Data;

public class MeterDbContext : DbContext
{
    public MeterDbContext(DbContextOptions<MeterDbContext> opts) : base(opts) { }

    public DbSet<Meter> Meters { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Meter>(e =>
        {
            e.ToTable("Meters");
            e.HasKey(m => m.Id);
            e.Property(m => m.SerialNumber).IsRequired().HasMaxLength(30);
            e.HasIndex(m => m.SerialNumber).IsUnique().HasDatabaseName("IX_Meters_SerialNumber");
            e.Property(m => m.Model).IsRequired().HasMaxLength(50);
            e.Property(m => m.AccuracyClass).IsRequired().HasMaxLength(20);
            e.Property(m => m.VoltageRating).IsRequired().HasMaxLength(40);
            e.Property(m => m.CurrentRating).IsRequired().HasMaxLength(30);
            e.Property(m => m.Standards).IsRequired().HasMaxLength(150);
            e.Property(m => m.MeterType).HasConversion<int>();
            e.Property(m => m.CommunicationType).HasConversion<int>();
            e.Property(m => m.Status).HasConversion<int>();
            e.Property(m => m.RelayStatus).HasConversion<int>();
            e.Property(m => m.Zone).HasMaxLength(100);
            e.Property(m => m.SubstationId).HasMaxLength(50);
            e.Property(m => m.FirmwareVersion).HasMaxLength(30);
            e.Property(m => m.InstallationAddress).HasMaxLength(300);
            e.Property(m => m.Notes).HasMaxLength(1000);
            e.Property(m => m.DlmsLogicalDeviceName).HasMaxLength(50);
        });
    }
}
