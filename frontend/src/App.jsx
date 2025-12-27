import { useState, useEffect, useCallback } from 'react';
import { LoadScript } from '@react-google-maps/api';
import Map from './components/Map';
import VehicleList from './components/VehicleList';
import VehicleDetails from './components/VehicleDetails';
import MultiVehiclePanel from './components/MultiVehiclePanel';
import Header from './components/Header';
import CredentialsModal from './components/CredentialsModal';
import { vehicleApi, companyApi } from './services/api';

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
  const [companies, setCompanies] = useState([]);
  const [activeCompany, setActiveCompany] = useState(null);
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

  // Modal for tracker credentials
  const [showCredentialsModal, setShowCredentialsModal] = useState(false);
  const [pendingCompany, setPendingCompany] = useState(null);
  const [credentialsLoading, setCredentialsLoading] = useState(false);
  const [credentialsError, setCredentialsError] = useState(null);

  // Load companies on mount
  useEffect(() => {
    loadCompanies();
  }, []);

  const loadCompanies = async () => {
    try {
      const response = await fetch('/api/external/rental-companies-with-tracker');
      if (!response.ok) throw new Error('Failed to load companies');
      const data = await response.json();
      setCompanies(data);
      
      // Restore active company from localStorage
      const savedCompanyId = companyApi.getActiveCompanyId();
      if (savedCompanyId) {
        const company = data.find(c => c.externalCompanyId === savedCompanyId);
        if (company && company.tokenValid) {
          companyApi.setActiveCompany(company.externalCompanyId);
          setActiveCompany(company);
        }
      }
    } catch (err) {
      console.error('Error loading companies:', err);
    } finally {
      setIsLoading(false);
    }
  };

  const handleCompanySelect = async (company) => {
    // Case 1: No tracker configured - show modal to enter credentials
    if (!company.externalCompanyId) {
      setPendingCompany(company);
      setCredentialsError(null);
      setShowCredentialsModal(true);
      return;
    }
    
    // Case 2: Has tracker but no valid token - try to login
    if (!company.tokenValid) {
      try {
        setIsLoading(true);
        await companyApi.login(company.externalCompanyId);
        await loadCompanies();
        // Refetch to get updated token status
        const response = await fetch('/api/external/rental-companies-with-tracker');
        const data = await response.json();
        const updated = data.find(c => c.id === company.id);
        if (updated?.tokenValid) {
          companyApi.setActiveCompany(updated.externalCompanyId);
          setActiveCompany(updated);
          setVehicles([]);
          setError(null);
        } else {
          // Login failed - maybe wrong credentials, show modal to re-enter
          setPendingCompany(company);
          setCredentialsError(null);
          setShowCredentialsModal(true);
        }
      } catch (err) {
        // Show modal to enter/fix credentials
        setPendingCompany(company);
        setCredentialsError(null);
        setShowCredentialsModal(true);
      } finally {
        setIsLoading(false);
      }
      return;
    }
    
    // Case 3: Has valid token - just use it
    companyApi.setActiveCompany(company.externalCompanyId);
    setActiveCompany(company);
    setVehicles([]);
    setError(null);
  };

  const handleCredentialsSubmit = async (formData) => {
    if (!pendingCompany) return;

    try {
      setCredentialsLoading(true);
      setCredentialsError(null);
      
      // Setup tracker with simple username/password - backend handles login
      const setupRes = await fetch('/api/external/setup-tracker', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          rentalCompanyId: pendingCompany.id,
          apiUsername: formData.apiUsername,
          apiPassword: formData.apiPassword,
          accountId: formData.accountId || null  // Optional manual accountId
        })
      });

      if (!setupRes.ok) {
        const err = await setupRes.json();
        throw new Error(err.error || 'Failed to connect - check username and password');
      }

      const setupData = await setupRes.json();
      console.log('Tracker setup successful:', setupData);
      
      // Close modal and refresh companies
      setShowCredentialsModal(false);
      setPendingCompany(null);
      
      // Refresh companies list
      const response = await companyApi.getCompanies();
      if (response.ok) {
        const data = await response.json();
        setCompanies(data);
        
        // Find and select the connected company
        const connectedCompany = data.find(c => c.id === pendingCompany.id);
        if (connectedCompany && connectedCompany.externalCompanyId) {
          companyApi.setActiveCompany(connectedCompany.externalCompanyId);
          setActiveCompany(connectedCompany);
          setVehicles([]);
        }
      }
    } catch (err) {
      console.error('Credentials error:', err);
      setCredentialsError(err.message);
    } finally {
      setCredentialsLoading(false);
    }
  };

  const handleCredentialsCancel = () => {
    setShowCredentialsModal(false);
    setPendingCompany(null);
    setCredentialsError(null);
  };

  const handleLogout = () => {
    // Clear active company
    companyApi.clearActiveCompany();
    setActiveCompany(null);
    setVehicles([]);
    setSelectedVehicles([]);
    setFocusedVehicle(null);
    setVehicleHistories({});
    setLastUpdated(null);
    setError(null);
  };

  // Assign colors to selected vehicles
  const vehicleColors = selectedVehicles.reduce((acc, serial, index) => {
    acc[serial] = VEHICLE_COLORS[index % VEHICLE_COLORS.length];
    return acc;
  }, {});

  // Fetch all vehicle statuses
  const fetchVehicles = useCallback(async (showRefreshIndicator = false) => {
    if (!activeCompany) {
      setIsLoading(false);
      return;
    }
    
    try {
      if (showRefreshIndicator) setIsRefreshing(true);
      
      const statuses = await vehicleApi.getAllStatuses();
      setVehicles(statuses);
      setLastUpdated(new Date());
      setError(null);
    } catch (err) {
      console.error('Error fetching vehicles:', err);
      const errMsg = err.response?.data?.error || 'Failed to fetch vehicle data.';
      setError(errMsg);
    } finally {
      setIsLoading(false);
      setIsRefreshing(false);
    }
  }, [activeCompany]);

  // Fetch vehicles when company is selected
  useEffect(() => {
    if (activeCompany) {
      fetchVehicles();
      
      const interval = setInterval(() => {
        fetchVehicles(true);
      }, REFRESH_INTERVAL);

      return () => clearInterval(interval);
    }
  }, [activeCompany, fetchVehicles]);

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
      <div className="h-screen flex flex-col bg-gray-100">
        <nav className="bg-white shadow-sm border-b">
          <div className="max-w-7xl mx-auto px-4">
            <div className="flex items-center h-16">
              <h1 className="text-xl font-bold text-gray-900">Vehicle Tracker</h1>
            </div>
          </div>
        </nav>
        
        <div className="flex items-center justify-center flex-1">
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
      </div>
    );
  }

  return (
    <LoadScript googleMapsApiKey={GOOGLE_MAPS_API_KEY}>
      <div className="h-screen flex flex-col bg-gray-100">
        {/* Navigation */}
        <nav className="bg-white shadow-sm border-b">
          <div className="px-4">
            <div className="flex items-center justify-between h-14">
              <div className="flex items-center gap-6">
                <h1 className="text-lg font-bold text-gray-900">Vehicle Tracker</h1>
                
                {/* Company Selector */}
                <div className="flex items-center gap-2 ml-4 pl-4 border-l">
                  <label className="text-sm text-gray-600">Company:</label>
                  <select
                    value={activeCompany?.id || ''}
                    onChange={(e) => {
                      const company = companies.find(c => c.id === e.target.value);
                      if (company) handleCompanySelect(company);
                    }}
                    className="border rounded-lg px-3 py-1.5 text-sm focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                  >
                    <option value="">-- Select Company --</option>
                    {companies.map(company => (
                      <option key={company.id} value={company.id}>
                        {company.companyName} {company.externalCompanyId ? (company.tokenValid ? '✓' : '🔒') : ''}
                      </option>
                    ))}
                  </select>
                  {activeCompany && (
                    <span className={`text-xs px-2 py-1 rounded-full ${
                      activeCompany.tokenValid 
                        ? 'bg-green-100 text-green-700' 
                        : 'bg-yellow-100 text-yellow-700'
                    }`}>
                      {activeCompany.tokenValid ? 'Connected' : 'Connecting...'}
                    </span>
                  )}
                </div>
              </div>
              <Header 
                lastUpdated={lastUpdated}
                isRefreshing={isRefreshing}
                onRefresh={handleRefresh}
                vehicleCount={vehicles.length}
                selectedCount={selectedVehicles.length}
                trackingMode={trackingMode}
                onTrackingModeChange={setTrackingMode}
                onLogout={handleLogout}
                companyName={activeCompany?.companyName}
                compact={true}
              />
            </div>
          </div>
        </nav>
        
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

        {/* Credentials Modal */}
        {showCredentialsModal && (
          <CredentialsModal
            company={pendingCompany}
            onSubmit={handleCredentialsSubmit}
            onCancel={handleCredentialsCancel}
            isLoading={credentialsLoading}
            error={credentialsError}
          />
        )}

        {/* No company selected message */}
        {!activeCompany && (
          <div className="flex-1 flex items-center justify-center">
            <div className="bg-white p-8 rounded-lg shadow-lg text-center max-w-md">
              <h2 className="text-xl font-bold text-gray-800 mb-4">Select a Company</h2>
              <p className="text-gray-600 mb-4">
                Choose a company from the dropdown above to view vehicles.
              </p>
              {companies.length === 0 && (
                <p className="text-sm text-gray-500">
                  No companies found in database.
                </p>
              )}
            </div>
          </div>
        )}

        {activeCompany && (
        <div className="flex-1 flex overflow-hidden h-full">
          {/* Vehicle List Sidebar */}
          <div className="w-80 bg-white shadow-lg overflow-y-auto flex-shrink-0 h-full">
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
            <div className="w-96 bg-white shadow-lg overflow-y-auto flex-shrink-0 h-full">
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
        )}
      </div>
    </LoadScript>
  );
}

export default App;
