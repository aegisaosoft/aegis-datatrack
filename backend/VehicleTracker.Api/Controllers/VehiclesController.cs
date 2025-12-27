using Microsoft.AspNetCore.Mvc;
using VehicleTracker.Api.Models;
using VehicleTracker.Api.Services;

namespace VehicleTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly IFleet77Service _fleet77Service;
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(
        IFleet77Service fleet77Service, 
        ILogger<VehiclesController> logger)
    {
        _fleet77Service = fleet77Service;
        _logger = logger;
    }

    /// <summary>
    /// Set active company for API calls (call this before other endpoints)
    /// </summary>
    [HttpPost("select-company/{companyId}")]
    public ActionResult SelectCompany(Guid companyId)
    {
        _fleet77Service.SetActiveCompany(companyId);
        return Ok(new { message = "Company selected", companyId });
    }

    /// <summary>
    /// Get currently selected company
    /// </summary>
    [HttpGet("active-company")]
    public ActionResult GetActiveCompany()
    {
        var companyId = _fleet77Service.GetActiveCompanyId();
        if (!companyId.HasValue)
        {
            return Ok(new { hasActiveCompany = false });
        }
        return Ok(new { hasActiveCompany = true, companyId = companyId.Value });
    }

    private void EnsureCompanySelected(Guid? companyId)
    {
        if (companyId.HasValue)
        {
            _fleet77Service.SetActiveCompany(companyId.Value);
        }
    }

    /// <summary>
    /// Debug: Get raw API response
    /// </summary>
    [HttpGet("debug/raw")]
    public async Task<ActionResult> GetRawResponse([FromQuery] Guid? companyId)
    {
        try
        {
            EnsureCompanySelected(companyId);
            var statuses = await _fleet77Service.GetAllVehicleStatusesAsync();
            return Ok(new { 
                count = statuses.Count, 
                vehicles = statuses,
                companyId = companyId
            });
        }
        catch (Exception ex)
        {
            return Ok(new { 
                error = ex.Message, 
                innerError = ex.InnerException?.Message,
                stack = ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))
            });
        }
    }

    /// <summary>
    /// Get all vehicle statuses with current locations
    /// </summary>
    [HttpGet("statuses")]
    public async Task<ActionResult<List<VehicleStatus>>> GetAllStatuses([FromQuery] Guid? companyId)
    {
        try
        {
            EnsureCompanySelected(companyId);
            var statuses = await _fleet77Service.GetAllVehicleStatusesAsync();
            return Ok(statuses);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
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
    public async Task<ActionResult<VehicleStatus>> GetStatus(string serial, [FromQuery] Guid? companyId)
    {
        try
        {
            EnsureCompanySelected(companyId);
            var status = await _fleet77Service.GetVehicleStatusAsync(serial);
            if (status == null)
            {
                return NotFound(new { error = $"Vehicle {serial} not found" });
            }
            return Ok(status);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
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
        [FromQuery] Guid? companyId,
        [FromQuery] long? start,
        [FromQuery] long? end,
        [FromQuery] int? hoursBack)
    {
        try
        {
            EnsureCompanySelected(companyId);
            
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
            
            var locations = await _fleet77Service.GetLocationsAsync(serial, startSecs, endSecs);
            return Ok(locations);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
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
    public async Task<ActionResult<Vehicle>> GetVehicle(string serial, [FromQuery] Guid? companyId)
    {
        try
        {
            EnsureCompanySelected(companyId);
            var vehicle = await _fleet77Service.GetVehicleAsync(serial);
            if (vehicle == null)
            {
                return NotFound(new { error = $"Vehicle {serial} not found" });
            }
            return Ok(vehicle);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
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
    public async Task<ActionResult<List<Vehicle>>> GetAllVehicles([FromQuery] Guid? companyId)
    {
        try
        {
            EnsureCompanySelected(companyId);
            var vehicles = await _fleet77Service.GetAllVehiclesAsync();
            return Ok(vehicles);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
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
    public async Task<ActionResult> SetStarter(string serial, [FromBody] StarterRequest request, [FromQuery] Guid? companyId)
    {
        try
        {
            EnsureCompanySelected(companyId);
            var success = await _fleet77Service.SetStarterAsync(serial, request.Disable);
            if (success)
            {
                return Ok(new { message = $"Starter {(request.Disable ? "disabled" : "enabled")} for {serial}" });
            }
            return BadRequest(new { error = "Failed to set starter" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting starter for {Serial}", serial);
            return StatusCode(500, new { error = "Failed to set starter" });
        }
    }
}

public record StarterRequest(bool Disable);
