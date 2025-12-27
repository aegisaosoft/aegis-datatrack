using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VehicleTracker.Api.Data;
using VehicleTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace VehicleTracker.Api.Services;

public interface IFleet77Service
{
    void SetActiveCompany(Guid externalCompanyId);
    Guid? GetActiveCompanyId();
    Task<List<VehicleStatus>> GetAllVehicleStatusesAsync();
    Task<VehicleStatus?> GetVehicleStatusAsync(string serial);
    Task<List<Location>> GetLocationsAsync(string serial, long startSecs, long endSecs);
    Task<Vehicle?> GetVehicleAsync(string serial);
    Task<List<Vehicle>> GetAllVehiclesAsync();
    Task<bool> SetStarterAsync(string serial, bool disable);
    Task<Fleet77LoginResult?> LoginAsync(string username, string password);
    Task<long> GetUserIdFromLoginAsync(string username, string password);
    Task<long> GetUserIdWithPassHashAsync(string username, string passHash);
    Task<bool> TestConnectionAsync(Guid companyId);
}

public class Fleet77Service : IFleet77Service
{
    private const string Fleet77ApiUrl = "https://admin-api.fleet77.com/priv4";
    private const string ApiVersion = "45.24";
    
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TrackingDbContext _context;
    private readonly ILogger<Fleet77Service> _logger;
    
    private Guid? _activeCompanyId;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public Fleet77Service(
        IHttpClientFactory httpClientFactory,
        TrackingDbContext context,
        ILogger<Fleet77Service> logger)
    {
        _httpClientFactory = httpClientFactory;
        _context = context;
        _logger = logger;
    }

    public void SetActiveCompany(Guid externalCompanyId)
    {
        _activeCompanyId = externalCompanyId;
        _logger.LogInformation("Active company set to {CompanyId}", externalCompanyId);
    }

    public Guid? GetActiveCompanyId() => _activeCompanyId;

    /// <summary>
    /// Generate passHash from password using HMAC-SHA256
    /// Formula: HMAC-SHA256(key=SECRET, message=password)
    /// </summary>
    private const string Fleet77Secret = "98unf9832n097pi4jk1df";
    
    public static string GeneratePassHash(string password)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Fleet77Secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Generate HMAC for request authentication
    /// The message should be in format: key1=value1&key2=value2&...& (sorted alphabetically, with trailing &)
    /// </summary>
    private static string GenerateHmac(string queryString, string passHash)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(passHash));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Build sorted query string for HMAC: key1=value1&key2=value2&...& (with trailing &)
    /// </summary>
    private static string BuildQueryString(SortedDictionary<string, object> parameters)
    {
        var sb = new StringBuilder();
        foreach (var kvp in parameters)
        {
            sb.Append(kvp.Key).Append('=').Append(kvp.Value).Append('&');
        }
        return sb.ToString();
    }

    private async Task<JsonDocument?> SendRequestAsync(string action, Dictionary<string, object>? extraParams = null)
    {
        if (!_activeCompanyId.HasValue)
        {
            throw new InvalidOperationException("No active company selected. Call SetActiveCompany first.");
        }

        var company = await _context.ExternalCompanies.FindAsync(_activeCompanyId.Value);
        if (company == null)
        {
            throw new InvalidOperationException($"Company {_activeCompanyId} not found");
        }

        // Get credentials - passHash stored as ApiToken
        var passHash = company.ApiToken;
        if (string.IsNullOrEmpty(passHash))
        {
            // Try to generate from password
            if (!string.IsNullOrEmpty(company.ApiPassword))
            {
                passHash = GeneratePassHash(company.ApiPassword);
                // Store it for future use
                company.ApiToken = passHash;
                company.TokenExpiresAt = DateTime.UtcNow.AddYears(10);
                await _context.SaveChangesAsync();
            }
            else
            {
                throw new InvalidOperationException($"No passHash or password for company {company.CompanyName}");
            }
        }

        // Parse Fleet77 credentials from ApiUsername: "accountId|userId|username"
        long accountId = 0;
        long userId = 0;
        
        if (!string.IsNullOrEmpty(company.ApiUsername))
        {
            var parts = company.ApiUsername.Split('|');
            if (parts.Length >= 2)
            {
                long.TryParse(parts[0], out accountId);
                long.TryParse(parts[1], out userId);
            }
        }

        if (accountId == 0 || userId == 0)
        {
            throw new InvalidOperationException($"Missing accountId or userId for company {company.CompanyName}. Format: accountId|userId|username");
        }

        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var nowSecs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var clientId = GenerateClientId();

        // Build parameters for HMAC calculation (sorted alphabetically by key)
        // These are the fields BEFORE hmac, clientId, endpoint are added
        var hmacParams = new SortedDictionary<string, object>
        {
            ["accountId"] = accountId,
            ["action"] = action,
            ["addressing"] = "all",  // Must be "all" to get vehicle data
            ["isAdmin"] = "false",
            ["lastModified"] = nowSecs - 86400,  // 24 hours ago - to get recent updates
            ["service"] = "fleet",
            ["startSecs"] = nowSecs,
            ["time"] = nowMs,
            ["userId"] = userId,
            ["version"] = ApiVersion
        };

        // Generate HMAC from query string: key1=value1&key2=value2&...& (with trailing &)
        var queryString = BuildQueryString(hmacParams);
        var hmac = GenerateHmac(queryString, passHash);
        
        _logger.LogDebug("Fleet77 HMAC query string: {QueryString}", queryString);
        
        // Build final payload for JSON (order matters for the API)
        var payload = new Dictionary<string, object>
        {
            ["accountId"] = accountId,
            ["action"] = action,
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

        // Send request
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var finalPayloadJson = JsonSerializer.Serialize(payload);
        var content = new StringContent(
            finalPayloadJson,
            Encoding.UTF8,
            "application/json"
        );

        _logger.LogInformation("Sending Fleet77 request: action={Action}, accountId={AccountId}, userId={UserId}", 
            action, accountId, userId);
        _logger.LogDebug("Fleet77 payload: {Payload}", finalPayloadJson);

        try
        {
            var response = await httpClient.PostAsync(Fleet77ApiUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Fleet77 response status: {Status}", response.StatusCode);
            _logger.LogInformation("Fleet77 response length: {Length} chars", responseContent.Length);
            _logger.LogDebug("Fleet77 full response: {Content}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Fleet77 API error: {Status} - {Content}", response.StatusCode, responseContent);
                return null;
            }

            return JsonDocument.Parse(responseContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Fleet77 API");
            throw;
        }
    }

    private static string GenerateClientId()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());
    }

    /// <summary>
    /// Login to Fleet77 with username/password and get accountId, userId, passHash
    /// </summary>
    public async Task<Fleet77LoginResult?> LoginAsync(string username, string password)
    {
        try
        {
            var passHash = GeneratePassHash(password);
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var clientId = GenerateClientId();

            // Login request - simpler than getAllData
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

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var payloadJson = JsonSerializer.Serialize(payload);
            var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

            _logger.LogInformation("Fleet77 login attempt for user: {Username}", username);
            _logger.LogDebug("Fleet77 login payload: {Payload}", payloadJson);

            var response = await httpClient.PostAsync(Fleet77ApiUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Fleet77 login response status: {Status}, length: {Length}", 
                response.StatusCode, responseContent.Length);
            _logger.LogDebug("Fleet77 login response: {Content}", responseContent);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Fleet77 login HTTP failed: {Status}", response.StatusCode);
                return null;
            }

            var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            // Log all root properties
            var props = new List<string>();
            foreach (var prop in root.EnumerateObject())
            {
                props.Add(prop.Name);
            }
            _logger.LogInformation("Fleet77 login response properties: {Props}", string.Join(", ", props));

            // Get status and currentUserId
            int status = 0;
            long currentUserId = 0;
            
            if (root.TryGetProperty("status", out var statusProp))
                status = statusProp.GetInt32();
            
            if (root.TryGetProperty("currentUserId", out var currentUserIdProp))
                currentUserId = currentUserIdProp.GetInt64();

            _logger.LogInformation("Fleet77 login: status={Status}, currentUserId={CurrentUserId}", status, currentUserId);

            // Status 443 with currentUserId means credentials are valid but need HMAC auth
            // We can use currentUserId to make a proper HMAC request
            if (status == 443 && currentUserId != 0)
            {
                _logger.LogInformation("Got currentUserId from login, fetching accountId via getAllData...");
                
                // Now make getAllData request to get accountId
                var accountId = await GetAccountIdAsync(currentUserId, passHash);
                
                if (accountId != 0)
                {
                    _logger.LogInformation("Fleet77 login successful via HMAC: accountId={AccountId}, userId={UserId}", 
                        accountId, currentUserId);
                    
                    return new Fleet77LoginResult
                    {
                        AccountId = accountId,
                        UserId = currentUserId,
                        PassHash = passHash,
                        Username = username,
                        Name = username
                    };
                }
            }

            // Status 200 - full success
            if (status == 200)
            {
                long accountId = 0;
                long userId = currentUserId;
                string? name = null;

                // Try data.id first (this is where accountId is in actual response)
                if (root.TryGetProperty("data", out var data))
                {
                    if (data.TryGetProperty("id", out var dataId))
                        accountId = dataId.GetInt64();
                }

                // Fallback to account.id
                if (accountId == 0 && root.TryGetProperty("account", out var account))
                {
                    if (account.TryGetProperty("id", out var accId))
                        accountId = accId.GetInt64();
                }

                if (root.TryGetProperty("user", out var user))
                {
                    if (user.TryGetProperty("id", out var uid))
                        userId = uid.GetInt64();
                    if (user.TryGetProperty("name", out var uname))
                        name = uname.GetString();
                }

                if (root.TryGetProperty("name", out var rootName))
                    name = rootName.GetString();

                if (accountId != 0 && userId != 0)
                {
                    _logger.LogInformation("Fleet77 login success: accountId={AccountId}, userId={UserId}", accountId, userId);
                    return new Fleet77LoginResult
                    {
                        AccountId = accountId,
                        UserId = userId,
                        PassHash = passHash,
                        Username = username,
                        Name = name ?? username
                    };
                }
            }

            _logger.LogWarning("Fleet77 login failed: status={Status}, no valid credentials extracted", status);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fleet77 login error");
            return null;
        }
    }

    /// <summary>
    /// Get just the userId from login response (for when accountId is provided manually)
    /// </summary>
    public async Task<long> GetUserIdFromLoginAsync(string username, string password)
    {
        try
        {
            var passHash = GeneratePassHash(password);
            return await GetUserIdWithPassHashAsync(username, passHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting userId from login");
            return 0;
        }
    }

    /// <summary>
    /// Get userId using passHash directly (from Local Storage)
    /// </summary>
    public async Task<long> GetUserIdWithPassHashAsync(string username, string passHash)
    {
        try
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var clientId = GenerateClientId();

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

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            _logger.LogInformation("Fleet77 login (with passHash) for user: {Username}", username);

            var response = await httpClient.PostAsync(Fleet77ApiUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Fleet77 login response: {Content}", responseContent);

            if (!response.IsSuccessStatusCode)
                return 0;

            var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("currentUserId", out var currentUserIdProp))
            {
                var userId = currentUserIdProp.GetInt64();
                _logger.LogInformation("Got userId from login: {UserId}", userId);
                return userId;
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting userId with passHash");
            return 0;
        }
    }

    /// <summary>
    /// Get accountId by trying HMAC-authenticated requests
    /// </summary>
    private async Task<long> GetAccountIdAsync(long userId, string passHash)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        // Method 1: Try getAllData with HMAC but accountId=0 (discovery mode)
        try
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var nowSecs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var clientId = GenerateClientId();

            // Build HMAC params with accountId=0
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
            var response = await httpClient.PostAsync(Fleet77ApiUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("Fleet77 getAllData (accountId=0) response: {Content}", 
                responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent);

            var doc = JsonDocument.Parse(responseContent);
            var accountId = FindAccountIdInJson(doc.RootElement);
            if (accountId != 0) return accountId;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("getAllData with accountId=0 failed: {Error}", ex.Message);
        }

        // Method 2: Try with negative userId as accountId (some APIs use this pattern)
        try
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var nowSecs = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var clientId = GenerateClientId();
            var testAccountId = Math.Abs(userId); // Try absolute value of userId

            var hmacParams = new SortedDictionary<string, object>
            {
                ["accountId"] = testAccountId,
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
                ["accountId"] = testAccountId,
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
            var response = await httpClient.PostAsync(Fleet77ApiUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("Fleet77 getAllData (accountId={TestAccountId}) response: {Content}", 
                testAccountId,
                responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent);

            var doc = JsonDocument.Parse(responseContent);
            
            // Check if we got valid data (status 200)
            if (doc.RootElement.TryGetProperty("status", out var statusProp) && statusProp.GetInt32() == 200)
            {
                var accountId = FindAccountIdInJson(doc.RootElement);
                if (accountId != 0) return accountId;
                
                // If we got status 200, the testAccountId might be valid
                return testAccountId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("getAllData with abs(userId) failed: {Error}", ex.Message);
        }

        // Method 3: Try selectAccount action
        try
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var clientId = GenerateClientId();

            var payload = new Dictionary<string, object>
            {
                ["action"] = "selectAccount",
                ["service"] = "fleet",
                ["version"] = ApiVersion,
                ["time"] = nowMs,
                ["userId"] = userId,
                ["passHash"] = passHash,
                ["clientId"] = clientId,
                ["endpoint"] = "private"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(Fleet77ApiUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("Fleet77 selectAccount response: {Content}", responseContent);

            var doc = JsonDocument.Parse(responseContent);
            var accountId = FindAccountIdInJson(doc.RootElement);
            if (accountId != 0) return accountId;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("selectAccount failed: {Error}", ex.Message);
        }

        // Method 4: Try getAccountList action
        try
        {
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var clientId = GenerateClientId();

            var payload = new Dictionary<string, object>
            {
                ["action"] = "getAccountList",
                ["service"] = "fleet",
                ["version"] = ApiVersion,
                ["time"] = nowMs,
                ["userId"] = userId,
                ["passHash"] = passHash,
                ["clientId"] = clientId,
                ["endpoint"] = "private"
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(Fleet77ApiUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();
            
            _logger.LogInformation("Fleet77 getAccountList response: {Content}", responseContent);

            var doc = JsonDocument.Parse(responseContent);
            var accountId = FindAccountIdInJson(doc.RootElement);
            if (accountId != 0) return accountId;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("getAccountList failed: {Error}", ex.Message);
        }

        _logger.LogWarning("Could not determine accountId from any Fleet77 API method");
        return 0;
    }

    /// <summary>
    /// Recursively search for accountId in JSON response
    /// </summary>
    private long FindAccountIdInJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            // First check for data.statuses array (from getAllData response)
            if (element.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (data.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
                {
                    foreach (var status in statuses.EnumerateArray())
                    {
                        if (status.TryGetProperty("accountId", out var accId) && accId.ValueKind == JsonValueKind.Number)
                        {
                            var val = accId.GetInt64();
                            if (val > 1000000)
                            {
                                _logger.LogInformation("Found accountId in statuses: {AccountId}", val);
                                return val;
                            }
                        }
                    }
                }
            }

            foreach (var prop in element.EnumerateObject())
            {
                // Direct accountId property
                if (prop.Name.Equals("accountId", StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number)
                    {
                        var val = prop.Value.GetInt64();
                        if (val > 1000000) // accountId is typically a large number
                        {
                            _logger.LogInformation("Found accountId: {AccountId}", val);
                            return val;
                        }
                    }
                }

                // Check for account object with id and companyName
                if (prop.Name.Equals("account", StringComparison.OrdinalIgnoreCase) && 
                    prop.Value.ValueKind == JsonValueKind.Object)
                {
                    if (prop.Value.TryGetProperty("id", out var idProp) && 
                        prop.Value.TryGetProperty("companyName", out _))
                    {
                        var val = idProp.GetInt64();
                        _logger.LogInformation("Found accountId in account object: {AccountId}", val);
                        return val;
                    }
                }

                // Check accounts array
                if (prop.Name.Equals("accounts", StringComparison.OrdinalIgnoreCase) && 
                    prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var acc in prop.Value.EnumerateArray())
                    {
                        if (acc.TryGetProperty("id", out var idProp))
                        {
                            var val = idProp.GetInt64();
                            if (val > 1000000)
                            {
                                _logger.LogInformation("Found accountId in accounts array: {AccountId}", val);
                                return val;
                            }
                        }
                    }
                }

                // Recurse into nested objects (but not arrays to avoid infinite loops)
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    var found = FindAccountIdInJson(prop.Value);
                    if (found != 0)
                        return found;
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Test connection with stored credentials
    /// </summary>
    public async Task<bool> TestConnectionAsync(Guid companyId)
    {
        try
        {
            SetActiveCompany(companyId);
            var statuses = await GetAllVehicleStatusesAsync();
            return statuses.Count > 0 || true; // Even 0 vehicles is OK if no error
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<VehicleStatus>> GetAllVehicleStatusesAsync()
    {
        try
        {
            _logger.LogInformation("=== GetAllVehicleStatusesAsync START ===");
            var doc = await SendRequestAsync("getAllData");
            if (doc == null)
            {
                _logger.LogWarning("SendRequestAsync returned null");
                return new List<VehicleStatus>();
            }

            var result = new List<VehicleStatus>();
            var root = doc.RootElement;
            
            // Log raw response structure
            _logger.LogInformation("Response root properties: {Props}", 
                string.Join(", ", root.EnumerateObject().Select(p => p.Name)));

            // Check status
            if (root.TryGetProperty("status", out var statusProp))
            {
                _logger.LogInformation("API status: {Status}", statusProp.GetInt32());
            }

            // Fleet77 returns data in: { status: 200, data: { statuses: [...] } }
            if (root.TryGetProperty("data", out var data))
            {
                _logger.LogInformation("Data properties: {Props}", 
                    string.Join(", ", data.EnumerateObject().Select(p => p.Name)));
                    
                if (data.TryGetProperty("statuses", out var statusesArray))
                {
                    var statusCount = statusesArray.GetArrayLength();
                    _logger.LogInformation("Found {Count} statuses in response", statusCount);
                    
                    foreach (var status in statusesArray.EnumerateArray())
                    {
                        try
                        {
                            var vehicleStatus = ParseFleet77Status(status);
                            if (vehicleStatus != null) result.Add(vehicleStatus);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing vehicle status");
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No 'statuses' array in data");
                }
            }
            else
            {
                _logger.LogWarning("No 'data' property in response");
            }

            _logger.LogInformation("=== GetAllVehicleStatusesAsync END: Retrieved {Count} vehicles ===", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all vehicle statuses");
            throw;
        }
    }

    /// <summary>
    /// Parse Fleet77 status object into VehicleStatus
    /// Fleet77 format: lastLat/lastLng in 10^7 format (409181100 = 40.9181100)
    /// </summary>
    private VehicleStatus? ParseFleet77Status(JsonElement status)
    {
        // vehicleId is the unique identifier
        var vehicleId = status.GetProperty("vehicleId").GetInt64();
        var serial = vehicleId.ToString();
        
        // Address can be used as name, or we could get it from vehicles list
        var name = status.TryGetProperty("address", out var addr) ? addr.GetString() ?? serial : serial;
        
        // Coordinates are in 10^7 format (409181100 = 40.9181100)
        double lat = 0, lng = 0;
        if (status.TryGetProperty("lastLat", out var latProp))
        {
            lat = latProp.GetInt64() / 10_000_000.0;
        }
        if (status.TryGetProperty("lastLng", out var lngProp))
        {
            lng = lngProp.GetInt64() / 10_000_000.0;
        }

        // Speed
        var speed = status.TryGetProperty("lastSpeed", out var s) ? s.GetInt32() : 0;

        // Date (Unix timestamp)
        long date = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (status.TryGetProperty("lastDate", out var d) && d.ValueKind == JsonValueKind.Number)
        {
            date = d.GetInt64();
        }

        // State: 0 = stopped, 1 = moving
        int typeId = 2; // Default: Ignition Off
        if (status.TryGetProperty("stateId", out var stateId))
        {
            var state = stateId.GetInt32();
            if (state == 1)
            {
                typeId = speed > 0 ? 5 : 4; // Moving or Ignition On
            }
        }

        // Heading
        int? heading = null;
        if (status.TryGetProperty("heading", out var h))
        {
            heading = h.GetInt32();
        }

        // Voltage in millivolts
        double volts = 0;
        if (status.TryGetProperty("lastVolts", out var v))
        {
            volts = v.GetDouble();
        }

        // Distance in meters
        double distance = 0;
        if (status.TryGetProperty("totalDistance", out var dist))
        {
            distance = dist.GetDouble();
        }

        // Starter disabled timestamp (0 = enabled)
        long disabled = 0;
        if (status.TryGetProperty("starterDisabledDate", out var disabledProp))
        {
            disabled = disabledProp.GetInt64();
        }

        // Buzzer
        long buzzer = 0;
        if (status.TryGetProperty("buzzerStart", out var buzzerProp))
        {
            buzzer = buzzerProp.GetInt64();
        }

        return new VehicleStatus
        {
            Serial = serial,
            Name = name,
            Lat = lat,
            Lng = lng,
            Speed = speed,
            Date = date,
            TypeId = typeId,
            Heading = heading,
            Volts = volts,
            Distance = distance,
            Disabled = disabled,
            Buzzer = buzzer
        };
    }

    public async Task<VehicleStatus?> GetVehicleStatusAsync(string serial)
    {
        var allStatuses = await GetAllVehicleStatusesAsync();
        return allStatuses.FirstOrDefault(s => s.Serial == serial);
    }

    public async Task<List<Location>> GetLocationsAsync(string serial, long startSecs, long endSecs)
    {
        try
        {
            var doc = await SendRequestAsync("getHistory", new Dictionary<string, object>
            {
                ["serial"] = serial,
                ["startTime"] = startSecs,
                ["endTime"] = endSecs
            });

            if (doc == null) return new List<Location>();

            var locations = new List<Location>();
            var root = doc.RootElement;

            if (root.TryGetProperty("history", out var history))
            {
                foreach (var point in history.EnumerateArray())
                {
                    var loc = new Location
                    {
                        Lat = point.TryGetProperty("lat", out var lat) ? lat.GetDouble() : 0,
                        Lng = point.TryGetProperty("lon", out var lon) ? lon.GetDouble() : 
                              point.TryGetProperty("lng", out var lng) ? lng.GetDouble() : 0,
                        Speed = point.TryGetProperty("speed", out var s) ? (int)s.GetDouble() : 0,
                        Heading = point.TryGetProperty("heading", out var h) ? h.GetInt32() : 0,
                        Date = point.TryGetProperty("time", out var t) ? t.GetInt64() :
                               point.TryGetProperty("date", out var d) ? d.GetInt64() :
                               DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    locations.Add(loc);
                }
            }

            return locations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching locations for {Serial}", serial);
            throw;
        }
    }

    public async Task<Vehicle?> GetVehicleAsync(string serial)
    {
        var allVehicles = await GetAllVehiclesAsync();
        return allVehicles.FirstOrDefault(v => v.Serial == serial);
    }

    public async Task<List<Vehicle>> GetAllVehiclesAsync()
    {
        try
        {
            var doc = await SendRequestAsync("getAllData");
            if (doc == null) 
            {
                _logger.LogWarning("GetAllVehiclesAsync: SendRequestAsync returned null");
                return new List<Vehicle>();
            }

            var vehicles = new List<Vehicle>();
            var root = doc.RootElement;

            // Debug: log root properties
            var rootProps = new List<string>();
            foreach (var prop in root.EnumerateObject())
            {
                rootProps.Add(prop.Name);
            }
            _logger.LogInformation("GetAllVehiclesAsync: root properties = [{Props}]", string.Join(", ", rootProps));
            
            // Check status
            if (root.TryGetProperty("status", out var statusProp))
            {
                _logger.LogInformation("GetAllVehiclesAsync: status = {Status}", statusProp.GetInt32());
            }

            // Vehicles are in account.vehicles (NOT data.vehicles)
            JsonElement vehiclesArray;
            bool foundVehicles = false;
            
            if (root.TryGetProperty("account", out var account))
            {
                // Log account properties
                var accountProps = new List<string>();
                if (account.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in account.EnumerateObject())
                    {
                        accountProps.Add(prop.Name);
                    }
                }
                _logger.LogInformation("GetAllVehiclesAsync: account properties = [{Props}]", string.Join(", ", accountProps));
                
                if (account.TryGetProperty("vehicles", out vehiclesArray))
                {
                    _logger.LogInformation("Found {Count} vehicles in account.vehicles", vehiclesArray.GetArrayLength());
                    foundVehicles = true;
                    
                    foreach (var v in vehiclesArray.EnumerateArray())
                    {
                        // Use "id" as Serial to match with statuses (vehicleId)
                        var id = v.TryGetProperty("id", out var idProp) ? idProp.GetInt64().ToString() : "";
                        var deviceSerial = v.TryGetProperty("serial", out var s) ? s.GetString() ?? "" : "";
                        
                        var vehicle = new Vehicle
                        {
                            Serial = id,  // Match with vehicleId from statuses
                            Name = v.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                            Notes = v.TryGetProperty("notes", out var notes) ? notes.GetString() : null,
                            Vin = v.TryGetProperty("vin", out var vin) ? vin.GetString() : null,
                            Plate = v.TryGetProperty("plate", out var plate) ? plate.GetString() : null,
                            Make = v.TryGetProperty("make", out var make) ? make.GetString() : null,
                            Model = v.TryGetProperty("model", out var model) ? model.GetString() : null,
                            Year = v.TryGetProperty("year", out var year) ? year.GetInt32() : 0,
                            VehicleColor = v.TryGetProperty("vehicleColor", out var color) ? color.GetInt32() : 0,
                            AlternateName = deviceSerial,  // Store device serial in AlternateName
                            OdometerOffset = v.TryGetProperty("odometerOffset", out var odo) ? (int)odo.GetDouble() : 0
                        };
                        vehicles.Add(vehicle);
                    }
                }
            }
            
            // Fallback to root.vehicles
            if (!foundVehicles && root.TryGetProperty("vehicles", out vehiclesArray))
            {
                _logger.LogInformation("Found {Count} vehicles in root.vehicles", vehiclesArray.GetArrayLength());
                
                foreach (var v in vehiclesArray.EnumerateArray())
                {
                    // Use "id" as Serial to match with statuses (vehicleId)
                    var id = v.TryGetProperty("id", out var idProp) ? idProp.GetInt64().ToString() : "";
                    var deviceSerial = v.TryGetProperty("serial", out var s) ? s.GetString() ?? "" : "";
                    
                    var vehicle = new Vehicle
                    {
                        Serial = id,  // Match with vehicleId from statuses
                        Name = v.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "",
                        Notes = v.TryGetProperty("notes", out var notes) ? notes.GetString() : null,
                        Vin = v.TryGetProperty("vin", out var vin) ? vin.GetString() : null,
                        Plate = v.TryGetProperty("plate", out var plate) ? plate.GetString() : null,
                        Make = v.TryGetProperty("make", out var make) ? make.GetString() : null,
                        Model = v.TryGetProperty("model", out var model) ? model.GetString() : null,
                        Year = v.TryGetProperty("year", out var year) ? year.GetInt32() : 0,
                        VehicleColor = v.TryGetProperty("vehicleColor", out var color) ? color.GetInt32() : 0,
                        AlternateName = deviceSerial,  // Store device serial in AlternateName
                        OdometerOffset = v.TryGetProperty("odometerOffset", out var odo) ? (int)odo.GetDouble() : 0
                    };
                    vehicles.Add(vehicle);
                }
            }
            
            if (vehicles.Count == 0)
            {
                _logger.LogWarning("No vehicles found in response");
            }

            _logger.LogInformation("Returning {Count} vehicles with details", vehicles.Count);
            return vehicles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all vehicles");
            throw;
        }
    }

    public async Task<bool> SetStarterAsync(string serial, bool disable)
    {
        try
        {
            var doc = await SendRequestAsync("setStarter", new Dictionary<string, object>
            {
                ["serial"] = serial,
                ["disable"] = disable
            });

            return doc != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting starter for {Serial}", serial);
            return false;
        }
    }
}

public class Fleet77LoginResult
{
    public long AccountId { get; set; }
    public long UserId { get; set; }
    public string PassHash { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
