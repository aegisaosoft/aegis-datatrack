import { RefreshCw, Truck, Clock, Users, User } from 'lucide-react';

function Header({ 
  lastUpdated, 
  isRefreshing, 
  onRefresh, 
  vehicleCount, 
  selectedCount = 0,
  trackingMode,
  onTrackingModeChange 
}) {
  const formatTime = (date) => {
    if (!date) return '--:--:--';
    return date.toLocaleTimeString();
  };

  return (
    <header className="bg-white shadow-sm border-b border-gray-200">
      <div className="px-4 py-3 flex items-center justify-between">
        <div className="flex items-center space-x-3">
          <div className="bg-primary-600 p-2 rounded-lg">
            <Truck className="h-6 w-6 text-white" />
          </div>
          <div>
            <h1 className="text-xl font-bold text-gray-900">Vehicle Tracker</h1>
            <p className="text-sm text-gray-500">Real-Time Fleet Monitoring</p>
          </div>
        </div>

        <div className="flex items-center space-x-6">
          {/* Tracking Mode Toggle */}
          <div className="flex items-center bg-gray-100 rounded-lg p-1">
            <button
              onClick={() => onTrackingModeChange('single')}
              className={`flex items-center space-x-1 px-3 py-1.5 rounded-md text-sm font-medium transition-colors ${
                trackingMode === 'single'
                  ? 'bg-white text-primary-600 shadow-sm'
                  : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              <User className="h-4 w-4" />
              <span>Single</span>
            </button>
            <button
              onClick={() => onTrackingModeChange('multi')}
              className={`flex items-center space-x-1 px-3 py-1.5 rounded-md text-sm font-medium transition-colors ${
                trackingMode === 'multi'
                  ? 'bg-white text-primary-600 shadow-sm'
                  : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              <Users className="h-4 w-4" />
              <span>Multi</span>
            </button>
          </div>

          {/* Vehicle Count */}
          <div className="flex items-center space-x-2 text-gray-600">
            <Truck className="h-5 w-5" />
            <span className="font-medium">{vehicleCount} Vehicles</span>
          </div>

          {/* Selected Count */}
          {selectedCount > 0 && (
            <div className="flex items-center space-x-2 px-3 py-1 bg-primary-100 text-primary-700 rounded-full">
              <Users className="h-4 w-4" />
              <span className="font-medium">{selectedCount} Tracking</span>
            </div>
          )}

          {/* Last Updated */}
          <div className="flex items-center space-x-2 text-gray-600">
            <Clock className="h-5 w-5" />
            <span className="text-sm">
              Updated: <span className="font-medium">{formatTime(lastUpdated)}</span>
            </span>
          </div>

          {/* Refresh Button */}
          <button
            onClick={onRefresh}
            disabled={isRefreshing}
            className={`
              flex items-center space-x-2 px-4 py-2 rounded-lg
              bg-primary-600 text-white font-medium
              hover:bg-primary-700 transition-colors
              disabled:opacity-50 disabled:cursor-not-allowed
            `}
          >
            <RefreshCw className={`h-4 w-4 ${isRefreshing ? 'animate-spin' : ''}`} />
            <span>{isRefreshing ? 'Refreshing...' : 'Refresh'}</span>
          </button>
        </div>
      </div>
    </header>
  );
}

export default Header;
