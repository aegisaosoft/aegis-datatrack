using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VehicleTracker.Api.Data;
using VehicleTracker.Api.Services;

namespace VehicleTracker.Api.Controllers;

/// <summary>
/// Public API controller for external integrations
/// Provides vehicle data access via username/password authentication
/// </summary>
[ApiController]
[Route("api/public")]
public class PublicApiController : ControllerBase
{
    private const string Fleet77ApiUrl = "https://admin-api.fleet77.com/priv4";
    private const string ApiVersion = "45.24";
    private const string Fleet77Secret = "98unf9832n097pi4jk1df";
    
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IFleet77Service _fleet77Service;
    private readonly TrackingDbContext _context;
    private readonly ILogger<PublicApiController> _logger;

    public PublicApiController(
        IHttpClientFactory httpClientFactory,
        IFleet77Service fleet77Service,
        TrackingDbContext context,
        ILogger<PublicApiController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _fleet77Service = fleet77Service;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get all vehicles with models and license plates
    /// Authenticates using Fleet77 username and password
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>List of vehicles with make, model, year, plate, VIN, and color</returns>
    [HttpPost("vehicles")]
    [ProducesResponseType(typeof(PublicVehiclesResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 401)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<ActionResult<PublicVehiclesResponse>> GetVehicles([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new ErrorResponse { Error = "Username and password are required" });
            }

            _logger.LogInformation("Public API: Attempting to get vehicles for user {Username}", request.Username);

            // Use Fleet77Service to login (it handles the complex auth flow)
            var loginResult = await _fleet77Service.LoginAsync(request.Username, request.Password);
            if (loginResult == null)
            {
                _logger.LogWarning("Public API: Login failed for user {Username}, trying direct approach", request.Username);
                
                // Fallback: try direct login approach
                var directResult = await LoginDirectAsync(request.Username, request.Password);
                if (directResult == null)
                {
                    return Unauthorized(new ErrorResponse { Error = "Invalid credentials or unable to authenticate" });
                }
                
                // Use direct result
                var vehiclesDirect = await GetAllVehiclesDirectAsync(directResult);
                
                _logger.LogInformation("Public API: Retrieved {Count} vehicles for user {Username} (direct)", 
                    vehiclesDirect.Count, request.Username);

                return Ok(new PublicVehiclesResponse
                {
                    Success = true,
                    Count = vehiclesDirect.Count,
                    Vehicles = vehiclesDirect
                });
            }

            _logger.LogInformation("Public API: Login successful for user {Username}, accountId={AccountId}, userId={UserId}", 
                request.Username, loginResult.AccountId, loginResult.UserId);

            // Get vehicles using the login result
            var vehicles = await GetAllVehiclesDirectAsync(new LoginResult
            {
                AccountId = loginResult.AccountId,
                UserId = loginResult.UserId,
                PassHash = loginResult.PassHash,
                Username = loginResult.Username
            });
            
            _logger.LogInformation("Public API: Retrieved {Count} vehicles for user {Username}", 
                vehicles.Count, request.Username);

            return Ok(new PublicVehiclesResponse
            {
                Success = true,
                Count = vehicles.Count,
                Vehicles = vehicles
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Public API: Error getting vehicles for user {Username}", request.Username);
            return StatusCode(500, new ErrorResponse 
            { 
                Error = "An error occurred while fetching vehicles",
                Details = ex.Message
            });
        }
    }

    /// <summary>
    /// Get vehicles using existing external company credentials (by company ID)
    /// </summary>
    [HttpGet("vehicles/{companyId}")]
    [ProducesResponseType(typeof(PublicVehiclesResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 404)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<ActionResult<PublicVehiclesResponse>> GetVehiclesByCompany(Guid companyId)
    {
        try
        {
            var company = await _context.ExternalCompanies.FindAsync(companyId);
            if (company == null)
            {
                return NotFound(new ErrorResponse { Error = "Company not found" });
            }

            // Parse credentials from ApiUsername: "accountId|userId|username"
            var parts = company.ApiUsername?.Split('|') ?? Array.Empty<string>();
            if (parts.Length < 2 || string.IsNullOrEmpty(company.ApiToken))
            {
                return BadRequest(new ErrorResponse { Error = "Company credentials not configured properly" });
            }

            if (!long.TryParse(parts[0], out var accountId) || !long.TryParse(parts[1], out var userId))
            {
                return BadRequest(new ErrorResponse { Error = "Invalid company credentials format" });
            }

            var loginResult = new LoginResult
            {
                AccountId = accountId,
                UserId = userId,
                PassHash = company.ApiToken,
                Username = parts.Length > 2 ? parts[2] : ""
            };

            var vehicles = await GetAllVehiclesDirectAsync(loginResult);
            
            return Ok(new PublicVehiclesResponse
            {
                Success = true,
                Count = vehicles.Count,
                Vehicles = vehicles
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Public API: Error getting vehicles for company {CompanyId}", companyId);
            return StatusCode(500, new ErrorResponse 
            { 
                Error = "An error occurred while fetching vehicles",
                Details = ex.Message
            });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public ActionResult Health()
    {
        return Ok(new { status = "ok", timestamp = DateTime.UtcNow });
    }

    #region Private Methods

    private static string GeneratePassHash(string password)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Fleet77Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GenerateHmac(string queryString, string passHash)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(passHash));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildQueryString(SortedDictionary<string, object> parameters)
    {
        var sb = new StringBuilder();
        foreach (var kvp in parameters)
        {
            sb.Append(kvp.Key).Append('=').Append(kvp.Value).Append('&');
        }
        return sb.ToString();
    }

    private static string GenerateClientId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    /// <summary>
    /// Direct login approach - mirrors Fleet77Service.LoginAsync logic
    /// </summary>
    private async Task<LoginResult?> LoginDirectAsync(string username, string password)
    {
        try
        {
            var passHash = GeneratePassHash(password);
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var clientId = GenerateClientId();

            // Login request
            var payload = new Dictionary<string, object>
            {
                ["action"] = "login",
                ["service"] = "fleet",
                ["version"] = ApiVersion,
                ["time"] = nowMs,
                ["login"] = username,
                ["passHash"] = passHash,
                ["clientId"] = clientId,
                ["endpoint"] = "private"
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(Fleet77ApiUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Direct login response: {Content}", responseContent);

            if (!response.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            int status = 0;
            long currentUserId = 0;

            if (root.TryGetProperty("status", out var statusProp))
                status = statusProp.GetInt32();

            if (root.TryGetProperty("currentUserId", out var currentUserIdProp))
                currentUserId = currentUserIdProp.GetInt64();

            _logger.LogInformation("Direct login: status={Status}, currentUserId={CurrentUserId}", status, currentUserId);

            // Status 443 with currentUserId means we need to find accountId
            if (status == 443 && currentUserId != 0)
            {
                var accountId = await GetAccountIdAsync(currentUserId, passHash);
                if (accountId != 0)
                {
                    return new LoginResult
                    {
                        AccountId = accountId,
                        UserId = currentUserId,
                        PassHash = passHash,
                        Username = username
                    };
                }
            }

            // Status 200 - try to extract accountId and userId
            if (status == 200)
            {
                long accountId = 0;
                long userId = currentUserId;

                if (root.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var dataId))
                    accountId = dataId.GetInt64();

                if (accountId == 0 && root.TryGetProperty("account", out var account) && account.TryGetProperty("id", out var accId))
                    accountId = accId.GetInt64();

                if (root.TryGetProperty("user", out var user) && user.TryGetProperty("id", out var uid))
                    userId = uid.GetInt64();

                if (accountId != 0 && userId != 0)
                {
                    return new LoginResult
                    {
                        AccountId = accountId,
                        UserId = userId,
                        PassHash = passHash,
                        Username = username
                    };
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Direct login error");
            return null;
        }
    }

    /// <summary>
    /// Get accountId using HMAC-authenticated request
    /// </summary>
    private async Task<long> GetAccountIdAsync(long userId, string passHash)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var nowSecs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var clientId = GenerateClientId();

            // Try with accountId=0 (discovery mode)
            var hmacParams = new SortedDictionary<string, object>
            {
                ["accountId"] = 0,
                ["action"] = "getAllData",
                ["addressing"] = "all",
                ["isAdmin"] = "false",
                ["lastModified"] = nowSecs - 86400,
                ["service"] = "fleet",
                ["startSecs"] = nowSecs,
                ["time"] = nowMs,
                ["userId"] = userId,
                ["version"] = ApiVersion
            };

            var queryString = BuildQueryString(hmacParams);
            var hmac = GenerateHmac(queryString, passHash);

            var payload = new Dictionary<string, object>
            {
                ["accountId"] = 0,
                ["action"] = "getAllData",
                ["addressing"] = "all",
                ["isAdmin"] = "false",
                ["lastModified"] = nowSecs - 86400,
                ["service"] = "fleet",
                ["startSecs"] = nowSecs,
                ["time"] = nowMs,
                ["userId"] = userId,
                ["version"] = ApiVersion,
                ["hmac"] = hmac,
                ["clientId"] = clientId,
                ["endpoint"] = "private"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(Fleet77ApiUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("GetAccountId response: {Content}", 
                responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent);

            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            // Try to find accountId in response
            if (root.TryGetProperty("account", out var account) && account.TryGetProperty("id", out var accId))
                return accId.GetInt64();

            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var dataId))
                return dataId.GetInt64();

            // Fallback: try using userId as accountId
            var testAccountId = Math.Abs(userId);
            hmacParams["accountId"] = testAccountId;
            queryString = BuildQueryString(hmacParams);
            hmac = GenerateHmac(queryString, passHash);
            payload["accountId"] = testAccountId;
            payload["hmac"] = hmac;

            content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            response = await client.PostAsync(Fleet77ApiUrl, content);
            responseContent = await response.Content.ReadAsStringAsync();

            using var doc2 = JsonDocument.Parse(responseContent);
            var root2 = doc2.RootElement;

            if (root2.TryGetProperty("status", out var statusProp) && statusProp.GetInt32() == 200)
            {
                return testAccountId;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAccountId error");
            return 0;
        }
    }

    private async Task<List<PublicVehicle>> GetAllVehiclesDirectAsync(LoginResult login)
    {
        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var nowSecs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var clientId = GenerateClientId();

        // Build parameters for HMAC calculation
        var hmacParams = new SortedDictionary<string, object>
        {
            ["accountId"] = login.AccountId,
            ["action"] = "getAllData",
            ["addressing"] = "all",
            ["isAdmin"] = "false",
            ["lastModified"] = nowSecs - 86400,
            ["service"] = "fleet",
            ["startSecs"] = nowSecs,
            ["time"] = nowMs,
            ["userId"] = login.UserId,
            ["version"] = ApiVersion
        };

        var queryString = BuildQueryString(hmacParams);
        var hmac = GenerateHmac(queryString, login.PassHash);

        var payload = new Dictionary<string, object>
        {
            ["accountId"] = login.AccountId,
            ["action"] = "getAllData",
            ["addressing"] = "all",
            ["isAdmin"] = "false",
            ["lastModified"] = nowSecs - 86400,
            ["service"] = "fleet",
            ["startSecs"] = nowSecs,
            ["time"] = nowMs,
            ["userId"] = login.UserId,
            ["version"] = ApiVersion,
            ["hmac"] = hmac,
            ["clientId"] = clientId,
            ["endpoint"] = "private"
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(Fleet77ApiUrl, content);
        var json = await response.Content.ReadAsStringAsync();
        
        _logger.LogDebug("GetAllData response length: {Length}", json.Length);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var vehicles = new List<PublicVehicle>();

        // Try to find vehicles in account.vehicles or root.vehicles
        JsonElement vehiclesArray;
        bool found = false;

        if (root.TryGetProperty("account", out var account) &&
            account.TryGetProperty("vehicles", out vehiclesArray))
        {
            found = true;
        }
        else if (root.TryGetProperty("vehicles", out vehiclesArray))
        {
            found = true;
        }

        if (found && vehiclesArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in vehiclesArray.EnumerateArray())
            {
                var vehicle = new PublicVehicle
                {
                    Id = v.TryGetProperty("id", out var id) ? id.GetInt64() : 0,
                    Name = v.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                    Make = v.TryGetProperty("make", out var make) ? make.GetString() : null,
                    Model = v.TryGetProperty("model", out var model) ? model.GetString() : null,
                    Year = v.TryGetProperty("year", out var year) ? year.GetInt32() : 0,
                    Plate = v.TryGetProperty("plate", out var plate) ? plate.GetString() : null,
                    Vin = v.TryGetProperty("vin", out var vin) ? vin.GetString() : null,
                    Color = GetColorName(v.TryGetProperty("vehicleColor", out var color) ? color.GetInt32() : 0),
                    Notes = v.TryGetProperty("notes", out var notes) ? notes.GetString() : null,
                    DeviceSerial = v.TryGetProperty("serial", out var serial) ? serial.GetString() : null
                };
                vehicles.Add(vehicle);
            }
        }

        return vehicles;
    }

    private static string GetColorName(int colorCode)
    {
        return colorCode switch
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

    #endregion
}

#region Request/Response Models

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class PublicVehiclesResponse
{
    public bool Success { get; set; }
    public int Count { get; set; }
    public List<PublicVehicle> Vehicles { get; set; } = new();
}

public class PublicVehicle
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int Year { get; set; }
    public string? Plate { get; set; }
    public string? Vin { get; set; }
    public string? Color { get; set; }
    public string? Notes { get; set; }
    public string? DeviceSerial { get; set; }
    
    /// <summary>
    /// Formatted vehicle description: Year Make Model
    /// </summary>
    public string Description => $"{(Year > 0 ? Year.ToString() + " " : "")}{Make ?? ""} {Model ?? ""}".Trim();
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class LoginResult
{
    public long UserId { get; set; }
    public long AccountId { get; set; }
    public string PassHash { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}

#endregion
