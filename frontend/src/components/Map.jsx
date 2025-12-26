import { useState, useCallback, useMemo } from 'react';
import { GoogleMap, Marker, InfoWindow, Polyline } from '@react-google-maps/api';
import { Navigation, Gauge, Battery, Clock } from 'lucide-react';

const mapContainerStyle = {
  width: '100%',
  height: '100%',
};

const defaultCenter = {
  lat: 39.8283,
  lng: -98.5795, // Center of US
};

const mapOptions = {
  disableDefaultUI: false,
  zoomControl: true,
  streetViewControl: false,
  mapTypeControl: true,
  fullscreenControl: true,
  styles: [
    {
      featureType: 'poi',
      elementType: 'labels',
      stylers: [{ visibility: 'off' }],
    },
  ],
};

// Vehicle icon based on status and selection
const getMarkerIcon = (vehicle, isSelected, isFocused, customColor) => {
  const isMoving = vehicle.typeId === 5;
  const isDisabled = vehicle.disabled > 0;
  
  let color;
  if (customColor && isSelected) {
    color = customColor;
  } else if (isDisabled) {
    color = '#ef4444'; // Red for disabled
  } else if (isMoving) {
    color = '#3b82f6'; // Blue for moving
  } else {
    color = '#22c55e'; // Green for stopped
  }
  
  return {
    path: 'M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z',
    fillColor: color,
    fillOpacity: 1,
    strokeColor: isFocused ? '#1e3a8a' : (isSelected ? '#ffffff' : '#374151'),
    strokeWeight: isFocused ? 3 : (isSelected ? 2 : 1),
    scale: isFocused ? 2.2 : (isSelected ? 1.8 : 1.3),
    anchor: { x: 12, y: 24 },
  };
};

function Map({ 
  vehicles, 
  selectedVehicles = [], 
  focusedVehicle, 
  onVehicleClick, 
  vehicleHistories = {}, 
  showHistories,
  vehicleColors = {}
}) {
  const [map, setMap] = useState(null);
  const [activeInfoWindow, setActiveInfoWindow] = useState(null);

  // Calculate map bounds based on selected vehicles or all vehicles
  const bounds = useMemo(() => {
    const vehiclesToBound = selectedVehicles.length > 0
      ? vehicles.filter(v => selectedVehicles.includes(v.serial))
      : vehicles;
    
    if (!vehiclesToBound.length) return null;
    
    const bounds = new window.google.maps.LatLngBounds();
    vehiclesToBound.forEach(vehicle => {
      if (vehicle.lat && vehicle.lng) {
        bounds.extend({ lat: vehicle.lat, lng: vehicle.lng });
      }
    });
    
    // Also include history paths in bounds
    if (showHistories) {
      Object.values(vehicleHistories).forEach(history => {
        history.forEach(point => {
          if (point.lat && point.lng) {
            bounds.extend({ lat: point.lat, lng: point.lng });
          }
        });
      });
    }
    
    return bounds;
  }, [vehicles, selectedVehicles, vehicleHistories, showHistories]);

  const onMapLoad = useCallback((map) => {
    setMap(map);
    if (bounds) {
      map.fitBounds(bounds);
    }
  }, [bounds]);

  // Center on focused vehicle
  useMemo(() => {
    if (map && focusedVehicle && focusedVehicle.lat && focusedVehicle.lng) {
      map.panTo({ lat: focusedVehicle.lat, lng: focusedVehicle.lng });
      if (!showHistories) {
        map.setZoom(15);
      }
    }
  }, [map, focusedVehicle, showHistories]);

  // Fit bounds when showing histories
  useMemo(() => {
    if (map && bounds && showHistories) {
      map.fitBounds(bounds);
    }
  }, [map, bounds, showHistories]);

  const formatTime = (unixTime) => {
    const date = new Date(unixTime * 1000);
    return date.toLocaleString();
  };

  const formatSpeed = (speedKmh) => {
    const mph = Math.round(speedKmh / 1.609);
    return `${mph} mph (${speedKmh} km/h)`;
  };

  return (
    <GoogleMap
      mapContainerStyle={mapContainerStyle}
      center={focusedVehicle ? { lat: focusedVehicle.lat, lng: focusedVehicle.lng } : defaultCenter}
      zoom={focusedVehicle ? 15 : 4}
      onLoad={onMapLoad}
      options={mapOptions}
    >
      {/* Vehicle Markers */}
      {vehicles.map((vehicle) => {
        const isSelected = selectedVehicles.includes(vehicle.serial);
        const isFocused = focusedVehicle?.serial === vehicle.serial;
        
        return vehicle.lat && vehicle.lng && (
          <Marker
            key={vehicle.serial}
            position={{ lat: vehicle.lat, lng: vehicle.lng }}
            icon={getMarkerIcon(vehicle, isSelected, isFocused, vehicleColors[vehicle.serial])}
            onClick={() => {
              onVehicleClick(vehicle);
              setActiveInfoWindow(vehicle.serial);
            }}
            title={vehicle.name || vehicle.serial}
            zIndex={isFocused ? 1000 : (isSelected ? 100 : 1)}
          />
        );
      })}

      {/* Info Window */}
      {activeInfoWindow && vehicles.find(v => v.serial === activeInfoWindow) && (
        <InfoWindow
          position={{
            lat: vehicles.find(v => v.serial === activeInfoWindow).lat,
            lng: vehicles.find(v => v.serial === activeInfoWindow).lng,
          }}
          onCloseClick={() => setActiveInfoWindow(null)}
        >
          <div className="p-2 min-w-[200px]">
            {(() => {
              const vehicle = vehicles.find(v => v.serial === activeInfoWindow);
              const color = vehicleColors[vehicle.serial];
              return (
                <>
                  <div className="flex items-center space-x-2 mb-2">
                    {color && (
                      <span 
                        className="w-3 h-3 rounded-full" 
                        style={{ backgroundColor: color }}
                      />
                    )}
                    <h3 className="font-bold text-lg text-gray-900">
                      {vehicle.name || vehicle.serial}
                    </h3>
                  </div>
                  <div className="space-y-1 text-sm">
                    <div className="flex items-center space-x-2">
                      <Gauge className="h-4 w-4 text-gray-500" />
                      <span>{formatSpeed(vehicle.speed)}</span>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Navigation className="h-4 w-4 text-gray-500" />
                      <span>{vehicle.typeId === 5 ? 'Moving' : 'Stopped'}</span>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Battery className="h-4 w-4 text-gray-500" />
                      <span>{(vehicle.volts / 1000).toFixed(1)}V</span>
                    </div>
                    <div className="flex items-center space-x-2">
                      <Clock className="h-4 w-4 text-gray-500" />
                      <span className="text-xs">{formatTime(vehicle.date)}</span>
                    </div>
                  </div>
                  {vehicle.disabled > 0 && (
                    <div className="mt-2 bg-red-100 text-red-700 px-2 py-1 rounded text-xs font-medium">
                      Starter Disabled
                    </div>
                  )}
                </>
              );
            })()}
          </div>
        </InfoWindow>
      )}

      {/* History Paths for Multiple Vehicles */}
      {showHistories && Object.entries(vehicleHistories).map(([serial, history]) => {
        if (history.length < 2) return null;
        
        const color = vehicleColors[serial] || '#3b82f6';
        
        return (
          <div key={`history-${serial}`}>
            {/* Path Line */}
            <Polyline
              path={history.map(point => ({ lat: point.lat, lng: point.lng }))}
              options={{
                strokeColor: color,
                strokeOpacity: 0.8,
                strokeWeight: 4,
              }}
            />
            {/* Start marker */}
            <Marker
              position={{ lat: history[0].lat, lng: history[0].lng }}
              icon={{
                path: window.google.maps.SymbolPath.CIRCLE,
                fillColor: color,
                fillOpacity: 1,
                strokeColor: '#ffffff',
                strokeWeight: 2,
                scale: 6,
              }}
              title={`Start - ${vehicles.find(v => v.serial === serial)?.name || serial}`}
            />
            {/* Direction arrows along path */}
            {history.length > 10 && history.filter((_, i) => i % Math.floor(history.length / 5) === 0).map((point, idx) => (
              <Marker
                key={`arrow-${serial}-${idx}`}
                position={{ lat: point.lat, lng: point.lng }}
                icon={{
                  path: window.google.maps.SymbolPath.FORWARD_CLOSED_ARROW,
                  fillColor: color,
                  fillOpacity: 0.8,
                  strokeColor: '#ffffff',
                  strokeWeight: 1,
                  scale: 3,
                  rotation: idx > 0 && history[idx - 1] 
                    ? Math.atan2(
                        point.lng - history[idx - 1].lng,
                        point.lat - history[idx - 1].lat
                      ) * 180 / Math.PI
                    : 0,
                }}
              />
            ))}
          </div>
        );
      })}
    </GoogleMap>
  );
}

export default Map;
