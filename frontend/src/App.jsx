import { useState, useEffect, useCallback } from 'react';
import { LoadScript } from '@react-google-maps/api';
import Map from './components/Map';
import VehicleList from './components/VehicleList';
import VehicleDetails from './components/VehicleDetails';
import MultiVehiclePanel from './components/MultiVehiclePanel';
import Header from './components/Header';
import { vehicleApi } from './services/api';

const GOOGLE_MAPS_API_KEY = import.meta.env.VITE_GOOGLE_MAPS_API_KEY || '';
const REFRESH_INTERVAL = 10000; // 10 seconds for real-time tracking

// Color palette for multiple vehicles
const VEHICLE_COLORS = [
  '#3b82f6', // blue
  '#ef4444', // red
  '#22c55e', // green
  '#f59e0b', // amber
  '#8b5cf6', // violet
  '#ec4899', // pink
  '#06b6d4', // cyan
  '#f97316', // orange
  '#84cc16', // lime
  '#6366f1', // indigo
];

function App() {
  const [vehicles, setVehicles] = useState([]);
  const [selectedVehicles, setSelectedVehicles] = useState([]); // Array of selected vehicle serials
  const [focusedVehicle, setFocusedVehicle] = useState(null); // Single vehicle for details panel
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState(null);
  const [lastUpdated, setLastUpdated] = useState(null);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [vehicleHistories, setVehicleHistories] = useState({}); // { serial: [locations] }
  const [historyHours, setHistoryHours] = useState(24);
  const [showHistories, setShowHistories] = useState(false);
  const [trackingMode, setTrackingMode] = useState('multi'); // 'single' or 'multi'

  // Assign colors to selected vehicles
  const vehicleColors = selectedVehicles.reduce((acc, serial, index) => {
    acc[serial] = VEHICLE_COLORS[index % VEHICLE_COLORS.length];
    return acc;
  }, {});

  // Fetch all vehicle statuses
  const fetchVehicles = useCallback(async (showRefreshIndicator = false) => {
    try {
      if (showRefreshIndicator) setIsRefreshing(true);
      
      const statuses = await vehicleApi.getAllStatuses();
      setVehicles(statuses);
      setLastUpdated(new Date());
      setError(null);
    } catch (err) {
      console.error('Error fetching vehicles:', err);
      setError('Failed to fetch vehicle data. Please try again.');
    } finally {
      setIsLoading(false);
      setIsRefreshing(false);
    }
  }, []);

  // Initial load and auto-refresh
  useEffect(() => {
    fetchVehicles();
    
    const interval = setInterval(() => {
      fetchVehicles(true);
    }, REFRESH_INTERVAL);

    return () => clearInterval(interval);
  }, [fetchVehicles]);

  // Fetch history for a single vehicle
  const fetchVehicleHistory = useCallback(async (serial, hoursBack = 24) => {
    try {
      const locations = await vehicleApi.getLocations(serial, { hoursBack });
      const path = locations.map(loc => ({
        lat: loc.lat,
        lng: loc.lng,
        date: loc.date,
        speed: loc.speed,
        typeId: loc.typeId,
      }));
      return path;
    } catch (err) {
      console.error('Error fetching history for', serial, err);
      return [];
    }
  }, []);

  // Fetch histories for all selected vehicles
  const fetchAllHistories = useCallback(async (hoursBack = 24) => {
    if (selectedVehicles.length === 0) return;
    
    setIsRefreshing(true);
    const histories = {};
    
    await Promise.all(
      selectedVehicles.map(async (serial) => {
        const path = await fetchVehicleHistory(serial, hoursBack);
        histories[serial] = path;
      })
    );
    
    setVehicleHistories(histories);
    setShowHistories(true);
    setIsRefreshing(false);
  }, [selectedVehicles, fetchVehicleHistory]);

  // Handle vehicle selection (multi-select with Ctrl/Cmd)
  const handleVehicleSelect = (vehicle, isMultiSelect = false) => {
    if (trackingMode === 'single' || !isMultiSelect) {
      // Single select mode
      setSelectedVehicles([vehicle.serial]);
      setFocusedVehicle(vehicle);
      setVehicleHistories({});
      setShowHistories(false);
    } else {
      // Multi-select mode
      setSelectedVehicles(prev => {
        const isAlreadySelected = prev.includes(vehicle.serial);
        if (isAlreadySelected) {
          // Deselect
          const newSelection = prev.filter(s => s !== vehicle.serial);
          if (focusedVehicle?.serial === vehicle.serial) {
            setFocusedVehicle(newSelection.length > 0 
              ? vehicles.find(v => v.serial === newSelection[0]) 
              : null
            );
          }
          // Remove history for deselected vehicle
          setVehicleHistories(h => {
            const { [vehicle.serial]: _, ...rest } = h;
            return rest;
          });
          return newSelection;
        } else {
          // Add to selection (max 10)
          if (prev.length >= 10) {
            setError('Maximum 10 vehicles can be tracked simultaneously');
            return prev;
          }
          return [...prev, vehicle.serial];
        }
      });
      setFocusedVehicle(vehicle);
    }
  };

  // Select all visible vehicles
  const handleSelectAll = (vehicleList) => {
    const serials = vehicleList.slice(0, 10).map(v => v.serial);
    setSelectedVehicles(serials);
    if (vehicleList.length > 0) {
      setFocusedVehicle(vehicleList[0]);
    }
  };

  // Clear all selections
  const handleClearSelection = () => {
    setSelectedVehicles([]);
    setFocusedVehicle(null);
    setVehicleHistories({});
    setShowHistories(false);
  };

  // Handle vehicle control actions
  const handleStarterControl = async (serial, disable) => {
    try {
      await vehicleApi.setStarter(serial, disable);
      setTimeout(() => fetchVehicles(true), 2000);
    } catch (err) {
      console.error('Error controlling starter:', err);
      setError('Failed to control starter. Please try again.');
    }
  };

  const handleBuzzerControl = async (serial, disable) => {
    try {
      await vehicleApi.setBuzzer(serial, disable);
      setTimeout(() => fetchVehicles(true), 2000);
    } catch (err) {
      console.error('Error controlling buzzer:', err);
      setError('Failed to control buzzer. Please try again.');
    }
  };

  // Manual refresh
  const handleRefresh = () => {
    fetchVehicles(true);
    if (showHistories && selectedVehicles.length > 0) {
      fetchAllHistories(historyHours);
    }
  };

  // Get selected vehicle objects
  const selectedVehicleObjects = vehicles.filter(v => 
    selectedVehicles.includes(v.serial)
  );

  if (!GOOGLE_MAPS_API_KEY) {
    return (
      <div className="flex items-center justify-center h-screen bg-gray-100">
        <div className="bg-white p-8 rounded-lg shadow-lg text-center max-w-md">
          <h1 className="text-2xl font-bold text-red-600 mb-4">Configuration Required</h1>
          <p className="text-gray-600 mb-4">
            Please set the <code className="bg-gray-100 px-2 py-1 rounded">VITE_GOOGLE_MAPS_API_KEY</code> 
            environment variable with your Google Maps API key.
          </p>
          <p className="text-sm text-gray-500">
            Create a <code className="bg-gray-100 px-1 rounded">.env</code> file in the frontend directory with:
          </p>
          <pre className="mt-2 bg-gray-100 p-3 rounded text-left text-sm">
            VITE_GOOGLE_MAPS_API_KEY=your_api_key_here
          </pre>
        </div>
      </div>
    );
  }

  return (
    <LoadScript googleMapsApiKey={GOOGLE_MAPS_API_KEY}>
      <div className="h-screen flex flex-col bg-gray-100">
        <Header 
          lastUpdated={lastUpdated}
          isRefreshing={isRefreshing}
          onRefresh={handleRefresh}
          vehicleCount={vehicles.length}
          selectedCount={selectedVehicles.length}
          trackingMode={trackingMode}
          onTrackingModeChange={setTrackingMode}
        />
        
        {error && (
          <div className="bg-red-100 border-l-4 border-red-500 text-red-700 p-4 mx-4 mt-4 rounded">
            <p className="font-medium">Error</p>
            <p>{error}</p>
            <button 
              onClick={() => setError(null)}
              className="mt-2 text-sm underline hover:no-underline"
            >
              Dismiss
            </button>
          </div>
        )}

        <div className="flex-1 flex overflow-hidden">
          {/* Vehicle List Sidebar */}
          <div className="w-80 bg-white shadow-lg overflow-y-auto">
            <VehicleList 
              vehicles={vehicles}
              selectedVehicles={selectedVehicles}
              focusedVehicle={focusedVehicle}
              onSelectVehicle={handleVehicleSelect}
              onSelectAll={handleSelectAll}
              onClearSelection={handleClearSelection}
              isLoading={isLoading}
              vehicleColors={vehicleColors}
              trackingMode={trackingMode}
            />
          </div>

          {/* Map Area */}
          <div className="flex-1 relative">
            {isLoading ? (
              <div className="flex items-center justify-center h-full">
                <div className="text-center">
                  <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-primary-600 mx-auto"></div>
                  <p className="mt-4 text-gray-600">Loading vehicles...</p>
                </div>
              </div>
            ) : (
              <Map 
                vehicles={vehicles}
                selectedVehicles={selectedVehicles}
                focusedVehicle={focusedVehicle}
                onVehicleClick={(vehicle) => handleVehicleSelect(vehicle, false)}
                vehicleHistories={vehicleHistories}
                showHistories={showHistories}
                vehicleColors={vehicleColors}
              />
            )}
          </div>

          {/* Right Panel - Multi-Vehicle or Single Vehicle Details */}
          {selectedVehicles.length > 0 && (
            <div className="w-96 bg-white shadow-lg overflow-y-auto">
              {selectedVehicles.length > 1 ? (
                <MultiVehiclePanel
                  vehicles={selectedVehicleObjects}
                  focusedVehicle={focusedVehicle}
                  onFocusVehicle={setFocusedVehicle}
                  onRemoveVehicle={(serial) => handleVehicleSelect(
                    vehicles.find(v => v.serial === serial), 
                    true
                  )}
                  onClearAll={handleClearSelection}
                  vehicleColors={vehicleColors}
                  onShowHistories={() => fetchAllHistories(historyHours)}
                  onHideHistories={() => { setShowHistories(false); setVehicleHistories({}); }}
                  showHistories={showHistories}
                  historyHours={historyHours}
                  onHistoryHoursChange={setHistoryHours}
                  isLoading={isRefreshing}
                />
              ) : (
                <VehicleDetails 
                  vehicle={focusedVehicle || selectedVehicleObjects[0]}
                  onClose={handleClearSelection}
                  onShowHistory={(hoursBack) => {
                    setHistoryHours(hoursBack);
                    fetchAllHistories(hoursBack);
                  }}
                  onHideHistory={() => { setShowHistories(false); setVehicleHistories({}); }}
                  showHistory={showHistories}
                  onStarterControl={handleStarterControl}
                  onBuzzerControl={handleBuzzerControl}
                  vehicleColor={vehicleColors[focusedVehicle?.serial || selectedVehicles[0]]}
                />
              )}
            </div>
          )}
        </div>
      </div>
    </LoadScript>
  );
}

export default App;
