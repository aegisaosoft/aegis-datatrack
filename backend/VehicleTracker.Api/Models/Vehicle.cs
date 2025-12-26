namespace VehicleTracker.Api.Models;

public class Vehicle
{
    public string Serial { get; set; } = string.Empty;
    public string? Vin { get; set; }
    public string? Plate { get; set; }
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int OdometerOffset { get; set; }
    public string? Notes { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public int VehicleColor { get; set; }
    public string? AlternateName { get; set; }
    
    public string ColorName => VehicleColor switch
    {
        1 => "Red",
        2 => "Black",
        3 => "White",
        4 => "Blue",
        5 => "Gray",
        6 => "Orange",
        7 => "Yellow",
        8 => "Green",
        9 => "Silver",
        10 => "Other",
        _ => "Unknown"
    };
}

public class VehicleResponse
{
    public int Status { get; set; }
    public Vehicle? Data { get; set; }
}

public class VehiclesResponse
{
    public int Status { get; set; }
    public List<Vehicle> Data { get; set; } = new();
}
