using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VehicleTracker.Api.Data;
using VehicleTracker.Api.Data.Entities;
using VehicleTracker.Api.Models;

namespace VehicleTracker.Api.Services;

/// <summary>
/// Service for syncing vehicles from external tracking APIs
/// </summary>
public interface IExternalVehicleSyncService
{
    Task<SyncResult> SyncExternalCompanyVehiclesAsync(Guid externalCompanyId);
    Task<SyncResult> SyncDatatrackVehiclesAsync();
    Task<ExternalCompany?> GetDatatrackCompanyAsync();
    Task<List<ExternalCompanyVehicle>> GetUnlinkedExternalVehiclesAsync(Guid? companyId = null);
    Task<ExternalVehicle> LinkVehicleAsync(Guid vehicleId, Guid externalCompanyVehicleId, bool isPrimary = true);
    Task UnlinkVehicleAsync(Guid vehicleId, Guid externalCompanyVehicleId);
}

public class SyncResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Total => Created + Updated;
}

public class ExternalVehicleSyncService : IExternalVehicleSyncService
{
    private readonly TrackingDbContext _context;
    private readonly IExternalAuthService _authService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExternalVehicleSyncService> _logger;
    
    private const string DatatrackCompanyName = "Datatrack 247";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExternalVehicleSyncService(
        TrackingDbContext context,
        IExternalAuthService authService,
        IHttpClientFactory httpClientFactory,
        ILogger<ExternalVehicleSyncService> logger)
    {
        _context = context;
        _authService = authService;
        _httpClientFactory = httpClientFactory;
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
                ApiBaseUrl = "https://fm.datatrack247.com/api",
                IsActive = true
            };
            _context.ExternalCompanies.Add(company);
            await _context.SaveChangesAsync();
        }

        return company;
    }

    /// <summary>
    /// Sync vehicles from any external company using stored credentials
    /// </summary>
    public async Task<SyncResult> SyncExternalCompanyVehiclesAsync(Guid externalCompanyId)
    {
        _logger.LogInformation("Starting vehicle sync for company {Id}", externalCompanyId);

        var result = new SyncResult();

        // Get company with valid token
        var company = await _authService.GetCompanyWithValidTokenAsync(externalCompanyId);
        if (company == null)
        {
            _logger.LogError("External company {Id} not found", externalCompanyId);
            return result;
        }

        if (string.IsNullOrEmpty(company.ApiToken))
        {
            _logger.LogError("No valid token for company {Company}. Login required.", company.CompanyName);
            throw new InvalidOperationException($"Authentication required for {company.CompanyName}");
        }

        // Fetch vehicles from external API
        var vehicles = await FetchVehiclesFromExternalApiAsync(company);
        if (vehicles == null || vehicles.Count == 0)
        {
            _logger.LogWarning("No vehicles returned from {Company}", company.CompanyName);
            return result;
        }

        _logger.LogInformation("Fetched {Count} vehicles from {Company}", vehicles.Count, company.CompanyName);

        foreach (var vehicle in vehicles)
        {
            try
            {
                var existing = await _context.ExternalCompanyVehicles
                    .FirstOrDefaultAsync(v =>
                        v.ExternalCompanyId == company.Id &&
                        v.ExternalId == vehicle.Serial);

                if (existing == null)
                {
                    var newVehicle = new ExternalCompanyVehicle
                    {
                        ExternalCompanyId = company.Id,
                        ExternalId = vehicle.Serial,
                        Name = vehicle.Name,
                        Vin = vehicle.Vin,
                        LicensePlate = vehicle.Plate,
                        Make = vehicle.Make,
                        Model = vehicle.Model,
                        Year = vehicle.Year > 0 ? vehicle.Year : null,
                        Color = vehicle.ColorName,
                        Notes = vehicle.Notes,
                        RawData = JsonSerializer.Serialize(vehicle),
                        IsActive = true,
                        LastSyncedAt = DateTime.UtcNow
                    };

                    _context.ExternalCompanyVehicles.Add(newVehicle);
                    result.Created++;
                }
                else
                {
                    existing.Name = vehicle.Name;
                    existing.Vin = vehicle.Vin;
                    existing.LicensePlate = vehicle.Plate;
                    existing.Make = vehicle.Make;
                    existing.Model = vehicle.Model;
                    existing.Year = vehicle.Year > 0 ? vehicle.Year : null;
                    existing.Color = vehicle.ColorName;
                    existing.Notes = vehicle.Notes;
                    existing.RawData = JsonSerializer.Serialize(vehicle);
                    existing.LastSyncedAt = DateTime.UtcNow;
                    existing.UpdatedAt = DateTime.UtcNow;
                    result.Updated++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing vehicle {Serial}", vehicle.Serial);
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Sync completed for {Company}: {Created} created, {Updated} updated",
            company.CompanyName, result.Created, result.Updated);

        return result;
    }

    private async Task<List<Vehicle>> FetchVehiclesFromExternalApiAsync(ExternalCompany company)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("ApiKey", company.ApiToken);

        var url = $"{company.ApiBaseUrl?.TrimEnd('/')}/getVehicles";

        try
        {
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<VehiclesResponse>(content, JsonOptions);

            if (result?.Status != 200)
            {
                _logger.LogWarning("API returned status {Status}", result?.Status);
                return new List<Vehicle>();
            }

            return result.Data ?? new List<Vehicle>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vehicles from {Url}", url);
            throw;
        }
    }

    /// <summary>
    /// Sync vehicles from Datatrack (for backward compatibility)
    /// </summary>
    public async Task<SyncResult> SyncDatatrackVehiclesAsync()
    {
        var company = await GetDatatrackCompanyAsync();
        if (company == null) return new SyncResult();

        return await SyncExternalCompanyVehiclesAsync(company.Id);
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
        var vehicle = await _context.Vehicles.FindAsync(vehicleId);
        if (vehicle == null)
            throw new ArgumentException($"Vehicle {vehicleId} not found");

        var externalVehicle = await _context.ExternalCompanyVehicles.FindAsync(externalCompanyVehicleId);
        if (externalVehicle == null)
            throw new ArgumentException($"External vehicle {externalCompanyVehicleId} not found");

        var existingLink = await _context.ExternalVehicles
            .FirstOrDefaultAsync(ev => ev.ExternalCompanyVehicleId == externalCompanyVehicleId);

        if (existingLink != null)
            throw new InvalidOperationException($"External vehicle is already linked to vehicle {existingLink.VehicleId}");

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
