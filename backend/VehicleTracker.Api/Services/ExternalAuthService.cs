using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VehicleTracker.Api.Data;
using VehicleTracker.Api.Data.Entities;

namespace VehicleTracker.Api.Services;

/// <summary>
/// Service for authenticating with external tracking APIs using credentials from DB
/// </summary>
public interface IExternalAuthService
{
    Task<string?> GetTokenAsync(Guid externalCompanyId);
    Task<string?> LoginAsync(Guid externalCompanyId);
    Task<bool> ValidateCredentialsAsync(Guid externalCompanyId);
    Task ClearTokenAsync(Guid externalCompanyId);
    Task<ExternalCompany?> GetCompanyWithValidTokenAsync(Guid externalCompanyId);
}

public class ExternalAuthService : IExternalAuthService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ExternalAuthService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExternalAuthService(
        IServiceProvider serviceProvider,
        IHttpClientFactory httpClientFactory,
        ILogger<ExternalAuthService> logger)
    {
        _serviceProvider = serviceProvider;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ExternalCompany?> GetCompanyWithValidTokenAsync(Guid externalCompanyId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();

        var company = await context.ExternalCompanies.FindAsync(externalCompanyId);
        if (company == null) return null;

        // Check if token is still valid
        if (!string.IsNullOrEmpty(company.ApiToken) && 
            company.TokenExpiresAt.HasValue && 
            company.TokenExpiresAt > DateTime.UtcNow)
        {
            return company;
        }

        // Token expired or missing, try to login
        var token = await LoginInternalAsync(company);
        if (!string.IsNullOrEmpty(token))
        {
            company.ApiToken = token;
            company.TokenExpiresAt = DateTime.UtcNow.AddHours(12);
            company.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        return company;
    }

    public async Task<string?> GetTokenAsync(Guid externalCompanyId)
    {
        var company = await GetCompanyWithValidTokenAsync(externalCompanyId);
        return company?.ApiToken;
    }

    public async Task<string?> LoginAsync(Guid externalCompanyId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();

        var company = await context.ExternalCompanies.FindAsync(externalCompanyId);
        if (company == null)
        {
            _logger.LogError("External company {Id} not found", externalCompanyId);
            return null;
        }

        var token = await LoginInternalAsync(company);
        
        if (!string.IsNullOrEmpty(token))
        {
            company.ApiToken = token;
            company.TokenExpiresAt = DateTime.UtcNow.AddHours(12);
            company.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            
            _logger.LogInformation("Successfully authenticated with {Company}", company.CompanyName);
        }

        return token;
    }

    private async Task<string?> LoginInternalAsync(ExternalCompany company)
    {
        if (string.IsNullOrEmpty(company.ApiBaseUrl) ||
            string.IsNullOrEmpty(company.ApiUsername) ||
            string.IsNullOrEmpty(company.ApiPassword))
        {
            _logger.LogWarning("Missing credentials for company {Company}", company.CompanyName);
            return null;
        }

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        // Try different login endpoints (including Navixy/FM tracking patterns)
        var loginEndpoints = new[] 
        { 
            "/user/auth",           // Navixy/FM tracking standard
            "/v1/user/auth",        // versioned API
            "/api/user/auth",       // nested API
            "/login", 
            "/auth", 
            "/getToken",
            "/session", 
            "/token", 
            "/authenticate", 
            "/api/login" 
        };

        foreach (var endpoint in loginEndpoints)
        {
            var token = await TryLoginAsync(httpClient, company.ApiBaseUrl, endpoint, 
                company.ApiUsername, company.ApiPassword);
            
            if (!string.IsNullOrEmpty(token))
            {
                _logger.LogInformation("Login successful via {Endpoint}", endpoint);
                return token;
            }
        }

        // Also try without /api suffix if base URL ends with it
        if (company.ApiBaseUrl.TrimEnd('/').EndsWith("/api"))
        {
            var altBaseUrl = company.ApiBaseUrl.TrimEnd('/').Substring(0, company.ApiBaseUrl.TrimEnd('/').Length - 4);
            foreach (var endpoint in new[] { "/user/auth", "/api/user/auth" })
            {
                var token = await TryLoginAsync(httpClient, altBaseUrl, endpoint, 
                    company.ApiUsername, company.ApiPassword);
                
                if (!string.IsNullOrEmpty(token))
                {
                    _logger.LogInformation("Login successful via alt URL {Endpoint}", endpoint);
                    return token;
                }
            }
        }

        _logger.LogError("All login attempts failed for {Company}", company.CompanyName);
        return null;
    }

    private async Task<string?> TryLoginAsync(HttpClient client, string baseUrl, string endpoint, 
        string username, string password)
    {
        var url = baseUrl.TrimEnd('/') + endpoint;

        // Try JSON body with different field names
        var jsonBodies = new[]
        {
            JsonSerializer.Serialize(new Dictionary<string, string> { ["login"] = username, ["password"] = password }),
            JsonSerializer.Serialize(new Dictionary<string, string> { ["username"] = username, ["password"] = password }),
            JsonSerializer.Serialize(new Dictionary<string, string> { ["uid"] = username, ["pwd"] = password }),
            JsonSerializer.Serialize(new Dictionary<string, string> { ["user"] = username, ["pass"] = password }),
            JsonSerializer.Serialize(new Dictionary<string, string> { ["email"] = username, ["password"] = password })
        };

        foreach (var jsonBody in jsonBodies)
        {
            try
            {
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("Login attempt {Url}: {Status} - {Body}", url, response.StatusCode, 
                    responseBody.Length > 200 ? responseBody.Substring(0, 200) : responseBody);
                
                if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var token = await ExtractTokenFromResponseBody(responseBody);
                    if (!string.IsNullOrEmpty(token)) return token;
                }
                
                // Some APIs return 200 but with error in body
                if (!string.IsNullOrEmpty(responseBody) && responseBody.Contains("hash"))
                {
                    var token = await ExtractTokenFromResponseBody(responseBody);
                    if (!string.IsNullOrEmpty(token)) return token;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Login attempt failed: {Error}", ex.Message);
            }
        }

        // Try form data
        var formAttempts = new (string, string)[][]
        {
            new[] { ("login", username), ("password", password) },
            new[] { ("username", username), ("password", password) },
            new[] { ("uid", username), ("pwd", password) },
            new[] { ("user", username), ("pass", password) }
        };

        foreach (var fields in formAttempts)
        {
            try
            {
                var formData = new FormUrlEncodedContent(
                    fields.Select(f => new KeyValuePair<string, string>(f.Item1, f.Item2)));

                var response = await client.PostAsync(url, formData);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    var token = await ExtractTokenFromResponseBody(responseBody);
                    if (!string.IsNullOrEmpty(token)) return token;
                }
            }
            catch { }
        }

        // Try query parameters with different field names
        var queryAttempts = new[]
        {
            $"?login={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}",
            $"?uid={Uri.EscapeDataString(username)}&pwd={Uri.EscapeDataString(password)}",
            $"?username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}"
        };

        foreach (var query in queryAttempts)
        {
            try
            {
                var queryUrl = url + query;
                var response = await client.GetAsync(queryUrl);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                _logger.LogDebug("Query login attempt {Url}: {Status}", queryUrl, response.StatusCode);
                
                if (response.IsSuccessStatusCode)
                {
                    var token = await ExtractTokenFromResponseBody(responseBody);
                    if (!string.IsNullOrEmpty(token)) return token;
                }
            }
            catch { }
        }

        return null;
    }

    private async Task<string?> ExtractTokenFromResponse(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return await ExtractTokenFromResponseBody(content);
    }

    private Task<string?> ExtractTokenFromResponseBody(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            // Check for success field first (Navixy pattern)
            if (root.TryGetProperty("success", out var successProp))
            {
                if (successProp.ValueKind == JsonValueKind.False ||
                    (successProp.ValueKind == JsonValueKind.String && successProp.GetString() == "false"))
                {
                    return Task.FromResult<string?>(null);
                }
            }

            var tokenFields = new[] { "hash", "token", "apiKey", "api_key", "apikey", "access_token", 
                "accessToken", "key", "session", "sessionId", "data", "value" };
            
            foreach (var field in tokenFields)
            {
                if (root.TryGetProperty(field, out var tokenElement))
                {
                    if (tokenElement.ValueKind == JsonValueKind.String)
                    {
                        var token = tokenElement.GetString();
                        if (!string.IsNullOrEmpty(token)) return Task.FromResult<string?>(token);
                    }
                    else if (tokenElement.ValueKind == JsonValueKind.Object)
                    {
                        // Check nested object
                        foreach (var nestedField in tokenFields)
                        {
                            if (tokenElement.TryGetProperty(nestedField, out var nestedToken) &&
                                nestedToken.ValueKind == JsonValueKind.String)
                            {
                                var token = nestedToken.GetString();
                                if (!string.IsNullOrEmpty(token)) return Task.FromResult<string?>(token);
                            }
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Response might be plain text token
            if (!string.IsNullOrWhiteSpace(content) && content.Length < 500 && !content.Contains('<'))
            {
                return Task.FromResult<string?>(content.Trim());
            }
        }

        return Task.FromResult<string?>(null);
    }

    public async Task<bool> ValidateCredentialsAsync(Guid externalCompanyId)
    {
        var token = await LoginAsync(externalCompanyId);
        return !string.IsNullOrEmpty(token);
    }

    public async Task ClearTokenAsync(Guid externalCompanyId)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TrackingDbContext>();

        var company = await context.ExternalCompanies.FindAsync(externalCompanyId);
        if (company != null)
        {
            company.ApiToken = null;
            company.TokenExpiresAt = null;
            company.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }
    }
}
