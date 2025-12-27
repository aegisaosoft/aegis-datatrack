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
    private readonly IExternalAuthService _authService;
    private readonly IExternalVehicleSyncService _syncService;
    private readonly IFleet77Service _fleet77Service;
    private readonly ILogger<ExternalVehiclesController> _logger;

    public ExternalVehiclesController(
        TrackingDbContext context,
        IExternalAuthService authService,
        IExternalVehicleSyncService syncService,
        IFleet77Service fleet77Service,
        ILogger<ExternalVehiclesController> logger)
    {
        _context = context;
        _authService = authService;
        _syncService = syncService;
        _fleet77Service = fleet77Service;
        _logger = logger;
    }

    #region External Companies CRUD

    /// <summary>
    /// Get all external companies
    /// </summary>
    [HttpGet("companies")]
    public async Task<ActionResult> GetCompanies()
    {
        try
        {
            var companies = await _context.ExternalCompanies
                .Include(c => c.RentalCompany)
                .OrderBy(c => c.CompanyName)
                .Select(c => new
                {
                    c.Id,
                    c.CompanyName,
                    c.ApiBaseUrl,
                    c.ApiUsername,
                    HasPassword = !string.IsNullOrEmpty(c.ApiPassword),
                    HasToken = !string.IsNullOrEmpty(c.ApiToken),
                    c.TokenExpiresAt,
                    TokenValid = c.TokenExpiresAt.HasValue && c.TokenExpiresAt > DateTime.UtcNow,
                    c.RentalCompanyId,
                    RentalCompanyName = c.RentalCompany != null ? c.RentalCompany.CompanyName : null,
                    c.IsActive,
                    c.CreatedAt,
                    c.UpdatedAt,
                    VehicleCount = c.Vehicles.Count
                })
                .ToListAsync();

            return Ok(companies);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting external companies");
            return StatusCode(500, new { error = ex.Message, details = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Debug: Check company configuration
    /// </summary>
    [HttpGet("companies/{id}/debug")]
    public async Task<ActionResult> DebugCompany(Guid id)
    {
        var company = await _context.ExternalCompanies.FindAsync(id);
        if (company == null)
            return NotFound(new { error = "Company not found" });

        // Parse credentials
        var parts = company.ApiUsername?.Split('|') ?? Array.Empty<string>();
        
        return Ok(new
        {
            company.Id,
            company.CompanyName,
            company.ApiBaseUrl,
            ApiUsernameRaw = company.ApiUsername,
            ParsedAccountId = parts.Length > 0 ? parts[0] : "MISSING",
            ParsedUserId = parts.Length > 1 ? parts[1] : "MISSING",
            ParsedUsername = parts.Length > 2 ? parts[2] : "MISSING",
            HasPassword = !string.IsNullOrEmpty(company.ApiPassword),
            HasToken = !string.IsNullOrEmpty(company.ApiToken),
            TokenLength = company.ApiToken?.Length ?? 0,
            TokenFirst10 = company.ApiToken?.Substring(0, Math.Min(10, company.ApiToken?.Length ?? 0)),
            company.TokenExpiresAt,
            TokenValid = company.TokenExpiresAt.HasValue && company.TokenExpiresAt > DateTime.UtcNow,
            company.RentalCompanyId,
            company.IsActive
        });
    }

    /// <summary>
    /// Get external company by ID
    /// </summary>
    [HttpGet("companies/{id}")]
    public async Task<ActionResult> GetCompany(Guid id)
    {
        var company = await _context.ExternalCompanies
            .Include(c => c.RentalCompany)
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.CompanyName,
                c.ApiBaseUrl,
                c.ApiUsername,
                HasPassword = !string.IsNullOrEmpty(c.ApiPassword),
                HasToken = !string.IsNullOrEmpty(c.ApiToken),
                c.TokenExpiresAt,
                TokenValid = c.TokenExpiresAt.HasValue && c.TokenExpiresAt > DateTime.UtcNow,
                c.RentalCompanyId,
                RentalCompanyName = c.RentalCompany != null ? c.RentalCompany.CompanyName : null,
                c.IsActive,
                c.CreatedAt,
                c.UpdatedAt,
                VehicleCount = c.Vehicles.Count
            })
            .FirstOrDefaultAsync();

        if (company == null)
            return NotFound();

        return Ok(company);
    }

    /// <summary>
    /// Create a new external company
    /// </summary>
    [HttpPost("companies")]
    public async Task<ActionResult> CreateCompany([FromBody] CreateExternalCompanyRequest request)
    {
        // Check if name already exists
        var existing = await _context.ExternalCompanies
            .AnyAsync(c => c.CompanyName == request.CompanyName);

        if (existing)
            return Conflict(new { error = $"Company '{request.CompanyName}' already exists" });

        var company = new ExternalCompany
        {
            CompanyName = request.CompanyName,
            ApiBaseUrl = request.ApiBaseUrl,
            ApiUsername = request.ApiUsername,
            ApiPassword = request.ApiPassword,
            RentalCompanyId = request.RentalCompanyId,
            IsActive = true
        };

        _context.ExternalCompanies.Add(company);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created external company: {Name}", company.CompanyName);

        return CreatedAtAction(nameof(GetCompany), new { id = company.Id }, new
        {
            company.Id,
            company.CompanyName,
            company.ApiBaseUrl,
            company.ApiUsername,
            company.RentalCompanyId,
            company.IsActive
        });
    }

    /// <summary>
    /// Update an external company
    /// </summary>
    [HttpPut("companies/{id}")]
    public async Task<ActionResult> UpdateCompany(Guid id, [FromBody] UpdateExternalCompanyRequest request)
    {
        var company = await _context.ExternalCompanies.FindAsync(id);
        if (company == null)
            return NotFound();

        // Check for name conflict
        if (!string.IsNullOrEmpty(request.CompanyName) && request.CompanyName != company.CompanyName)
        {
            var nameExists = await _context.ExternalCompanies
                .AnyAsync(c => c.CompanyName == request.CompanyName && c.Id != id);

            if (nameExists)
                return Conflict(new { error = $"Company '{request.CompanyName}' already exists" });

            company.CompanyName = request.CompanyName;
        }

        if (request.ApiBaseUrl != null)
            company.ApiBaseUrl = request.ApiBaseUrl;

        if (request.ApiUsername != null)
            company.ApiUsername = request.ApiUsername;

        if (request.ApiPassword != null)
        {
            company.ApiPassword = request.ApiPassword;
            // Clear cached token when password changes
            company.ApiToken = null;
            company.TokenExpiresAt = null;
        }

        if (request.RentalCompanyId.HasValue)
            company.RentalCompanyId = request.RentalCompanyId.Value == Guid.Empty ? null : request.RentalCompanyId;

        if (request.IsActive.HasValue)
            company.IsActive = request.IsActive.Value;

        company.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Company updated", company.Id });
    }

    /// <summary>
    /// Delete an external company
    /// </summary>
    [HttpDelete("companies/{id}")]
    public async Task<ActionResult> DeleteCompany(Guid id)
    {
        var company = await _context.ExternalCompanies.FindAsync(id);
        if (company == null)
            return NotFound();

        _context.ExternalCompanies.Remove(company);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted external company: {Name}", company.CompanyName);

        return NoContent();
    }

    #endregion

    #region Authentication

    /// <summary>
    /// Test login to external company and get token
    /// </summary>
    [HttpPost("companies/{id}/login")]
    public async Task<ActionResult> LoginToCompany(Guid id)
    {
        var company = await _context.ExternalCompanies.FindAsync(id);
        if (company == null)
            return NotFound();

        try
        {
            var token = await _authService.LoginAsync(id);

            if (string.IsNullOrEmpty(token))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Failed to authenticate. Check credentials and API URL."
                });
            }

            // Refresh company data
            await _context.Entry(company).ReloadAsync();

            return Ok(new
            {
                success = true,
                message = "Successfully authenticated",
                tokenExpiresAt = company.TokenExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authenticating with {Company}", company.CompanyName);
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Clear cached token for external company
    /// </summary>
    [HttpPost("companies/{id}/logout")]
    public async Task<ActionResult> LogoutFromCompany(Guid id)
    {
        await _authService.ClearTokenAsync(id);
        return Ok(new { message = "Token cleared" });
    }

    /// <summary>
    /// Check if credentials are valid
    /// </summary>
    [HttpGet("companies/{id}/validate")]
    public async Task<ActionResult> ValidateCompanyCredentials(Guid id)
    {
        var company = await _context.ExternalCompanies.FindAsync(id);
        if (company == null)
            return NotFound();

        var tokenValid = !string.IsNullOrEmpty(company.ApiToken) &&
                         company.TokenExpiresAt.HasValue &&
                         company.TokenExpiresAt > DateTime.UtcNow;

        return Ok(new
        {
            hasCredentials = !string.IsNullOrEmpty(company.ApiUsername) && !string.IsNullOrEmpty(company.ApiPassword),
            hasToken = !string.IsNullOrEmpty(company.ApiToken),
            tokenValid,
            tokenExpiresAt = company.TokenExpiresAt
        });
    }

    #endregion

    #region Sync Vehicles

    /// <summary>
    /// Sync vehicles from external company
    /// </summary>
    [HttpPost("companies/{id}/sync")]
    public async Task<ActionResult> SyncCompanyVehicles(Guid id)
    {
        var company = await _context.ExternalCompanies.FindAsync(id);
        if (company == null)
            return NotFound();

        try
        {
            var result = await _syncService.SyncExternalCompanyVehiclesAsync(id);
            return Ok(new
            {
                message = $"Sync complete: {result.Created} new, {result.Updated} updated",
                created = result.Created,
                updated = result.Updated,
                vehicleCount = result.Total
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing vehicles from {Company}", company.CompanyName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion

    #region External Company Vehicles

    /// <summary>
    /// Get all vehicles from external companies
    /// </summary>
    [HttpGet("vehicles")]
    public async Task<ActionResult> GetExternalVehicles(
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

    #region Rental Companies (for dropdown)

    /// <summary>
    /// Get all rental companies (for linking dropdown)
    /// </summary>
    [HttpGet("rental-companies")]
    public async Task<ActionResult> GetRentalCompanies()
    {
        var companies = await _context.Companies
            .Where(c => c.IsActive)
            .OrderBy(c => c.CompanyName)
            .Select(c => new { c.Id, c.CompanyName, c.Email })
            .ToListAsync();

        return Ok(companies);
    }

    /// <summary>
    /// Get all rental companies with their tracker configuration status
    /// </summary>
    [HttpGet("rental-companies-with-tracker")]
    public async Task<ActionResult> GetRentalCompaniesWithTracker()
    {
        try
        {
            var companies = await _context.Companies
                .Where(c => c.IsActive)
                .OrderBy(c => c.CompanyName)
                .Select(c => new 
                { 
                    c.Id, 
                    c.CompanyName, 
                    c.Email 
                })
                .ToListAsync();

            // Get external company mappings
            var externalCompanies = await _context.ExternalCompanies
                .Where(ec => ec.RentalCompanyId != null)
                .Select(ec => new
                {
                    ec.Id,
                    ec.RentalCompanyId,
                    ec.CompanyName,
                    ec.ApiBaseUrl,
                    ec.ApiUsername,
                    HasToken = !string.IsNullOrEmpty(ec.ApiToken),
                    TokenValid = ec.TokenExpiresAt.HasValue && ec.TokenExpiresAt > DateTime.UtcNow,
                    VehicleCount = ec.Vehicles.Count
                })
                .ToListAsync();

            var result = companies.Select(c =>
            {
                var tracker = externalCompanies.FirstOrDefault(ec => ec.RentalCompanyId == c.Id);
                return new
                {
                    c.Id,
                    c.CompanyName,
                    c.Email,
                    ExternalCompanyId = tracker?.Id,
                    TrackerProvider = tracker?.CompanyName,
                    ApiBaseUrl = tracker?.ApiBaseUrl,
                    TrackerUsername = tracker?.ApiUsername,
                    HasToken = tracker?.HasToken ?? false,
                    TokenValid = tracker?.TokenValid ?? false,
                    VehicleCount = tracker?.VehicleCount ?? 0
                };
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rental companies with tracker");
            return StatusCode(500, new { error = ex.Message, details = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Setup tracker for a rental company using simple username/password login
    /// </summary>
    [HttpPost("setup-tracker")]
    public async Task<ActionResult> SetupTracker([FromBody] SetupTrackerRequest request)
    {
        try
        {
            // Verify rental company exists
            var rentalCompany = await _context.Companies.FindAsync(request.RentalCompanyId);
            if (rentalCompany == null)
                return NotFound(new { error = "Rental company not found" });

            // If username and password provided, do login to get accountId/userId
            if (!string.IsNullOrEmpty(request.ApiUsername) && !string.IsNullOrEmpty(request.ApiPassword))
            {
                _logger.LogInformation("Attempting Fleet77 login for user: {Username}", request.ApiUsername);
                
                // Try to login and get credentials
                var loginResult = await _fleet77Service.LoginAsync(request.ApiUsername, request.ApiPassword);
                
                // If login didn't return accountId but we have one manually provided, use it
                if (loginResult == null && !string.IsNullOrEmpty(request.AccountId))
                {
                    _logger.LogInformation("Using manually provided accountId: {AccountId}", request.AccountId);
                    
                    // Get userId from a simpler login call
                    var userId = await _fleet77Service.GetUserIdFromLoginAsync(request.ApiUsername, request.ApiPassword);
                    
                    if (userId != 0 && long.TryParse(request.AccountId, out var accountId))
                    {
                        loginResult = new Fleet77LoginResult
                        {
                            AccountId = accountId,
                            UserId = userId,
                            PassHash = Fleet77Service.GeneratePassHash(request.ApiPassword),
                            Username = request.ApiUsername,
                            Name = request.ApiUsername
                        };
                    }
                }
                
                if (loginResult == null)
                {
                    return BadRequest(new { 
                        error = "Login failed - could not determine accountId. Please provide Account ID manually.",
                        needsAccountId = true
                    });
                }

                // Check if external company already exists for this rental company
                var externalCompany = await _context.ExternalCompanies
                    .FirstOrDefaultAsync(ec => ec.RentalCompanyId == request.RentalCompanyId);

                // Store credentials: "accountId|userId|username"
                var credentials = $"{loginResult.AccountId}|{loginResult.UserId}|{loginResult.Username}";

                if (externalCompany == null)
                {
                    externalCompany = new ExternalCompany
                    {
                        Id = Guid.NewGuid(),
                        CompanyName = $"{rentalCompany.CompanyName} - {loginResult.Name}",
                        ApiBaseUrl = "https://admin-api.fleet77.com",
                        ApiUsername = credentials,
                        ApiPassword = request.ApiPassword,
                        ApiToken = loginResult.PassHash,
                        TokenExpiresAt = DateTime.UtcNow.AddYears(10),
                        RentalCompanyId = request.RentalCompanyId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ExternalCompanies.Add(externalCompany);
                }
                else
                {
                    externalCompany.ApiUsername = credentials;
                    externalCompany.ApiPassword = request.ApiPassword;
                    externalCompany.ApiToken = loginResult.PassHash;
                    externalCompany.TokenExpiresAt = DateTime.UtcNow.AddYears(10);
                    externalCompany.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Fleet77 credentials saved for company {CompanyName}: accountId={AccountId}, userId={UserId}", 
                    rentalCompany.CompanyName, loginResult.AccountId, loginResult.UserId);

                return Ok(new
                {
                    message = "Tracker configured successfully",
                    externalCompanyId = externalCompany.Id,
                    companyName = rentalCompany.CompanyName,
                    trackerName = loginResult.Name,
                    accountId = loginResult.AccountId,
                    userId = loginResult.UserId,
                    tokenValid = true
                });
            }

            // Legacy: If accountId/userId/apiToken provided directly
            if (!string.IsNullOrEmpty(request.AccountId) && !string.IsNullOrEmpty(request.UserId))
            {
                var externalCompany = await _context.ExternalCompanies
                    .FirstOrDefaultAsync(ec => ec.RentalCompanyId == request.RentalCompanyId);

                var credentials = $"{request.AccountId}|{request.UserId}|{request.ApiUsername}";

                if (externalCompany == null)
                {
                    externalCompany = new ExternalCompany
                    {
                        Id = Guid.NewGuid(),
                        CompanyName = $"{rentalCompany.CompanyName} Tracker",
                        ApiBaseUrl = request.ApiBaseUrl ?? "https://admin-api.fleet77.com",
                        ApiUsername = credentials,
                        ApiPassword = request.ApiPassword,
                        ApiToken = request.ApiToken,
                        TokenExpiresAt = DateTime.UtcNow.AddYears(10),
                        RentalCompanyId = request.RentalCompanyId,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ExternalCompanies.Add(externalCompany);
                }
                else
                {
                    externalCompany.ApiUsername = credentials;
                    if (!string.IsNullOrEmpty(request.ApiPassword))
                        externalCompany.ApiPassword = request.ApiPassword;
                    if (!string.IsNullOrEmpty(request.ApiToken))
                    {
                        externalCompany.ApiToken = request.ApiToken;
                        externalCompany.TokenExpiresAt = DateTime.UtcNow.AddYears(10);
                    }
                    externalCompany.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Tracker configured (legacy mode)",
                    externalCompanyId = externalCompany.Id,
                    tokenValid = !string.IsNullOrEmpty(request.ApiToken)
                });
            }

            return BadRequest(new { error = "Please provide username and password" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up tracker");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    #endregion
}

#region Request Models

public class CreateExternalCompanyRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? ApiBaseUrl { get; set; }
    public string? ApiUsername { get; set; }
    public string? ApiPassword { get; set; }
    public Guid? RentalCompanyId { get; set; }
}

public class UpdateExternalCompanyRequest
{
    public string? CompanyName { get; set; }
    public string? ApiBaseUrl { get; set; }
    public string? ApiUsername { get; set; }
    public string? ApiPassword { get; set; }
    public Guid? RentalCompanyId { get; set; }
    public bool? IsActive { get; set; }
}

public class LinkVehicleRequest
{
    public Guid VehicleId { get; set; }
    public Guid ExternalCompanyVehicleId { get; set; }
    public bool IsPrimary { get; set; } = true;
}

public class SetupTrackerRequest
{
    public Guid RentalCompanyId { get; set; }
    public string? TrackerProvider { get; set; }
    public string? ApiBaseUrl { get; set; }
    public string? ApiUsername { get; set; }
    public string? ApiPassword { get; set; }
    public string? ApiToken { get; set; }  // passHash
    public string? AccountId { get; set; }  // Fleet77 accountId
    public string? UserId { get; set; }     // Fleet77 userId
}

#endregion
