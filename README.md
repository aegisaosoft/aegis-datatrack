# Vehicle Tracker - Real-Time Fleet Monitoring

A full-stack application for tracking vehicles in real-time on Google Maps using the Datatrack 247 API with PostgreSQL database storage.

## Architecture

```
┌──────────────────┐                    ┌──────────────────┐
│   React App      │                    │  Datatrack 247   │
│   Port: 5173     │                    │  (GPS positions) │
└────────┬─────────┘                    └────────┬─────────┘
         │                                       │
         │    REST API                           │ Sync every 30s
         ▼                                       ▼
┌─────────────────────────────────────────────────────────────┐
│                    ASP.NET Core API                         │
│                       Port: 5000                            │
│         DatatrackService ◀──── DatatrackSyncService         │
└─────────────────────────────────────────────────────────────┘
                              │
                              │ Entity Framework
                              ▼
              ┌───────────────────────────────┐
              │     Azure PostgreSQL          │
              │     aegis_ao_rental           │
              │  ───────────────────────────  │
              │  vehicles, companies          │
              │  tracking_devices             │
              │  vehicle_locations            │
              │  vehicle_tracking_status      │
              │  vehicle_trips, events        │
              └───────────────────────────────┘
```

## Features

- 📍 **Real-time vehicle tracking** on Google Maps
- 🚗 **Multi-vehicle tracking** - Track up to 10 vehicles simultaneously with color coding
- 🔄 **Auto-refresh** every 10 seconds
- 📊 **Vehicle status** (moving/stopped/disabled)
- 🗺️ **Trip history** visualization with colored paths
- 🔐 **Vehicle controls** (starter disable, buzzer)
- 🔍 **Search and filter** vehicles
- 📱 **Responsive design**
- 💾 **Database storage** - Store tracking history in PostgreSQL
- 🔔 **Event tracking** - Log ignition, starter, and other events
- 📈 **Trip analytics** - Track individual trips with statistics

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- [PostgreSQL 14+](https://www.postgresql.org/download/)
- [Google Maps API Key](https://console.cloud.google.com/apis/credentials)
- Datatrack 247 API Key (from your provider)

## Quick Start

### 1. Clone and Setup

```bash
# Navigate to the project
cd vehicle-tracker

# Backend setup
cd backend/VehicleTracker.Api
dotnet restore
```

### 2. Setup Database

Run the tracking migration on your aegis_ao_rental database:

```bash
# Connect to Azure PostgreSQL
psql "host=aegis-ao-db.postgres.database.azure.com port=5432 dbname=aegis_ao_rental user=alex sslmode=require"

# Run migration
\i database/migrations/001_tracking_schema.sql
```

Or using psql directly:
```bash
PGPASSWORD='Kis@1963' psql -h aegis-ao-db.postgres.database.azure.com -U alex -d aegis_ao_rental -f database/migrations/001_tracking_schema.sql
```

### 3. Configure Backend

The app uses `Database` section in `appsettings.json`:

```json
{
  "Database": {
    "Host": "aegis-ao-db.postgres.database.azure.com",
    "Port": 5432,
    "Database": "aegis_ao_rental",
    "Username": "alex",
    "Password": "your_password",
    "Pooling": true,
    "MinPoolSize": 0,
    "MaxPoolSize": 100,
    "ConnectionLifetime": 0,
    "SSLMode": "Require"
  },
  "Datatrack": {
    "BaseUrl": "https://fleet77.com/fleet4",
    "ApiKey": "YOUR_PREFIX.YOUR_API_KEY",
    "SyncEnabled": true,
    "StatusSyncIntervalSeconds": 30,
    "LocationSyncIntervalMinutes": 5
  }
}
```

### 3. Configure Frontend

```bash
cd ../../frontend
cp .env.example .env
```

Edit `.env`:
```
VITE_GOOGLE_MAPS_API_KEY=your_google_maps_api_key
```

### 4. Install Frontend Dependencies

```bash
npm install
```

### 5. Run the Application

**Terminal 1 - Backend:**
```bash
cd backend/VehicleTracker.Api
dotnet run
```

**Terminal 2 - Frontend:**
```bash
cd frontend
npm run dev
```

### 6. Access the Application

- **Frontend:** http://localhost:5173
- **Backend API:** http://localhost:5000
- **Swagger UI:** http://localhost:5000/swagger

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/vehicles` | Get all vehicles |
| GET | `/api/vehicles/statuses` | Get all vehicle statuses with locations |
| GET | `/api/vehicles/statuses/{serial}` | Get single vehicle status |
| GET | `/api/vehicles/{serial}` | Get vehicle details |
| GET | `/api/vehicles/{serial}/locations` | Get location history |
| POST | `/api/vehicles/{serial}/starter` | Control starter |
| POST | `/api/vehicles/{serial}/buzzer` | Control buzzer |

### Location History Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| start | long | Unix timestamp (optional) |
| end | long | Unix timestamp (optional) |
| hoursBack | int | Hours of history (default: 24) |

## Project Structure

```
vehicle-tracker/
├── backend/
│   └── VehicleTracker.Api/
│       ├── Controllers/
│       │   ├── VehiclesController.cs       # Direct Datatrack API access
│       │   └── TrackingController.cs       # Database-backed tracking
│       ├── Data/
│       │   ├── Entities/
│       │   │   └── TrackingEntities.cs     # EF Core entities
│       │   └── TrackingDbContext.cs        # PostgreSQL context
│       ├── Models/
│       │   ├── Location.cs
│       │   ├── Vehicle.cs
│       │   └── VehicleStatus.cs
│       ├── Services/
│       │   ├── DatatrackService.cs         # Datatrack API client
│       │   ├── DatatrackSyncService.cs     # GPS sync service
│       │   └── TrackingRepository.cs       # Database repository
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── appsettings.Production.json
│       └── VehicleTracker.Api.csproj
│
├── database/
│   └── migrations/
│       └── 001_tracking_schema.sql         # Tracking tables
│
└── frontend/
    ├── src/
    │   ├── components/
    │   │   ├── Header.jsx
    │   │   ├── Map.jsx
    │   │   ├── MultiVehiclePanel.jsx
    │   │   ├── VehicleDetails.jsx
    │   │   └── VehicleList.jsx
    │   ├── services/
    │   │   └── api.js
    │   ├── App.jsx
    │   └── main.jsx
    ├── package.json
    └── vite.config.js
```

## Sync Intervals

| Sync Type | Interval | Configurable |
|-----------|----------|--------------|
| GPS Status | 30 seconds | `Datatrack.StatusSyncIntervalSeconds` |
| Location History | 5 minutes | `Datatrack.LocationSyncIntervalMinutes` |

## Database Schema

### Core Tables

| Table | Description |
|-------|-------------|
| `vehicles` | Your existing vehicles table |
| `tracking_devices` | GPS device to vehicle mappings |
| `vehicle_locations` | Historical location data |
| `vehicle_tracking_status` | Current status for each vehicle |
| `vehicle_trips` | Individual trips (start to stop) |
| `vehicle_events` | Significant events (ignition, alerts) |
| `geofences` | Geographic boundaries for alerts |
| `tracking_sync_log` | Sync operation logs |

### Key Views

| View | Description |
|------|-------------|
| `v_vehicle_tracking_summary` | All vehicles with current status |
| `v_recent_vehicle_events` | Last 24 hours of events |
| `v_vehicle_utilization` | Usage statistics per vehicle |

## Tracking API Endpoints

### Device Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/tracking/devices` | Get all mapped devices |
| GET | `/api/tracking/devices/{serial}` | Get device by serial |
| GET | `/api/tracking/devices/available` | Get unmapped Datatrack devices |
| POST | `/api/tracking/devices` | Map a device to a vehicle |
| DELETE | `/api/tracking/devices/{serial}` | Unmap a device |

### Stored Tracking Data

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/tracking/status` | Get all vehicle statuses (from DB) |
| GET | `/api/tracking/status/{vehicleId}` | Get vehicle status |
| GET | `/api/tracking/status/company/{companyId}` | Get statuses by company |
| GET | `/api/tracking/locations/{vehicleId}` | Get location history |
| GET | `/api/tracking/trips/{vehicleId}` | Get trip history |
| GET | `/api/tracking/trips/{vehicleId}/active` | Get current trip |
| GET | `/api/tracking/events/{vehicleId}` | Get recent events |
| GET | `/api/tracking/events/unacknowledged` | Get alerts needing attention |

### Map Device to Vehicle Example

```bash
curl -X POST http://localhost:5000/api/tracking/devices \
  -H "Content-Type: application/json" \
  -d '{"vehicleId": "uuid-of-your-vehicle", "serial": "datatrack-device-serial"}'
```

## Google Maps API Setup

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select existing
3. Enable the **Maps JavaScript API**
4. Create credentials (API Key)
5. Restrict the API key to your domains (recommended)

## Datatrack 247 API Notes

- **Rate Limit:** 3600 requests/hour per account
- **History Limit:** Maximum 1 month in the past
- **Location Limit:** Maximum 5000 locations per query
- **Time Format:** All times are Unix timestamps (seconds since 1970)

### Vehicle Status Types

| TypeId | Status |
|--------|--------|
| 2 | Ignition Off |
| 3 | Stopped Heartbeat |
| 4 | Ignition On |
| 5 | Moving Heartbeat |
| 24 | Starter Disabled |
| 25 | Starter Enabled |
| 26 | Stop |

## Development

### Backend Development

```bash
cd backend/VehicleTracker.Api
dotnet watch run  # Hot reload enabled
```

### Frontend Development

```bash
cd frontend
npm run dev  # Vite dev server with HMR
```

### Building for Production

**Backend:**
```bash
cd backend/VehicleTracker.Api
dotnet publish -c Release -o ./publish
```

**Frontend:**
```bash
cd frontend
npm run build  # Output in dist/
```

## Troubleshooting

### "Configuration Required" message
- Ensure `VITE_GOOGLE_MAPS_API_KEY` is set in `.env` file
- Restart the frontend dev server after changing `.env`

### "Failed to fetch vehicle data"
- Check backend is running on port 5000
- Verify Datatrack API key is correct in `appsettings.json`
- Check browser console for CORS errors

### Map not loading
- Verify Google Maps API key is valid
- Ensure Maps JavaScript API is enabled in Google Cloud Console
- Check for JavaScript errors in browser console

### No vehicles showing
- Verify your Datatrack account has vehicles
- Check the backend logs for API errors
- Test the API directly: `curl -H "ApiKey: prefix.key" https://fleet77.com/fleet4/getStatuses`

## License

Private - For internal use only.

## Support

For Datatrack 247 API support, refer to the official API documentation.
