import { useState, useMemo } from 'react';
import { Search, Truck, MapPin, Gauge, AlertTriangle, CheckSquare, Square, CheckCircle } from 'lucide-react';

function VehicleList({ 
  vehicles, 
  selectedVehicles = [],
  focusedVehicle,
  onSelectVehicle, 
  onSelectAll,
  onClearSelection,
  isLoading,
  vehicleColors = {},
  trackingMode = 'multi'
}) {
  const [searchTerm, setSearchTerm] = useState('');
  const [filterStatus, setFilterStatus] = useState('all'); // all, moving, stopped, disabled

  // Filter vehicles based on search and status
  const filteredVehicles = useMemo(() => {
    return vehicles.filter(vehicle => {
      const matchesSearch = 
        vehicle.name?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        vehicle.serial?.toLowerCase().includes(searchTerm.toLowerCase()) ||
        vehicle.vin?.toLowerCase().includes(searchTerm.toLowerCase());

      const isMoving = vehicle.typeId === 5;
      const isDisabled = vehicle.disabled > 0;

      let matchesFilter = true;
      if (filterStatus === 'moving') matchesFilter = isMoving;
      if (filterStatus === 'stopped') matchesFilter = !isMoving && !isDisabled;
      if (filterStatus === 'disabled') matchesFilter = isDisabled;

      return matchesSearch && matchesFilter;
    });
  }, [vehicles, searchTerm, filterStatus]);

  // Count by status
  const statusCounts = useMemo(() => {
    const counts = { moving: 0, stopped: 0, disabled: 0 };
    vehicles.forEach(v => {
      if (v.disabled > 0) counts.disabled++;
      else if (v.typeId === 5) counts.moving++;
      else counts.stopped++;
    });
    return counts;
  }, [vehicles]);

  const getStatusIndicator = (vehicle) => {
    const color = vehicleColors[vehicle.serial];
    
    if (color && selectedVehicles.includes(vehicle.serial)) {
      return (
        <span 
          className="w-3 h-3 rounded-full" 
          style={{ backgroundColor: color }}
          title="Tracking"
        />
      );
    }
    
    if (vehicle.disabled > 0) {
      return <span className="w-3 h-3 rounded-full bg-red-500" title="Disabled" />;
    }
    if (vehicle.typeId === 5) {
      return <span className="w-3 h-3 rounded-full bg-blue-500 animate-pulse" title="Moving" />;
    }
    return <span className="w-3 h-3 rounded-full bg-green-500" title="Stopped" />;
  };

  const formatTime = (unixTime) => {
    const date = new Date(unixTime * 1000);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    return `${diffDays}d ago`;
  };

  const handleClick = (vehicle, event) => {
    const isMultiSelect = trackingMode === 'multi' && (event.ctrlKey || event.metaKey || event.shiftKey);
    onSelectVehicle(vehicle, isMultiSelect);
  };

  const handleCheckboxClick = (vehicle, event) => {
    event.stopPropagation();
    onSelectVehicle(vehicle, true);
  };

  if (isLoading) {
    return (
      <div className="p-4">
        <div className="animate-pulse space-y-4">
          <div className="h-10 bg-gray-200 rounded"></div>
          <div className="h-8 bg-gray-200 rounded w-3/4"></div>
          {[1, 2, 3, 4, 5].map(i => (
            <div key={i} className="h-20 bg-gray-200 rounded"></div>
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="flex flex-col h-full">
      {/* Search */}
      <div className="p-4 border-b border-gray-200">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-5 w-5 text-gray-400" />
          <input
            type="text"
            placeholder="Search vehicles..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-full pl-10 pr-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-transparent"
          />
        </div>
      </div>

      {/* Multi-Select Actions */}
      {trackingMode === 'multi' && (
        <div className="px-4 py-2 border-b border-gray-200 flex items-center justify-between bg-gray-50">
          <div className="flex items-center space-x-2">
            <button
              onClick={() => onSelectAll(filteredVehicles)}
              className="text-sm text-primary-600 hover:text-primary-800 font-medium"
            >
              Select All ({Math.min(filteredVehicles.length, 10)})
            </button>
            {selectedVehicles.length > 0 && (
              <>
                <span className="text-gray-300">|</span>
                <button
                  onClick={onClearSelection}
                  className="text-sm text-gray-600 hover:text-gray-800"
                >
                  Clear ({selectedVehicles.length})
                </button>
              </>
            )}
          </div>
          <span className="text-xs text-gray-500">
            Ctrl+Click to multi-select
          </span>
        </div>
      )}

      {/* Filter Tabs */}
      <div className="flex border-b border-gray-200">
        <button
          onClick={() => setFilterStatus('all')}
          className={`flex-1 py-2 text-sm font-medium ${
            filterStatus === 'all' 
              ? 'text-primary-600 border-b-2 border-primary-600' 
              : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          All ({vehicles.length})
        </button>
        <button
          onClick={() => setFilterStatus('moving')}
          className={`flex-1 py-2 text-sm font-medium ${
            filterStatus === 'moving' 
              ? 'text-blue-600 border-b-2 border-blue-600' 
              : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          Moving ({statusCounts.moving})
        </button>
        <button
          onClick={() => setFilterStatus('stopped')}
          className={`flex-1 py-2 text-sm font-medium ${
            filterStatus === 'stopped' 
              ? 'text-green-600 border-b-2 border-green-600' 
              : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          Stopped ({statusCounts.stopped})
        </button>
        <button
          onClick={() => setFilterStatus('disabled')}
          className={`flex-1 py-2 text-sm font-medium ${
            filterStatus === 'disabled' 
              ? 'text-red-600 border-b-2 border-red-600' 
              : 'text-gray-500 hover:text-gray-700'
          }`}
        >
          Disabled ({statusCounts.disabled})
        </button>
      </div>

      {/* Vehicle List */}
      <div className="flex-1 overflow-y-auto">
        {filteredVehicles.length === 0 ? (
          <div className="p-8 text-center text-gray-500">
            <Truck className="h-12 w-12 mx-auto mb-4 text-gray-300" />
            <p>No vehicles found</p>
            {searchTerm && (
              <button 
                onClick={() => setSearchTerm('')}
                className="mt-2 text-primary-600 hover:underline"
              >
                Clear search
              </button>
            )}
          </div>
        ) : (
          <ul className="divide-y divide-gray-200">
            {filteredVehicles.map(vehicle => {
              const isSelected = selectedVehicles.includes(vehicle.serial);
              const isFocused = focusedVehicle?.serial === vehicle.serial;
              const vehicleColor = vehicleColors[vehicle.serial];
              
              return (
                <li
                  key={vehicle.serial}
                  onClick={(e) => handleClick(vehicle, e)}
                  className={`
                    p-4 cursor-pointer transition-colors hover:bg-gray-50
                    ${isFocused ? 'bg-primary-50 border-l-4 border-primary-600' : ''}
                    ${isSelected && !isFocused ? 'bg-blue-50' : ''}
                  `}
                  style={isSelected && vehicleColor ? { borderLeftColor: vehicleColor, borderLeftWidth: '4px' } : {}}
                >
                  <div className="flex items-start space-x-3">
                    {/* Checkbox for multi-select mode */}
                    {trackingMode === 'multi' && (
                      <button
                        onClick={(e) => handleCheckboxClick(vehicle, e)}
                        className="flex-shrink-0 mt-0.5"
                      >
                        {isSelected ? (
                          <CheckSquare 
                            className="h-5 w-5" 
                            style={{ color: vehicleColor || '#3b82f6' }}
                          />
                        ) : (
                          <Square className="h-5 w-5 text-gray-400 hover:text-gray-600" />
                        )}
                      </button>
                    )}
                    
                    <div className="flex-shrink-0 mt-1">
                      {getStatusIndicator(vehicle)}
                    </div>
                    
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center justify-between">
                        <p className="text-sm font-medium text-gray-900 truncate">
                          {vehicle.name || vehicle.serial}
                        </p>
                        <span className="text-xs text-gray-500">
                          {formatTime(vehicle.date)}
                        </span>
                      </div>
                      <div className="mt-1 flex items-center space-x-4 text-xs text-gray-500">
                        <span className="flex items-center">
                          <Gauge className="h-3 w-3 mr-1" />
                          {Math.round(vehicle.speed / 1.609)} mph
                        </span>
                        <span className="flex items-center">
                          <MapPin className="h-3 w-3 mr-1" />
                          {vehicle.lat?.toFixed(4)}, {vehicle.lng?.toFixed(4)}
                        </span>
                      </div>
                      {vehicle.disabled > 0 && (
                        <div className="mt-1 flex items-center text-xs text-red-600">
                          <AlertTriangle className="h-3 w-3 mr-1" />
                          Starter Disabled
                        </div>
                      )}
                    </div>
                  </div>
                </li>
              );
            })}
          </ul>
        )}
      </div>
    </div>
  );
}

export default VehicleList;
