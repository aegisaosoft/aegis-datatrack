using VehicleTracker.Api.Data.Entities;

namespace VehicleTracker.Api.Services;

/// <summary>
/// Background service that syncs data from Datatrack API to the database
/// </summary>
public class DatatrackSyncService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatatrackSyncService> _logger;
    private readonly IConfiguration _configuration;
    
    private readonly TimeSpan _statusSyncInterval;
    private readonly TimeSpan _locationSyncInterval;
    private readonly bool _isEnabled;

    public DatatrackSyncService(
        IServiceProvider serviceProvider,
        ILogger<DatatrackSyncService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;

        _isEnabled = configuration.GetValue<bool>("Datatrack:SyncEnabled", true);
        _statusSyncInterval = TimeSpan.FromSeconds(configuration.GetValue<int>("Datatrack:StatusSyncIntervalSeconds", 30));
        _locationSyncInterval = TimeSpan.FromMinutes(configuration.GetValue<int>("Datatrack:LocationSyncIntervalMinutes", 5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_isEnabled)
        {
            _logger.LogInformation("Datatrack sync service is disabled");
            return;
        }

        _logger.LogInformation("Datatrack sync service started. Status interval: {StatusInterval}, Location interval: {LocationInterval}",
            _statusSyncInterval, _locationSyncInterval);

        // Run status sync more frequently
        var statusTask = SyncStatusesLoop(stoppingToken);
        
        // Run location sync less frequently
        var locationTask = SyncLocationsLoop(stoppingToken);

        await Task.WhenAll(statusTask, locationTask);
    }

    private async Task SyncStatusesLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncStatuses();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing vehicle statuses");
            }

            await Task.Delay(_statusSyncInterval, stoppingToken);
        }
    }

    private async Task SyncLocationsLoop(CancellationToken stoppingToken)
    {
        // Initial delay to stagger with status sync
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncLocations();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing vehicle locations");
            }

            await Task.Delay(_locationSyncInterval, stoppingToken);
        }
    }

    private async Task SyncStatuses()
    {
        using var scope = _serviceProvider.CreateScope();
        var datatrackService = scope.ServiceProvider.GetRequiredService<DatatrackService>();
        var repository = scope.ServiceProvider.GetRequiredService<ITrackingRepository>();

        var syncLog = await repository.StartSyncLogAsync("statuses");
        int fetched = 0, inserted = 0, updated = 0;

        try
        {
            // Get all statuses from Datatrack API
            var statuses = await datatrackService.GetAllVehicleStatusesAsync();
            fetched = statuses?.Count ?? 0;

            if (statuses == null || statuses.Count == 0)
            {
                _logger.LogWarning("No statuses received from Datatrack API");
                await repository.CompleteSyncLogAsync(syncLog.Id, fetched, inserted, updated);
                return;
            }

            foreach (var status in statuses)
            {
                try
                {
                    // Look up vehicle by device serial
                    var vehicleId = await repository.GetVehicleIdByDeviceSerialAsync(status.Serial);
                    
                    if (!vehicleId.HasValue)
                    {
                        // Device not mapped to a vehicle, skip
                        continue;
                    }

                    var deviceTimestamp = DateTimeOffset.FromUnixTimeSeconds(status.Date).UtcDateTime;

                    // Update tracking status
                    var trackingStatus = new VehicleTrackingStatus
                    {
                        VehicleId = vehicleId.Value,
                        DeviceSerial = status.Serial,
                        Latitude = (decimal)status.Lat,
                        Longitude = (decimal)status.Lng,
                        SpeedKmh = (short)status.Speed,
                        LocationTypeId = (short)status.TypeId,
                        VoltageMv = status.Volts,
                        OdometerMeters = status.Distance,
                        IsMoving = status.TypeId == 5,
                        IgnitionOn = status.TypeId == 4 || status.TypeId == 5,
                        StarterDisabled = status.Disabled > 0,
                        DeviceTimestamp = deviceTimestamp
                    };

                    await repository.UpsertTrackingStatusAsync(trackingStatus);
                    updated++;

                    // Check for significant events
                    await ProcessStatusEvents(repository, vehicleId.Value, status.Serial, status, deviceTimestamp);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing status for device {Serial}", status.Serial);
                }
            }

            await repository.CompleteSyncLogAsync(syncLog.Id, fetched, inserted, updated);
            _logger.LogInformation("Status sync completed: {Fetched} fetched, {Updated} updated", fetched, updated);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Status sync failed");
            await repository.FailSyncLogAsync(syncLog.Id, ex.Message);
        }
    }

    private async Task ProcessStatusEvents(
        ITrackingRepository repository,
        Guid vehicleId,
        string serial,
        Models.VehicleStatus status,
        DateTime deviceTimestamp)
    {
        // Get current status to detect changes
        var currentStatus = await repository.GetTrackingStatusAsync(vehicleId);

        if (currentStatus == null) return;

        // Detect starter disabled event
        if (status.Disabled > 0 && !currentStatus.StarterDisabled)
        {
            await repository.InsertEventAsync(new VehicleEvent
            {
                VehicleId = vehicleId,
                DeviceSerial = serial,
                EventType = "starter_disabled",
                EventCode = 24,
                Severity = "alert",
                Latitude = (decimal)status.Lat,
                Longitude = (decimal)status.Lng,
                EventTime = deviceTimestamp
            });
        }

        // Detect starter enabled event
        if (status.Disabled == 0 && currentStatus.StarterDisabled)
        {
            await repository.InsertEventAsync(new VehicleEvent
            {
                VehicleId = vehicleId,
                DeviceSerial = serial,
                EventType = "starter_enabled",
                EventCode = 25,
                Severity = "info",
                Latitude = (decimal)status.Lat,
                Longitude = (decimal)status.Lng,
                EventTime = deviceTimestamp
            });
        }

        // Detect ignition on (trip start)
        if ((status.TypeId == 4 || status.TypeId == 5) && !currentStatus.IgnitionOn)
        {
            // Check if there's an active trip
            var activeTrip = await repository.GetActiveTripAsync(vehicleId);
            if (activeTrip == null)
            {
                await repository.CreateTripAsync(new VehicleTrip
                {
                    VehicleId = vehicleId,
                    DeviceSerial = serial,
                    StartTime = deviceTimestamp,
                    StartLatitude = (decimal)status.Lat,
                    StartLongitude = (decimal)status.Lng,
                    StartOdometerMeters = status.Distance,
                    Status = "in_progress"
                });
            }

            await repository.InsertEventAsync(new VehicleEvent
            {
                VehicleId = vehicleId,
                DeviceSerial = serial,
                EventType = "ignition_on",
                EventCode = 4,
                Severity = "info",
                Latitude = (decimal)status.Lat,
                Longitude = (decimal)status.Lng,
                EventTime = deviceTimestamp
            });
        }

        // Detect ignition off (trip end)
        if (status.TypeId == 2 && currentStatus.IgnitionOn)
        {
            // Complete active trip
            var activeTrip = await repository.GetActiveTripAsync(vehicleId);
            if (activeTrip != null)
            {
                activeTrip.EndTime = deviceTimestamp;
                activeTrip.EndLatitude = (decimal)status.Lat;
                activeTrip.EndLongitude = (decimal)status.Lng;
                activeTrip.EndOdometerMeters = status.Distance;
                activeTrip.Status = "completed";
                
                if (activeTrip.StartOdometerMeters.HasValue && status.Distance > 0)
                {
                    activeTrip.DistanceMeters = (int)(status.Distance - activeTrip.StartOdometerMeters.Value);
                }

                await repository.UpdateTripAsync(activeTrip);
            }

            await repository.InsertEventAsync(new VehicleEvent
            {
                VehicleId = vehicleId,
                DeviceSerial = serial,
                EventType = "ignition_off",
                EventCode = 2,
                Severity = "info",
                Latitude = (decimal)status.Lat,
                Longitude = (decimal)status.Lng,
                EventTime = deviceTimestamp
            });
        }

        // Detect low battery
        if (status.Volts < 11500 && currentStatus.VoltageMv >= 11500) // Less than 11.5V
        {
            await repository.InsertEventAsync(new VehicleEvent
            {
                VehicleId = vehicleId,
                DeviceSerial = serial,
                EventType = "low_battery",
                Severity = "warning",
                Latitude = (decimal)status.Lat,
                Longitude = (decimal)status.Lng,
                EventData = $"{{\"voltage_mv\": {status.Volts}}}",
                EventTime = deviceTimestamp
            });
        }
    }

    private async Task SyncLocations()
    {
        using var scope = _serviceProvider.CreateScope();
        var datatrackService = scope.ServiceProvider.GetRequiredService<DatatrackService>();
        var repository = scope.ServiceProvider.GetRequiredService<ITrackingRepository>();

        var syncLog = await repository.StartSyncLogAsync("locations");
        int totalFetched = 0, totalInserted = 0;

        try
        {
            // Get all active devices
            var devices = await repository.GetActiveDevicesAsync();

            foreach (var device in devices)
            {
                try
                {
                    // Fetch locations for the last sync interval
                    var hoursBack = Math.Max(1, (int)(_locationSyncInterval.TotalHours * 2));
                    var locations = await datatrackService.GetLocationsAsync(device.Serial, hoursBack);

                    if (locations == null || locations.Count == 0) continue;

                    totalFetched += locations.Count;

                    // Get the latest location we have for this device
                    var latestStoredLocation = await repository.GetLocationsBySerialAsync(
                        device.Serial,
                        DateTime.UtcNow.AddHours(-hoursBack),
                        DateTime.UtcNow,
                        1);

                    var lastTimestamp = latestStoredLocation.FirstOrDefault()?.DeviceTimestamp ?? DateTime.MinValue;

                    // Filter to only new locations
                    var newLocations = locations
                        .Where(l => DateTimeOffset.FromUnixTimeSeconds(l.Date).UtcDateTime > lastTimestamp)
                        .Select(l => new VehicleLocation
                        {
                            VehicleId = device.VehicleId,
                            DeviceSerial = device.Serial,
                            Latitude = (decimal)l.Lat,
                            Longitude = (decimal)l.Lng,
                            SpeedKmh = (short)l.Speed,
                            OdometerMeters = l.Distance,
                            LocationTypeId = (short)l.TypeId,
                            GpsQuality = (short)l.GpsFlags,
                            IgnitionOn = l.TypeId == 4 || l.TypeId == 5,
                            StarterDisabled = false, // Not available in location data
                            DeviceTimestamp = DateTimeOffset.FromUnixTimeSeconds(l.Date).UtcDateTime
                        })
                        .ToList();

                    if (newLocations.Count > 0)
                    {
                        await repository.InsertLocationsAsync(newLocations);
                        totalInserted += newLocations.Count;

                        // Update device last communication time
                        device.LastCommunicationAt = DateTime.UtcNow;
                        await repository.CreateOrUpdateDeviceAsync(device);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing locations for device {Serial}", device.Serial);
                }
            }

            await repository.CompleteSyncLogAsync(syncLog.Id, totalFetched, totalInserted, 0);
            _logger.LogInformation("Location sync completed: {Fetched} fetched, {Inserted} inserted", totalFetched, totalInserted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Location sync failed");
            await repository.FailSyncLogAsync(syncLog.Id, ex.Message);
        }
    }
}
