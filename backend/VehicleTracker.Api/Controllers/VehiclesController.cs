using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleTracker.Api.Data;
using VehicleTracker.Api.Data.Entities;
using VehicleTracker.Api.Models;
using VehicleTracker.Api.Services;

namespace VehicleTracker.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly IFleet77Service _fleet77Service;
    private readonly IDatatrackService _datatrackService;
    private readonly TrackingDbContext _context;
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(
        IFleet77Service fleet77Service,
        IDatatrackService datatrackService,
        TrackingDbContext context,
        ILogger<VehiclesController> logger)
    {
        _fleet77Service = fleet77Service;
        _datatrackService = datatrackService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Determine API type based on company's ApiBaseUrl
    /// </summary>
    private async Task<string> GetApiTypeAsync(Guid companyId)
    {
        var company = await _context.ExternalCompanies.FindAsync(companyId);
        if (company == null) return "fleet77";
        
        var baseUrl = company.ApiBaseUrl?.ToLowerInvariant() ?? "";
        if (baseUrl.Contains("datatrack247") || baseUrl.Contains("datatrack"))
        {
            return "datatrack247";
        }
        return "fleet77";
    }

    /// <summary>
    /// Set active company for API calls (call this before other endpoints)
    /// </summary>
    [HttpPost("select-company/{companyId}")]
    public ActionResult SelectCompany(Guid companyId)
    {
        _fleet77Service.SetActiveCompany(companyId);
        _datatrackService.SetActiveCompany(companyId);
        return Ok(new { message = "Company selected", companyId });
    }

    /// <summary>
    /// Get currently selected company
    /// </summary>
    [HttpGet("active-company")]
    public ActionResult GetActiveCompany()
    {
        var companyId = _fleet77Service.GetActiveCompanyId() ?? _datatrackService.GetActiveCompanyId();
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
            _datatrackService.SetActiveCompany(companyId.Value);
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
            
            var apiType = companyId.HasValue ? await GetApiTypeAsync(companyId.Value) : "fleet77";
            List<VehicleStatus> statuses;
            
            if (apiType == "datatrack247")
            {
                statuses = await _datatrackService.GetAllVehicleStatusesAsync();
            }
            else
            {
                statuses = await _fleet77Service.GetAllVehicleStatusesAsync();
            }
            
            return Ok(new { 
                count = statuses.Count, 
                vehicles = statuses,
                companyId = companyId,
                apiType = apiType
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
            
            var apiType = companyId.HasValue ? await GetApiTypeAsync(companyId.Value) : "fleet77";
            List<VehicleStatus> statuses;
            
            if (apiType == "datatrack247")
            {
                statuses = await _datatrackService.GetAllVehicleStatusesAsync();
            }
            else
            {
                statuses = await _fleet77Service.GetAllVehicleStatusesAsync();
            }
            
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
            
            var apiType = companyId.HasValue ? await GetApiTypeAsync(companyId.Value) : "fleet77";
            VehicleStatus? status;
            
            if (apiType == "datatrack247")
            {
                status = await _datatrackService.GetVehicleStatusAsync(serial);
            }
            else
            {
                status = await _fleet77Service.GetVehicleStatusAsync(serial);
            }
            
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
            
            var apiType = companyId.HasValue ? await GetApiTypeAsync(companyId.Value) : "fleet77";
            List<Location> locations;
            
            if (apiType == "datatrack247")
            {
                locations = await _datatrackService.GetLocationsAsync(serial, startSecs, endSecs);
            }
            else
            {
                locations = await _fleet77Service.GetLocationsAsync(serial, startSecs, endSecs);
            }
            
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
    /// Get vehicle details - uses Fleet77 API
    /// </summary>
    [HttpGet("{serial}")]
    public async Task<ActionResult<Vehicle>> GetVehicle(string serial, [FromQuery] Guid? companyId)
    {
        try
        {
            EnsureCompanySelected(companyId);
            _logger.LogInformation("GetVehicle: serial={Serial}", serial);
            
            // Fleet77 returns full vehicle details
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
    /// Get all vehicles with details (make, model, year, VIN, plate, color)
    /// Uses Fleet77 API which returns full vehicle details
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<Vehicle>>> GetAllVehicles([FromQuery] Guid? companyId)
    {
        try
        {
            EnsureCompanySelected(companyId);
            _logger.LogInformation("GetAllVehicles: companyId={CompanyId}", companyId);
            
            // Fleet77 returns full vehicle details including make, model, year, plate, vin
            var vehicles = await _fleet77Service.GetAllVehiclesAsync();
            _logger.LogInformation("Fleet77 returned {Count} vehicles with details", vehicles.Count);
            
            return Ok(vehicles);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("GetAllVehicles failed: {Message}", ex.Message);
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
            
            var apiType = companyId.HasValue ? await GetApiTypeAsync(companyId.Value) : "fleet77";
            bool success;
            
            if (apiType == "datatrack247")
            {
                success = await _datatrackService.SetStarterAsync(serial, request.Disable);
            }
            else
            {
                success = await _fleet77Service.SetStarterAsync(serial, request.Disable);
            }
            
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

    /// <summary>
    /// Sync vehicles directly to rental database
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult> SyncVehicles([FromBody] SyncVehiclesRequest request)
    {
        try
        {
            _logger.LogInformation("=== SYNC VEHICLES TO RENTAL DB START ===");
            _logger.LogInformation("ExternalCompanyId: {ExternalCompanyId}, Vehicles: {Count}", 
                request.ExternalCompanyId, request.Vehicles?.Count ?? 0);
            
            if (request.Vehicles == null || request.Vehicles.Count == 0)
            {
                return BadRequest(new { error = "No vehicles provided" });
            }
            
            // Get external company with rental company link
            var externalCompany = await _context.ExternalCompanies
                .FirstOrDefaultAsync(ec => ec.Id == request.ExternalCompanyId);
            
            if (externalCompany == null)
            {
                return BadRequest(new { error = "External company not found" });
            }
            
            if (!externalCompany.RentalCompanyId.HasValue)
            {
                return BadRequest(new { error = "External company is not linked to a rental company. Please link it first in Company Settings." });
            }
            
            var rentalCompanyId = externalCompany.RentalCompanyId.Value;
            _logger.LogInformation("RentalCompanyId: {RentalCompanyId}", rentalCompanyId);
            
            // Verify rental company exists
            var rentalCompany = await _context.Companies.FindAsync(rentalCompanyId);
            if (rentalCompany == null)
            {
                return BadRequest(new { error = $"Rental company {rentalCompanyId} not found" });
            }
            
            int created = 0, updated = 0, skipped = 0;
            var errors = new List<string>();
            
            foreach (var v in request.Vehicles)
            {
                try
                {
                    // Skip vehicles without plate
                    if (string.IsNullOrWhiteSpace(v.Plate))
                    {
                        skipped++;
                        continue;
                    }
                    
                    // Skip vehicles without make/model/year
                    if (string.IsNullOrWhiteSpace(v.Make) || string.IsNullOrWhiteSpace(v.Model) || v.Year == 0)
                    {
                        _logger.LogDebug("Skipping vehicle {Plate} - missing make/model/year", v.Plate);
                        skipped++;
                        continue;
                    }
                    
                    // Check if vehicle already exists by license plate
                    var existingVehicle = await _context.Vehicles
                        .FirstOrDefaultAsync(veh => veh.LicensePlate == v.Plate && veh.CompanyId == rentalCompanyId);
                    
                    if (existingVehicle != null)
                    {
                        // Update existing vehicle
                        if (!string.IsNullOrWhiteSpace(v.Vin)) existingVehicle.Vin = v.Vin;
                        existingVehicle.UpdatedAt = DateTime.UtcNow;
                        updated++;
                        _logger.LogDebug("Updated vehicle {Plate}", v.Plate);
                        continue;
                    }
                    
                    // Find or create Model in models table
                    var model = await _context.Models
                        .FirstOrDefaultAsync(m => 
                            m.Make.ToUpper() == v.Make.ToUpper() && 
                            m.ModelName.ToUpper() == v.Model.ToUpper() && 
                            m.Year == v.Year);
                    
                    if (model == null)
                    {
                        // Create new model
                        model = new RentalModel
                        {
                            Id = Guid.NewGuid(),
                            Make = v.Make,
                            ModelName = v.Model,
                            Year = v.Year
                        };
                        _context.Models.Add(model);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Created new model: {Make} {Model} {Year}", v.Make, v.Model, v.Year);
                    }
                    
                    // Find or create VehicleModel catalog entry
                    var vehicleModel = await _context.VehicleModels
                        .FirstOrDefaultAsync(vm => vm.ModelId == model.Id && vm.CompanyId == rentalCompanyId);
                    
                    if (vehicleModel == null)
                    {
                        vehicleModel = new RentalVehicleModel
                        {
                            Id = Guid.NewGuid(),
                            CompanyId = rentalCompanyId,
                            ModelId = model.Id,
                            DailyRate = null
                        };
                        _context.VehicleModels.Add(vehicleModel);
                        await _context.SaveChangesAsync();
                    }
                    
                    // Create new vehicle
                    var newVehicle = new RentalVehicle
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = rentalCompanyId,
                        LicensePlate = v.Plate,
                        Vin = v.Vin,
                        Color = null,
                        VehicleModelId = vehicleModel.Id,
                        Status = "Available",
                        Mileage = 0
                    };
                    
                    _context.Vehicles.Add(newVehicle);
                    created++;
                    _logger.LogDebug("Created vehicle {Plate}: {Make} {Model} {Year}", v.Plate, v.Make, v.Model, v.Year);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing vehicle {Plate}", v.Plate);
                    errors.Add($"{v.Plate}: {ex.Message}");
                    skipped++;
                }
            }
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("=== SYNC VEHICLES TO RENTAL DB COMPLETE ===");
            _logger.LogInformation("Created: {Created}, Updated: {Updated}, Skipped: {Skipped}", created, updated, skipped);
            
            return Ok(new { 
                message = $"Sync completed: {created} created, {updated} updated, {skipped} skipped",
                synced = created + updated,
                created,
                updated,
                skipped,
                errors = errors.Count > 0 ? errors : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing vehicles to rental DB");
            return StatusCode(500, new { error = ex.Message });
        }
    }

}

public record StarterRequest(bool Disable);
public record SyncVehiclesRequest(Guid ExternalCompanyId, List<SyncVehicleDto> Vehicles);

public record SyncVehicleDto(
    string? Make,
    string? Model,
    int Year,
    string? Plate,
    string? Vin,
    int VehicleColor
);
