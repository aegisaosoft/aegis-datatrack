using System.Net.Http.Headers;
using System.Text.Json;
using VehicleTracker.Api.Models;

namespace VehicleTracker.Api.Services;

public interface IDatatrackService
{
    /// <summary>
    /// Set the active external company to use for API calls
    /// </summary>
    void SetActiveCompany(Guid externalCompanyId);
    
    /// <summary>
    /// Get currently active external company ID
    /// </summary>
    Guid? GetActiveCompanyId();
    
    Task<List<VehicleStatus>> GetAllVehicleStatusesAsync();
    Task<VehicleStatus?> GetVehicleStatusAsync(string serial);
    Task<List<Location>> GetLocationsAsync(string serial, long startSecs, long endSecs);
    Task<Vehicle?> GetVehicleAsync(string serial);
    Task<List<Vehicle>> GetAllVehiclesAsync();
    Task<bool> SetStarterAsync(string serial, bool disable);
    Task<bool> SetBuzzerAsync(string serial, bool disable);
}

public class DatatrackService : IDatatrackService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IExternalAuthService _authService;
    private readonly ILogger<DatatrackService> _logger;
    
    private Guid? _activeCompanyId;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DatatrackService(
        IHttpClientFactory httpClientFactory,
        IExternalAuthService authService,
        ILogger<DatatrackService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _logger = logger;
    }

    public void SetActiveCompany(Guid externalCompanyId)
    {
        _activeCompanyId = externalCompanyId;
        _logger.LogInformation("Active company set to {CompanyId}", externalCompanyId);
    }

    public Guid? GetActiveCompanyId() => _activeCompanyId;

    private async Task<HttpResponseMessage> SendAsync(string endpoint)
    {
        if (!_activeCompanyId.HasValue)
        {
            throw new InvalidOperationException("No active company selected. Call SetActiveCompany first.");
        }

        var company = await _authService.GetCompanyWithValidTokenAsync(_activeCompanyId.Value);
        if (company == null)
        {
            throw new InvalidOperationException($"Company {_activeCompanyId} not found");
        }

        if (string.IsNullOrEmpty(company.ApiToken))
        {
            throw new InvalidOperationException($"No valid token for company {company.CompanyName}. Login required.");
        }

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        var baseUrl = company.ApiBaseUrl?.TrimEnd('/') ?? "https://fm.datatrack247.com/api";
        var url = $"{baseUrl}/{endpoint.TrimStart('/')}";
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", company.ApiToken);
        
        _logger.LogDebug("Sending request to {Url}", url);
        return await httpClient.SendAsync(request);
    }

    public async Task<List<VehicleStatus>> GetAllVehicleStatusesAsync()
    {
        try
        {
            var response = await SendAsync("getStatuses");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<VehicleStatusResponse>(content, JsonOptions);
            
            if (result?.Status != 200)
            {
                _logger.LogWarning("API returned status {Status}", result?.Status);
                return new List<VehicleStatus>();
            }
            
            return result.Data ?? new List<VehicleStatus>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all vehicle statuses");
            throw;
        }
    }

    public async Task<VehicleStatus?> GetVehicleStatusAsync(string serial)
    {
        try
        {
            var response = await SendAsync($"getStatus?serial={serial}");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SingleVehicleStatusResponse>(content, JsonOptions);
            
            if (result?.Status != 200)
            {
                _logger.LogWarning("API returned status {Status} for serial {Serial}", 
                    result?.Status, serial);
                return null;
            }
            
            return result.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vehicle status for {Serial}", serial);
            throw;
        }
    }

    public async Task<List<Location>> GetLocationsAsync(string serial, long startSecs, long endSecs)
    {
        try
        {
            var response = await SendAsync($"getLocations?serial={serial}&start={startSecs}&end={endSecs}");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LocationResponse>(content, JsonOptions);
            
            if (result?.Status != 200)
            {
                _logger.LogWarning("API returned status {Status} for locations", result?.Status);
                return new List<Location>();
            }
            
            return result.Data ?? new List<Location>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching locations for {Serial}", serial);
            throw;
        }
    }

    public async Task<Vehicle?> GetVehicleAsync(string serial)
    {
        try
        {
            var response = await SendAsync($"getVehicle?serial={serial}");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<VehicleResponse>(content, JsonOptions);
            
            if (result?.Status != 200)
            {
                _logger.LogWarning("API returned status {Status} for vehicle {Serial}", 
                    result?.Status, serial);
                return null;
            }
            
            return result.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching vehicle {Serial}", serial);
            throw;
        }
    }

    public async Task<List<Vehicle>> GetAllVehiclesAsync()
    {
        try
        {
            var response = await SendAsync("getVehicles");
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
            _logger.LogError(ex, "Error fetching all vehicles");
            throw;
        }
    }

    public async Task<bool> SetStarterAsync(string serial, bool disable)
    {
        try
        {
            var response = await SendAsync($"setStarter?serial={serial}&disable={disable.ToString().ToLower()}");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<Dictionary<string, int>>(content, JsonOptions);
            
            return result?.GetValueOrDefault("status") == 200;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting starter for {Serial}", serial);
            throw;
        }
    }

    public async Task<bool> SetBuzzerAsync(string serial, bool disable)
    {
        try
        {
            var response = await SendAsync($"setBuzzer?serial={serial}&disable={disable.ToString().ToLower()}");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<Dictionary<string, int>>(content, JsonOptions);
            
            return result?.GetValueOrDefault("status") == 200;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting buzzer for {Serial}", serial);
            throw;
        }
    }
}
