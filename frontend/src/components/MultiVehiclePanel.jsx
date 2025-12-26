import { 
  X, 
  MapPin, 
  Gauge, 
  Battery, 
  Clock,
  Route,
  Trash2,
  Eye,
  EyeOff,
  Navigation,
  Loader2,
  AlertTriangle
} from 'lucide-react';

function MultiVehiclePanel({ 
  vehicles,
  focusedVehicle,
  onFocusVehicle,
  onRemoveVehicle,
  onClearAll,
  vehicleColors,
  onShowHistories,
  onHideHistories,
  showHistories,
  historyHours,
  onHistoryHoursChange,
  isLoading
}) {
  const formatTime = (unixTime) => {
    const date = new Date(unixTime * 1000);
    return date.toLocaleTimeString();
  };

  const formatSpeed = (kmh) => {
    return `${Math.round(kmh / 1.609)} mph`;
  };

  const getStatusText = (vehicle) => {
    if (vehicle.disabled > 0) return 'Disabled';
    if (vehicle.typeId === 5) return 'Moving';
    return 'Stopped';
  };

  const getStatusClass = (vehicle) => {
    if (vehicle.disabled > 0) return 'text-red-600 bg-red-50';
    if (vehicle.typeId === 5) return 'text-blue-600 bg-blue-50';
    return 'text-green-600 bg-green-50';
  };

  // Calculate aggregate stats
  const stats = {
    total: vehicles.length,
    moving: vehicles.filter(v => v.typeId === 5).length,
    stopped: vehicles.filter(v => v.typeId !== 5 && v.disabled === 0).length,
    disabled: vehicles.filter(v => v.disabled > 0).length,
    totalDistance: vehicles.reduce((sum, v) => sum + (v.distance || 0), 0),
    avgSpeed: vehicles.reduce((sum, v) => sum + (v.speed || 0), 0) / vehicles.length,
  };

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div className="p-4 border-b border-gray-200 bg-gradient-to-r from-primary-600 to-primary-700">
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-lg font-bold text-white">
              Tracking {vehicles.length} Vehicles
            </h2>
            <p className="text-sm text-primary-100">Multi-vehicle monitoring</p>
          </div>
          <button
            onClick={onClearAll}
            className="p-2 hover:bg-primary-500 rounded-full transition-colors"
            title="Clear all selections"
          >
            <X className="h-5 w-5 text-white" />
          </button>
        </div>
      </div>

      {/* Aggregate Stats */}
      <div className="p-4 border-b border-gray-200 bg-gray-50">
        <div className="grid grid-cols-3 gap-3">
          <div className="text-center p-2 bg-blue-100 rounded-lg">
            <div className="text-2xl font-bold text-blue-600">{stats.moving}</div>
            <div className="text-xs text-blue-700">Moving</div>
          </div>
          <div className="text-center p-2 bg-green-100 rounded-lg">
            <div className="text-2xl font-bold text-green-600">{stats.stopped}</div>
            <div className="text-xs text-green-700">Stopped</div>
          </div>
          <div className="text-center p-2 bg-red-100 rounded-lg">
            <div className="text-2xl font-bold text-red-600">{stats.disabled}</div>
            <div className="text-xs text-red-700">Disabled</div>
          </div>
        </div>
        <div className="mt-3 flex items-center justify-between text-sm text-gray-600">
          <span>Avg Speed: {formatSpeed(stats.avgSpeed)}</span>
          <span>Total: {(stats.totalDistance / 1609.34).toFixed(1)} mi</span>
        </div>
      </div>

      {/* History Controls */}
      <div className="p-4 border-b border-gray-200">
        <h3 className="text-sm font-semibold text-gray-700 uppercase tracking-wider mb-3">
          Trip History
        </h3>
        <div className="flex items-center space-x-3 mb-3">
          <label className="text-sm text-gray-600">Show last</label>
          <select
            value={historyHours}
            onChange={(e) => onHistoryHoursChange(Number(e.target.value))}
            className="border border-gray-300 rounded px-2 py-1 text-sm flex-1"
            disabled={showHistories}
          >
            <option value={1}>1 hour</option>
            <option value={6}>6 hours</option>
            <option value={12}>12 hours</option>
            <option value={24}>24 hours</option>
            <option value={48}>48 hours</option>
            <option value={168}>1 week</option>
          </select>
        </div>
        <button
          onClick={showHistories ? onHideHistories : onShowHistories}
          disabled={isLoading}
          className={`w-full py-2 px-4 rounded-lg font-medium transition-colors flex items-center justify-center space-x-2 ${
            showHistories
              ? 'bg-gray-200 text-gray-700 hover:bg-gray-300'
              : 'bg-primary-600 text-white hover:bg-primary-700'
          } disabled:opacity-50`}
        >
          {isLoading ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : showHistories ? (
            <EyeOff className="h-4 w-4" />
          ) : (
            <Eye className="h-4 w-4" />
          )}
          <span>
            {isLoading ? 'Loading...' : showHistories ? 'Hide All Histories' : 'Show All Histories'}
          </span>
        </button>
      </div>

      {/* Vehicle List */}
      <div className="flex-1 overflow-y-auto">
        <div className="p-2">
          <h3 className="text-sm font-semibold text-gray-700 uppercase tracking-wider px-2 py-2">
            Selected Vehicles
          </h3>
          <ul className="space-y-2">
            {vehicles.map(vehicle => {
              const isFocused = focusedVehicle?.serial === vehicle.serial;
              const color = vehicleColors[vehicle.serial];
              
              return (
                <li
                  key={vehicle.serial}
                  className={`
                    p-3 rounded-lg cursor-pointer transition-all
                    ${isFocused ? 'ring-2 ring-primary-500 bg-white shadow-md' : 'bg-gray-50 hover:bg-white hover:shadow-sm'}
                  `}
                  onClick={() => onFocusVehicle(vehicle)}
                >
                  <div className="flex items-start justify-between">
                    <div className="flex items-start space-x-3">
                      {/* Color indicator */}
                      <div 
                        className="w-4 h-4 rounded-full mt-1 flex-shrink-0"
                        style={{ backgroundColor: color }}
                      />
                      
                      <div className="min-w-0">
                        <div className="flex items-center space-x-2">
                          <p className="text-sm font-semibold text-gray-900 truncate">
                            {vehicle.name || vehicle.serial}
                          </p>
                          <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${getStatusClass(vehicle)}`}>
                            {getStatusText(vehicle)}
                          </span>
                        </div>
                        
                        <div className="mt-1 grid grid-cols-2 gap-x-4 gap-y-1 text-xs text-gray-500">
                          <div className="flex items-center">
                            <Gauge className="h-3 w-3 mr-1" />
                            {formatSpeed(vehicle.speed)}
                          </div>
                          <div className="flex items-center">
                            <Battery className="h-3 w-3 mr-1" />
                            {(vehicle.volts / 1000).toFixed(1)}V
                          </div>
                          <div className="flex items-center col-span-2">
                            <Clock className="h-3 w-3 mr-1" />
                            {formatTime(vehicle.date)}
                          </div>
                        </div>
                        
                        {vehicle.disabled > 0 && (
                          <div className="mt-1 flex items-center text-xs text-red-600">
                            <AlertTriangle className="h-3 w-3 mr-1" />
                            Starter Disabled
                          </div>
                        )}
                      </div>
                    </div>
                    
                    {/* Remove button */}
                    <button
                      onClick={(e) => {
                        e.stopPropagation();
                        onRemoveVehicle(vehicle.serial);
                      }}
                      className="p-1 text-gray-400 hover:text-red-500 hover:bg-red-50 rounded transition-colors"
                      title="Remove from tracking"
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </div>
                </li>
              );
            })}
          </ul>
        </div>
      </div>

      {/* Legend */}
      <div className="p-4 border-t border-gray-200 bg-gray-50">
        <h4 className="text-xs font-semibold text-gray-500 uppercase mb-2">Color Legend</h4>
        <div className="flex flex-wrap gap-2">
          {vehicles.map(vehicle => (
            <div
              key={vehicle.serial}
              className="flex items-center space-x-1 text-xs bg-white px-2 py-1 rounded-full shadow-sm"
            >
              <span 
                className="w-2 h-2 rounded-full"
                style={{ backgroundColor: vehicleColors[vehicle.serial] }}
              />
              <span className="truncate max-w-[80px]">
                {vehicle.name || vehicle.serial}
              </span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

export default MultiVehiclePanel;