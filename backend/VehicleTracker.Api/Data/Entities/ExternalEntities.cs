using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VehicleTracker.Api.Data.Entities;

/// <summary>
/// External tracking company (e.g., Datatrack 247, CalAmp, etc.)
/// </summary>
[Table("external_companies")]
public class ExternalCompany
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("company_name")]
    [MaxLength(255)]
    public string CompanyName { get; set; } = string.Empty;

    [Column("api_base_url")]
    [MaxLength(500)]
    public string? ApiBaseUrl { get; set; }

    [Column("api_username")]
    [MaxLength(255)]
    public string? ApiUsername { get; set; }

    [Column("api_password")]
    [MaxLength(255)]
    public string? ApiPassword { get; set; }

    [Column("api_token")]
    public string? ApiToken { get; set; }

    [Column("token_expires_at")]
    public DateTime? TokenExpiresAt { get; set; }

    [Column("rental_company_id")]
    public Guid? RentalCompanyId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual RentalCompany? RentalCompany { get; set; }
    public virtual ICollection<ExternalCompanyVehicle> Vehicles { get; set; } = new List<ExternalCompanyVehicle>();
}

/// <summary>
/// Vehicle as it exists in an external tracking system
/// </summary>
[Table("external_company_vehicles")]
public class ExternalCompanyVehicle
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("external_company_id")]
    public Guid ExternalCompanyId { get; set; }

    [Column("external_id")]
    [MaxLength(100)]
    public string ExternalId { get; set; } = string.Empty;  // Serial, device ID, etc.

    [Column("name")]
    [MaxLength(255)]
    public string? Name { get; set; }

    [Column("vin")]
    [MaxLength(17)]
    public string? Vin { get; set; }

    [Column("license_plate")]
    [MaxLength(50)]
    public string? LicensePlate { get; set; }

    [Column("make")]
    [MaxLength(100)]
    public string? Make { get; set; }

    [Column("model")]
    [MaxLength(100)]
    public string? Model { get; set; }

    [Column("year")]
    public int? Year { get; set; }

    [Column("color")]
    [MaxLength(50)]
    public string? Color { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("raw_data", TypeName = "jsonb")]
    public string? RawData { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("last_synced_at")]
    public DateTime? LastSyncedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ExternalCompany? ExternalCompany { get; set; }
    public virtual ExternalVehicle? ExternalVehicle { get; set; }
}

/// <summary>
/// Links our vehicles to external company vehicles
/// </summary>
[Table("external_vehicles")]
public class ExternalVehicle
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("vehicle_id")]
    public Guid VehicleId { get; set; }

    [Column("external_company_vehicle_id")]
    public Guid ExternalCompanyVehicleId { get; set; }

    [Column("is_primary")]
    public bool IsPrimary { get; set; } = true;

    [Column("linked_at")]
    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;

    [Column("linked_by")]
    public Guid? LinkedBy { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual RentalVehicle? Vehicle { get; set; }
    public virtual ExternalCompanyVehicle? ExternalCompanyVehicle { get; set; }
}
