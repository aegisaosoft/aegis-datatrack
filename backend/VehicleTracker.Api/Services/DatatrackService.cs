using System.Net.Http.Headers;
using System.Text.Json;
using VehicleTracker.Api.Models;

namespace VehicleTracker.Api.Services;

public interface IDatatrackService
{
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
    private readonly HttpClient _httpClient;
    private readonly ILogger<DatatrackService> _logger;
    private readonly string _baseUrl;
    private readonly string _apiKey;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DatatrackService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<DatatrackService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        _baseUrl = configuration["Datatrack:BaseUrl"] 
            ?? throw new InvalidOperationException("Datatrack:BaseUrl not configured");
        _apiKey = configuration["Datatrack:ApiKey"] 
            ?? throw new InvalidOperationException("Datatrack:ApiKey not configured");
        
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("ApiKey", _apiKey);
    }

    public async Task<List<VehicleStatus>> GetAllVehicleStatusesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/getStatuses");
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
            var response = await _httpClient.GetAsync($"{_baseUrl}/getStatus?serial={serial}");
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
            var url = $"{_baseUrl}/getLocations?serial={serial}&start={startSecs}&end={endSecs}";
            var response = await _httpClient.GetAsync(url);
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
            var response = await _httpClient.GetAsync($"{_baseUrl}/getVehicle?serial={serial}");
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
            var response = await _httpClient.GetAsync($"{_baseUrl}/getVehicles");
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
            var url = $"{_baseUrl}/setStarter?serial={serial}&disable={disable.ToString().ToLower()}";
            var response = await _httpClient.GetAsync(url);
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
            var url = $"{_baseUrl}/setBuzzer?serial={serial}&disable={disable.ToString().ToLower()}";
            var response = await _httpClient.GetAsync(url);
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
