using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VehicleTracker.Api.Data;
using VehicleTracker.Api.Data.Entities;

namespace VehicleTracker.Api.Services;

/// <summary>
/// Service for syncing vehicles from external tracking APIs
/// </summary>
public interface IExternalVehicleSyncService
{
    Task<int> SyncDatatrackVehiclesAsync();
    Task<ExternalCompany?> GetDatatrackCompanyAsync();
    Task<List<ExternalCompanyVehicle>> GetUnlinkedExternalVehiclesAsync(Guid? companyId = null);
    Task<ExternalVehicle> LinkVehicleAsync(Guid vehicleId, Guid externalCompanyVehicleId, bool isPrimary = true);
    Task UnlinkVehicleAsync(Guid vehicleId, Guid externalCompanyVehicleId);
}

public class ExternalVehicleSyncService : IExternalVehicleSyncService
{
    private readonly TrackingDbContext _context;
    private readonly IDatatrackService _datatrackService;
    private readonly ILogger<ExternalVehicleSyncService> _logger;
    private const string DatatrackCompanyName = "Datatrack 247";

    public ExternalVehicleSyncService(
        TrackingDbContext context,
        IDatatrackService datatrackService,
        ILogger<ExternalVehicleSyncService> logger)
    {
        _context = context;
        _datatrackService = datatrackService;
        _logger = logger;
    }

    /// <summary>
    /// Get or create the Datatrack 247 company record
    /// </summary>
    public async Task<ExternalCompany?> GetDatatrackCompanyAsync()
    {
        var company = await _context.ExternalCompanies
            .FirstOrDefaultAsync(c => c.CompanyName == DatatrackCompanyName);

        if (company == null)
        {
            company = new ExternalCompany
            {
                CompanyName = DatatrackCompanyName,
                ApiBaseUrl = "https://datatrack247.com/api",
                ApiKeyName = "Datatrack",
                IsActive = true
            };
            _context.ExternalCompanies.Add(company);
            await _context.SaveChangesAsync();
        }

        return company;
    }

    /// <summary>
    /// Sync all vehicles from Datatrack API to external_company_vehicles table
    /// </summary>
    public async Task<int> SyncDatatrackVehiclesAsync()
    {
        _logger.LogInformation("Starting Datatrack vehicles sync...");

        var company = await GetDatatrackCompanyAsync();
        if (company == null)
        {
            _logger.LogError("Failed to get/create Datatrack company record");
            return 0;
        }

        // Fetch all vehicles from Datatrack API
        var datatrackVehicles = await _datatrackService.GetAllVehiclesAsync();
        if (datatrackVehicles == null || datatrackVehicles.Count == 0)
        {
            _logger.LogWarning("No vehicles returned from Datatrack API");
            return 0;
        }

        _logger.LogInformation("Fetched {Count} vehicles from Datatrack API", datatrackVehicles.Count);

        int created = 0, updated = 0;

        foreach (var dtVehicle in datatrackVehicles)
        {
            try
            {
                // Check if this vehicle already exists
                var existing = await _context.ExternalCompanyVehicles
                    .FirstOrDefaultAsync(v => 
                        v.ExternalCompanyId == company.Id && 
                        v.ExternalId == dtVehicle.Serial);

                if (existing == null)
                {
                    // Create new
                    var newVehicle = new ExternalCompanyVehicle
                    {
                        ExternalCompanyId = company.Id,
                        ExternalId = dtVehicle.Serial,
                        Name = dtVehicle.Name,
                        Vin = dtVehicle.Vin,
                        LicensePlate = dtVehicle.Plate,
                        Make = dtVehicle.Make,
                        Model = dtVehicle.Model,
                        Year = dtVehicle.Year > 0 ? dtVehicle.Year : null,
                        Color = dtVehicle.ColorName,
                        Notes = dtVehicle.Notes,
                        RawData = JsonSerializer.Serialize(dtVehicle),
                        IsActive = true,
                        LastSyncedAt = DateTime.UtcNow
                    };

                    _context.ExternalCompanyVehicles.Add(newVehicle);
                    created++;
                }
                else
                {
                    // Update existing
                    existing.Name = dtVehicle.Name;
                    existing.Vin = dtVehicle.Vin;
                    existing.LicensePlate = dtVehicle.Plate;
                    existing.Make = dtVehicle.Make;
                    existing.Model = dtVehicle.Model;
                    existing.Year = dtVehicle.Year > 0 ? dtVehicle.Year : null;
                    existing.Color = dtVehicle.ColorName;
                    existing.Notes = dtVehicle.Notes;
                    existing.RawData = JsonSerializer.Serialize(dtVehicle);
                    existing.LastSyncedAt = DateTime.UtcNow;
                    existing.UpdatedAt = DateTime.UtcNow;
                    updated++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing vehicle {Serial}", dtVehicle.Serial);
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Datatrack sync completed: {Created} created, {Updated} updated", created, updated);
        return created + updated;
    }

    /// <summary>
    /// Get external vehicles that are not linked to any of our vehicles
    /// </summary>
    public async Task<List<ExternalCompanyVehicle>> GetUnlinkedExternalVehiclesAsync(Guid? companyId = null)
    {
        var query = _context.ExternalCompanyVehicles
            .Include(v => v.ExternalCompany)
            .Where(v => v.IsActive)
            .Where(v => !_context.ExternalVehicles.Any(ev => ev.ExternalCompanyVehicleId == v.Id));

        if (companyId.HasValue)
        {
            query = query.Where(v => v.ExternalCompanyId == companyId.Value);
        }

        return await query.OrderBy(v => v.Name).ToListAsync();
    }

    /// <summary>
    /// Link one of our vehicles to an external company vehicle
    /// </summary>
    public async Task<ExternalVehicle> LinkVehicleAsync(Guid vehicleId, Guid externalCompanyVehicleId, bool isPrimary = true)
    {
        // Verify both exist
        var vehicle = await _context.Vehicles.FindAsync(vehicleId);
        if (vehicle == null)
            throw new ArgumentException($"Vehicle {vehicleId} not found");

        var externalVehicle = await _context.ExternalCompanyVehicles.FindAsync(externalCompanyVehicleId);
        if (externalVehicle == null)
            throw new ArgumentException($"External vehicle {externalCompanyVehicleId} not found");

        // Check if already linked
        var existingLink = await _context.ExternalVehicles
            .FirstOrDefaultAsync(ev => ev.ExternalCompanyVehicleId == externalCompanyVehicleId);

        if (existingLink != null)
            throw new InvalidOperationException($"External vehicle is already linked to vehicle {existingLink.VehicleId}");

        // If setting as primary, unset other primary links for this vehicle
        if (isPrimary)
        {
            var otherPrimaryLinks = await _context.ExternalVehicles
                .Where(ev => ev.VehicleId == vehicleId && ev.IsPrimary)
                .ToListAsync();

            foreach (var link in otherPrimaryLinks)
            {
                link.IsPrimary = false;
            }
        }

        var newLink = new ExternalVehicle
        {
            VehicleId = vehicleId,
            ExternalCompanyVehicleId = externalCompanyVehicleId,
            IsPrimary = isPrimary,
            LinkedAt = DateTime.UtcNow
        };

        _context.ExternalVehicles.Add(newLink);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Linked vehicle {VehicleId} to external vehicle {ExternalId}", 
            vehicleId, externalVehicle.ExternalId);

        return newLink;
    }

    /// <summary>
    /// Unlink a vehicle from an external company vehicle
    /// </summary>
    public async Task UnlinkVehicleAsync(Guid vehicleId, Guid externalCompanyVehicleId)
    {
        var link = await _context.ExternalVehicles
            .FirstOrDefaultAsync(ev => 
                ev.VehicleId == vehicleId && 
                ev.ExternalCompanyVehicleId == externalCompanyVehicleId);

        if (link != null)
        {
            _context.ExternalVehicles.Remove(link);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Unlinked vehicle {VehicleId} from external vehicle {ExternalVehicleId}", 
                vehicleId, externalCompanyVehicleId);
        }
    }
}
