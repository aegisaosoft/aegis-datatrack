import { useState } from 'react';
import { 
  X, 
  MapPin, 
  Gauge, 
  Battery, 
  Clock, 
  Navigation,
  Route,
  Power,
  Bell,
  AlertTriangle,
  CheckCircle,
  Loader2,
  Car,
  Hash
} from 'lucide-react';

function VehicleDetails({ 
  vehicle, 
  onClose, 
  onShowHistory, 
  onHideHistory, 
  showHistory,
  onStarterControl,
  onBuzzerControl,
  vehicleColor
}) {
  const [isStarterLoading, setIsStarterLoading] = useState(false);
  const [isBuzzerLoading, setIsBuzzerLoading] = useState(false);
  const [historyHours, setHistoryHours] = useState(24);

  const formatDate = (unixTime) => {
    return new Date(unixTime * 1000).toLocaleString();
  };

  const formatDistance = (meters) => {
    const km = meters / 1000;
    const miles = km / 1.609;
    return `${miles.toFixed(1)} mi (${km.toFixed(1)} km)`;
  };

  const formatSpeed = (kmh) => {
    const mph = Math.round(kmh / 1.609);
    return `${mph} mph`;
  };

  const getStatusText = (typeId) => {
    const types = {
      2: 'Ignition Off',
      3: 'Stopped',
      4: 'Ignition On',
      5: 'Moving',
      24: 'Starter Disabled',
      25: 'Starter Enabled',
      26: 'Stopped',
    };
    return types[typeId] || 'Unknown';
  };

  const handleStarterToggle = async () => {
    setIsStarterLoading(true);
    await onStarterControl(vehicle.serial, vehicle.disabled === 0);
    setIsStarterLoading(false);
  };

  const handleBuzzerToggle = async () => {
    setIsBuzzerLoading(true);
    await onBuzzerControl(vehicle.serial, vehicle.buzzer > 0);
    setIsBuzzerLoading(false);
  };

  const handleHistoryToggle = () => {
    if (showHistory) {
      onHideHistory();
    } else {
      onShowHistory(historyHours);
    }
  };

  const isMoving = vehicle.typeId === 5;
  const isDisabled = vehicle.disabled > 0;
  const hasBuzzer = vehicle.buzzer > 0;

  return (
    <div className="flex flex-col h-full">
      {/* Header */}
      <div 
        className="p-4 border-b border-gray-200 flex items-center justify-between"
        style={{ backgroundColor: vehicleColor ? `${vehicleColor}15` : '#f9fafb' }}
      >
        <div className="flex items-center space-x-3">
          {vehicleColor && (
            <div 
              className="w-4 h-4 rounded-full flex-shrink-0"
              style={{ backgroundColor: vehicleColor }}
            />
          )}
          <div>
            <h2 className="text-lg font-bold text-gray-900">
              {vehicle.name || vehicle.serial}
            </h2>
            <p className="text-sm text-gray-500">Serial: {vehicle.serial}</p>
          </div>
        </div>
        <button
          onClick={onClose}
          className="p-2 hover:bg-gray-200 rounded-full transition-colors"
        >
          <X className="h-5 w-5 text-gray-500" />
        </button>
      </div>

      {/* Status Banner */}
      <div className={`p-3 flex items-center space-x-2 ${
        isDisabled ? 'bg-red-100 text-red-800' : 
        isMoving ? 'bg-blue-100 text-blue-800' : 
        'bg-green-100 text-green-800'
      }`}>
        {isDisabled ? (
          <AlertTriangle className="h-5 w-5" />
        ) : isMoving ? (
          <Navigation className="h-5 w-5" />
        ) : (
          <CheckCircle className="h-5 w-5" />
        )}
        <span className="font-medium">
          {isDisabled ? 'Starter Disabled' : getStatusText(vehicle.typeId)}
        </span>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto p-4 space-y-6">
        {/* Location Info */}
        <section>
          <h3 className="text-sm font-semibold text-gray-700 uppercase tracking-wider mb-3">
            Location
          </h3>
          <div className="bg-gray-50 rounded-lg p-4 space-y-3">
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-2 text-gray-600">
                <MapPin className="h-4 w-4" />
                <span className="text-sm">Coordinates</span>
              </div>
              <span className="text-sm font-medium">
                {vehicle.lat?.toFixed(6)}, {vehicle.lng?.toFixed(6)}
              </span>
            </div>
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-2 text-gray-600">
                <Gauge className="h-4 w-4" />
                <span className="text-sm">Speed</span>
              </div>
              <span className="text-sm font-medium">{formatSpeed(vehicle.speed)}</span>
            </div>
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-2 text-gray-600">
                <Clock className="h-4 w-4" />
                <span className="text-sm">Last Update</span>
              </div>
              <span className="text-sm font-medium">{formatDate(vehicle.date)}</span>
            </div>
          </div>
        </section>

        {/* Vehicle Info */}
        <section>
          <h3 className="text-sm font-semibold text-gray-700 uppercase tracking-wider mb-3">
            Vehicle Info
          </h3>
          <div className="bg-gray-50 rounded-lg p-4 space-y-3">
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-2 text-gray-600">
                <Route className="h-4 w-4" />
                <span className="text-sm">Distance</span>
              </div>
              <span className="text-sm font-medium">{formatDistance(vehicle.distance)}</span>
            </div>
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-2 text-gray-600">
                <Battery className="h-4 w-4" />
                <span className="text-sm">Voltage</span>
              </div>
              <span className="text-sm font-medium">{(vehicle.volts / 1000).toFixed(2)}V</span>
            </div>
            {vehicle.vin && (
              <div className="flex items-center justify-between">
                <div className="flex items-center space-x-2 text-gray-600">
                  <Car className="h-4 w-4" />
                  <span className="text-sm">VIN</span>
                </div>
                <span className="text-sm font-medium font-mono">{vehicle.vin}</span>
              </div>
            )}
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-2 text-gray-600">
                <Hash className="h-4 w-4" />
                <span className="text-sm">Serial</span>
              </div>
              <span className="text-sm font-medium font-mono">{vehicle.serial}</span>
            </div>
          </div>
        </section>

        {/* History Controls */}
        <section>
          <h3 className="text-sm font-semibold text-gray-700 uppercase tracking-wider mb-3">
            Trip History
          </h3>
          <div className="bg-gray-50 rounded-lg p-4">
            <div className="flex items-center space-x-3 mb-3">
              <label className="text-sm text-gray-600">Show last</label>
              <select
                value={historyHours}
                onChange={(e) => setHistoryHours(Number(e.target.value))}
                className="border border-gray-300 rounded px-2 py-1 text-sm"
                disabled={showHistory}
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
              onClick={handleHistoryToggle}
              className={`w-full py-2 px-4 rounded-lg font-medium transition-colors flex items-center justify-center space-x-2 ${
                showHistory
                  ? 'bg-gray-200 text-gray-700 hover:bg-gray-300'
                  : 'bg-primary-600 text-white hover:bg-primary-700'
              }`}
            >
              <Route className="h-4 w-4" />
              <span>{showHistory ? 'Hide History' : 'Show History'}</span>
            </button>
          </div>
        </section>

        {/* Vehicle Controls */}
        <section>
          <h3 className="text-sm font-semibold text-gray-700 uppercase tracking-wider mb-3">
            Vehicle Controls
          </h3>
          <div className="space-y-3">
            {/* Starter Control */}
            <button
              onClick={handleStarterToggle}
              disabled={isStarterLoading}
              className={`w-full py-3 px-4 rounded-lg font-medium transition-colors flex items-center justify-center space-x-2 ${
                isDisabled
                  ? 'bg-green-600 text-white hover:bg-green-700'
                  : 'bg-red-600 text-white hover:bg-red-700'
              } disabled:opacity-50`}
            >
              {isStarterLoading ? (
                <Loader2 className="h-5 w-5 animate-spin" />
              ) : (
                <Power className="h-5 w-5" />
              )}
              <span>{isDisabled ? 'Enable Starter' : 'Disable Starter'}</span>
            </button>

            {/* Buzzer Control */}
            <button
              onClick={handleBuzzerToggle}
              disabled={isBuzzerLoading}
              className={`w-full py-3 px-4 rounded-lg font-medium transition-colors flex items-center justify-center space-x-2 ${
                hasBuzzer
                  ? 'bg-gray-600 text-white hover:bg-gray-700'
                  : 'bg-yellow-500 text-white hover:bg-yellow-600'
              } disabled:opacity-50`}
            >
              {isBuzzerLoading ? (
                <Loader2 className="h-5 w-5 animate-spin" />
              ) : (
                <Bell className="h-5 w-5" />
              )}
              <span>{hasBuzzer ? 'Disable Buzzer' : 'Enable Buzzer'}</span>
            </button>
          </div>
          
          <p className="mt-3 text-xs text-gray-500">
            Note: Starter disable takes effect when the vehicle stops. Buzzer activates on next ignition.
          </p>
        </section>
      </div>
    </div>
  );
}

export default VehicleDetails;
