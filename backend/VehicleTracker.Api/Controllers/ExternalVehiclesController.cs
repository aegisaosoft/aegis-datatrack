using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehicleTracker.Api.Data;
using VehicleTracker.Api.Data.Entities;
using VehicleTracker.Api.Services;

namespace VehicleTracker.Api.Controllers;

/// <summary>
/// Controller for managing external vehicle integrations
/// </summary>
[ApiController]
[Route("api/external")]
public class ExternalVehiclesController : ControllerBase
{
    private readonly TrackingDbContext _context;
    private readonly IExternalVehicleSyncService _syncService;
    private readonly ILogger<ExternalVehiclesController> _logger;

    public ExternalVehiclesController(
        TrackingDbContext context,
        IExternalVehicleSyncService syncService,
        ILogger<ExternalVehiclesController> logger)
    {
        _context = context;
        _syncService = syncService;
        _logger = logger;
    }

    #region External Companies

    /// <summary>
    /// Get all external companies
    /// </summary>
    [HttpGet("companies")]
    public async Task<ActionResult<List<ExternalCompany>>> GetCompanies()
    {
        var companies = await _context.ExternalCompanies
            .Where(c => c.IsActive)
            .OrderBy(c => c.CompanyName)
            .ToListAsync();

        return Ok(companies);
    }

    /// <summary>
    /// Get external company by ID
    /// </summary>
    [HttpGet("companies/{id}")]
    public async Task<ActionResult<ExternalCompany>> GetCompany(Guid id)
    {
        var company = await _context.ExternalCompanies.FindAsync(id);
        if (company == null)
            return NotFound();

        return Ok(company);
    }

    #endregion

    #region External Company Vehicles

    /// <summary>
    /// Get all vehicles from external companies
    /// </summary>
    [HttpGet("vehicles")]
    public async Task<ActionResult<List<ExternalCompanyVehicle>>> GetExternalVehicles(
        [FromQuery] Guid? companyId = null,
        [FromQuery] bool unlinkedOnly = false)
    {
        var query = _context.ExternalCompanyVehicles
            .Include(v => v.ExternalCompany)
            .Include(v => v.ExternalVehicle)
            .Where(v => v.IsActive);

        if (companyId.HasValue)
        {
            query = query.Where(v => v.ExternalCompanyId == companyId.Value);
        }

        if (unlinkedOnly)
        {
            query = query.Where(v => v.ExternalVehicle == null);
        }

        var vehicles = await query
            .OrderBy(v => v.Name)
            .Select(v => new
            {
                v.Id,
                v.ExternalCompanyId,
                CompanyName = v.ExternalCompany!.CompanyName,
                v.ExternalId,
                v.Name,
                v.Vin,
                v.LicensePlate,
                v.Make,
                v.Model,
                v.Year,
                v.Color,
                v.IsActive,
                v.LastSyncedAt,
                IsLinked = v.ExternalVehicle != null,
                LinkedVehicleId = v.ExternalVehicle != null ? v.ExternalVehicle.VehicleId : (Guid?)null
            })
            .ToListAsync();

        return Ok(vehicles);
    }

    /// <summary>
    /// Sync vehicles from Datatrack API
    /// </summary>
    [HttpPost("sync/datatrack")]
    public async Task<ActionResult> SyncDatatrackVehicles()
    {
        try
        {
            var count = await _syncService.SyncDatatrackVehiclesAsync();
            return Ok(new { message = $"Synced {count} vehicles from Datatrack", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing Datatrack vehicles");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region Vehicle Links

    /// <summary>
    /// Get all vehicle links
    /// </summary>
    [HttpGet("links")]
    public async Task<ActionResult> GetLinks([FromQuery] Guid? vehicleId = null)
    {
        var query = _context.ExternalVehicles
            .Include(ev => ev.Vehicle)
            .Include(ev => ev.ExternalCompanyVehicle)
                .ThenInclude(ecv => ecv!.ExternalCompany)
            .AsQueryable();

        if (vehicleId.HasValue)
        {
            query = query.Where(ev => ev.VehicleId == vehicleId.Value);
        }

        var links = await query
            .Select(ev => new
            {
                ev.Id,
                ev.VehicleId,
                VehiclePlate = ev.Vehicle!.LicensePlate,
                VehicleVin = ev.Vehicle.Vin,
                ev.ExternalCompanyVehicleId,
                ExternalId = ev.ExternalCompanyVehicle!.ExternalId,
                ExternalName = ev.ExternalCompanyVehicle.Name,
                CompanyName = ev.ExternalCompanyVehicle.ExternalCompany!.CompanyName,
                ev.IsPrimary,
                ev.LinkedAt
            })
            .ToListAsync();

        return Ok(links);
    }

    /// <summary>
    /// Link a vehicle to an external company vehicle
    /// </summary>
    [HttpPost("links")]
    public async Task<ActionResult> LinkVehicle([FromBody] LinkVehicleRequest request)
    {
        try
        {
            var link = await _syncService.LinkVehicleAsync(
                request.VehicleId, 
                request.ExternalCompanyVehicleId, 
                request.IsPrimary);

            return CreatedAtAction(nameof(GetLinks), new { vehicleId = request.VehicleId }, new
            {
                link.Id,
                link.VehicleId,
                link.ExternalCompanyVehicleId,
                link.IsPrimary,
                link.LinkedAt
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Unlink a vehicle from an external company vehicle
    /// </summary>
    [HttpDelete("links/{vehicleId}/{externalCompanyVehicleId}")]
    public async Task<ActionResult> UnlinkVehicle(Guid vehicleId, Guid externalCompanyVehicleId)
    {
        await _syncService.UnlinkVehicleAsync(vehicleId, externalCompanyVehicleId);
        return NoContent();
    }

    #endregion

    #region Summary Views

    /// <summary>
    /// Get vehicles with their linked trackers
    /// </summary>
    [HttpGet("vehicles-with-trackers")]
    public async Task<ActionResult> GetVehiclesWithTrackers()
    {
        var vehicles = await _context.Vehicles
            .Select(v => new
            {
                v.Id,
                v.LicensePlate,
                v.Vin,
                v.Color,
                v.Status,
                Trackers = _context.ExternalVehicles
                    .Where(ev => ev.VehicleId == v.Id)
                    .Select(ev => new
                    {
                        ev.ExternalCompanyVehicle!.ExternalId,
                        ev.ExternalCompanyVehicle.Name,
                        ev.ExternalCompanyVehicle.ExternalCompany!.CompanyName,
                        ev.IsPrimary,
                        ev.LinkedAt
                    })
                    .ToList()
            })
            .ToListAsync();

        return Ok(vehicles);
    }

    #endregion
}

public class LinkVehicleRequest
{
    public Guid VehicleId { get; set; }
    public Guid ExternalCompanyVehicleId { get; set; }
    public bool IsPrimary { get; set; } = true;
}
