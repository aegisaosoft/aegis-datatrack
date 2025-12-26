using Microsoft.AspNetCore.Mvc;
using VehicleTracker.Api.Data.Entities;
using VehicleTracker.Api.Services;

namespace VehicleTracker.Api.Controllers;

/// <summary>
/// Controller for managing tracking devices and stored tracking data
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TrackingController : ControllerBase
{
    private readonly ITrackingRepository? _repository;
    private readonly IDatatrackService _datatrackService;
    private readonly ILogger<TrackingController> _logger;

    public TrackingController(
        IDatatrackService datatrackService,
        ILogger<TrackingController> logger,
        ITrackingRepository? repository = null)
    {
        _repository = repository;
        _datatrackService = datatrackService;
        _logger = logger;
    }

    #region Device Management

    /// <summary>
    /// Get all tracking devices
    /// </summary>
    [HttpGet("devices")]
    public async Task<ActionResult<List<TrackingDevice>>> GetDevices()
    {
        if (_repository == null)
            return BadRequest("Database not configured");

        var devices = await _repository.GetActiveDevicesAsync();
        return Ok(devices);
    }

    /// <summary>
    /// Get a tracking device by serial number
    /// </summary>
    [HttpGet("devices/{serial}")]
    public async Task<ActionResult<TrackingDevice>> GetDevice(string serial)
    {
        if (_repository == null)
            return BadRequest("Database not configured");

        var device = await _repository.GetDeviceBySerialAsync(serial);
        if (device == null)
            return NotFound();

        return Ok(device);
    }

    /// <summary>
    /// Map a Datatrack device to a vehicle
    /// </summary>
    [HttpPost("devices")]
    public async Task<ActionResult<TrackingDevice>> MapDevice([FromBody] MapDeviceRequest request)
    {
        if (_repository == null)
            return BadRequest("Database not configured");

        // Verify the device exists in Datatrack
        var datatrackVehicle = await _datatrackService.GetVehicleAsync(request.Serial);
        if (datatrackVehicle == null)
            return BadRequest($"Device with serial {request.Serial} not found in Datatrack");

        var device = new TrackingDevice
        {
            VehicleId = request.VehicleId,
            Serial = request.Serial,
            DeviceName = datatrackVehicle.Name,
            IsActive = true,
            InstalledAt = DateTime.UtcNow
        };

        var result = await _repository.CreateOrUpdateDeviceAsync(device);
        return CreatedAtAction(nameof(GetDevice), new { serial = result.Serial }, result);
    }

    /// <summary>
    /// Remove device mapping
    /// </summary>
    [HttpDelete("devices/{serial}")]
    public async Task<ActionResult> UnmapDevice(string serial)
    {
        if (_repository == null)
            return BadRequest("Database not configured");

        var device = await _repository.GetDeviceBySerialAsync(serial);
        if (device == null)
            return NotFound();

        device.IsActive = false;
        await _repository.CreateOrUpdateDeviceAsync(device);

        return NoContent();
    }

    /// <summary>
    /// Get available (unmapped) devices from Datatrack
    /// </summary>
    [HttpGet("devices/available")]
    public async Task<ActionResult> GetAvailableDevices()
    {
        // Get all devices from Datatrack
        var datatrackVehicles = await _datatrackService.GetAllVehiclesAsync();
        if (datatrackVehicles == null)
            return Ok(Array.Empty<object>());

        if (_repository == null)
        {
            // No database, return all Datatrack devices
            return Ok(datatrackVehicles.Select(v => new
            {
                v.Serial,
                v.Name,
                v.Vin,
                v.Color
            }));
        }

        // Get mapped devices
        var mappedDevices = await _repository.GetActiveDevicesAsync();
        var mappedSerials = mappedDevices.Select(d => d.Serial).ToHashSet();

        // Filter to unmapped devices
        var available = datatrackVehicles
            .Where(v => !mappedSerials.Contains(v.Serial))
            .Select(v => new
            {
                v.Serial,
                v.Name,
                v.Vin,
                v.Color
            });

        return Ok(available);
    }

    #endregion

    #region Stored Tracking Data

    /// <summary>
    /// Get current tracking status for all vehicles
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<List<VehicleTrackingStatus>>> GetAllStatus()
    {
        if (_repository == null)
            return BadRequest("Database not configured");

        var statuses = await _repository.GetAllTrackingStatusesAsync();
        return Ok(statuses);
    }

    /// <summary>
    /// Get current tracking status for a vehicle
    /// </summary>
    [HttpGet("status/{vehicleId}")]
    public async Task<ActionResult<VehicleTrackingStatus>> GetStatus(Guid vehicleId)
    {
        if (_repository == null)
            return BadRequest("Database not configured");

        var status = await _repository.GetTrackingStatusAsync(vehicleId);
        if (status == null)
            return NotFound();

        return Ok(status);
    }

    /// <summary>
    /// Get tracking status for all vehicles in a company
    /// </summary>
    [HttpGet("status/company/{companyId}")]
    public async Task<ActionResult<List<VehicleTrackingStatus>>> GetStatusByCompany(Guid companyId)
    {
        if (_repository == null)
            return BadRequest("Database not configured");

        var statuses = await _repository.GetTrackingStatusesByCompanyAsync(companyId);
        return Ok(statuses);
    }

    /// <summary>
    /// Get location history from database
    /// </summary>
    [HttpGet("locations/{vehicleId}")]
    public async Task<ActionResult<List<VehicleLocation>>> GetLocations(
        Guid vehicleId,
        [FromQuery] int hoursBack = 24)
    {
        if (_repository == null)
            return BadRequest("Database not configured");

        if (hoursBack > 720) // Max 30 days
            hoursBack = 720;

        var end = DateTime.UtcNow;
        var start = end.AddHours(-hoursBack);

        var locations = await _repository.GetLocationsAsync(vehicleId, start, end);
        return Ok(locations);
    }

    #endregion

    #region Trips

    /// <summary>
    /// Get trips for a vehicle
    /// </summary>
    [HttpGet("trips/{vehicleId}")]
    public async Task<ActionResult<List<VehicleTrip>>> GetTrips(
        Guid vehicleId,
        [FromQuery] int daysBack = 7)
    {
        if (_repository == null)
            return BadRequest("Database not configured");

        if (daysBack > 90)
            daysBack = 90;

        var end = DateTime.UtcNow;
        var start = end.AddDays(-daysBack);

        var trips = await _repository.GetTripsAsync(vehicleId, start, end);
        return Ok(trips);
    }

    /// <summary>
    /// Get active/in-progress trip for a vehicle
    /// </summary>
    [HttpGet("trips/{vehicleId}/active")]
    public async Task<ActionResult<VehicleTrip>> GetActiveTrip(Guid vehicleId)
    {
        if (_repository == null)
            return BadRequest("Database not configured");

        var trip = await _repository.GetActiveTripAsync(vehicleId);
        if (trip == null)
            return NotFound();

        return Ok(trip);
    }

    #endregion

    #region Events

    /// <summary>
    /// Get recent events for a vehicle
    /// </summary>
    [HttpGet("events/{vehicleId}")]
    public async Task<ActionResult<List<VehicleEvent>>> GetEvents(
        Guid vehicleId,
        [FromQuery] int hoursBack = 24)
    {
        if (_repository == null)
            return BadRequest("Database not configured");

        var events = await _repository.GetRecentEventsAsync(vehicleId, hoursBack);
        return Ok(events);
    }

    /// <summary>
    /// Get unacknowledged events
    /// </summary>
    [HttpGet("events/unacknowledged")]
    public async Task<ActionResult<List<VehicleEvent>>> GetUnacknowledgedEvents(
        [FromQuery] Guid? companyId = null)
    {
        if (_repository == null)
            return BadRequest("Database not configured");

        var events = await _repository.GetUnacknowledgedEventsAsync(companyId);
        return Ok(events);
    }

    #endregion
}

public class MapDeviceRequest
{
    public Guid VehicleId { get; set; }
    public string Serial { get; set; } = string.Empty;
}
