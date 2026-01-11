using CarServiceAssistant.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CarServiceAssistant.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<MileageEntry> MileageEntries => Set<MileageEntry>();
    public DbSet<ServiceRecord> ServiceRecords => Set<ServiceRecord>();
    public DbSet<AiIntervalCache> AiIntervalCaches => Set<AiIntervalCache>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Vehicle>().HasIndex(v => new { v.UserId, v.Brand, v.Model, v.Year, v.FuelType });
        builder.Entity<Vehicle>().Property(v => v.Brand).HasMaxLength(60);
        builder.Entity<Vehicle>().Property(v => v.Model).HasMaxLength(80);

        builder.Entity<AiIntervalCache>()
            .HasIndex(x => new { x.VehicleId, x.Area })
            .IsUnique();

        builder.Entity<AiIntervalCache>()
            .Property(x => x.ResultJson)
            .IsRequired();
    }
}
