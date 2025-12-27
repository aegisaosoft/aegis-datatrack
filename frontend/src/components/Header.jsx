import { RefreshCw, Truck, Clock, Users, User, LogOut } from 'lucide-react';

function Header({ 
  lastUpdated, 
  isRefreshing, 
  onRefresh, 
  vehicleCount, 
  selectedCount = 0,
  trackingMode,
  onTrackingModeChange,
  onLogout,
  companyName,
  compact = false
}) {
  const formatTime = (date) => {
    if (!date) return '--:--:--';
    return date.toLocaleTimeString();
  };

  // Compact mode for embedded header
  if (compact) {
    return (
      <div className="flex items-center space-x-4">
        {/* Tracking Mode Toggle */}
        <div className="flex items-center bg-gray-100 rounded-lg p-0.5">
          <button
            onClick={() => onTrackingModeChange('single')}
            className={`flex items-center space-x-1 px-2 py-1 rounded text-xs font-medium transition-colors ${
              trackingMode === 'single'
                ? 'bg-white text-blue-600 shadow-sm'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            <User className="h-3 w-3" />
            <span>Single</span>
          </button>
          <button
            onClick={() => onTrackingModeChange('multi')}
            className={`flex items-center space-x-1 px-2 py-1 rounded text-xs font-medium transition-colors ${
              trackingMode === 'multi'
                ? 'bg-white text-blue-600 shadow-sm'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            <Users className="h-3 w-3" />
            <span>Multi</span>
          </button>
        </div>

        {/* Vehicle Count */}
        <div className="flex items-center space-x-1 text-gray-600 text-sm">
          <Truck className="h-4 w-4" />
          <span>{vehicleCount}</span>
        </div>

        {/* Selected Count */}
        {selectedCount > 0 && (
          <div className="flex items-center space-x-1 px-2 py-0.5 bg-blue-100 text-blue-700 rounded-full text-xs">
            <Users className="h-3 w-3" />
            <span>{selectedCount} tracking</span>
          </div>
        )}

        {/* Last Updated */}
        <div className="flex items-center space-x-1 text-gray-500 text-xs">
          <Clock className="h-3 w-3" />
          <span>{formatTime(lastUpdated)}</span>
        </div>

        {/* Refresh Button */}
        <button
          onClick={onRefresh}
          disabled={isRefreshing}
          className={`
            flex items-center space-x-1 px-3 py-1.5 rounded-lg text-sm
            bg-blue-600 text-white font-medium
            hover:bg-blue-700 transition-colors
            disabled:opacity-50 disabled:cursor-not-allowed
          `}
        >
          <RefreshCw className={`h-3 w-3 ${isRefreshing ? 'animate-spin' : ''}`} />
          <span>{isRefreshing ? '...' : 'Refresh'}</span>
        </button>

        {/* Logout Button */}
        {onLogout && (
          <button
            onClick={onLogout}
            className="flex items-center space-x-1 px-3 py-1.5 rounded-lg text-sm bg-gray-200 text-gray-700 font-medium hover:bg-gray-300 transition-colors"
            title={companyName ? `Logout from ${companyName}` : 'Logout'}
          >
            <LogOut className="h-3 w-3" />
          </button>
        )}
      </div>
    );
  }

  // Full header mode
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

          {/* Logout Button */}
          {onLogout && (
            <button
              onClick={onLogout}
              className="flex items-center space-x-2 px-4 py-2 rounded-lg bg-gray-200 text-gray-700 font-medium hover:bg-gray-300 transition-colors"
              title={companyName ? `Logout from ${companyName}` : 'Logout'}
            >
              <LogOut className="h-4 w-4" />
              <span>Logout</span>
            </button>
          )}
        </div>
      </div>
    </header>
  );
}

export default Header;
