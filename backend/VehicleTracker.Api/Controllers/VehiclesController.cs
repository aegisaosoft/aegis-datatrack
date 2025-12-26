using Microsoft.AspNetCore.Mvc;
using VehicleTracker.Api.Models;
using VehicleTracker.Api.Services;

namespace VehicleTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly IDatatrackService _datatrackService;
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(
        IDatatrackService datatrackService, 
        ILogger<VehiclesController> logger)
    {
        _datatrackService = datatrackService;
        _logger = logger;
    }

    /// <summary>
    /// Get all vehicle statuses with current locations
    /// </summary>
    [HttpGet("statuses")]
    public async Task<ActionResult<List<VehicleStatus>>> GetAllStatuses()
    {
        try
        {
            var statuses = await _datatrackService.GetAllVehicleStatusesAsync();
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all vehicle statuses");
            return StatusCode(500, new { error = "Failed to fetch vehicle statuses" });
        }
    }

    /// <summary>
    /// Get a single vehicle's current status
    /// </summary>
    [HttpGet("statuses/{serial}")]
    public async Task<ActionResult<VehicleStatus>> GetStatus(string serial)
    {
        try
        {
            var status = await _datatrackService.GetVehicleStatusAsync(serial);
            if (status == null)
            {
                return NotFound(new { error = $"Vehicle {serial} not found" });
            }
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status for {Serial}", serial);
            return StatusCode(500, new { error = "Failed to fetch vehicle status" });
        }
    }

    /// <summary>
    /// Get location history for a vehicle
    /// </summary>
    [HttpGet("{serial}/locations")]
    public async Task<ActionResult<List<Location>>> GetLocations(
        string serial,
        [FromQuery] long? start,
        [FromQuery] long? end,
        [FromQuery] int? hoursBack)
    {
        try
        {
            long endSecs = end ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long startSecs;
            
            if (start.HasValue)
            {
                startSecs = start.Value;
            }
            else if (hoursBack.HasValue)
            {
                startSecs = endSecs - (hoursBack.Value * 3600);
            }
            else
            {
                // Default to last 24 hours
                startSecs = endSecs - 86400;
            }
            
            // API limitation: no more than 1 month in the past
            var oneMonthAgo = DateTimeOffset.UtcNow.AddMonths(-1).ToUnixTimeSeconds();
            if (startSecs < oneMonthAgo)
            {
                startSecs = oneMonthAgo;
            }
            
            var locations = await _datatrackService.GetLocationsAsync(serial, startSecs, endSecs);
            return Ok(locations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting locations for {Serial}", serial);
            return StatusCode(500, new { error = "Failed to fetch locations" });
        }
    }

    /// <summary>
    /// Get vehicle details
    /// </summary>
    [HttpGet("{serial}")]
    public async Task<ActionResult<Vehicle>> GetVehicle(string serial)
    {
        try
        {
            var vehicle = await _datatrackService.GetVehicleAsync(serial);
            if (vehicle == null)
            {
                return NotFound(new { error = $"Vehicle {serial} not found" });
            }
            return Ok(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting vehicle {Serial}", serial);
            return StatusCode(500, new { error = "Failed to fetch vehicle" });
        }
    }

    /// <summary>
    /// Get all vehicles
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Vehicle>>> GetAllVehicles()
    {
        try
        {
            var vehicles = await _datatrackService.GetAllVehiclesAsync();
            return Ok(vehicles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all vehicles");
            return StatusCode(500, new { error = "Failed to fetch vehicles" });
        }
    }

    /// <summary>
    /// Enable or disable vehicle starter
    /// </summary>
    [HttpPost("{serial}/starter")]
    public async Task<ActionResult> SetStarter(string serial, [FromBody] StarterRequest request)
    {
        try
        {
            var success = await _datatrackService.SetStarterAsync(serial, request.Disable);
            if (success)
            {
                return Ok(new { message = $"Starter {(request.Disable ? "disabled" : "enabled")} for {serial}" });
            }
            return BadRequest(new { error = "Failed to set starter" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting starter for {Serial}", serial);
            return StatusCode(500, new { error = "Failed to set starter" });
        }
    }

    /// <summary>
    /// Enable or disable vehicle buzzer
    /// </summary>
    [HttpPost("{serial}/buzzer")]
    public async Task<ActionResult> SetBuzzer(string serial, [FromBody] BuzzerRequest request)
    {
        try
        {
            var success = await _datatrackService.SetBuzzerAsync(serial, request.Disable);
            if (success)
            {
                return Ok(new { message = $"Buzzer {(request.Disable ? "disabled" : "enabled")} for {serial}" });
            }
            return BadRequest(new { error = "Failed to set buzzer" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting buzzer for {Serial}", serial);
            return StatusCode(500, new { error = "Failed to set buzzer" });
        }
    }
}

public record StarterRequest(bool Disable);
public record BuzzerRequest(bool Disable);
