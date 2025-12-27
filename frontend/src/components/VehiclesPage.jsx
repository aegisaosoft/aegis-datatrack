import { useState, useEffect, useMemo } from 'react';
import { vehicleApi } from '../services/api';

// Color map for vehicle colors
const COLOR_MAP = {
  0: { name: 'Unknown', bg: 'bg-gray-100', text: 'text-gray-800' },
  1: { name: 'Red', bg: 'bg-red-100', text: 'text-red-800' },
  2: { name: 'Black', bg: 'bg-gray-800', text: 'text-white' },
  3: { name: 'White', bg: 'bg-gray-50', text: 'text-gray-800', border: 'border border-gray-300' },
  4: { name: 'Blue', bg: 'bg-blue-100', text: 'text-blue-800' },
  5: { name: 'Gray', bg: 'bg-gray-200', text: 'text-gray-800' },
  6: { name: 'Orange', bg: 'bg-orange-100', text: 'text-orange-800' },
  7: { name: 'Yellow', bg: 'bg-yellow-100', text: 'text-yellow-800' },
  8: { name: 'Green', bg: 'bg-green-100', text: 'text-green-800' },
  9: { name: 'Silver', bg: 'bg-slate-200', text: 'text-slate-800' },
  10: { name: 'Other', bg: 'bg-purple-100', text: 'text-purple-800' },
};

export default function VehiclesPage({ onSelectVehicle, vehicleStatuses = [], isLoading: parentLoading, onRefresh, selectedCompanyId }) {
  const [searchTerm, setSearchTerm] = useState('');
  const [sortField, setSortField] = useState('name');
  const [sortDirection, setSortDirection] = useState('asc');
  const [vehicleDetails, setVehicleDetails] = useState([]);
  const [detailsLoading, setDetailsLoading] = useState(false);
  const [detailsError, setDetailsError] = useState(null);
  const [syncing, setSyncing] = useState(false);
  const [syncResult, setSyncResult] = useState(null);

  // Fetch vehicle details (make, model, year, vin, plate, color) from API
  const fetchDetails = async () => {
    console.log('fetchDetails called, vehicleStatuses.length =', vehicleStatuses.length);
    if (vehicleStatuses.length === 0) {
      console.log('fetchDetails: skipping - no statuses yet');
      return;
    }
    
    try {
      setDetailsLoading(true);
      setDetailsError(null);
      console.log('fetchDetails: calling vehicleApi.getAllVehicles()...');
      const details = await vehicleApi.getAllVehicles();
      console.log('Vehicle details from API:', details);
      setVehicleDetails(details);
    } catch (err) {
      console.error('Error fetching vehicle details:', err);
      setDetailsError(err.message);
    } finally {
      setDetailsLoading(false);
    }
  };

  useEffect(() => {
    console.log('useEffect triggered, vehicleStatuses.length =', vehicleStatuses.length);
    fetchDetails();
  }, [vehicleStatuses.length]);

  // Handle refresh - reload both statuses and details
  const handleRefresh = async () => {
    if (onRefresh) {
      onRefresh();
    }
    await fetchDetails();
  };

  // Handle sync - save vehicles to database
  const handleSync = async () => {
    if (!selectedCompanyId) {
      alert('Please select a company first');
      return;
    }
    
    if (vehicleDetails.length === 0) {
      alert('No vehicles loaded to sync');
      return;
    }
    
    try {
      setSyncing(true);
      setSyncResult(null);
      
      const response = await vehicleApi.syncVehicles(selectedCompanyId, vehicleDetails);
      setSyncResult({
        success: true,
        message: response.message,
        created: response.created,
        updated: response.updated,
        skipped: response.skipped
      });
      
      // Refresh the list
      await fetchDetails();
    } catch (err) {
      console.error('Sync error:', err);
      setSyncResult({
        success: false,
        message: err.response?.data?.error || err.message || 'Sync failed'
      });
    } finally {
      setSyncing(false);
    }
  };

  // Merge statuses with vehicle details
  const vehicles = useMemo(() => {
    if (!vehicleStatuses || !Array.isArray(vehicleStatuses)) {
      return [];
    }
    
    return vehicleStatuses.map(status => {
      // Find matching vehicle details by serial
      const details = vehicleDetails.find(d => d.serial === status.serial) || {};
      
      return {
        serial: status.serial,
        name: details.name || status.name || status.serial,
        lat: status.lat,
        lng: status.lng,
        speed: status.speed,
        date: status.date,
        volts: status.volts,
        distance: status.distance,
        disabled: status.disabled,
        buzzer: status.buzzer,
        typeId: status.typeId,
        heading: status.heading,
        // Vehicle details from Datatrack247 API
        make: details.make || '',
        model: details.model || '',
        year: details.year || 0,
        vehicleColor: details.vehicleColor || 0,
        plate: details.plate || '',
        vin: details.vin || '',
        alternateName: details.alternateName || '',
        notes: details.notes || '',
        // Computed fields
        isOnline: status.date ? (Date.now() / 1000 - status.date) < 3600 : false,
        isMoving: status.speed > 0,
        isStopped: status.typeId === 3 || status.typeId === 26,
        isDisabled: status.disabled > 0,
      };
    });
  }, [vehicleStatuses, vehicleDetails]);

  const isLoading = parentLoading || detailsLoading;

  // Filter vehicles based on search term
  const filteredVehicles = vehicles.filter(v => {
    const term = searchTerm.toLowerCase();
    return (
      v.name?.toLowerCase().includes(term) ||
      v.make?.toLowerCase().includes(term) ||
      v.model?.toLowerCase().includes(term) ||
      v.plate?.toLowerCase().includes(term) ||
      v.vin?.toLowerCase().includes(term) ||
      v.serial?.toLowerCase().includes(term)
    );
  });

  // Sort vehicles
  const sortedVehicles = [...filteredVehicles].sort((a, b) => {
    let aVal, bVal;
    
    if (sortField === 'status') {
      aVal = a.isOnline ? 1 : 0;
      bVal = b.isOnline ? 1 : 0;
    } else if (sortField === 'speed') {
      aVal = a.speed || 0;
      bVal = b.speed || 0;
    } else if (sortField === 'date') {
      aVal = a.date || 0;
      bVal = b.date || 0;
    } else if (sortField === 'year' || sortField === 'vehicleColor') {
      aVal = Number(a[sortField]) || 0;
      bVal = Number(b[sortField]) || 0;
    } else {
      aVal = String(a[sortField] || '').toLowerCase();
      bVal = String(b[sortField] || '').toLowerCase();
    }
    
    if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
    if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
    return 0;
  });

  const handleSort = (field) => {
    if (sortField === field) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : 'asc');
    } else {
      setSortField(field);
      setSortDirection('asc');
    }
  };

  const SortIcon = ({ field }) => {
    if (sortField !== field) return <span className="text-gray-300 ml-1">↕</span>;
    return <span className="text-blue-600 ml-1">{sortDirection === 'asc' ? '↑' : '↓'}</span>;
  };

  const getColorBadge = (colorId) => {
    const color = COLOR_MAP[colorId] || COLOR_MAP[0];
    return (
      <span className={`px-2 py-1 rounded-full text-xs font-medium ${color.bg} ${color.text} ${color.border || ''}`}>
        {color.name}
      </span>
    );
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="text-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-blue-600 mx-auto"></div>
          <p className="mt-4 text-gray-600">Loading vehicles...</p>
        </div>
      </div>
    );
  }

  return (
    <div className="h-full flex flex-col bg-gray-50">
      {/* Header */}
      <div className="bg-white border-b px-6 py-4">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-xl font-semibold text-gray-900">Vehicles</h2>
            <p className="text-sm text-gray-500 mt-1">
              {filteredVehicles.length} of {vehicles.length} vehicles
              {detailsLoading && <span className="ml-2 text-blue-600">(loading details...)</span>}
              {detailsError && <span className="ml-2 text-amber-600">(details unavailable)</span>}
            </p>
          </div>
          
          {/* Search */}
          <div className="flex items-center gap-4">
            <div className="relative">
              <input
                type="text"
                placeholder="Search vehicles..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="w-64 pl-10 pr-4 py-2 border rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
              />
              <svg
                className="absolute left-3 top-1/2 -translate-y-1/2 h-5 w-5 text-gray-400"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
                />
              </svg>
            </div>
            
            <button
              onClick={handleRefresh}
              disabled={isLoading}
              className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 flex items-center gap-2"
            >
              <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              Refresh
            </button>
            
            <button
              onClick={handleSync}
              disabled={isLoading || syncing || !selectedCompanyId}
              className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 disabled:opacity-50 flex items-center gap-2"
              title={!selectedCompanyId ? 'Select a company first' : 'Sync vehicles to aegis-rental'}
            >
              <svg className={`h-4 w-4 ${syncing ? 'animate-spin' : ''}`} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              {syncing ? 'Syncing...' : 'Sync to Rental'}
            </button>
          </div>
        </div>
      </div>
      
      {/* Sync Result Notification */}
      {syncResult && (
        <div className={`mx-6 mt-4 p-4 rounded-lg ${syncResult.success ? 'bg-green-50 border border-green-200' : 'bg-red-50 border border-red-200'}`}>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              {syncResult.success ? (
                <svg className="h-5 w-5 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 13l4 4L19 7" />
                </svg>
              ) : (
                <svg className="h-5 w-5 text-red-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              )}
              <span className={syncResult.success ? 'text-green-800' : 'text-red-800'}>
                {syncResult.message}
              </span>
              {syncResult.success && (
                <span className="text-sm text-green-600 ml-2">
                  ({syncResult.created} created, {syncResult.updated} updated, {syncResult.skipped} skipped)
                </span>
              )}
            </div>
            <button 
              onClick={() => setSyncResult(null)}
              className="text-gray-400 hover:text-gray-600"
            >
              <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>
          </div>
        </div>
      )}
      
      {/* Table */}
      <div className="flex-1 overflow-auto p-6">
        <div className="bg-white rounded-lg shadow overflow-hidden">
          <table className="min-w-full divide-y divide-gray-200">
            <thead className="bg-gray-50">
              <tr>
                <th 
                  className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
                  onClick={() => handleSort('name')}
                >
                  <div className="flex items-center">
                    Name <SortIcon field="name" />
                  </div>
                </th>
                <th 
                  className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
                  onClick={() => handleSort('make')}
                >
                  <div className="flex items-center">
                    Make <SortIcon field="make" />
                  </div>
                </th>
                <th 
                  className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
                  onClick={() => handleSort('model')}
                >
                  <div className="flex items-center">
                    Model <SortIcon field="model" />
                  </div>
                </th>
                <th 
                  className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
                  onClick={() => handleSort('year')}
                >
                  <div className="flex items-center">
                    Year <SortIcon field="year" />
                  </div>
                </th>
                <th 
                  className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
                  onClick={() => handleSort('vehicleColor')}
                >
                  <div className="flex items-center">
                    Color <SortIcon field="vehicleColor" />
                  </div>
                </th>
                <th 
                  className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
                  onClick={() => handleSort('plate')}
                >
                  <div className="flex items-center">
                    License Plate <SortIcon field="plate" />
                  </div>
                </th>
                <th 
                  className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
                  onClick={() => handleSort('vin')}
                >
                  <div className="flex items-center">
                    VIN <SortIcon field="vin" />
                  </div>
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Status
                </th>
                <th className="px-4 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="bg-white divide-y divide-gray-200">
              {sortedVehicles.length === 0 ? (
                <tr>
                  <td colSpan={9} className="px-4 py-8 text-center text-gray-500">
                    {searchTerm ? 'No vehicles match your search' : 'No vehicles found'}
                  </td>
                </tr>
              ) : (
                sortedVehicles.map((vehicle) => (
                  <tr 
                    key={vehicle.serial} 
                    className="hover:bg-gray-50 cursor-pointer"
                    onClick={() => onSelectVehicle?.(vehicle)}
                  >
                    <td className="px-4 py-3 whitespace-nowrap">
                      <div className="flex items-center">
                        <div className="flex-shrink-0 h-10 w-10 bg-blue-100 rounded-full flex items-center justify-center">
                          <svg className="h-5 w-5 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 7h8m-8 4h8m-4 4v-4m-6 4h12a2 2 0 002-2V9a2 2 0 00-2-2H6a2 2 0 00-2 2v4a2 2 0 002 2z" />
                          </svg>
                        </div>
                        <div className="ml-3">
                          <div className="text-sm font-medium text-gray-900">
                            {vehicle.name || vehicle.serial}
                          </div>
                          {vehicle.alternateName && (
                            <div className="text-xs text-gray-500">{vehicle.alternateName}</div>
                          )}
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-900">
                      {vehicle.make || '-'}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-900">
                      {vehicle.model || '-'}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-900">
                      {vehicle.year || '-'}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap">
                      {getColorBadge(vehicle.vehicleColor)}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-900">
                      {vehicle.plate ? (
                        <span className="font-mono bg-gray-100 px-2 py-1 rounded">
                          {vehicle.plate}
                        </span>
                      ) : (
                        <span className="text-gray-400">-</span>
                      )}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-sm text-gray-500 font-mono">
                      {vehicle.vin ? (
                        <span title={vehicle.vin}>
                          {vehicle.vin.length > 10 ? `${vehicle.vin.slice(0, 10)}...` : vehicle.vin}
                        </span>
                      ) : (
                        <span className="text-gray-400">-</span>
                      )}
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap">
                      <span className={`inline-flex items-center px-2 py-1 rounded-full text-xs font-medium ${
                        vehicle.isOnline 
                          ? 'bg-green-100 text-green-800' 
                          : 'bg-gray-100 text-gray-800'
                      }`}>
                        <span className={`h-2 w-2 rounded-full mr-1.5 ${
                          vehicle.isOnline ? 'bg-green-500' : 'bg-gray-400'
                        }`}></span>
                        {vehicle.isOnline ? 'Online' : 'Offline'}
                      </span>
                    </td>
                    <td className="px-4 py-3 whitespace-nowrap text-sm">
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          onSelectVehicle?.(vehicle);
                        }}
                        className="text-blue-600 hover:text-blue-800 font-medium"
                      >
                        View on Map
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
      
      {/* Footer with stats */}
      <div className="bg-white border-t px-6 py-3">
        <div className="flex items-center justify-between text-sm text-gray-500">
          <div className="flex gap-4">
            <span>
              <strong className="text-green-600">
                {vehicles.filter(v => v.isOnline).length}
              </strong> online
            </span>
            <span>
              <strong className="text-gray-600">
                {vehicles.filter(v => !v.isOnline).length}
              </strong> offline
            </span>
          </div>
          <div>
            Serial column hidden for security. Click a row to view details.
          </div>
        </div>
      </div>
    </div>
  );
}
