namespace VehicleTracker.Api.Models;

public class Location
{
    public long Date { get; set; }
    public int TypeId { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int Speed { get; set; }
    public int Status { get; set; }
    public double Voltage { get; set; }
    public int? Heading { get; set; }
    public int? Hdop { get; set; }
    public int? Sats { get; set; }
    
    // Computed properties
    public DateTime DateTimeUtc => DateTimeOffset.FromUnixTimeSeconds(Date).UtcDateTime;
    public int SpeedMph => (int)(Speed / 1.609);
    public bool HasGoodGps => (Status & 1) == 1;
    public bool HasGoodComm => (Status & 2) == 2;
    public bool IsOldFix => (Status & 8) == 8;
}

public class LocationResponse
{
    public int Status { get; set; }
    public List<Location> Data { get; set; } = new();
}
