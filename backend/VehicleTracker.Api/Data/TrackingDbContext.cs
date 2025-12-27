using Microsoft.EntityFrameworkCore;
using VehicleTracker.Api.Data.Entities;

namespace VehicleTracker.Api.Data;

public class TrackingDbContext : DbContext
{
    public TrackingDbContext(DbContextOptions<TrackingDbContext> options) : base(options)
    {
    }

    // Rental system tables
    public DbSet<RentalCompany> Companies => Set<RentalCompany>();
    public DbSet<RentalVehicle> Vehicles => Set<RentalVehicle>();
    
    // External integration tables
    public DbSet<ExternalCompany> ExternalCompanies => Set<ExternalCompany>();
    public DbSet<ExternalCompanyVehicle> ExternalCompanyVehicles => Set<ExternalCompanyVehicle>();
    public DbSet<ExternalVehicle> ExternalVehicles => Set<ExternalVehicle>();
    
    // Tracking tables
    public DbSet<TrackingDevice> TrackingDevices => Set<TrackingDevice>();
    public DbSet<VehicleLocation> VehicleLocations => Set<VehicleLocation>();
    public DbSet<VehicleTrackingStatus> VehicleTrackingStatuses => Set<VehicleTrackingStatus>();
    public DbSet<VehicleTrip> VehicleTrips => Set<VehicleTrip>();
    public DbSet<VehicleEvent> VehicleEvents => Set<VehicleEvent>();
    public DbSet<TrackingSyncLog> TrackingSyncLogs => Set<TrackingSyncLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Company configuration
        modelBuilder.Entity<RentalCompany>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Vehicle configuration
        modelBuilder.Entity<RentalVehicle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.LicensePlate).IsUnique();
            entity.HasIndex(e => e.Vin).IsUnique();
            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // External Company configuration
        modelBuilder.Entity<ExternalCompany>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CompanyName).IsUnique();

            entity.HasOne(e => e.RentalCompany)
                .WithMany()
                .HasForeignKey(e => e.RentalCompanyId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // External Company Vehicle configuration
        modelBuilder.Entity<ExternalCompanyVehicle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ExternalCompanyId, e.ExternalId }).IsUnique();
            entity.HasIndex(e => e.ExternalId);

            entity.HasOne(e => e.ExternalCompany)
                .WithMany(c => c.Vehicles)
                .HasForeignKey(e => e.ExternalCompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // External Vehicle (link table) configuration
        modelBuilder.Entity<ExternalVehicle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ExternalCompanyVehicleId).IsUnique();
            entity.HasIndex(e => e.VehicleId);

            entity.HasOne(e => e.Vehicle)
                .WithMany()
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.ExternalCompanyVehicle)
                .WithOne(ecv => ecv.ExternalVehicle)
                .HasForeignKey<ExternalVehicle>(e => e.ExternalCompanyVehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TrackingDevice configuration
        modelBuilder.Entity<TrackingDevice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Serial).IsUnique();
            entity.HasIndex(e => e.VehicleId).IsUnique();

            entity.HasOne(e => e.Vehicle)
                .WithOne(v => v.TrackingDevice)
                .HasForeignKey<TrackingDevice>(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // VehicleLocation configuration
        modelBuilder.Entity<VehicleLocation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.VehicleId, e.DeviceTimestamp });
            entity.HasIndex(e => new { e.DeviceSerial, e.DeviceTimestamp });
            entity.HasIndex(e => e.DeviceTimestamp);
            entity.HasIndex(e => e.LocationTypeId);

            entity.Property(e => e.Latitude).HasPrecision(10, 7);
            entity.Property(e => e.Longitude).HasPrecision(10, 7);
            entity.Property(e => e.Altitude).HasPrecision(8, 2);
        });

        // VehicleTrackingStatus configuration
        modelBuilder.Entity<VehicleTrackingStatus>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.VehicleId).IsUnique();

            entity.HasOne(e => e.Vehicle)
                .WithOne(v => v.TrackingStatus)
                .HasForeignKey<VehicleTrackingStatus>(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.Property(e => e.Latitude).HasPrecision(10, 7);
            entity.Property(e => e.Longitude).HasPrecision(10, 7);
        });

        // VehicleTrip configuration
        modelBuilder.Entity<VehicleTrip>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.VehicleId, e.StartTime });
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.StartTime);

            entity.Property(e => e.StartLatitude).HasPrecision(10, 7);
            entity.Property(e => e.StartLongitude).HasPrecision(10, 7);
            entity.Property(e => e.EndLatitude).HasPrecision(10, 7);
            entity.Property(e => e.EndLongitude).HasPrecision(10, 7);
        });

        // VehicleEvent configuration
        modelBuilder.Entity<VehicleEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.VehicleId, e.EventTime });
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.Severity);
            entity.HasIndex(e => e.EventTime);

            entity.Property(e => e.Latitude).HasPrecision(10, 7);
            entity.Property(e => e.Longitude).HasPrecision(10, 7);
        });

        // TrackingSyncLog configuration
        modelBuilder.Entity<TrackingSyncLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.SyncType, e.StartedAt });
        });
    }
}
