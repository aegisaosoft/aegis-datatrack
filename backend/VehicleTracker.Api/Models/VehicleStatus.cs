namespace VehicleTracker.Api.Models;

public class VehicleStatus
{
    public string Serial { get; set; } = string.Empty;
    public long Date { get; set; }
    public int TypeId { get; set; }
    public int Speed { get; set; }
    public long Disabled { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public long Buzzer { get; set; }
    public double Distance { get; set; }
    public double Volts { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Vin { get; set; }
    
    // Computed properties for frontend
    public DateTime DateTimeUtc => DateTimeOffset.FromUnixTimeSeconds(Date).UtcDateTime;
    public bool IsDisabled => Disabled > 0;
    public bool IsBuzzerActive => Buzzer > 0;
    public double DistanceKm => Distance / 1000.0;
    public double DistanceMiles => DistanceKm / 1.609;
    public double Voltage => Volts / 1000.0;
    public string LocationType => GetLocationType(TypeId);
    
    private static string GetLocationType(int typeId) => typeId switch
    {
        2 => "Ignition Off",
        3 => "Stopped Heartbeat",
        4 => "Ignition On",
        5 => "Moving Heartbeat",
        6 => "Input 1 High",
        7 => "Input 1 Low",
        8 => "Input 2 High",
        9 => "Input 2 Low",
        22 => "Power Connected",
        23 => "Power Disconnected",
        24 => "Starter Disabled",
        25 => "Starter Enabled",
        26 => "Stop",
        30 => "GPS Acquired",
        32 => "Impact Alert",
        33 => "Harsh Acceleration",
        34 => "Harsh Brake",
        35 => "Swerve Left",
        36 => "Swerve Right",
        39 => "Cold Boot",
        40 => "User Locate",
        41 => "Status Check",
        42 => "Warm Boot",
        _ => "Unknown"
    };
}

public class VehicleStatusResponse
{
    public int Status { get; set; }
    public List<VehicleStatus> Data { get; set; } = new();
}

public class SingleVehicleStatusResponse
{
    public int Status { get; set; }
    public VehicleStatus? Data { get; set; }
}
