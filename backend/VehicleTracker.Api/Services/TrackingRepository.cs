using Microsoft.EntityFrameworkCore;
using VehicleTracker.Api.Data;
using VehicleTracker.Api.Data.Entities;

namespace VehicleTracker.Api.Services;

public interface ITrackingRepository
{
    // Device management
    Task<TrackingDevice?> GetDeviceBySerialAsync(string serial);
    Task<TrackingDevice?> GetDeviceByVehicleIdAsync(Guid vehicleId);
    Task<List<TrackingDevice>> GetActiveDevicesAsync();
    Task<TrackingDevice> CreateOrUpdateDeviceAsync(TrackingDevice device);
    Task<Guid?> GetVehicleIdByDeviceSerialAsync(string serial);

    // Location tracking
    Task InsertLocationAsync(VehicleLocation location);
    Task InsertLocationsAsync(IEnumerable<VehicleLocation> locations);
    Task<List<VehicleLocation>> GetLocationsAsync(Guid vehicleId, DateTime start, DateTime end, int limit = 5000);
    Task<List<VehicleLocation>> GetLocationsBySerialAsync(string serial, DateTime start, DateTime end, int limit = 5000);

    // Current status
    Task UpsertTrackingStatusAsync(VehicleTrackingStatus status);
    Task<VehicleTrackingStatus?> GetTrackingStatusAsync(Guid vehicleId);
    Task<List<VehicleTrackingStatus>> GetAllTrackingStatusesAsync();
    Task<List<VehicleTrackingStatus>> GetTrackingStatusesByCompanyAsync(Guid companyId);

    // Trips
    Task<VehicleTrip> CreateTripAsync(VehicleTrip trip);
    Task<VehicleTrip?> GetActiveTripAsync(Guid vehicleId);
    Task UpdateTripAsync(VehicleTrip trip);
    Task<List<VehicleTrip>> GetTripsAsync(Guid vehicleId, DateTime start, DateTime end);

    // Events
    Task InsertEventAsync(VehicleEvent vehicleEvent);
    Task<List<VehicleEvent>> GetRecentEventsAsync(Guid vehicleId, int hours = 24);
    Task<List<VehicleEvent>> GetUnacknowledgedEventsAsync(Guid? companyId = null);

    // Sync log
    Task<TrackingSyncLog> StartSyncLogAsync(string syncType);
    Task CompleteSyncLogAsync(Guid logId, int fetched, int inserted, int updated);
    Task FailSyncLogAsync(Guid logId, string errorMessage);
}

public class TrackingRepository : ITrackingRepository
{
    private readonly TrackingDbContext _context;
    private readonly ILogger<TrackingRepository> _logger;

    public TrackingRepository(TrackingDbContext context, ILogger<TrackingRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Device Management

    public async Task<TrackingDevice?> GetDeviceBySerialAsync(string serial)
    {
        return await _context.TrackingDevices
            .Include(d => d.Vehicle)
            .FirstOrDefaultAsync(d => d.Serial == serial);
    }

    public async Task<TrackingDevice?> GetDeviceByVehicleIdAsync(Guid vehicleId)
    {
        return await _context.TrackingDevices
            .FirstOrDefaultAsync(d => d.VehicleId == vehicleId && d.IsActive);
    }

    public async Task<List<TrackingDevice>> GetActiveDevicesAsync()
    {
        return await _context.TrackingDevices
            .Include(d => d.Vehicle)
            .Where(d => d.IsActive)
            .ToListAsync();
    }

    public async Task<TrackingDevice> CreateOrUpdateDeviceAsync(TrackingDevice device)
    {
        var existing = await _context.TrackingDevices
            .FirstOrDefaultAsync(d => d.Serial == device.Serial);

        if (existing == null)
        {
            device.Id = Guid.NewGuid();
            device.CreatedAt = DateTime.UtcNow;
            device.UpdatedAt = DateTime.UtcNow;
            _context.TrackingDevices.Add(device);
        }
        else
        {
            existing.DeviceName = device.DeviceName;
            existing.Imei = device.Imei;
            existing.SimNumber = device.SimNumber;
            existing.FirmwareVersion = device.FirmwareVersion;
            existing.LastCommunicationAt = device.LastCommunicationAt;
            existing.UpdatedAt = DateTime.UtcNow;
            device = existing;
        }

        await _context.SaveChangesAsync();
        return device;
    }

    public async Task<Guid?> GetVehicleIdByDeviceSerialAsync(string serial)
    {
        var device = await _context.TrackingDevices
            .Where(d => d.Serial == serial && d.IsActive)
            .Select(d => new { d.VehicleId })
            .FirstOrDefaultAsync();
        
        return device?.VehicleId;
    }

    #endregion

    #region Location Tracking

    public async Task InsertLocationAsync(VehicleLocation location)
    {
        location.Id = Guid.NewGuid();
        location.ReceivedAt = DateTime.UtcNow;
        _context.VehicleLocations.Add(location);
        await _context.SaveChangesAsync();
    }

    public async Task InsertLocationsAsync(IEnumerable<VehicleLocation> locations)
    {
        foreach (var location in locations)
        {
            location.Id = Guid.NewGuid();
            location.ReceivedAt = DateTime.UtcNow;
        }
        
        _context.VehicleLocations.AddRange(locations);
        await _context.SaveChangesAsync();
    }

    public async Task<List<VehicleLocation>> GetLocationsAsync(Guid vehicleId, DateTime start, DateTime end, int limit = 5000)
    {
        return await _context.VehicleLocations
            .Where(l => l.VehicleId == vehicleId && l.DeviceTimestamp >= start && l.DeviceTimestamp <= end)
            .OrderBy(l => l.DeviceTimestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<VehicleLocation>> GetLocationsBySerialAsync(string serial, DateTime start, DateTime end, int limit = 5000)
    {
        return await _context.VehicleLocations
            .Where(l => l.DeviceSerial == serial && l.DeviceTimestamp >= start && l.DeviceTimestamp <= end)
            .OrderBy(l => l.DeviceTimestamp)
            .Take(limit)
            .ToListAsync();
    }

    #endregion

    #region Current Status

    public async Task UpsertTrackingStatusAsync(VehicleTrackingStatus status)
    {
        var existing = await _context.VehicleTrackingStatuses
            .FirstOrDefaultAsync(s => s.VehicleId == status.VehicleId);

        if (existing == null)
        {
            status.Id = Guid.NewGuid();
            status.LastUpdated = DateTime.UtcNow;
            _context.VehicleTrackingStatuses.Add(status);
        }
        else
        {
            // Only update if the new data is more recent
            if (status.DeviceTimestamp > existing.DeviceTimestamp)
            {
                existing.DeviceSerial = status.DeviceSerial;
                existing.Latitude = status.Latitude;
                existing.Longitude = status.Longitude;
                existing.Address = status.Address;
                existing.SpeedKmh = status.SpeedKmh;
                existing.Heading = status.Heading;
                existing.LocationTypeId = status.LocationTypeId;
                existing.VoltageMv = status.VoltageMv;
                existing.OdometerMeters = status.OdometerMeters;
                existing.IsMoving = status.IsMoving;
                existing.IgnitionOn = status.IgnitionOn;
                existing.StarterDisabled = status.StarterDisabled;
                existing.DeviceTimestamp = status.DeviceTimestamp;
                existing.LastUpdated = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task<VehicleTrackingStatus?> GetTrackingStatusAsync(Guid vehicleId)
    {
        return await _context.VehicleTrackingStatuses
            .Include(s => s.Vehicle)
            .FirstOrDefaultAsync(s => s.VehicleId == vehicleId);
    }

    public async Task<List<VehicleTrackingStatus>> GetAllTrackingStatusesAsync()
    {
        return await _context.VehicleTrackingStatuses
            .Include(s => s.Vehicle)
            .ToListAsync();
    }

    public async Task<List<VehicleTrackingStatus>> GetTrackingStatusesByCompanyAsync(Guid companyId)
    {
        return await _context.VehicleTrackingStatuses
            .Include(s => s.Vehicle)
            .Where(s => s.Vehicle != null && s.Vehicle.CompanyId == companyId)
            .ToListAsync();
    }

    #endregion

    #region Trips

    public async Task<VehicleTrip> CreateTripAsync(VehicleTrip trip)
    {
        trip.Id = Guid.NewGuid();
        trip.CreatedAt = DateTime.UtcNow;
        trip.UpdatedAt = DateTime.UtcNow;
        _context.VehicleTrips.Add(trip);
        await _context.SaveChangesAsync();
        return trip;
    }

    public async Task<VehicleTrip?> GetActiveTripAsync(Guid vehicleId)
    {
        return await _context.VehicleTrips
            .FirstOrDefaultAsync(t => t.VehicleId == vehicleId && t.Status == "in_progress");
    }

    public async Task UpdateTripAsync(VehicleTrip trip)
    {
        trip.UpdatedAt = DateTime.UtcNow;
        _context.VehicleTrips.Update(trip);
        await _context.SaveChangesAsync();
    }

    public async Task<List<VehicleTrip>> GetTripsAsync(Guid vehicleId, DateTime start, DateTime end)
    {
        return await _context.VehicleTrips
            .Where(t => t.VehicleId == vehicleId && t.StartTime >= start && t.StartTime <= end)
            .OrderByDescending(t => t.StartTime)
            .ToListAsync();
    }

    #endregion

    #region Events

    public async Task InsertEventAsync(VehicleEvent vehicleEvent)
    {
        vehicleEvent.Id = Guid.NewGuid();
        vehicleEvent.ReceivedAt = DateTime.UtcNow;
        _context.VehicleEvents.Add(vehicleEvent);
        await _context.SaveChangesAsync();
    }

    public async Task<List<VehicleEvent>> GetRecentEventsAsync(Guid vehicleId, int hours = 24)
    {
        var cutoff = DateTime.UtcNow.AddHours(-hours);
        return await _context.VehicleEvents
            .Where(e => e.VehicleId == vehicleId && e.EventTime >= cutoff)
            .OrderByDescending(e => e.EventTime)
            .ToListAsync();
    }

    public async Task<List<VehicleEvent>> GetUnacknowledgedEventsAsync(Guid? companyId = null)
    {
        var query = _context.VehicleEvents
            .Where(e => e.AcknowledgedAt == null);

        if (companyId.HasValue)
        {
            var vehicleIds = await _context.Vehicles
                .Where(v => v.CompanyId == companyId.Value)
                .Select(v => v.Id)
                .ToListAsync();
            
            query = query.Where(e => vehicleIds.Contains(e.VehicleId));
        }

        return await query.OrderByDescending(e => e.EventTime).ToListAsync();
    }

    #endregion

    #region Sync Log

    public async Task<TrackingSyncLog> StartSyncLogAsync(string syncType)
    {
        var log = new TrackingSyncLog
        {
            Id = Guid.NewGuid(),
            SyncType = syncType,
            StartedAt = DateTime.UtcNow,
            Status = "running"
        };
        _context.TrackingSyncLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task CompleteSyncLogAsync(Guid logId, int fetched, int inserted, int updated)
    {
        var log = await _context.TrackingSyncLogs.FindAsync(logId);
        if (log != null)
        {
            log.CompletedAt = DateTime.UtcNow;
            log.RecordsFetched = fetched;
            log.RecordsInserted = inserted;
            log.RecordsUpdated = updated;
            log.Status = "completed";
            await _context.SaveChangesAsync();
        }
    }

    public async Task FailSyncLogAsync(Guid logId, string errorMessage)
    {
        var log = await _context.TrackingSyncLogs.FindAsync(logId);
        if (log != null)
        {
            log.CompletedAt = DateTime.UtcNow;
            log.Status = "failed";
            log.ErrorMessage = errorMessage;
            await _context.SaveChangesAsync();
        }
    }

    #endregion
}
