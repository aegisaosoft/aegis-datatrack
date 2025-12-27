using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VehicleTracker.Api.Data.Entities;

/// <summary>
/// GPS tracking device mapped to a vehicle
/// </summary>
[Table("tracking_devices")]
public class TrackingDevice
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("vehicle_id")]
    public Guid VehicleId { get; set; }

    [Column("serial")]
    [MaxLength(50)]
    public string Serial { get; set; } = string.Empty;

    [Column("device_name")]
    [MaxLength(100)]
    public string? DeviceName { get; set; }

    [Column("imei")]
    [MaxLength(20)]
    public string? Imei { get; set; }

    [Column("sim_number")]
    [MaxLength(20)]
    public string? SimNumber { get; set; }

    [Column("firmware_version")]
    [MaxLength(20)]
    public string? FirmwareVersion { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("installed_at")]
    public DateTime? InstalledAt { get; set; }

    [Column("last_communication_at")]
    public DateTime? LastCommunicationAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual RentalVehicle? Vehicle { get; set; }
}

/// <summary>
/// Historical location data from GPS tracking devices
/// </summary>
[Table("vehicle_locations")]
public class VehicleLocation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("vehicle_id")]
    public Guid VehicleId { get; set; }

    [Column("device_serial")]
    [MaxLength(50)]
    public string DeviceSerial { get; set; } = string.Empty;

    [Column("latitude")]
    public decimal Latitude { get; set; }

    [Column("longitude")]
    public decimal Longitude { get; set; }

    [Column("altitude")]
    public decimal? Altitude { get; set; }

    [Column("heading")]
    public short? Heading { get; set; }

    [Column("speed_kmh")]
    public short SpeedKmh { get; set; }

    [Column("odometer_meters")]
    public long? OdometerMeters { get; set; }

    [Column("location_type_id")]
    public short LocationTypeId { get; set; }

    [Column("gps_quality")]
    public short GpsQuality { get; set; }

    [Column("voltage_mv")]
    public int? VoltageMv { get; set; }

    [Column("ignition_on")]
    public bool IgnitionOn { get; set; }

    [Column("starter_disabled")]
    public bool StarterDisabled { get; set; }

    [Column("device_timestamp")]
    public DateTime DeviceTimestamp { get; set; }

    [Column("received_at")]
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Current tracking status for each vehicle (latest position)
/// </summary>
[Table("vehicle_tracking_status")]
public class VehicleTrackingStatus
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("vehicle_id")]
    public Guid VehicleId { get; set; }

    [Column("device_serial")]
    [MaxLength(50)]
    public string DeviceSerial { get; set; } = string.Empty;

    [Column("latitude")]
    public decimal Latitude { get; set; }

    [Column("longitude")]
    public decimal Longitude { get; set; }

    [Column("address")]
    [MaxLength(500)]
    public string? Address { get; set; }

    [Column("speed_kmh")]
    public short SpeedKmh { get; set; }

    [Column("heading")]
    public short? Heading { get; set; }

    [Column("location_type_id")]
    public short LocationTypeId { get; set; }

    [Column("voltage_mv")]
    public int? VoltageMv { get; set; }

    [Column("odometer_meters")]
    public long? OdometerMeters { get; set; }

    [Column("is_moving")]
    public bool IsMoving { get; set; }

    [Column("ignition_on")]
    public bool IgnitionOn { get; set; }

    [Column("starter_disabled")]
    public bool StarterDisabled { get; set; }

    [Column("device_timestamp")]
    public DateTime DeviceTimestamp { get; set; }

    [Column("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual RentalVehicle? Vehicle { get; set; }
}

/// <summary>
/// Individual vehicle trips from ignition on to ignition off
/// </summary>
[Table("vehicle_trips")]
public class VehicleTrip
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("vehicle_id")]
    public Guid VehicleId { get; set; }

    [Column("device_serial")]
    [MaxLength(50)]
    public string DeviceSerial { get; set; } = string.Empty;

    [Column("start_time")]
    public DateTime StartTime { get; set; }

    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    [Column("start_latitude")]
    public decimal StartLatitude { get; set; }

    [Column("start_longitude")]
    public decimal StartLongitude { get; set; }

    [Column("start_address")]
    [MaxLength(500)]
    public string? StartAddress { get; set; }

    [Column("end_latitude")]
    public decimal? EndLatitude { get; set; }

    [Column("end_longitude")]
    public decimal? EndLongitude { get; set; }

    [Column("end_address")]
    [MaxLength(500)]
    public string? EndAddress { get; set; }

    [Column("distance_meters")]
    public int DistanceMeters { get; set; }

    [Column("max_speed_kmh")]
    public short MaxSpeedKmh { get; set; }

    [Column("avg_speed_kmh")]
    public short AvgSpeedKmh { get; set; }

    [Column("idle_duration_seconds")]
    public int IdleDurationSeconds { get; set; }

    [Column("start_odometer_meters")]
    public long? StartOdometerMeters { get; set; }

    [Column("end_odometer_meters")]
    public long? EndOdometerMeters { get; set; }

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "in_progress";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Significant vehicle events from tracking devices
/// </summary>
[Table("vehicle_events")]
public class VehicleEvent
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("vehicle_id")]
    public Guid VehicleId { get; set; }

    [Column("device_serial")]
    [MaxLength(50)]
    public string DeviceSerial { get; set; } = string.Empty;

    [Column("event_type")]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    [Column("event_code")]
    public short? EventCode { get; set; }

    [Column("severity")]
    [MaxLength(20)]
    public string Severity { get; set; } = "info";

    [Column("latitude")]
    public decimal? Latitude { get; set; }

    [Column("longitude")]
    public decimal? Longitude { get; set; }

    [Column("address")]
    [MaxLength(500)]
    public string? Address { get; set; }

    [Column("event_data", TypeName = "jsonb")]
    public string? EventData { get; set; }

    [Column("event_time")]
    public DateTime EventTime { get; set; }

    [Column("received_at")]
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    [Column("acknowledged_at")]
    public DateTime? AcknowledgedAt { get; set; }

    [Column("acknowledged_by")]
    public Guid? AcknowledgedBy { get; set; }
}

/// <summary>
/// Log of data synchronization with Datatrack API
/// </summary>
[Table("tracking_sync_log")]
public class TrackingSyncLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("sync_type")]
    [MaxLength(50)]
    public string SyncType { get; set; } = string.Empty;

    [Column("started_at")]
    public DateTime StartedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("records_fetched")]
    public int RecordsFetched { get; set; }

    [Column("records_inserted")]
    public int RecordsInserted { get; set; }

    [Column("records_updated")]
    public int RecordsUpdated { get; set; }

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = "running";

    [Column("error_message")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Vehicle from the rental system (aegis_ao_rental.vehicles)
/// </summary>
[Table("vehicles")]
public class RentalVehicle
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Column("color")]
    [MaxLength(50)]
    public string? Color { get; set; }

    [Column("license_plate")]
    [MaxLength(50)]
    public string LicensePlate { get; set; } = string.Empty;

    [Column("vin")]
    [MaxLength(17)]
    public string? Vin { get; set; }

    [Column("mileage")]
    public int Mileage { get; set; }

    [Column("transmission")]
    [MaxLength(50)]
    public string? Transmission { get; set; }

    [Column("seats")]
    public int? Seats { get; set; }

    [Column("status")]
    [MaxLength(50)]
    public string Status { get; set; } = "Available";

    [Column("state")]
    [MaxLength(2)]
    public string? State { get; set; }

    [Column("location")]
    [MaxLength(255)]
    public string? Location { get; set; }

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [Column("tag")]
    [MaxLength(50)]
    public string? Tag { get; set; }

    [Column("location_id")]
    public Guid? LocationId { get; set; }

    [Column("current_location_id")]
    public Guid? CurrentLocationId { get; set; }

    [Column("vehicle_model_id")]
    public Guid? VehicleModelId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual RentalCompany? Company { get; set; }
    public virtual TrackingDevice? TrackingDevice { get; set; }
    public virtual VehicleTrackingStatus? TrackingStatus { get; set; }
}

/// <summary>
/// Company from the rental system (aegis_ao_rental.companies)
/// </summary>
[Table("companies")]
public class RentalCompany
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("company_name")]
    [MaxLength(255)]
    public string CompanyName { get; set; } = string.Empty;

    [Column("email")]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model catalog from rental system (aegis_ao_rental.models)
/// </summary>
[Table("models")]
public class RentalModel
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("make")]
    [MaxLength(100)]
    public string Make { get; set; } = string.Empty;

    [Required]
    [Column("model")]
    [MaxLength(100)]
    public string ModelName { get; set; } = string.Empty;

    [Required]
    [Column("year")]
    public int Year { get; set; }

    [Column("fuel_type")]
    [MaxLength(50)]
    public string? FuelType { get; set; }

    [Column("transmission")]
    [MaxLength(50)]
    public string? Transmission { get; set; }

    [Column("seats")]
    public int? Seats { get; set; }

    [Column("category_id")]
    public Guid? CategoryId { get; set; }
}

/// <summary>
/// Vehicle model catalog linking companies to models (aegis_ao_rental.vehicle_model)
/// </summary>
[Table("vehicle_model")]
public class RentalVehicleModel
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("company_id")]
    public Guid CompanyId { get; set; }

    [Column("model_id")]
    public Guid ModelId { get; set; }

    [Column("daily_rate")]
    public decimal? DailyRate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
